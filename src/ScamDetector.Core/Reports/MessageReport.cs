using ScamDetector.Core.Classification;

namespace ScamDetector.Core.Reports;

public sealed class MessageReport
{
    public MessageReport(Guid id, string maskedText, MessageLabel reportedLabel, ReportChannel channel, DateTimeOffset createdAt)
    {
        Id = id;
        MaskedText = maskedText;
        ReportedLabel = reportedLabel;
        Channel = channel;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string MaskedText { get; private set; }

    public MessageLabel ReportedLabel { get; private set; }

    public ReportChannel Channel { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
