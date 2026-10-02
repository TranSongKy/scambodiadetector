using System.Text;
using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;
using ScamDetector.Core.Tests.Fakes;

namespace ScamDetector.Core.Tests.Reports;

public sealed class MessageReportServiceTests
{
    private const int GuidVersion7 = 7;
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 9, 15, 0, TimeSpan.Zero);

    private readonly FakeMessageReportRepository _repository = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    private MessageReportService CreateService(FakeMessageReportRepository? repository = null) =>
        new(repository ?? _repository, _timeProvider);

    [Fact]
    public async Task SubmitAsync_ValidText_ReturnsSuccessAndStoresOneReport()
    {
        var service = CreateService();

        var result = await service.SubmitAsync("Chuyển khoản ngay hôm nay", MessageLabel.Scam, ReportChannel.Api, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var report = Assert.Single(_repository.AddedReports);
        Assert.Equal(result.Value, report.Id);
        Assert.Equal("Chuyển khoản ngay hôm nay", report.MaskedText);
        Assert.Equal(MessageLabel.Scam, report.ReportedLabel);
        Assert.Equal(ReportChannel.Api, report.Channel);
        Assert.Equal(Now, report.CreatedAt);
    }

    [Theory]
    [InlineData(MessageLabel.Normal, ReportChannel.Api)]
    [InlineData(MessageLabel.Spam, ReportChannel.Telegram)]
    [InlineData(MessageLabel.Scam, ReportChannel.Extension)]
    public async Task SubmitAsync_LabelAndChannel_ArePassedToRepository(MessageLabel label, ReportChannel channel)
    {
        var service = CreateService();

        await service.SubmitAsync("tin nhắn thử", label, channel, CancellationToken.None);

        var report = Assert.Single(_repository.AddedReports);
        Assert.Equal(label, report.ReportedLabel);
        Assert.Equal(channel, report.Channel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n ")]
    public async Task SubmitAsync_EmptyOrWhitespaceText_ReturnsEmptyTextFailureWithoutCallingRepository(string text)
    {
        var service = CreateService();

        var result = await service.SubmitAsync(text, MessageLabel.Scam, ReportChannel.Api, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Same(ClassificationErrors.EmptyText, result.Error);
        Assert.Equal(0, _repository.CallCount);
    }

    [Fact]
    public async Task SubmitAsync_NullText_ReturnsEmptyTextFailureWithoutCallingRepository()
    {
        var service = CreateService();

        var result = await service.SubmitAsync(null!, MessageLabel.Scam, ReportChannel.Api, CancellationToken.None);

        Assert.Same(ClassificationErrors.EmptyText, result.Error);
        Assert.Equal(0, _repository.CallCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(ClassificationLimits.MaxMessageLength)]
    public async Task SubmitAsync_TextAtBoundaryLength_ReturnsSuccess(int length)
    {
        var service = CreateService();

        var result = await service.SubmitAsync(new string('a', length), MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(length, Assert.Single(_repository.AddedReports).MaskedText.Length);
    }

    [Fact]
    public async Task SubmitAsync_TextOneOverMaxLength_ReturnsTextTooLongFailureWithoutCallingRepository()
    {
        var service = CreateService();
        var text = new string('a', ClassificationLimits.MaxMessageLength + 1);

        var result = await service.SubmitAsync(text, MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Same(ClassificationErrors.TextTooLong, result.Error);
        Assert.Equal(0, _repository.CallCount);
    }

    [Fact]
    public async Task SubmitAsync_RawTextOverMaxButNormalizedWithinMax_ReturnsSuccess()
    {
        var service = CreateService();
        var text = new string('a', 999) + "   " + new string('a', 1000);

        var result = await service.SubmitAsync(text, MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.True(text.Length > ClassificationLimits.MaxMessageLength);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SubmitAsync_TextWithPhoneAndUrl_StoresMaskedTextOnly()
    {
        var service = CreateService();
        const string text = "Gọi 0912345678 hoặc vào https://vi-du-gia.example/nhan-thuong để nhận quà";

        await service.SubmitAsync(text, MessageLabel.Scam, ReportChannel.Telegram, CancellationToken.None);

        var report = Assert.Single(_repository.AddedReports);
        Assert.Equal("Gọi <PHONE> hoặc vào <URL> để nhận quà", report.MaskedText);
        Assert.DoesNotContain("0912345678", report.MaskedText);
        Assert.DoesNotContain("vi-du-gia", report.MaskedText);
    }

    [Fact]
    public async Task SubmitAsync_TextWithEmail_StoresMaskedEmail()
    {
        var service = CreateService();

        await service.SubmitAsync("Gửi về nguoi.dung@vidu.example ngay", MessageLabel.Scam, ReportChannel.Api, CancellationToken.None);

        var report = Assert.Single(_repository.AddedReports);
        Assert.Equal("Gửi về <EMAIL> ngay", report.MaskedText);
    }

    [Fact]
    public async Task SubmitAsync_UnaccentedTextWithPhone_StoresMaskedText()
    {
        var service = CreateService();

        await service.SubmitAsync("Goi ngay 0987654321 de nhan thuong", MessageLabel.Scam, ReportChannel.Api, CancellationToken.None);

        var report = Assert.Single(_repository.AddedReports);
        Assert.Equal("Goi ngay <PHONE> de nhan thuong", report.MaskedText);
    }

    [Fact]
    public async Task SubmitAsync_DecomposedUnicodeText_StoresComposedForm()
    {
        var service = CreateService();
        const string composed = "Chuyển khoản ngay hôm nay";
        var decomposed = composed.Normalize(NormalizationForm.FormD);

        await service.SubmitAsync(decomposed, MessageLabel.Scam, ReportChannel.Api, CancellationToken.None);

        var report = Assert.Single(_repository.AddedReports);
        Assert.NotEqual(composed, decomposed);
        Assert.Equal(composed, report.MaskedText);
    }

    [Fact]
    public async Task SubmitAsync_DecomposedTextWithPhone_MasksPhoneAndComposes()
    {
        var service = CreateService();
        var decomposed = "Liên hệ 0912345678 gấp".Normalize(NormalizationForm.FormD);

        await service.SubmitAsync(decomposed, MessageLabel.Scam, ReportChannel.Api, CancellationToken.None);

        var report = Assert.Single(_repository.AddedReports);
        Assert.Equal("Liên hệ <PHONE> gấp", report.MaskedText);
    }

    [Fact]
    public async Task SubmitAsync_TextWithExtraWhitespace_StoresCollapsedText()
    {
        var service = CreateService();

        await service.SubmitAsync("  xin   chào \n bạn  ", MessageLabel.Normal, ReportChannel.Api, CancellationToken.None);

        var report = Assert.Single(_repository.AddedReports);
        Assert.Equal("xin chào bạn", report.MaskedText);
    }

    [Fact]
    public async Task SubmitAsync_ValidText_ReturnsVersion7Guid()
    {
        var service = CreateService();

        var result = await service.SubmitAsync("tin nhắn thử", MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.Equal(GuidVersion7, result.Value.Version);
    }

    [Fact]
    public async Task SubmitAsync_TwoReportsAtDifferentTimes_ReturnsDifferentVersion7Guids()
    {
        var service = CreateService();

        var first = await service.SubmitAsync("tin một", MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);
        _timeProvider.UtcNow = Now.AddMinutes(5);
        var second = await service.SubmitAsync("tin hai", MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.NotEqual(first.Value, second.Value);
        Assert.Equal(GuidVersion7, first.Value.Version);
        Assert.Equal(GuidVersion7, second.Value.Version);
    }

    [Fact]
    public async Task SubmitAsync_TwoReportsAtSameTime_ReturnsDifferentGuids()
    {
        var service = CreateService();

        var first = await service.SubmitAsync("tin một", MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);
        var second = await service.SubmitAsync("tin hai", MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.NotEqual(first.Value, second.Value);
    }

    [Fact]
    public async Task SubmitAsync_TimeProviderAdvanced_UsesCurrentTimeForCreatedAt()
    {
        var service = CreateService();
        var later = Now.AddHours(3);
        _timeProvider.UtcNow = later;

        await service.SubmitAsync("tin nhắn thử", MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.Equal(later, Assert.Single(_repository.AddedReports).CreatedAt);
    }

    [Fact]
    public async Task SubmitAsync_CancellationToken_IsPassedToRepository()
    {
        var service = CreateService();
        using var source = new CancellationTokenSource();

        await service.SubmitAsync("tin nhắn thử", MessageLabel.Spam, ReportChannel.Api, source.Token);

        Assert.Equal(source.Token, _repository.ReceivedToken);
    }

    [Fact]
    public async Task SubmitAsync_RepositoryThrowsReportsUnavailable_PropagatesException()
    {
        var repository = new FakeMessageReportRepository { ExceptionToThrow = new ReportsUnavailableException("db down") };
        var service = CreateService(repository);

        var submit = () => service.SubmitAsync("tin nhắn thử", MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        await Assert.ThrowsAsync<ReportsUnavailableException>(submit);
    }
}
