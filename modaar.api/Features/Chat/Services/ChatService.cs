using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Realtime;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Chat.Dtos;
using modaar.api.Features.Chat.Entities;
using modaar.api.Features.Chat.Enums;
using modaar.api.Features.Notifications.Enums;
using modaar.api.Features.Notifications.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Chat.Services;

public sealed class ChatService : IChatService
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;

    // Images and PDF, per the agreed rule. Extension allow-list, like the other uploads.
    private static readonly Dictionary<string, ChatAttachmentKind> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ChatAttachmentKind.Image,
        [".jpeg"] = ChatAttachmentKind.Image,
        [".png"] = ChatAttachmentKind.Image,
        [".webp"] = ChatAttachmentKind.Image,
        [".heic"] = ChatAttachmentKind.Image,
        [".pdf"] = ChatAttachmentKind.Pdf
    };

    private readonly ModaarDbContext _db;
    private readonly ChatAccess _access;
    private readonly IRealtimeNotifier _realtime;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly IHttpContextAccessor _http;
    private readonly string _uploadsRoot;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ModaarDbContext db,
        ChatAccess access,
        IRealtimeNotifier realtime,
        INotificationService notifications,
        TimeProvider time,
        IHttpContextAccessor http,
        IConfiguration config,
        ILogger<ChatService> logger)
    {
        _db = db;
        _access = access;
        _realtime = realtime;
        _notifications = notifications;
        _time = time;
        _http = http;
        _uploadsRoot = config["Uploads:Root"] ?? throw new InvalidOperationException("Uploads:Root is not configured.");
        _logger = logger;
    }

    // ---- start ----

    public async Task<Result<(ConversationDto Conversation, bool Created)>> StartAsync(
        Guid callerUserId, AccountType activeRole, StartConversationDto request, CancellationToken ct)
    {
        var resolved = await _access.ResolveAsync(callerUserId, activeRole, request, ct);
        if (!resolved.Success)
            return Result<(ConversationDto, bool)>.Fail(resolved.ErrorCode!.Value, resolved.Error!);

        var counterpart = resolved.Value!;

        // Same property, same subject, same two people → the same conversation. Tapping
        // "contact the owner" twice must not open a second thread.
        var existingId = await _db.Conversations
            .Where(c => c.PropertyId == counterpart.PropertyId
                     && c.ContextType == request.ContextType
                     && c.ContextId == request.ContextId
                     && _db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == callerUserId)
                     && _db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == counterpart.UserId))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        var created = existingId is null;
        var conversationId = existingId ?? Guid.NewGuid();

        if (created)
        {
            var now = _time.GetUtcNow();
            _db.Conversations.Add(new Conversation
            {
                Id = conversationId,
                PropertyId = counterpart.PropertyId,
                ContextType = request.ContextType,
                ContextId = request.ContextId,
                CreatedAt = now
            });
            _db.ConversationParticipants.AddRange(
                new ConversationParticipant { ConversationId = conversationId, UserId = callerUserId, Role = activeRole, JoinedAt = now },
                new ConversationParticipant { ConversationId = conversationId, UserId = counterpart.UserId, Role = counterpart.Role, JoinedAt = now });
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Conversation {Id} started by {Caller} with {Counterpart} ({Role}).",
                conversationId, callerUserId, counterpart.UserId, counterpart.Role);
        }

        var dto = (await LoadConversationsAsync(callerUserId, q => q.Where(p => p.ConversationId == conversationId), ct)).Single();
        return Result<(ConversationDto, bool)>.Ok((dto, created));
    }

    // ---- list conversations ----

    public async Task<Result<PagedResultDto<ConversationDto>>> ListConversationsAsync(
        Guid userId, PagedRequestDto query, CancellationToken ct)
    {
        var total = await _db.ConversationParticipants.CountAsync(p => p.UserId == userId, ct);

        var items = await LoadConversationsAsync(userId, q => q
            // Most recent activity first; a conversation with no messages yet sorts by when it began.
            .OrderByDescending(p => _db.Conversations.Where(c => c.Id == p.ConversationId)
                .Select(c => c.LastMessageAt ?? c.CreatedAt).FirstOrDefault())
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount), ct);

        return Result<PagedResultDto<ConversationDto>>.Ok(new PagedResultDto<ConversationDto> { Items = items, TotalCount = total });
    }

    // One projection for both the list and the start response, so they can never differ.
    // `shape` filters / orders / pages the caller's participant rows.
    private async Task<List<ConversationDto>> LoadConversationsAsync(
        Guid userId, Func<IQueryable<ConversationParticipant>, IQueryable<ConversationParticipant>> shape, CancellationToken ct)
    {
        var mine = _db.ConversationParticipants.AsNoTracking().Where(p => p.UserId == userId);

        return await shape(mine)
            .Select(me => new
            {
                Conversation = _db.Conversations.First(c => c.Id == me.ConversationId),
                Other = _db.ConversationParticipants.First(o => o.ConversationId == me.ConversationId && o.UserId != userId),
                me.LastReadAt
            })
            .Select(x => new ConversationDto
            {
                Id = x.Conversation.Id,
                Participant = new ChatParticipantDto
                {
                    UserId = x.Other.UserId,
                    Role = x.Other.Role,
                    Name = _db.Users.Where(u => u.Id == x.Other.UserId).Select(u => u.FullName).FirstOrDefault(),
                    AvatarUrl = _db.Users.Where(u => u.Id == x.Other.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault()
                },
                ContextType = x.Conversation.ContextType,
                ContextId = x.Conversation.ContextId,
                PropertyId = x.Conversation.PropertyId,
                PropertyTitle = _db.Properties.Where(p => p.Id == x.Conversation.PropertyId).Select(p => p.Title).FirstOrDefault() ?? "",
                LastMessage = x.Conversation.LastMessagePreview,
                LastMessageAt = x.Conversation.LastMessageAt,
                // Messages from the other side since I last opened it.
                UnreadCount = _db.ChatMessages.Count(m => m.ConversationId == x.Conversation.Id
                    && m.SenderUserId != userId
                    && (x.LastReadAt == null || m.CreatedAt > x.LastReadAt))
            })
            .ToListAsync(ct);
    }

    // ---- messages ----

    public async Task<Result<PagedResultDto<ChatMessageDto>>> ListMessagesAsync(
        Guid userId, Guid conversationId, MessageListQueryDto query, CancellationToken ct)
    {
        var me = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId, ct);

        // Not a participant reads the same as not existing: conversation ids can't be probed.
        if (me is null)
            return Result<PagedResultDto<ChatMessageDto>>.Fail(AppErrorCode.NotFound, "Conversation not found.");

        var messages = _db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        var total = await messages.CountAsync(ct);

        var page = await messages
            .OrderByDescending(m => m.CreatedAt)
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .ToListAsync(ct);

        var attachments = await LoadAttachmentsAsync(page.Select(m => m.Id).ToList(), ct);

        // Opening the conversation (its newest page) is reading it. Scrolling back through older
        // pages changes nothing.
        if (query.SkipCount == 0)
        {
            me.LastReadAt = _time.GetUtcNow();
            await _db.SaveChangesAsync(ct);
        }

        return Result<PagedResultDto<ChatMessageDto>>.Ok(new PagedResultDto<ChatMessageDto>
        {
            Items = page.Select(m => ToDto(m, userId, attachments)).ToList(),
            TotalCount = total
        });
    }

    public async Task<Result<ChatMessageDto>> SendAsync(
        Guid senderUserId, Guid conversationId, SendMessageDto request, CancellationToken ct)
    {
        var participants = await _db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId)
            .ToListAsync(ct);

        var me = participants.FirstOrDefault(p => p.UserId == senderUserId);
        if (me is null)
            return Result<ChatMessageDto>.Fail(AppErrorCode.NotFound, "Conversation not found.");

        var attachmentIds = request.AttachmentIds ?? [];
        List<ChatAttachment> attachments = [];
        if (attachmentIds.Count > 0)
        {
            // Only this sender's own, not-yet-used uploads: anything else would let someone attach
            // a file they never uploaded, or move one out of another conversation.
            attachments = await _db.ChatAttachments
                .Where(a => attachmentIds.Contains(a.Id) && a.MessageId == null && a.UploadedByUserId == senderUserId)
                .ToListAsync(ct);

            if (attachments.Count != attachmentIds.Count)
                return Result<ChatMessageDto>.Fail(AppErrorCode.ValidationFailed, "One or more attachments are unknown or already used.");
        }

        var now = _time.GetUtcNow();
        var text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            Text = text,
            CreatedAt = now
        };
        _db.ChatMessages.Add(message);

        foreach (var attachment in attachments)
            attachment.MessageId = message.Id;

        var conversation = await _db.Conversations.FirstAsync(c => c.Id == conversationId, ct);
        conversation.LastMessagePreview = Preview(text, attachments);
        conversation.LastMessageAt = now;

        // Your own message is never unread to you.
        me.LastReadAt = now;

        await _db.SaveChangesAsync(ct);

        var attachmentMap = new Dictionary<Guid, List<ChatAttachmentDto>> { [message.Id] = attachments.Select(ToDto).ToList() };

        // Live to everyone in the conversation, the sender's other devices included. Sent per
        // person because IsMine and the unread count differ between them.
        foreach (var participant in participants)
        {
            var unread = participant.UserId == senderUserId ? 0 : await CountUnreadAsync(conversationId, participant, ct);
            await _realtime.SendToUserAsync(participant.UserId, HubEvents.MessageReceived, new MessagePushDto
            {
                Message = ToDto(message, participant.UserId, attachmentMap),
                UnreadCount = unread
            }, ct);
        }

        // And a row on the notifications page (the "messages" filter) for the other side.
        var senderName = await _db.Users.Where(u => u.Id == senderUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var preview = conversation.LastMessagePreview ?? "";
        await _notifications.NotifyAsync(
            participants.Where(p => p.UserId != senderUserId).Select(p => p.UserId),
            new NewNotification(NotificationType.Messages,
                $"رسالة جديدة من {senderName ?? "مستخدم"}", $"New message from {senderName ?? "a user"}",
                preview, preview,
                NotificationTargetType.Conversation, conversationId),
            ct);

        return Result<ChatMessageDto>.Ok(ToDto(message, senderUserId, attachmentMap));
    }

    // ---- attachments ----

    public async Task<Result<ChatAttachmentDto>> UploadAttachmentAsync(Guid uploaderUserId, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return Result<ChatAttachmentDto>.Fail(AppErrorCode.ValidationFailed, "The file is empty.");

        if (file.Length > MaxAttachmentBytes)
            return Result<ChatAttachmentDto>.Fail(AppErrorCode.ValidationFailed, "The file exceeds 10 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedFiles.TryGetValue(extension, out var kind))
            return Result<ChatAttachmentDto>.Fail(AppErrorCode.ValidationFailed, "Only images and PDF files can be sent.");

        // The stored name comes from a server id only; the client's file name is a label and never
        // touches the disk path.
        var id = Guid.NewGuid();
        var storedName = $"{id:N}{extension.ToLowerInvariant()}";
        var folder = Path.Combine(_uploadsRoot, "chat");
        Directory.CreateDirectory(folder);

        await using (var stream = new FileStream(Path.Combine(folder, storedName), FileMode.CreateNew))
            await file.CopyToAsync(stream, ct);

        var request = _http.HttpContext!.Request;
        var attachment = new ChatAttachment
        {
            Id = id,
            UploadedByUserId = uploaderUserId,
            Url = $"{request.Scheme}://{request.Host}/uploads/chat/{storedName}",
            FileName = Path.GetFileName(file.FileName),
            Kind = kind,
            SizeBytes = file.Length,
            CreatedAt = _time.GetUtcNow()
        };
        _db.ChatAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return Result<ChatAttachmentDto>.Ok(ToDto(attachment));
    }

    // ---- helpers ----

    private Task<int> CountUnreadAsync(Guid conversationId, ConversationParticipant participant, CancellationToken ct) =>
        _db.ChatMessages.CountAsync(m => m.ConversationId == conversationId
            && m.SenderUserId != participant.UserId
            && (participant.LastReadAt == null || m.CreatedAt > participant.LastReadAt), ct);

    private async Task<Dictionary<Guid, List<ChatAttachmentDto>>> LoadAttachmentsAsync(List<Guid> messageIds, CancellationToken ct) =>
        (await _db.ChatAttachments.AsNoTracking()
            .Where(a => a.MessageId != null && messageIds.Contains(a.MessageId.Value))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct))
        .GroupBy(a => a.MessageId!.Value)
        .ToDictionary(g => g.Key, g => g.Select(ToDto).ToList());

    // The line under the name in the conversations list. Language-neutral on purpose: it is the
    // sender's own words, or a file name.
    private static string Preview(string? text, List<ChatAttachment> attachments)
    {
        if (text is not null)
            return text.Length > 200 ? text[..200] + "…" : text;

        return attachments.Count == 1 ? $"📎 {attachments[0].FileName}" : $"📎 ×{attachments.Count}";
    }

    private static ChatMessageDto ToDto(ChatMessage m, Guid viewerUserId, Dictionary<Guid, List<ChatAttachmentDto>> attachments) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderUserId = m.SenderUserId,
        Text = m.Text,
        Attachments = attachments.TryGetValue(m.Id, out var list) ? list : [],
        SentAt = m.CreatedAt,
        IsMine = m.SenderUserId == viewerUserId
    };

    private static ChatAttachmentDto ToDto(ChatAttachment a) => new()
    {
        Id = a.Id,
        Url = a.Url,
        FileName = a.FileName,
        Kind = a.Kind
    };
}
