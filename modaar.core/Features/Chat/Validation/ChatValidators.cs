using FluentValidation;
using modaar.api.Features.Chat.Dtos;

namespace modaar.api.Features.Chat.Validation;

public sealed class StartConversationValidator : AbstractValidator<StartConversationDto>
{
    public StartConversationValidator()
    {
        RuleFor(x => x.With).IsInEnum();
        RuleFor(x => x.ContextType).IsInEnum();

        // A context type without an id, or the reverse, says nothing usable.
        RuleFor(x => x.ContextId)
            .NotNull().When(x => x.ContextType is not null)
            .WithMessage("contextId is required with contextType.");

        RuleFor(x => x.ContextType)
            .NotNull().When(x => x.ContextId is not null)
            .WithMessage("contextType is required with contextId.");
    }
}

public sealed class SendMessageValidator : AbstractValidator<SendMessageDto>
{
    public const int MaxAttachments = 5;

    public SendMessageValidator()
    {
        RuleFor(x => x.Text).MaximumLength(2000);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || x.AttachmentIds is { Count: > 0 })
            .WithMessage("A message needs text or at least one attachment.");

        RuleFor(x => x.AttachmentIds!)
            .Must(ids => ids.Count <= MaxAttachments)
            .WithMessage($"At most {MaxAttachments} attachments per message.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Attachment ids must be unique.")
            .When(x => x.AttachmentIds is not null);
    }
}
