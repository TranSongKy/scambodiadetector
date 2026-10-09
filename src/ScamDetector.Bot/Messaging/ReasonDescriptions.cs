using System.Collections.Frozen;
using ScamDetector.Core.Classification;
using ScamDetector.Core.ThreatIntel;
using ScamDetector.Core.Urls;

namespace ScamDetector.Bot.Messaging;

public static class ReasonDescriptions
{
    private static readonly FrozenDictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ClassificationReasons.ModelPredictedScam] = "Nội dung giống các tin lừa đảo đã biết",
        [ClassificationReasons.ModelPredictedSpam] = "Nội dung giống tin quảng cáo hàng loạt",
        [ClassificationReasons.ScamBelowThreshold] = "Có vài dấu hiệu lừa đảo nhưng chưa đủ chắc chắn",
        [UrlReasons.Shortener] = "Có link rút gọn, che giấu trang đích",
        [UrlReasons.IpAddressHost] = "Link dùng địa chỉ IP thay vì tên miền",
        [UrlReasons.Punycode] = "Tên miền dùng ký tự giả mạo (punycode)",
        [UrlReasons.SuspiciousTopLevelDomain] = "Tên miền có đuôi thường bị dùng để lừa đảo",
        [UrlReasons.Obfuscated] = "Link bị viết lách (như [.] hay hxxp) để né bộ lọc",
        [UrlReasons.BrandImpersonation] = "Tên miền giả danh ngân hàng, ví điện tử hoặc cơ quan nhà nước",
        [ThreatReasons.BlocklistedDomain] = "Link nằm trong danh sách tên miền lừa đảo đã được cảnh báo",
        [ThreatReasons.KnownScamTemplate] = "Nội dung trùng với mẫu tin lừa đảo đã được cảnh báo",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static string Describe(string reasonCode) =>
        Descriptions.TryGetValue(reasonCode, out var description) ? description : reasonCode;
}
