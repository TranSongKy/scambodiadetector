using System.Text.Json;
using ScamDetector.Core.Text;

namespace ScamDetector.Core.Tests.Text;

public sealed class SharedMaskingCasesTests
{
    private static readonly string CasesPath = Path.Combine(AppContext.BaseDirectory, "Shared", "masking-cases.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static TheoryData<string, string> Cases()
    {
        var cases = JsonSerializer.Deserialize<List<MaskingCase>>(File.ReadAllText(CasesPath), JsonOptions) ?? [];
        var data = new TheoryData<string, string>();
        foreach (var maskingCase in cases)
            data.Add(maskingCase.Input, maskingCase.Expected);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Mask_SharedCase_MatchesExpectedOutputUsedByPythonAnonymizer(string input, string expected)
    {
        var masked = ModelInputMasker.Mask(input);

        Assert.Equal(expected, masked);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Mask_AlreadyMaskedSharedCase_IsUnchanged(string input, string expected)
    {
        Assert.NotNull(input);

        var maskedAgain = ModelInputMasker.Mask(expected);

        Assert.Equal(expected, maskedAgain);
    }
}
