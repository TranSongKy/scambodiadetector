using ScamDetector.Core.Classification;
using ScamDetector.Core.Tests.Builders;
using ScamDetector.Core.ThreatIntel;
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

    [Fact]
    public void From_ModelNormalButBlocklistedDomain_ReturnsScamWithBlocklistConfidence()
    {
        var prediction = PredictionFactory.Create(normal: 0.9, spam: 0.05, scam: 0.05);
        var threat = new ThreatMatch(["bad.example"], null);

        var result = ClassificationResult.From(prediction, NoFindings, threat, Threshold);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(ThreatMatch.BlocklistedDomainConfidence, result.Confidence);
        Assert.Equal([ThreatReasons.BlocklistedDomain], result.Reasons);
    }

    [Fact]
    public void From_ModelNormalButTemplateMatch_ReturnsScamWithTemplateReasonAndUrlReasons()
    {
        var prediction = PredictionFactory.Create(normal: 0.9, spam: 0.05, scam: 0.05);
        var threat = new ThreatMatch([], new TemplateMatch("t1", 0.8));

        var result = ClassificationResult.From(prediction, TwoFindings, threat, Threshold);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(0.8, result.Confidence);
        Assert.Equal([ThreatReasons.KnownScamTemplate, FirstUrlReason, SecondUrlReason], result.Reasons);
    }

    [Fact]
    public void From_ModelScamHigherThanThreatConfidence_UsesModelProbability()
    {
        var prediction = PredictionFactory.Create(normal: 0.0, spam: 0.0, scam: 1.0);
        var threat = new ThreatMatch([], new TemplateMatch("t1", 0.6));

        var result = ClassificationResult.From(prediction, NoFindings, threat, Threshold);

        Assert.Equal(1.0, result.Confidence);
        Assert.Equal([ClassificationReasons.ModelPredictedScam, ThreatReasons.KnownScamTemplate], result.Reasons);
    }

    [Fact]
    public void From_ThreatConfidenceHigherThanModelScam_UsesThreatConfidence()
    {
        var prediction = PredictionFactory.Create(normal: 0.1, spam: 0.1, scam: 0.8);
        var threat = new ThreatMatch(["bad.example"], null);

        var result = ClassificationResult.From(prediction, NoFindings, threat, Threshold);

        Assert.Equal(ThreatMatch.BlocklistedDomainConfidence, result.Confidence);
        Assert.Equal([ClassificationReasons.ModelPredictedScam, ThreatReasons.BlocklistedDomain], result.Reasons);
    }

    [Fact]
    public void From_NoThreat_BehavesLikeOverloadWithoutThreat()
    {
        var prediction = PredictionFactory.Create(normal: 0.6, spam: 0.3, scam: 0.1);

        var withNone = ClassificationResult.From(prediction, TwoFindings, ThreatMatch.None, Threshold);
        var without = ClassificationResult.From(prediction, TwoFindings, Threshold);

        Assert.Equal(without.Label, withNone.Label);
        Assert.Equal(without.Confidence, withNone.Confidence);
        Assert.Equal(without.Reasons, withNone.Reasons);
    }

    [Fact]
    public void FromThreatSignals_BlocklistedDomainAndUrlFindings_ReturnsScamWithAllReasons()
    {
        var threat = new ThreatMatch(["bad.example"], new TemplateMatch("t1", 0.9));

        var result = ClassificationResult.FromThreatSignals(threat, TwoFindings);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(ThreatMatch.BlocklistedDomainConfidence, result.Confidence);
        Assert.Equal(
            [ThreatReasons.BlocklistedDomain, ThreatReasons.KnownScamTemplate, FirstUrlReason, SecondUrlReason],
            result.Reasons);
    }

    [Fact]
    public void FromThreatSignals_TemplateOnlyAndNoUrlFindings_ReturnsTemplateCoverageAsConfidence()
    {
        var threat = new ThreatMatch([], new TemplateMatch("t1", 0.7));

        var result = ClassificationResult.FromThreatSignals(threat, NoFindings);

        Assert.Equal(0.7, result.Confidence);
        Assert.Equal([ThreatReasons.KnownScamTemplate], result.Reasons);
    }

    [Fact]
    public void From_ModelBelowThresholdButBrandImpersonation_ReturnsScamWithoutModelReason()
    {
        var prediction = PredictionFactory.Create(normal: 0.3, spam: 0.2, scam: 0.5);
        var findings = new[] { new UrlFinding("http://bidv-xacthuc.com", UrlReasons.BrandImpersonation) };

        var result = ClassificationResult.From(prediction, findings, Threshold);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(0.8, result.Confidence, precision: 10);
        Assert.Equal([UrlReasons.BrandImpersonation], result.Reasons);
        Assert.DoesNotContain(ClassificationReasons.ModelPredictedScam, result.Reasons);
    }

    [Fact]
    public void From_ModelAboveThresholdAndUrlFinding_IncludesModelReasonBeforeUrlReason()
    {
        var prediction = PredictionFactory.Create(normal: 0.1, spam: 0.1, scam: 0.8);
        var findings = new[] { new UrlFinding("http://bidv-xacthuc.com", UrlReasons.BrandImpersonation) };

        var result = ClassificationResult.From(prediction, findings, Threshold);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(UrlRisk.CombineWithModel(0.8, UrlRisk.BrandImpersonationWeight), result.Confidence, precision: 10);
        Assert.Equal([ClassificationReasons.ModelPredictedScam, UrlReasons.BrandImpersonation], result.Reasons);
    }

    [Fact]
    public void From_ModelNormalAndWeakUrlRisk_StaysNormalWithModelConfidence()
    {
        var prediction = PredictionFactory.Create(normal: 0.9, spam: 0.05, scam: 0.05);
        var findings = new[] { new UrlFinding("http://a.example", UrlReasons.Shortener) };

        var result = ClassificationResult.From(prediction, findings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.Equal(0.9, result.Confidence);
        Assert.Equal([UrlReasons.Shortener], result.Reasons);
    }

    [Fact]
    public void From_ModelNormalAndUnknownUrlReason_DoesNotRaiseScamProbability()
    {
        var prediction = PredictionFactory.Create(normal: 0.9, spam: 0.05, scam: 0.05);

        var result = ClassificationResult.From(prediction, TwoFindings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.DoesNotContain(ClassificationReasons.ScamBelowThreshold, result.Reasons);
    }

    [Fact]
    public void From_UrlRiskLiftsScamAboveNonScamLabel_ReturnsScamBelowThresholdReasonWhenStillBelow()
    {
        var prediction = PredictionFactory.Create(normal: 0.4, spam: 0.2, scam: 0.4);
        var findings = new[] { new UrlFinding("http://a.example", UrlReasons.Shortener) };

        var result = ClassificationResult.From(prediction, findings, Threshold);

        Assert.Equal(MessageLabel.Normal, result.Label);
        Assert.Equal([ClassificationReasons.ScamBelowThreshold, UrlReasons.Shortener], result.Reasons);
    }

    [Fact]
    public void FromUrlSignals_Findings_ReturnsScamWithCombinedRiskAndReasons()
    {
        var findings = new[]
        {
            new UrlFinding("vtp-vandon.online", UrlReasons.Obfuscated),
            new UrlFinding("vtp-vandon.online", UrlReasons.BrandImpersonation),
        };

        var result = ClassificationResult.FromUrlSignals(findings);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(UrlRisk.Combine(findings), result.Confidence, precision: 10);
        Assert.Equal([UrlReasons.Obfuscated, UrlReasons.BrandImpersonation], result.Reasons);
    }

    [Fact]
    public void FromUrlSignals_NoFindings_ReturnsScamWithZeroConfidenceAndNoReasons()
    {
        var result = ClassificationResult.FromUrlSignals(NoFindings);

        Assert.Equal(MessageLabel.Scam, result.Label);
        Assert.Equal(0, result.Confidence);
        Assert.Empty(result.Reasons);
    }
}
