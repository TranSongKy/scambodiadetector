using System.Text.Json;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Llm;

namespace ScamDetector.Infrastructure.Tests.Llm;

public sealed class LlmVerdictTests
{
    [Theory]
    [InlineData("""{"label":"scam","confidence":0.9}""", MessageLabel.Scam, 0.9)]
    [InlineData("""{"label":"Spam","confidence":0.6}""", MessageLabel.Spam, 0.6)]
    [InlineData("""{"label":" normal ","confidence":1}""", MessageLabel.Normal, 1.0)]
    public void Parse_ValidJson_ReturnsVerdict(string json, MessageLabel expectedLabel, double expectedConfidence)
    {
        var verdict = LlmVerdict.Parse(json);

        Assert.Equal(new LlmVerdict(expectedLabel, expectedConfidence), verdict);
    }

    [Theory]
    [InlineData("""{"label":"scam","confidence":1.7}""", 1.0)]
    [InlineData("""{"label":"scam","confidence":-0.3}""", LlmVerdict.MinimumChosenLabelConfidence)]
    [InlineData("""{"label":"scam","confidence":0.2}""", LlmVerdict.MinimumChosenLabelConfidence)]
    public void Parse_ConfidenceOutOfRange_ClampsToChosenLabelRange(string json, double expectedConfidence)
    {
        var verdict = LlmVerdict.Parse(json);

        Assert.Equal(expectedConfidence, verdict!.Confidence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("""{"label":"phishing","confidence":0.9}""")]
    [InlineData("""{"confidence":0.9}""")]
    [InlineData("null")]
    [InlineData("""{"label":"scam"}""")]
    [InlineData("""{"label":"scam","confidence":null}""")]
    public void Parse_MissingLabelOrConfidenceOrUnknownLabel_ReturnsNull(string? json)
    {
        Assert.Null(LlmVerdict.Parse(json));
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => LlmVerdict.Parse("{label: scam"));
    }

    [Fact]
    public void ToPrediction_LowConfidenceVerdict_KeepsChosenLabelAsMostProbable()
    {
        var verdict = LlmVerdict.Parse("""{"label":"scam","confidence":0.2}""");

        var prediction = verdict!.ToPrediction();

        Assert.Equal(MessageLabel.Scam, prediction.Probabilities.MaxBy(pair => pair.Value).Key);
    }

    [Fact]
    public void ToPrediction_ScamVerdict_SplitsRemainingProbabilityEvenly()
    {
        var prediction = new LlmVerdict(MessageLabel.Scam, 0.8).ToPrediction();

        Assert.Equal(0.8, prediction.ProbabilityOf(MessageLabel.Scam), precision: 10);
        Assert.Equal(0.1, prediction.ProbabilityOf(MessageLabel.Spam), precision: 10);
        Assert.Equal(0.1, prediction.ProbabilityOf(MessageLabel.Normal), precision: 10);
    }

    [Fact]
    public void ToPrediction_AnyVerdict_ProbabilitiesSumToOne()
    {
        var prediction = new LlmVerdict(MessageLabel.Normal, 0.35).ToPrediction();

        Assert.Equal(1.0, prediction.Probabilities.Values.Sum(), precision: 10);
    }
}
