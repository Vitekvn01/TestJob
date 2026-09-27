using FluentValidation;
using TestJob.Models;

namespace TestJob.Validators;

public class HtmlAnalysisRequestValidator : AbstractValidator<HtmlRequest>
{
    public HtmlAnalysisRequestValidator()
    {
        RuleFor(x => x.Selector)
            .NotEmpty()
            .WithErrorCode("EMPTY_SELECTOR")
            .WithMessage("Selector is required.");

        RuleFor(x => x.Attribute)
            .NotEmpty()
            .WithErrorCode("EMPTY_ATTRIBUTE")
            .WithMessage("Attribute is required.");

        RuleFor(x => x.UrlB64)
            .NotEmpty()
            .WithErrorCode("MISSING_URL")
            .WithMessage("url_b64 is required.")
            .Must(BeValidBase64)
            .WithErrorCode("INVALID_BASE64_URL")
            .WithMessage("url_b64 is not valid base64.");

        RuleFor(x => x.PageB64)
            .NotEmpty()
            .WithErrorCode("MISSING_PAGE")
            .WithMessage("page_b64 is required.")
            .Must(BeValidBase64)
            .WithErrorCode("INVALID_BASE64_PAGE")
            .WithMessage("page_b64 is not valid base64.");

        RuleFor(x => x.KeyBytesB64)
            .NotEmpty()
            .WithErrorCode("MISSING_KEY")
            .WithMessage("key_bytes_b64 is required.")
            .Must(BeValidBase64)
            .WithErrorCode("INVALID_BASE64_KEY")
            .WithMessage("key_bytes_b64 is not valid base64.");

        RuleFor(x => x.EncryptedTextBytesB64)
            .NotEmpty()
            .WithErrorCode("MISSING_ENCRYPTED")
            .WithMessage("encrypted_text_bytes_b64 is required.")
            .Must(BeValidBase64)
            .WithErrorCode("INVALID_BASE64_ENCRYPTED")
            .WithMessage("encrypted_text_bytes_b64 is not valid base64.");
    }

    private static bool BeValidBase64(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}