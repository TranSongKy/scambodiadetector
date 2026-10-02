using ScamDetector.Core.Common;

namespace ScamDetector.Core.Classification;

public static class ClassificationErrors
{
    public static readonly DomainError EmptyText = new(
        "classification.empty_text",
        "Message text must not be empty.");

    public static readonly DomainError TextTooLong = new(
        "classification.text_too_long",
        $"Message text must not exceed {ClassificationLimits.MaxMessageLength} characters.");
}
