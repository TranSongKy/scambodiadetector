using ScamDetector.Core.Classification;

namespace ScamDetector.Infrastructure.DependencyInjection;

public static class ClassificationOptionsValidator
{
    private const double MinExclusiveThreshold = 0;
    private const double MaxInclusiveThreshold = 1;

    public static ClassificationOptions Validate(ClassificationOptions options)
    {
        if (options.ScamThreshold is <= MinExclusiveThreshold or > MaxInclusiveThreshold)
        {
            throw new InvalidOperationException(
                $"{ClassificationOptions.SectionName}:{nameof(ClassificationOptions.ScamThreshold)} must be in (0, 1], got {options.ScamThreshold}.");
        }
        return options;
    }
}
