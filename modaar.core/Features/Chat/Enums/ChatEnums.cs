using System.Text.Json.Serialization;

namespace modaar.api.Features.Chat.Enums;

// What a conversation is about, when it is about something. Null on a general conversation, e.g.
// the tenant's "contact the owner" button.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConversationContextType
{
    MaintenanceRequest,
    Contract
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatAttachmentKind
{
    Image,
    Pdf
}
