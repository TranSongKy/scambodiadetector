using ScamDetector.Core.Classification;
using ScamDetector.Core.Tests.Builders;
using ScamDetector.Core.Tests.Fakes;

namespace ScamDetector.Core.Tests.Classification;

public sealed class FallbackScamModelTests
{
    private static readonly ModelPrediction FirstPrediction = PredictionFactory.Create(0.9, 0.05, 0.05);
    private static readonly ModelPrediction SecondPrediction = PredictionFactory.Create(0.1, 0.1, 0.8);

    [Fact]
    public void Constructor_EmptyList_ThrowsArgumentOutOfRangeException()
    {
        var models = new List<IScamModel>();

        Assert.Throws<ArgumentOutOfRangeException>(() => new FallbackScamModel(models));
    }

    [Fact]
    public async Task PredictAsync_FirstAvailable_UsesFirstModel()
    {
        var first = new FakeScamModel(FirstPrediction);
        var second = new FakeScamModel(SecondPrediction);
        var model = new FallbackScamModel([first, second]);

        var prediction = await model.PredictAsync("tin nhắn", CancellationToken.None);

        Assert.Same(FirstPrediction, prediction);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
    }

    [Fact]
    public async Task PredictAsync_FirstUnavailable_UsesSecondModel()
    {
        var first = new FakeScamModel(FirstPrediction, isAvailable: false);
        var second = new FakeScamModel(SecondPrediction);
        var model = new FallbackScamModel([first, second]);

        var prediction = await model.PredictAsync("tin nhan", CancellationToken.None);

        Assert.Same(SecondPrediction, prediction);
        Assert.Equal(0, first.CallCount);
        Assert.Equal(1, second.CallCount);
    }

    [Fact]
    public async Task PredictAsync_NoneAvailable_UsesFirstModel()
    {
        var first = new FakeScamModel(FirstPrediction, isAvailable: false);
        var second = new FakeScamModel(SecondPrediction, isAvailable: false);
        var model = new FallbackScamModel([first, second]);

        var prediction = await model.PredictAsync("tin nhắn", CancellationToken.None);

        Assert.Same(FirstPrediction, prediction);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
    }

    [Fact]
    public async Task PredictAsync_SingleModel_UsesThatModel()
    {
        var only = new FakeScamModel(FirstPrediction, isAvailable: false);
        var model = new FallbackScamModel([only]);

        var prediction = await model.PredictAsync("a", CancellationToken.None);

        Assert.Same(FirstPrediction, prediction);
        Assert.Equal(1, only.CallCount);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("")]
    [InlineData("Chuyển khoản gấp để nhận quà")]
    [InlineData("Chuyen khoan gap de nhan qua")]
    [InlineData("Chuyển khỏan")]
    public async Task PredictAsync_AnyText_PassesTextUnchangedToSelectedModel(string text)
    {
        var selected = new FakeScamModel(SecondPrediction);
        var model = new FallbackScamModel([new FakeScamModel(FirstPrediction, isAvailable: false), selected]);

        await model.PredictAsync(text, CancellationToken.None);

        Assert.Equal(text, selected.ReceivedText);
        Assert.Equal(text.Length, selected.ReceivedText!.Length);
    }

    [Fact]
    public async Task PredictAsync_MaxLengthText_PassesTextUnchangedToSelectedModel()
    {
        var text = new string('ế', 2000);
        var selected = new FakeScamModel(FirstPrediction);
        var model = new FallbackScamModel([selected]);

        await model.PredictAsync(text, CancellationToken.None);

        Assert.Equal(text, selected.ReceivedText);
    }

    [Fact]
    public async Task PredictAsync_CancellableToken_PassesTokenToSelectedModel()
    {
        using var source = new CancellationTokenSource();
        var selected = new FakeScamModel(SecondPrediction);
        var model = new FallbackScamModel([new FakeScamModel(FirstPrediction, isAvailable: false), selected]);

        await model.PredictAsync("tin nhắn", source.Token);

        Assert.Equal(source.Token, selected.ReceivedToken);
    }

    [Fact]
    public void IsAvailable_AtLeastOneAvailable_ReturnsTrue()
    {
        var model = new FallbackScamModel(
        [
            new FakeScamModel(FirstPrediction, isAvailable: false),
            new FakeScamModel(SecondPrediction),
        ]);

        Assert.True(model.IsAvailable);
    }

    [Fact]
    public void IsAvailable_NoneAvailable_ReturnsFalse()
    {
        var model = new FallbackScamModel(
        [
            new FakeScamModel(FirstPrediction, isAvailable: false),
            new FakeScamModel(SecondPrediction, isAvailable: false),
        ]);

        Assert.False(model.IsAvailable);
    }

    [Fact]
    public async Task IsReadyAsync_SelectedModelReady_ReturnsTrue()
    {
        var model = new FallbackScamModel(
        [
            new FakeReadinessScamModel(isAvailable: false, isReady: false),
            new FakeReadinessScamModel(isAvailable: true, isReady: true),
        ]);

        var isReady = await ((IScamModel)model).IsReadyAsync(CancellationToken.None);

        Assert.True(isReady);
    }

    [Fact]
    public async Task IsReadyAsync_SelectedModelNotReady_ReturnsFalse()
    {
        var model = new FallbackScamModel(
        [
            new FakeReadinessScamModel(isAvailable: true, isReady: false),
            new FakeReadinessScamModel(isAvailable: true, isReady: true),
        ]);

        var isReady = await ((IScamModel)model).IsReadyAsync(CancellationToken.None);

        Assert.False(isReady);
    }

    [Fact]
    public async Task IsReadyAsync_FirstUnavailable_DelegatesToSecondOnly()
    {
        var first = new FakeReadinessScamModel(isAvailable: false, isReady: true);
        var second = new FakeReadinessScamModel(isAvailable: true, isReady: true);
        var model = new FallbackScamModel([first, second]);

        await ((IScamModel)model).IsReadyAsync(CancellationToken.None);

        Assert.Equal(0, first.ReadinessCallCount);
        Assert.Equal(1, second.ReadinessCallCount);
    }

    [Fact]
    public async Task IsReadyAsync_NoneAvailable_DelegatesToFirstModel()
    {
        var first = new FakeReadinessScamModel(isAvailable: false, isReady: false);
        var second = new FakeReadinessScamModel(isAvailable: false, isReady: true);
        var model = new FallbackScamModel([first, second]);

        var isReady = await ((IScamModel)model).IsReadyAsync(CancellationToken.None);

        Assert.False(isReady);
        Assert.Equal(1, first.ReadinessCallCount);
        Assert.Equal(0, second.ReadinessCallCount);
    }

    [Fact]
    public async Task IsReadyAsync_CancellableToken_PassesTokenToSelectedModel()
    {
        using var source = new CancellationTokenSource();
        var selected = new FakeReadinessScamModel(isAvailable: true, isReady: true);
        var model = new FallbackScamModel([selected]);

        await ((IScamModel)model).IsReadyAsync(source.Token);

        Assert.Equal(source.Token, selected.ReceivedReadinessToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsReadyAsync_ModelWithoutOverride_ReturnsIsAvailable(bool isAvailable)
    {
        var model = new FakeScamModel(FirstPrediction, isAvailable);

        var isReady = await ((IScamModel)model).IsReadyAsync(CancellationToken.None);

        Assert.Equal(isAvailable, isReady);
    }

    [Fact]
    public async Task IsReadyAsync_FallbackOverModelsWithoutOverride_ReturnsAvailabilityOfSelectedModel()
    {
        var model = new FallbackScamModel(
        [
            new FakeScamModel(FirstPrediction, isAvailable: false),
            new FakeScamModel(SecondPrediction),
        ]);

        var isReady = await ((IScamModel)model).IsReadyAsync(CancellationToken.None);

        Assert.True(isReady);
    }

    [Fact]
    public void Dispose_DisposableModels_DisposesEachOnce()
    {
        var first = new FakeDisposableScamModel(FirstPrediction);
        var second = new FakeDisposableScamModel(SecondPrediction);
        var model = new FallbackScamModel([first, second]);

        model.Dispose();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public void Dispose_MixedModels_DisposesOnlyDisposableOnes()
    {
        var disposable = new FakeDisposableScamModel(FirstPrediction);
        var model = new FallbackScamModel([new FakeScamModel(SecondPrediction), disposable]);

        var exception = Record.Exception(model.Dispose);

        Assert.Null(exception);
        Assert.Equal(1, disposable.DisposeCount);
    }

    [Fact]
    public void Dispose_NonDisposableModels_DoesNotThrow()
    {
        var model = new FallbackScamModel([new FakeScamModel(FirstPrediction)]);

        var exception = Record.Exception(model.Dispose);

        Assert.Null(exception);
    }
}
