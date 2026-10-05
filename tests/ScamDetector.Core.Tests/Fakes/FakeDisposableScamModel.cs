using ScamDetector.Core.Classification;

namespace ScamDetector.Core.Tests.Fakes;

public sealed class FakeDisposableScamModel(ModelPrediction prediction) : IScamModel, IDisposable
{
    public int DisposeCount { get; private set; }

    public void Dispose() => DisposeCount++;

    public Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken) =>
        Task.FromResult(prediction);
}
