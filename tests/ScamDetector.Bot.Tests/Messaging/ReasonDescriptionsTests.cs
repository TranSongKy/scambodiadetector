using ScamDetector.Bot.Messaging;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Urls;

namespace ScamDetector.Bot.Tests.Messaging;

public sealed class ReasonDescriptionsTests
{
    public static TheoryData<string> KnownReasonCodes => new()
    {
        ClassificationReasons.ModelPredictedScam,
        ClassificationReasons.ModelPredictedSpam,
        ClassificationReasons.ScamBelowThreshold,
        UrlReasons.Shortener,
        UrlReasons.IpAddressHost,
        UrlReasons.Punycode,
        UrlReasons.SuspiciousTopLevelDomain,
    };

    [Theory]
    [MemberData(nameof(KnownReasonCodes))]
    public void Describe_KnownReasonCode_ReturnsVietnameseDescription(string reasonCode)
    {
        var description = ReasonDescriptions.Describe(reasonCode);

        Assert.NotEqual(reasonCode, description);
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.DoesNotContain('_', description);
    }

    [Fact]
    public void Describe_UnknownReasonCode_ReturnsCode()
    {
        var description = ReasonDescriptions.Describe("unknown_code");

        Assert.Equal("unknown_code", description);
    }
}
