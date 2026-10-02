using ScamDetector.Core.Classification;

namespace ScamDetector.Core.Tests.Classification;

public sealed class ClassificationOptionsTests
{
    [Fact]
    public void ScamThreshold_NotConfigured_EqualsDefaultScamThreshold()
    {
        var options = new ClassificationOptions();

        var threshold = options.ScamThreshold;

        Assert.Equal(ClassificationOptions.DefaultScamThreshold, threshold);
    }
}
