using FluentValidation;

namespace ArturRios.Hermes.WebApi.Emails;

/// <summary>The field rules of a send request. Its messages name fields and never repeat a value.</summary>
public sealed class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    public const int MaximumSubjectLength = 200;
    public const int MaximumBodyLength = 10_000;

    public SendEmailRequestValidator()
    {
        RuleFor(request => request.To)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("to is required.")
            .Must(Mailbox.IsAddress).WithMessage("to must be one valid e-mail address.");

        RuleFor(request => request.Subject)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("subject is required.")
            .MaximumLength(MaximumSubjectLength).WithMessage($"subject must be at most {MaximumSubjectLength} characters.")
            .Must(subject => subject!.IndexOfAny(['\r', '\n']) < 0).WithMessage("subject must be a single line.");

        RuleFor(request => request.Body)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("body is required.")
            .MaximumLength(MaximumBodyLength).WithMessage($"body must be at most {MaximumBodyLength} characters.");

        RuleFor(request => request.ReplyTo)
            .Must(Mailbox.IsAddress).WithMessage("replyTo must be one valid e-mail address.")
            .When(request => request.ReplyTo is not null);
    }
}
