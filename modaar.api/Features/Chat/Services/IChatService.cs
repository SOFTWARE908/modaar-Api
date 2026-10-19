using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Chat.Dtos;

namespace modaar.api.Features.Chat.Services;

public interface IChatService
{
    // Returns the existing conversation about the same thing if there is one; Created says
    // whether this call made it.
    Task<Result<(ConversationDto Conversation, bool Created)>> StartAsync(
        Guid callerUserId, AccountType activeRole, StartConversationDto request, CancellationToken ct);

    Task<Result<PagedResultDto<ConversationDto>>> ListConversationsAsync(
        Guid userId, PagedRequestDto query, CancellationToken ct);

    // Newest first. Opening the first page marks the conversation read.
    Task<Result<PagedResultDto<ChatMessageDto>>> ListMessagesAsync(
        Guid userId, Guid conversationId, MessageListQueryDto query, CancellationToken ct);

    Task<Result<ChatMessageDto>> SendAsync(
        Guid senderUserId, Guid conversationId, SendMessageDto request, CancellationToken ct);

    // Marks everything in the conversation read up to now. The app calls it while the
    // conversation is open, when a new message arrives over the hub.
    Task<Result<bool>> MarkReadAsync(Guid userId, Guid conversationId, CancellationToken ct);

    Task<Result<ChatUnreadCountDto>> GetUnreadCountAsync(Guid userId, CancellationToken ct);

    Task<Result<ChatAttachmentDto>> UploadAttachmentAsync(Guid uploaderUserId, IFormFile file, CancellationToken ct);
}
