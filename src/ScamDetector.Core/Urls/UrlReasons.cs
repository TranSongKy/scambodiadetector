namespace ScamDetector.Core.Urls;

public static class UrlReasons
{
    public const string Shortener = "url_shortener";
    public const string IpAddressHost = "url_ip_address_host";
    public const string Punycode = "url_punycode";
    public const string SuspiciousTopLevelDomain = "url_suspicious_tld";
    public const string Obfuscated = "url_obfuscated";
    public const string BrandImpersonation = "url_brand_impersonation";
}
