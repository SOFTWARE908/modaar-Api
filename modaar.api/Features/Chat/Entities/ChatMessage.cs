using modaar.api.Features.Chat.Enums;

namespace modaar.api.Features.Chat.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }
    public Guid SenderUserId { get; set; }

    // Null on a message that is only attachments.
    public string? Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

// Uploaded first, then linked when the message is sent — the same two-step flow as maintenance
// attachments, so a slow upload never holds a half-sent message. MessageId is null until then.
public class ChatAttachment
{
    public Guid Id { get; set; }

    public Guid? MessageId { get; set; }

    // Only the uploader can attach it, and orphaned uploads can be cleaned up per user.
    public Guid UploadedByUserId { get; set; }

    public string Url { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public ChatAttachmentKind Kind { get; set; }
    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
