using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.DependencyInjection;

namespace ScamDetector.Infrastructure.Tests.DependencyInjection;

public sealed class ClassificationOptionsValidatorTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(0.7)]
    [InlineData(1)]
    public void Validate_ThresholdInRange_ReturnsSameOptions(double threshold)
    {
        var options = new ClassificationOptions { ScamThreshold = threshold };

        var validated = ClassificationOptionsValidator.Validate(options);

        Assert.Same(options, validated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    public void Validate_ThresholdOutOfRange_ThrowsInvalidOperationException(double threshold)
    {
        var options = new ClassificationOptions { ScamThreshold = threshold };

        Assert.Throws<InvalidOperationException>(() => ClassificationOptionsValidator.Validate(options));
    }
}
