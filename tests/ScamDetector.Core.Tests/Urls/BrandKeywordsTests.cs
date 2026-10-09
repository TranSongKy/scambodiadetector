using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Urls;

public sealed class BrandKeywordsTests
{
    [Fact]
    public void All_Keywords_AreLowercaseNonEmptyAndUnique()
    {
        var keywords = BrandKeywords.All;

        Assert.All(keywords, keyword => Assert.Equal(keyword.ToLowerInvariant(), keyword));
        Assert.All(keywords, keyword => Assert.NotEmpty(keyword));
        Assert.Equal(keywords.Count, keywords.Distinct().Count());
    }

    [Theory]
    [InlineData("vietcombank")]
    [InlineData("bidv")]
    [InlineData("momo")]
    [InlineData("zalo")]
    public void All_WellKnownBrand_IsIncluded(string brand)
    {
        var keywords = BrandKeywords.All;

        Assert.Contains(brand, keywords);
    }
}
