using ScamDetector.Core.Text;

namespace ScamDetector.Core.Tests.Text;

public sealed class ModelInputMaskerTests
{
    [Fact]
    public void Mask_EmptyText_ReturnsEmpty()
    {
        var masked = ModelInputMasker.Mask(string.Empty);

        Assert.Equal(string.Empty, masked);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("Tài khoản của bạn bị khóa")]
    [InlineData("Tai khoan cua ban bi khoa")]
    public void Mask_TextWithoutSensitiveData_ReturnsSameText(string text)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal(text, masked);
    }

    [Fact]
    public void Mask_Email_ReplacesWithEmailPlaceholder()
    {
        var masked = ModelInputMasker.Mask("gửi về nguoi.dung+a@mail.example ngay");

        Assert.Equal("gửi về <EMAIL> ngay", masked);
    }

    [Fact]
    public void Mask_EmailFollowedBySentencePeriod_KeepsPeriod()
    {
        var masked = ModelInputMasker.Mask("Mail a@b.example. Xong");

        Assert.Equal("Mail <EMAIL>. Xong", masked);
    }

    [Fact]
    public void Mask_Url_ReplacesWithUrlPlaceholder()
    {
        var masked = ModelInputMasker.Mask("nhấn http://a.example/x để nhận");

        Assert.Equal("nhấn <URL> để nhận", masked);
    }

    [Fact]
    public void Mask_UrlWithTrailingPunctuation_KeepsPunctuation()
    {
        var masked = ModelInputMasker.Mask("nhấn http://a.example/x.");

        Assert.Equal("nhấn <URL>.", masked);
    }

    [Theory]
    [InlineData("gọi 0900000000 ngay")]
    [InlineData("gọi 0900 000 000 ngay")]
    [InlineData("gọi 0900.000.000 ngay")]
    [InlineData("gọi 0900-000-000 ngay")]
    [InlineData("gọi +84900000000 ngay")]
    [InlineData("gọi +84 900 000 000 ngay")]
    [InlineData("gọi 84900000000 ngay")]
    public void Mask_VietnamesePhone_ReplacesWithPhonePlaceholder(string text)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal("gọi <PHONE> ngay", masked);
    }

    [Fact]
    public void Mask_ElevenDigitLandline_ReplacesWithPhonePlaceholder()
    {
        var masked = ModelInputMasker.Mask("gọi 02400000000 ngay");

        Assert.Equal("gọi <PHONE> ngay", masked);
    }

    [Theory]
    [InlineData("gọi 090 123 4567 ngay")]
    [InlineData("gọi 0901.234.567 ngay")]
    [InlineData("gọi +84 901 234 567 ngay")]
    [InlineData("gọi 0901234567 ngay")]
    public void Mask_PhoneWithGroupedDigits_ReplacesWithPhonePlaceholder(string text)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal("gọi <PHONE> ngay", masked);
    }

    [Theory]
    [InlineData("Han 01.02.2026 10.30 den")]
    [InlineData("gọi 1900 5454 13 ngay")]
    public void Mask_DateTimeOrHotline_IsNotMasked(string text)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal(text, masked);
    }

    [Theory]
    [InlineData("OTP: 123456 het han", "OTP: <OTP> het han")]
    [InlineData("otp 1234 het han", "otp <OTP> het han")]
    [InlineData("OTP: 12345678 het han", "OTP: <OTP> het han")]
    [InlineData("OTP: 123 456 het han luc 10:30", "OTP: <OTP> het han luc 10:30")]
    [InlineData("OTP: 123-456 het han", "OTP: <OTP> het han")]
    [InlineData("OTP: 12.34.56 het han", "OTP: <OTP> het han")]
    [InlineData("Mã xác nhận là 654321.", "Mã xác nhận là <OTP>.")]
    [InlineData("Ma xac thuc: 654321 nhe", "Ma xac thuc: <OTP> nhe")]
    [InlineData("mã xác minh 6543 nhé", "mã xác minh <OTP> nhé")]
    [InlineData("Mã bảo mật 888999 nhé", "Mã bảo mật <OTP> nhé")]
    [InlineData("MÃ GIAO DỊCH 888999 nhé", "MÃ GIAO DỊCH <OTP> nhé")]
    public void Mask_OtpAfterKeyword_ReplacesWithOtpPlaceholder(string text, string expected)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal(expected, masked);
    }

    [Fact]
    public void Mask_FourDigitsWithoutOtpKeyword_IsNotMasked()
    {
        var masked = ModelInputMasker.Mask("nam 2026 va ma 1234 ok");

        Assert.Equal("nam 2026 va ma 1234 ok", masked);
    }

    [Fact]
    public void Mask_OtpCodeFarFromKeyword_IsNotMasked()
    {
        var masked = ModelInputMasker.Mask("OTP la mot ma bi mat khong duoc chia se 123456");

        Assert.Equal("OTP la mot ma bi mat khong duoc chia se 123456", masked);
    }

    [Fact]
    public void Mask_NineDigitsAfterOtp_ReplacesWithAccountPlaceholder()
    {
        var masked = ModelInputMasker.Mask("OTP: 123456789 ok");

        Assert.Equal("OTP: <ACCOUNT> ok", masked);
    }

    [Theory]
    [InlineData("CCCD: 012345678901 của tôi", "CCCD: <ID> của tôi")]
    [InlineData("cmnd 012345678 cua toi", "cmnd <ID> cua toi")]
    [InlineData("căn cước 012345678901 ok", "căn cước <ID> ok")]
    [InlineData("can cuoc 012345678901 ok", "can cuoc <ID> ok")]
    [InlineData("chứng minh nhân dân số 012345678 ok", "chứng minh nhân dân số <ID> ok")]
    [InlineData("chung minh 012345678 ok", "chung minh <ID> ok")]
    [InlineData("định danh 012345678901 ok", "định danh <ID> ok")]
    [InlineData("dinh danh 012345678901 ok", "dinh danh <ID> ok")]
    public void Mask_IdAfterKeyword_ReplacesWithIdPlaceholder(string text, string expected)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal(expected, masked);
    }

    [Theory]
    [InlineData("012345678901")]
    [InlineData("012345678")]
    public void Mask_IdLengthDigitsWithoutKeyword_ReplacesWithAccountPlaceholder(string digits)
    {
        var masked = ModelInputMasker.Mask($"so {digits} ok");

        Assert.Equal("so <ACCOUNT> ok", masked);
    }

    [Theory]
    [InlineData("CCCD 0123456789 ok")]
    [InlineData("CCCD 01234567890 ok")]
    public void Mask_IdKeywordWithTenOrElevenDigits_IsNotMaskedAsId(string text)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.DoesNotContain("<ID>", masked);
    }

    [Theory]
    [InlineData("thẻ 1234 5678 9012 3456 bị khóa")]
    [InlineData("thẻ 1234-5678-9012-3456 bị khóa")]
    [InlineData("thẻ 1234567890123456 bị khóa")]
    public void Mask_CardNumber_ReplacesWithAccountPlaceholder(string text)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal("thẻ <ACCOUNT> bị khóa", masked);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("1234567890123456789")]
    public void Mask_AccountDigitRunWithinBounds_ReplacesWithAccountPlaceholder(string digits)
    {
        var masked = ModelInputMasker.Mask($"stk {digits} ok");

        Assert.Equal("stk <ACCOUNT> ok", masked);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("12345678901234567890")]
    public void Mask_DigitRunOutsideBounds_IsNotMasked(string digits)
    {
        var masked = ModelInputMasker.Mask($"stk {digits} ok");

        Assert.Equal($"stk {digits} ok", masked);
    }

    [Theory]
    [InlineData("nhận 500.000.000 đồng")]
    [InlineData("nhận 500.000 đồng")]
    [InlineData("ngày 01/02/2025 và 01-02-2025")]
    public void Mask_AmountOrDate_IsNotMasked(string text)
    {
        var masked = ModelInputMasker.Mask(text);

        Assert.Equal(text, masked);
    }

    [Theory]
    [InlineData("<URL>")]
    [InlineData("<PHONE>")]
    [InlineData("<EMAIL>")]
    [InlineData("<ACCOUNT>")]
    [InlineData("<ID>")]
    [InlineData("<OTP>")]
    public void Mask_ExistingPlaceholder_StaysIntact(string placeholder)
    {
        var masked = ModelInputMasker.Mask($"xem {placeholder} ngay");

        Assert.Equal($"xem {placeholder} ngay", masked);
    }

    [Fact]
    public void Mask_AllKindsTogether_ReplacesEach()
    {
        var masked = ModelInputMasker.Mask("a@b.example http://a.example 0900000000 1234567890");

        Assert.Equal("<EMAIL> <URL> <PHONE> <ACCOUNT>", masked);
    }

    [Fact]
    public void Mask_TextAtMaxLength_ReturnsSameLength()
    {
        var text = new string('a', 2000);

        var masked = ModelInputMasker.Mask(text);

        Assert.Equal(text, masked);
    }

    [Fact]
    public void Mask_NonNfcText_IsNotChanged()
    {
        var decomposed = "Tài khoản".Normalize(System.Text.NormalizationForm.FormD);

        var masked = ModelInputMasker.Mask(decomposed);

        Assert.Equal(decomposed, masked);
    }
}
