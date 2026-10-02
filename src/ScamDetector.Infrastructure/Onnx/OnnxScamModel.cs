using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Tokenization;

namespace ScamDetector.Infrastructure.Onnx;

public sealed class OnnxScamModel(
    InferenceSession session,
    PhoBertTokenizer tokenizer,
    OnnxModelOptions options) : IScamModel, IDisposable
{
    private readonly IReadOnlyList<MessageLabel> _labels = LabelMapping.Parse(options.LabelOrder);

    public Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inputIds = tokenizer.Encode(maskedText, options.MaxSequenceLength).ToArray();
        int[] shape = [1, inputIds.Length];
        var attentionMask = Enumerable.Repeat(1L, inputIds.Length).ToArray();

        NamedOnnxValue[] inputs =
        [
            NamedOnnxValue.CreateFromTensor(options.InputIdsName, new DenseTensor<long>(inputIds, shape)),
            NamedOnnxValue.CreateFromTensor(options.AttentionMaskName, new DenseTensor<long>(attentionMask, shape)),
        ];
        using var outputs = session.Run(inputs, [options.LogitsName]);
        var logits = outputs[0].AsEnumerable<float>().ToArray();

        return Task.FromResult(LabelMapping.ToPrediction(_labels, logits));
    }

    public void Dispose() => session.Dispose();
}
