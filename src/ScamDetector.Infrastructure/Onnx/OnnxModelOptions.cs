using ScamDetector.Core.Classification;

namespace ScamDetector.Infrastructure.Onnx;

public sealed record OnnxModelOptions
{
    public const string SectionName = "OnnxModel";

    public const int DefaultMaxSequenceLength = 256;

    public string ModelPath { get; init; } = "models/scam-detector.onnx";

    public string VocabularyPath { get; init; } = "models/vocab.txt";

    public string BpeCodesPath { get; init; } = "models/bpe.codes";

    public int MaxSequenceLength { get; init; } = DefaultMaxSequenceLength;

    public IReadOnlyList<string> LabelOrder { get; init; } =
        [MessageLabelNames.Normal, MessageLabelNames.Spam, MessageLabelNames.Scam];

    public string InputIdsName { get; init; } = "input_ids";

    public string AttentionMaskName { get; init; } = "attention_mask";

    public string LogitsName { get; init; } = "logits";
}
