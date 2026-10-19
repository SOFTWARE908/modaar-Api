using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Chat.Enums;

namespace modaar.api.Features.Chat.Entities;

// A two-person conversation. Every conversation hangs off a property — that is what makes the
// two people related at all — and optionally off a maintenance request or a contract on it.
public class Conversation
{
    public Guid Id { get; set; }

    public Guid PropertyId { get; set; }

    public ConversationContextType? ContextType { get; set; }
    public Guid? ContextId { get; set; }

    // Denormalised for the conversation list, so it doesn't scan every message to draw a row.
    // Updated with each message.
    public string? LastMessagePreview { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

// One row per person in a conversation. Holds what is personal to them in it.
public class ConversationParticipant
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }

    // The capacity they are in this conversation as — the role badge the other side sees.
    // Stored rather than derived because one user can be an owner here and a tenant elsewhere.
    public AccountType Role { get; set; }

    // Everything sent by the other side after this is unread. Null = never opened.
    public DateTimeOffset? LastReadAt { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}
