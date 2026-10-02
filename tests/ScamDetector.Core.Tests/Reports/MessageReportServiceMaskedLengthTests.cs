using ScamDetector.Core.Classification;
using ScamDetector.Core.Reports;
using ScamDetector.Core.Tests.Fakes;

namespace ScamDetector.Core.Tests.Reports;

public sealed class MessageReportServiceMaskedLengthTests
{
    private const string ShortEmail = "a@b.vn ";

    [Fact]
    public async Task SubmitAsync_MaskingMakesTextLongerThanLimit_ReturnsTextTooLongWithoutSaving()
    {
        var repository = new FakeMessageReportRepository();
        var service = new MessageReportService(repository, TimeProvider.System);
        var text = string.Concat(Enumerable.Repeat(ShortEmail, ClassificationLimits.MaxMessageLength / ShortEmail.Length));

        var result = await service.SubmitAsync(text, MessageLabel.Spam, ReportChannel.Api, CancellationToken.None);

        Assert.True(text.Trim().Length <= ClassificationLimits.MaxMessageLength);
        Assert.Same(ClassificationErrors.TextTooLong, result.Error);
        Assert.Empty(repository.AddedReports);
    }
}
