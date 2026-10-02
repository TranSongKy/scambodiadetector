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
        @"(?<=(?:otp|m[ãa] (?:x[áa]c (?:nh[ậa]n|th[ựu]c|minh)|b[ảa]o m[ậa]t|giao d[ịi]ch|k[íi]ch ho[ạa]t)|nh[ậa]p m[ãa])[^\d<>]{0,20})(?<![\d.,/])\d(?:[ .\-]?\d){3,7}(?![\d.,/]?\d)",
        RegexOptions.IgnoreCase)]
    private static partial Regex OtpPattern();

    [GeneratedRegex(
        @"(?<=(?:cccd|cmnd|c[ăa]n c[ưu][ớo]c|ch[ứu]ng minh|đ[ịi]nh danh|dinh danh)[^\d<>]{0,25})(?<![\d.,/])(?:\d{12}|\d{9}|\d{3}(?:[ .\-]\d{3}){2,3}|\d{4}(?:[ .\-]\d{4}){2})(?![\d.,/]?\d)",
        RegexOptions.IgnoreCase)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"(?<![\d.,/])(?:(?:\(\+?84\)|\+?84|0)[ .\-]?(?:\d{9,10}|\d{2,4}(?:[ .\-]\d{3,4}){2,3}|\d(?:[ .\-]\d{2}){4})|\(0\d{1,3}\)[ .\-]?\d{3,4}[ .\-]?\d{3,4})(?![\d.,/]?\d)")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(
        @"(?<!\d)\d{4}(?:[ .\-]\d{4}){2,3}(?:[ .\-]\d{1,3})?(?!\d)|(?<!\d)\d{9,19}(?!\d)(?!\s?(?:đồng|dong|vnđ|vnd|đ|d)(?![^\W\d_]))",
        RegexOptions.IgnoreCase)]
    private static partial Regex LongNumberPattern();
}
