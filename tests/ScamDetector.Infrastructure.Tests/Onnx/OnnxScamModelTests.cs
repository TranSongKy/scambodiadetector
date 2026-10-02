using ScamDetector.Core.Classification;
using ScamDetector.Infrastructure.Onnx;
using ScamDetector.Infrastructure.Tests.Support;

namespace ScamDetector.Infrastructure.Tests.Onnx;

public sealed class OnnxScamModelTests : IDisposable
{
    private const double Tolerance = 1e-6;

    private readonly OnnxScamModel _model = (OnnxScamModel)ScamModelFactory.Create(
        new OnnxModelOptions
        {
            ModelPath = FixturePaths.ModelFileName,
            VocabularyPath = FixturePaths.VocabularyFileName,
            BpeCodesPath = FixturePaths.BpeCodesFileName,
        },
        FixturePaths.Root);

    [Fact]
    public async Task PredictAsync_FixtureModel_ReturnsSoftmaxOfFixtureLogits()
    {
        var prediction = await _model.PredictAsync("xin chào", CancellationToken.None);

        var expected = Softmax([1, 0, 5]);
        Assert.Equal(expected[0], prediction.ProbabilityOf(MessageLabel.Normal), Tolerance);
        Assert.Equal(expected[1], prediction.ProbabilityOf(MessageLabel.Spam), Tolerance);
        Assert.Equal(expected[2], prediction.ProbabilityOf(MessageLabel.Scam), Tolerance);
    }

    [Fact]
    public async Task PredictAsync_OnlyUnknownWords_ScamLogitEqualsEndMarkerId()
    {
        var prediction = await _model.PredictAsync("zq", CancellationToken.None);

        var expected = Softmax([1, 0, 3]);
        Assert.Equal(expected[2], prediction.ProbabilityOf(MessageLabel.Scam), Tolerance);
    }

    [Fact]
    public async Task PredictAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _model.PredictAsync("xin chào", cancellation.Token));
    }

    public void Dispose() => _model.Dispose();

    private static double[] Softmax(double[] logits)
    {
        var exponents = logits.Select(Math.Exp).ToArray();
        var sum = exponents.Sum();
        return exponents.Select(exponent => exponent / sum).ToArray();
    }
}
