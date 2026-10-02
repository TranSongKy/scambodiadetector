using System.Text.RegularExpressions;
using ScamDetector.Core.Urls;

namespace ScamDetector.Core.Text;

public static partial class ModelInputMasker
{
    public static string Mask(string normalizedText)
    {
        var maskedText = EmailPattern().Replace(normalizedText, Placeholders.Email);
        maskedText = UrlExtractor.Replace(maskedText, Placeholders.Url);
        maskedText = OtpPattern().Replace(maskedText, Placeholders.Otp);
        maskedText = IdPattern().Replace(maskedText, Placeholders.Id);
        maskedText = PhonePattern().Replace(maskedText, Placeholders.Phone);
        return LongNumberPattern().Replace(maskedText, Placeholders.Account);
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(
        @"(?<=(?:otp|m[ãa] (?:x[áa]c (?:nh[ậa]n|th[ựu]c|minh)|b[ảa]o m[ậa]t|giao d[ịi]ch))\D{0,20})(?<![\d.,/])\d(?:[ .\-]?\d){3,7}(?![\d.,/]?\d)",
        RegexOptions.IgnoreCase)]
    private static partial Regex OtpPattern();

    [GeneratedRegex(
        @"(?<=(?:cccd|cmnd|c[ăa]n c[ưu][ớo]c|ch[ứu]ng minh|đ[ịi]nh danh|dinh danh)\D{0,25})(?<!\d)(?:\d{12}|\d{9})(?!\d)",
        RegexOptions.IgnoreCase)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"(?<![\d.,/])(?:\+?84|0)[ .\-]?(?:\d{9,10}|\d{2,4}(?:[ .\-]\d{3,4}){2,3})(?![\d.,/]?\d)")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(?<!\d)\d{4}(?:[ .\-]\d{4}){3}(?!\d)|(?<!\d)\d{9,19}(?!\d)")]
    private static partial Regex LongNumberPattern();
}
