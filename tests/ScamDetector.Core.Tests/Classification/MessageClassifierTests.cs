using System.Text;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Tests.Builders;
using ScamDetector.Core.Tests.Fakes;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Tests.Classification;

public sealed class MessageClassifierTests
{
    private const string AccentedText = "Tài khoản của bạn bị khóa";
    private const string UnaccentedText = "Tai khoan cua ban bi khoa";
    private const string UrlReason = "url_shortener";

    private readonly FakeScamModel _model = new(PredictionFactory.Create(normal: 0.1, spam: 0.1, scam: 0.8));
    private readonly FakeUrlInspector _urlInspector = new([new UrlFinding("http://a.example", UrlReason)]);

    private MessageClassifier CreateClassifier() => new(_model, _urlInspector, new ClassificationOptions());

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" \t\r\n ")]
    public async Task ClassifyAsync_EmptyOrWhitespaceText_ReturnsEmptyTextFailure(string text)
    {
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync(text, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Same(ClassificationErrors.EmptyText, result.Error);
    }

    [Fact]
    public async Task ClassifyAsync_NullText_ReturnsEmptyTextFailure()
    {
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync(null!, CancellationToken.None);

        Assert.Same(ClassificationErrors.EmptyText, result.Error);
    }

    [Fact]
    public async Task ClassifyAsync_EmptyText_DoesNotCallModelOrUrlInspector()
    {
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync("   ", CancellationToken.None);

        Assert.Equal(0, _model.CallCount);
        Assert.Equal(0, _urlInspector.CallCount);
    }

    [Fact]
    public async Task ClassifyAsync_SingleCharacter_ReturnsSuccess()
    {
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync("a", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("a", _model.ReceivedText);
    }

    [Fact]
    public async Task ClassifyAsync_MaxLengthText_ReturnsSuccess()
    {
        var text = new string('a', ClassificationLimits.MaxMessageLength);
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync(text, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _model.CallCount);
    }

    [Fact]
    public async Task ClassifyAsync_TextOneOverMaxLength_ReturnsTextTooLongFailure()
    {
        var text = new string('a', ClassificationLimits.MaxMessageLength + 1);
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync(text, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Same(ClassificationErrors.TextTooLong, result.Error);
    }

    [Fact]
    public async Task ClassifyAsync_TextTooLong_DoesNotCallModelOrUrlInspector()
    {
        var text = new string('a', ClassificationLimits.MaxMessageLength + 1);
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync(text, CancellationToken.None);

        Assert.Equal(0, _model.CallCount);
        Assert.Equal(0, _urlInspector.CallCount);
    }

    [Fact]
    public async Task ClassifyAsync_DecomposedTextWhoseComposedLengthIsMax_ReturnsSuccess()
    {
        var composed = new string('é', ClassificationLimits.MaxMessageLength);
        var decomposed = composed.Normalize(NormalizationForm.FormD);
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync(decomposed, CancellationToken.None);

        Assert.True(decomposed.Length > ClassificationLimits.MaxMessageLength);
        Assert.True(result.IsSuccess);
        Assert.Equal(composed, _model.ReceivedText);
    }

    [Fact]
    public async Task ClassifyAsync_DecomposedTextWhoseComposedLengthExceedsMax_ReturnsTextTooLongFailure()
    {
        var composed = new string('é', ClassificationLimits.MaxMessageLength + 1);
        var decomposed = composed.Normalize(NormalizationForm.FormD);
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync(decomposed, CancellationToken.None);

        Assert.Same(ClassificationErrors.TextTooLong, result.Error);
    }

    [Fact]
    public async Task ClassifyAsync_DecomposedVietnamese_PassesNfcTextToModelAndUrlInspector()
    {
        var decomposed = AccentedText.Normalize(NormalizationForm.FormD);
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync(decomposed, CancellationToken.None);

        Assert.NotEqual(AccentedText, decomposed);
        Assert.Equal(AccentedText, _model.ReceivedText);
        Assert.Equal(AccentedText, _urlInspector.ReceivedText);
    }

    [Fact]
    public async Task ClassifyAsync_ExtraWhitespace_PassesCollapsedTextToModelAndUrlInspector()
    {
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync("  Tài   khoản \n bị khóa  ", CancellationToken.None);

        Assert.Equal("Tài khoản bị khóa", _model.ReceivedText);
        Assert.Equal("Tài khoản bị khóa", _urlInspector.ReceivedText);
    }

    [Fact]
    public async Task ClassifyAsync_VietnameseWithoutDiacritics_PassesTextUnchanged()
    {
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync(UnaccentedText, CancellationToken.None);

        Assert.Equal(UnaccentedText, _model.ReceivedText);
    }

    [Fact]
    public async Task ClassifyAsync_CancellationToken_IsPassedToModelAndUrlInspector()
    {
        using var source = new CancellationTokenSource();
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync(AccentedText, source.Token);

        Assert.Equal(source.Token, _model.ReceivedToken);
        Assert.Equal(source.Token, _urlInspector.ReceivedToken);
    }

    [Fact]
    public async Task ClassifyAsync_ScamPredictionWithUrlFinding_ReturnsScamResultWithUrlReason()
    {
        var classifier = CreateClassifier();

        var result = await classifier.ClassifyAsync(AccentedText, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MessageLabel.Scam, result.Value.Label);
        Assert.Equal(0.8, result.Value.Confidence);
        Assert.Equal([ClassificationReasons.ModelPredictedScam, UrlReason], result.Value.Reasons);
    }

    [Fact]
    public async Task ClassifyAsync_CustomThresholdAboveScamProbability_ReturnsNonScam()
    {
        var options = new ClassificationOptions { ScamThreshold = 0.95 };
        var classifier = new MessageClassifier(_model, _urlInspector, options);

        var result = await classifier.ClassifyAsync(AccentedText, CancellationToken.None);

        Assert.NotEqual(MessageLabel.Scam, result.Value.Label);
        Assert.Contains(ClassificationReasons.ScamBelowThreshold, result.Value.Reasons);
    }

    [Fact]
    public async Task ClassifyAsync_TextWithUrl_PassesMaskedTextToModel()
    {
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync("Nhấn http://a.example/x để nhận quà", CancellationToken.None);

        Assert.Equal("Nhấn <URL> để nhận quà", _model.ReceivedText);
    }

    [Fact]
    public async Task ClassifyAsync_TextWithUrl_PassesUnmaskedTextToUrlInspector()
    {
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync("Nhấn http://a.example/x để nhận quà", CancellationToken.None);

        Assert.Equal("Nhấn http://a.example/x để nhận quà", _urlInspector.ReceivedText);
    }

    [Fact]
    public async Task ClassifyAsync_TextWithPhoneAndEmail_PassesMaskedTextToModelOnly()
    {
        var classifier = CreateClassifier();

        await classifier.ClassifyAsync("gọi 0900000000 hoặc a@b.example", CancellationToken.None);

        Assert.Equal("gọi <PHONE> hoặc <EMAIL>", _model.ReceivedText);
        Assert.Equal("gọi 0900000000 hoặc a@b.example", _urlInspector.ReceivedText);
    }
}
