using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Chat.Dtos;
using modaar.api.Features.Chat.Services;

namespace modaar.api.Features.Chat.Controllers;

// The messages tab. Sending stays REST — validated, enveloped, with real status codes — and new
// messages reach the other side live as messageReceived on /hubs/app.
[ApiController]
[Route("api/conversations")]
[Authorize]
[Produces("application/json")]
public class ConversationsController : ControllerBase
{
    private readonly IChatService _chat;

    public ConversationsController(IChatService chat) => _chat = chat;

    // All of the user's conversations, whatever role they are in now: a conversation belongs to
    // the person, like their notifications.
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] PagedRequestDto query, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _chat.ListConversationsAsync(userId, query, ct)).ToActionResult();
    }

    // Unread messages across every conversation: the messages tab badge.
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ChatUnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _chat.GetUnreadCountAsync(userId, ct)).ToActionResult();
    }

    // Marks the conversation read up to now. Call it when a message arrives over the hub while
    // that conversation is on screen.
    [HttpPost("{conversationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _chat.MarkReadAsync(userId, conversationId, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // Starting one needs the active role: it decides who the caller is allowed to talk to.
    // 201 for a new conversation, 200 when the same one already existed.
    [HttpPost]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start([FromBody] StartConversationDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } activeRole)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

        var result = await _chat.StartAsync(userId, activeRole, request, ct);
        if (!result.Success)
            return Result<ConversationDto>.Fail(result.ErrorCode!.Value, result.Error!).ToActionResult();

        var (conversation, created) = result.Value;
        return created
            ? CreatedAtAction(nameof(Messages), new { conversationId = conversation.Id }, conversation)
            : Ok(conversation);
    }

    // Newest first. Loading the first page (no skipCount, no before) marks the conversation read.
    // Older pages: ?before=<id of the oldest message you have>.
    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(PagedResultDto<ChatMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Messages(Guid conversationId, [FromQuery] MessageListQueryDto query, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _chat.ListMessagesAsync(userId, conversationId, query, ct)).ToActionResult();
    }

    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(Guid conversationId, [FromBody] SendMessageDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _chat.SendAsync(userId, conversationId, request, ct)).ToActionResult();
    }

    // Upload first (images or PDF, up to 10 MB), then pass the returned id in attachmentIds.
    [HttpPost("attachments")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(ChatAttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadAttachment(IFormFile file, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (file is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No file was supplied.");

        return (await _chat.UploadAttachmentAsync(userId, file, ct)).ToActionResult();
    }
}
