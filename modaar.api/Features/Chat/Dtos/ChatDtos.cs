using modaar.api.Common.Dtos;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Chat.Enums;

namespace modaar.api.Features.Chat.Dtos;

// "I want to talk to the <With> about <context>". The app never sends the other person's id: it
// names the role, and the server works out who that is and whether the two may talk.
public record StartConversationDto
{
    // owner | broker | technician | tenant
    public required AccountType With { get; init; }

    // Optional: what the conversation is about. Both or neither.
    public ConversationContextType? ContextType { get; init; }
    public Guid? ContextId { get; init; }

    // For a general conversation (no context). A tenant with a single active contract can omit it.
    public Guid? PropertyId { get; init; }
}

public record ChatParticipantDto
{
    public required Guid UserId { get; init; }
    public string? Name { get; init; }
    public required AccountType Role { get; init; }
    public string? AvatarUrl { get; init; }
}

// A row in the conversations tab.
public record ConversationDto
{
    public required Guid Id { get; init; }

    // The other person.
    public required ChatParticipantDto Participant { get; init; }

    public ConversationContextType? ContextType { get; init; }
    public Guid? ContextId { get; init; }

    public required Guid PropertyId { get; init; }
    public required string PropertyTitle { get; init; }

    public string? LastMessage { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }

    public required int UnreadCount { get; init; }
}

public record ChatAttachmentDto
{
    public required Guid Id { get; init; }
    public required string Url { get; init; }
    public required string FileName { get; init; }
    public required ChatAttachmentKind Kind { get; init; }
}

public record ChatMessageDto
{
    public required Guid Id { get; init; }
    public required Guid ConversationId { get; init; }
    public required Guid SenderUserId { get; init; }

    public string? Text { get; init; }
    public required IReadOnlyList<ChatAttachmentDto> Attachments { get; init; }

    public required DateTimeOffset SentAt { get; init; }

    // Computed for whoever is reading, so the same message is "mine" on one phone and not on
    // the other.
    public required bool IsMine { get; init; }
}

public record SendMessageDto
{
    public string? Text { get; init; }

    // Uploaded beforehand via POST /api/conversations/attachments.
    public IReadOnlyList<Guid>? AttachmentIds { get; init; }
}

public record MessageListQueryDto : PagedRequestDto
{
    // Load older messages: the page of messages sent before this one. Unlike SkipCount it doesn't
    // shift when new messages arrive while the user is scrolling back, so nothing repeats.
    public Guid? Before { get; init; }
}

public record ChatUnreadCountDto
{
    // Unread messages across all of the user's conversations — the messages tab badge.
    public required int Count { get; init; }
}

// What the hub sends with messageReceived: the message as this recipient should see it, plus
// their unread count in that conversation for the badge.
public record MessagePushDto
{
    public required ChatMessageDto Message { get; init; }
    public required int UnreadCount { get; init; }
}
