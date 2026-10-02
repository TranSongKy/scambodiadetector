using ScamDetector.Core.Classification;
using ScamDetector.Core.Tests.Builders;

namespace ScamDetector.Core.Tests.Classification;

public sealed class ModelPredictionTests
{
    [Fact]
    public void ProbabilityOf_ExistingLabel_ReturnsStoredProbability()
    {
        var prediction = PredictionFactory.Create(normal: 0.1, spam: 0.2, scam: 0.7);

        var probability = prediction.ProbabilityOf(MessageLabel.Spam);

        Assert.Equal(0.2, probability);
    }

    [Theory]
    [InlineData(MessageLabel.Normal)]
    [InlineData(MessageLabel.Spam)]
    [InlineData(MessageLabel.Scam)]
    public void ProbabilityOf_MissingLabel_ReturnsZero(MessageLabel label)
    {
        var prediction = PredictionFactory.Empty();

        var probability = prediction.ProbabilityOf(label);

        Assert.Equal(0, probability);
    }

    [Fact]
    public void ProbabilityOf_OnlyOtherLabelPresent_ReturnsZero()
    {
        var prediction = new ModelPrediction(new Dictionary<MessageLabel, double> { [MessageLabel.Normal] = 1 });

        var probability = prediction.ProbabilityOf(MessageLabel.Scam);

        Assert.Equal(0, probability);
    }
}
