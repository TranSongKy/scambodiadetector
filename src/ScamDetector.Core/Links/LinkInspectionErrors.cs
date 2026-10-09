using ScamDetector.Core.Common;

namespace ScamDetector.Core.Links;

public static class LinkInspectionErrors
{
    public static readonly DomainError NoLinks = new(
        "link_inspection.no_links",
        "At least one link is required.");

    public static readonly DomainError TooManyLinks = new(
        "link_inspection.too_many_links",
        $"At most {LinkInspectionLimits.MaxLinksPerRequest} links can be inspected per request.");

    public static readonly DomainError InvalidLink = new(
        "link_inspection.invalid_link",
        $"Each link must be non-empty and at most {LinkInspectionLimits.MaxLinkLength} characters.");
}
