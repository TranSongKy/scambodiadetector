using ScamDetector.Core.Classification;
using ScamDetector.Core.Tests.Builders;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Classification;

public sealed class ClassificationResultTests
{
    private const double Threshold = 0.7;
    private const string FirstUrlReason = "url_shortener";
    private const string SecondUrlReason = "lookalike_domain";

    private static readonly IReadOnlyList<UrlFinding> NoFindings = [];

    private static readonly IReadOnlyList<UrlFinding> TwoFindings =
    [
        new UrlFinding("http://a.example", FirstUrlReason),
        new UrlFinding("http://b.example", SecondUrlReason),
    ];

    [Fact]
    public void From_ScamAboveThreshold_ReturnsScamWithScamProbability()
    {
        var prediction = PredictionFactory.Create(normal: 0.05, spam: 0.05, scam: 0.9);

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(0.9, result.Confidence);
        Assert.Equal([ClassificationReasons.ModelPredictedScam], result.Reasons);
    }

    [Fact]
    public void From_ScamExactlyAtThreshold_ReturnsScam()
    {
        var prediction = PredictionFactory.Create(normal: 0.2, spam: 0.1, scam: Threshold);

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(Threshold, result.Confidence);
    }

    [Fact]
    public void From_ScamJustBelowThresholdAndSpamHigher_ReturnsSpamWithBelowThresholdReason()
    {
        var prediction = PredictionFactory.Create(normal: 0.1, spam: 0.3, scam: 0.6);

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Spam, result.Label);
        Assert.Equal(0.3, result.Confidence);
        Assert.Equal(
            [ClassificationReasons.ModelPredictedSpam, ClassificationReasons.ScamBelowThreshold],
            result.Reasons);
    }

    [Fact]
    public void From_ScamBelowThresholdAndNormalHigher_ReturnsNormalWithBelowThresholdReason()
    {
        var prediction = PredictionFactory.Create(normal: 0.3, spam: 0.1, scam: 0.6);

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.Equal(0.3, result.Confidence);
        Assert.Equal([ClassificationReasons.ScamBelowThreshold], result.Reasons);
    }

    [Fact]
    public void From_ScamBelowThresholdButNotMax_OmitsBelowThresholdReason()
    {
        var prediction = PredictionFactory.Create(normal: 0.2, spam: 0.5, scam: 0.3);

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Spam, result.Label);
        Assert.Equal(0.5, result.Confidence);
        Assert.Equal([ClassificationReasons.ModelPredictedSpam], result.Reasons);
    }

    [Fact]
    public void From_NormalDominant_ReturnsNormalWithoutReasons()
    {
        var prediction = PredictionFactory.Create(normal: 0.8, spam: 0.1, scam: 0.1);

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.Equal(0.8, result.Confidence);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void From_SpamEqualsNormal_ReturnsNormal()
    {
        var prediction = PredictionFactory.Create(normal: 0.4, spam: 0.4, scam: 0.2);

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.Equal(0.4, result.Confidence);
    }

    [Fact]
    public void From_UrlFindingsWithScam_AppendsUrlReasonsAfterModelReason()
    {
        var prediction = PredictionFactory.Create(normal: 0.05, spam: 0.05, scam: 0.9);

        var result = ClassificationResult.From(prediction, TwoFindings, Threshold);

        Assert.Equal(
            [ClassificationReasons.ModelPredictedScam, FirstUrlReason, SecondUrlReason],
            result.Reasons);
    }

    [Fact]
    public void From_UrlFindingsWithNonScam_AppendsUrlReasonsAtEnd()
    {
        var prediction = PredictionFactory.Create(normal: 0.1, spam: 0.3, scam: 0.6);

        var result = ClassificationResult.From(prediction, TwoFindings, Threshold);

        Assert.Equal(
            [
                ClassificationReasons.ModelPredictedSpam,
                ClassificationReasons.ScamBelowThreshold,
                FirstUrlReason,
                SecondUrlReason,
            ],
            result.Reasons);
    }

    [Fact]
    public void From_UrlFindingsDoNotChangeLabelOrConfidence_ReturnsSameLabelAndConfidence()
    {
        var prediction = PredictionFactory.Create(normal: 0.8, spam: 0.1, scam: 0.1);

        var result = ClassificationResult.From(prediction, TwoFindings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal([FirstUrlReason, SecondUrlReason], result.Reasons);
    }

    [Fact]
    public void From_EmptyPrediction_ReturnsNormalWithZeroConfidence()
    {
        var prediction = PredictionFactory.Empty();

        var result = ClassificationResult.From(prediction, NoFindings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.Equal(0, result.Confidence);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void From_ZeroThreshold_ReturnsScamEvenForZeroProbability()
    {
        var prediction = PredictionFactory.Create(normal: 1, spam: 0, scam: 0);

        var result = ClassificationResult.From(prediction, NoFindings, scamThreshold: 0);

        Assert.Equal(MessageLabel.Scam, result.Label);
    }
}
