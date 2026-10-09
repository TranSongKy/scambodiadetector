using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Urls;

public sealed class BrandImpersonationTests
{
    private static bool NoHostIsOfficial(string host) => false;

    private static bool EveryHostIsOfficial(string host) => true;

    [Fact]
    public void IsImpersonating_OfficialHost_ReturnsFalse()
    {
        var result = BrandImpersonation.IsImpersonating("vietcombank.com.vn", EveryHostIsOfficial);

        Assert.False(result);
    }

    [Fact]
    public void IsImpersonating_BrandNameOnSuspiciousTopLevelDomainNotOfficial_ReturnsTrue()
    {
        var result = BrandImpersonation.IsImpersonating("vietcombank.top", NoHostIsOfficial);

        Assert.True(result);
    }

    [Fact]
    public void IsImpersonating_PassesHostToOfficialCheck()
    {
        string? receivedHost = null;

        BrandImpersonation.IsImpersonating("a.example", host =>
        {
            receivedHost = host;
            return true;
        });

        Assert.Equal("a.example", receivedHost);
    }

    [Theory]
    [InlineData("bidv-xacthuc.com")]
    [InlineData("acb.online")]
    [InlineData("zalo-verify.top")]
    [InlineData("momo.top")]
    [InlineData("secure.momo.xyz")]
    [InlineData("login_vcb.net")]
    [InlineData("ACB.online")]
    public void IsImpersonating_ShortKeywordAsExactToken_ReturnsTrue(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.True(result);
    }

    [Theory]
    [InlineData("vibe.com")]
    [InlineData("acbd.net")]
    [InlineData("momostore.com")]
    [InlineData("shbank-fake.example")]
    [InlineData("bidvn.com")]
    public void IsImpersonating_ShortKeywordInsideLongerToken_ReturnsFalse(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.False(result);
    }

    [Theory]
    [InlineData("vietcombank24h.com")]
    [InlineData("xacthucvietcombank.net")]
    [InlineData("shopee-khuyenmai.top")]
    [InlineData("tiktokshop.vip")]
    [InlineData("dichvucong-gov.xyz")]
    public void IsImpersonating_LongKeywordInsideToken_ReturnsTrue(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.True(result);
    }

    [Theory]
    [InlineData("vietcornbank.com")]
    [InlineData("v1etinbank.com")]
    [InlineData("vietc0mbank.com")]
    [InlineData("VIETCORNBANK.com")]
    [InlineData("faceb00k.com")]
    public void IsImpersonating_LookalikeCharacters_ReturnsTrue(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.True(result);
    }

    [Theory]
    [InlineData("vietcombnk.com")]
    [InlineData("techcombnak.com")]
    [InlineData("vietcombanx.com")]
    [InlineData("vietcombbank.com")]
    public void IsImpersonating_SingleTypoInLongKeyword_ReturnsTrue(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.True(result);
    }

    [Theory]
    [InlineData("shoppe.com")]
    [InlineData("lazadda.com")]
    [InlineData("zalpay.com")]
    public void IsImpersonating_SingleTypoInShortKeyword_ReturnsFalse(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.False(result);
    }

    [Fact]
    public void IsImpersonating_TwoTyposInLongKeyword_ReturnsFalse()
    {
        var result = BrandImpersonation.IsImpersonating("vietcmbnk.com", NoHostIsOfficial);

        Assert.False(result);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("google.com")]
    [InlineData("thanhnien.vn")]
    [InlineData("a.example")]
    [InlineData("x")]
    [InlineData("")]
    public void IsImpersonating_OrdinaryHost_ReturnsFalse(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.False(result);
    }

    [Fact]
    public void IsImpersonating_VeryLongHostWithoutBrand_ReturnsFalse()
    {
        var host = new string('x', 253);

        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.False(result);
    }

    [Theory]
    [InlineData("rn", "m")]
    [InlineData("vv", "w")]
    [InlineData("0", "o")]
    [InlineData("1", "l")]
    [InlineData("i", "l")]
    [InlineData("3", "e")]
    [InlineData("4", "a")]
    [InlineData("5", "s")]
    [InlineData("ABC", "abc")]
    [InlineData("Rn", "m")]
    [InlineData("", "")]
    [InlineData("xyz", "xyz")]
    public void Skeleton_Token_ReturnsMappedLowercase(string token, string expected)
    {
        var skeleton = BrandImpersonation.Skeleton(token);

        Assert.Equal(expected, skeleton);
    }

    [Fact]
    public void Skeleton_LookalikeBrand_EqualsSkeletonOfOriginal()
    {
        var lookalike = BrandImpersonation.Skeleton("vietcornbank");
        var original = BrandImpersonation.Skeleton("vietcombank");

        Assert.Equal(original, lookalike);
    }

    [Theory]
    [InlineData("vietcombank.com")]
    [InlineData("vietcombank.vn")]
    [InlineData("momo.link")]
    [InlineData("bidv.info")]
    [InlineData("www.vietcombank.com.vn")]
    public void IsImpersonating_BrandNameNotListedAsOfficial_ReturnsTrue(string host)
    {
        var result = BrandImpersonation.IsImpersonating(host, NoHostIsOfficial);

        Assert.True(result);
    }
}
