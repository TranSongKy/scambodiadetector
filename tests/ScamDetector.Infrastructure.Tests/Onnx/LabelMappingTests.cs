using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Infrastructure.Tests.Onnx;

public sealed class LabelMappingTests
{
    private const double Tolerance = 1e-9;
    private static readonly IReadOnlyList<MessageLabel> Labels = [MessageLabel.Normal, MessageLabel.Spam, MessageLabel.Scam];

    [Fact]
    public void Parse_MixedCaseNames_ReturnsLabelsInOrder()
    {
        var labels = LabelMapping.Parse(["SCAM", "normal", "Spam"]);

        Assert.Equal([MessageLabel.Scam, MessageLabel.Normal, MessageLabel.Spam], labels);
    }

    [Fact]
    public void Parse_UnknownName_Throws()
    {
        Assert.Throws<ArgumentException>(() => LabelMapping.Parse(["phishing"]));
    }

    [Fact]
    public void ToPrediction_EqualLogits_ReturnsUniformProbabilities()
    {
        var prediction = LabelMapping.ToPrediction(Labels, [0f, 0f, 0f]);

        Assert.All(Labels, label => Assert.Equal(1.0 / Labels.Count, prediction.ProbabilityOf(label), Tolerance));
    }

    [Fact]
    public void ToPrediction_AnyLogits_ProbabilitiesSumToOne()
    {
        var prediction = LabelMapping.ToPrediction(Labels, [1.5f, -2f, 3f]);

        Assert.Equal(1.0, Labels.Sum(prediction.ProbabilityOf), Tolerance);
    }

    [Fact]
    public void ToPrediction_LargeLogits_DoesNotOverflow()
    {
        var prediction = LabelMapping.ToPrediction(Labels, [1000f, 0f, 1000f]);

        Assert.Equal(0.5, prediction.ProbabilityOf(MessageLabel.Scam), Tolerance);
    }

    [Fact]
    public void ToPrediction_HighestLogit_HasHighestProbability()
    {
        var prediction = LabelMapping.ToPrediction(Labels, [0f, 1f, 4f]);

        Assert.True(prediction.ProbabilityOf(MessageLabel.Scam) > prediction.ProbabilityOf(MessageLabel.Spam));
        Assert.True(prediction.ProbabilityOf(MessageLabel.Spam) > prediction.ProbabilityOf(MessageLabel.Normal));
    }

    [Fact]
    public void ToPrediction_LogitCountMismatch_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => LabelMapping.ToPrediction(Labels, [0f, 1f]));
    }
}
