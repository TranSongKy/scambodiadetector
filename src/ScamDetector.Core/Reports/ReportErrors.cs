using ScamDetector.Core.Common;

namespace ScamDetector.Core.Reports;

public static class ReportErrors
{
    public static readonly DomainError InvalidLabel = new(
        "report.invalid_label",
        "Label must be one of: scam, spam, normal.");

    public static readonly DomainError InvalidChannel = new(
        "report.invalid_channel",
        "Channel must be one of: api, telegram, extension.");
}
