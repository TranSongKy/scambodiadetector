namespace ScamDetector.Core.Classification;

public sealed class FallbackScamModel : IScamModel, IDisposable
{
    private readonly IReadOnlyList<IScamModel> _models;

    public FallbackScamModel(IReadOnlyList<IScamModel> models)
    {
        ArgumentOutOfRangeException.ThrowIfZero(models.Count);
        _models = models;
    }

    public bool IsAvailable => _models.Any(model => model.IsAvailable);

    public Task<bool> IsReadyAsync(CancellationToken cancellationToken) =>
        SelectModel().IsReadyAsync(cancellationToken);

    public Task<ModelPrediction> PredictAsync(string maskedText, CancellationToken cancellationToken) =>
        SelectModel().PredictAsync(maskedText, cancellationToken);

    public void Dispose()
    {
        foreach (var model in _models.OfType<IDisposable>())
            model.Dispose();
    }

    private IScamModel SelectModel() => _models.FirstOrDefault(model => model.IsAvailable) ?? _models[0];
}
