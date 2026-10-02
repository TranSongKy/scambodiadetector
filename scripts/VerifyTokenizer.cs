#:project ../src/ScamDetector.Infrastructure/ScamDetector.Infrastructure.csproj

using System.Text.Json;
using ScamDetector.Infrastructure.Tokenization;

const int MaxSequenceLength = 256;
const string GoldenFileName = "tokenizer-golden.json";

var modelDirectory = args.Length == 1 ? args[0] : "models";
var tokenizer = new PhoBertTokenizer(
    PhoBertVocabulary.Load(Path.Combine(modelDirectory, "vocab.txt")),
    new BpeWordEncoder(BpeMergeRanks.Load(Path.Combine(modelDirectory, "bpe.codes"))));

using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(modelDirectory, GoldenFileName)));
var mismatches = 0;
var total = 0;
foreach (var goldenCase in golden.RootElement.EnumerateArray())
{
    total++;
    var text = goldenCase.GetProperty("text").GetString() ?? string.Empty;
    var expectedIds = goldenCase.GetProperty("ids").EnumerateArray().Select(id => id.GetInt64()).ToList();
    var actualIds = tokenizer.Encode(text, MaxSequenceLength);
    if (expectedIds.SequenceEqual(actualIds))
        continue;

    mismatches++;
    Console.WriteLine($"Lệch ở mẫu {total}: HF [{string.Join(' ', expectedIds)}] / .NET [{string.Join(' ', actualIds)}]");
}

Console.WriteLine($"{total - mismatches}/{total} mẫu khớp với tokenizer HuggingFace.");
return mismatches == 0 ? 0 : 1;
