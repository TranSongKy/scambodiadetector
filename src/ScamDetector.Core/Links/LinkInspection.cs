namespace ScamDetector.Core.Links;

public sealed record LinkInspection(string Url, LinkVerdict Verdict, IReadOnlyList<string> Reasons);
