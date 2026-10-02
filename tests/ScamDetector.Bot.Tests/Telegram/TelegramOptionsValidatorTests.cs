using ScamDetector.Bot.Telegram;

namespace ScamDetector.Bot.Tests.Telegram;

public sealed class TelegramOptionsValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingToken_Throws(string token)
    {
        var options = new TelegramOptions { BotToken = token };

        Assert.Throws<InvalidOperationException>(() => TelegramOptionsValidator.Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositivePollingTimeout_Throws(int timeoutSeconds)
    {
        var options = new TelegramOptions { BotToken = "fake-token", PollingTimeoutSeconds = timeoutSeconds };

        Assert.Throws<InvalidOperationException>(() => TelegramOptionsValidator.Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveRetryDelay_Throws(int delaySeconds)
    {
        var options = new TelegramOptions { BotToken = "fake-token", ErrorRetryDelaySeconds = delaySeconds };

        Assert.Throws<InvalidOperationException>(() => TelegramOptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_ValidOptions_ReturnsSameOptions()
    {
        var options = new TelegramOptions { BotToken = "fake-token" };

        var validated = TelegramOptionsValidator.Validate(options);

        Assert.Same(options, validated);
    }

    [Fact]
    public void BotApiAddress_DefaultBaseUrl_BuildsBotTokenUrlWithTrailingSlash()
    {
        var options = new TelegramOptions { BotToken = "fake-token" };

        var address = TelegramOptionsValidator.BotApiAddress(options);

        Assert.Equal("https://api.telegram.org/botfake-token/", address.AbsoluteUri);
    }

    [Fact]
    public void BotApiAddress_RealTokenFormatWithColon_StaysUnderApiBaseUrl()
    {
        var options = new TelegramOptions { BotToken = "123456:ABC-def_ghi" };

        var address = TelegramOptionsValidator.BotApiAddress(options);

        Assert.Equal("https://api.telegram.org/bot123456:ABC-def_ghi/", address.AbsoluteUri);
    }

    [Fact]
    public void Validate_PlainHttpRemoteApiBaseUrl_Throws()
    {
        var options = new TelegramOptions { BotToken = "fake-token", ApiBaseUrl = new Uri("http://telegram.example") };

        Assert.Throws<InvalidOperationException>(() => TelegramOptionsValidator.Validate(options));
    }

    [Fact]
    public void Validate_PlainHttpLoopbackApiBaseUrl_IsAllowed()
    {
        var options = new TelegramOptions { BotToken = "fake-token", ApiBaseUrl = new Uri("http://127.0.0.1:5901") };

        Assert.Same(options, TelegramOptionsValidator.Validate(options));
    }
}
