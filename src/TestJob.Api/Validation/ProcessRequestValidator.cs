using FluentValidation;
using TestJob.Api.Models;

namespace TestJob.Api.Validation;

internal sealed class ProcessRequestValidator : AbstractValidator<ProcessRequest>
{
    public ProcessRequestValidator()
    {
        RuleFor(x => x.Selector)
            .NotEmpty();

        RuleFor(x => x.Attribute)
            .NotEmpty();

        RuleFor(x => x.UrlB64)
            .NotEmpty();

        RuleFor(x => x.PageB64)
            .NotEmpty();

        RuleFor(x => x.EncryptedTextBytesB64)
            .NotEmpty();

        RuleFor(x => x.KeyBytesB64)
            .NotEmpty();
    }
}