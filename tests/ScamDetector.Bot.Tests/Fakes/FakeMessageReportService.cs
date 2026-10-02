using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;
using ScamDetector.Core.Reports;

namespace ScamDetector.Bot.Tests.Fakes;

public sealed class FakeMessageReportService(Exception? exception = null) : IMessageReportService
{
    public List<(string Text, MessageLabel Label, ReportChannel Channel)> Submissions { get; } = [];

    public Task<Result<Guid>> SubmitAsync(
        string text,
        MessageLabel reportedLabel,
        ReportChannel channel,
        CancellationToken cancellationToken)
    {
        if (exception is not null)
            throw exception;

        Submissions.Add((text, reportedLabel, channel));
        return Task.FromResult(Result.Success(Guid.CreateVersion7()));
    }

    public Task<IReadOnlyList<MessageReport>> ListAsync(DateTimeOffset? since, int? limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MessageReport>>([]);
}
