using ScamDetector.Infrastructure.Onnx;

namespace ScamDetector.Infrastructure.Tests.Onnx;

public sealed class UnavailableScamModelTests
{
    [Fact]
    public async Task PredictAsync_Always_ThrowsScamModelUnavailableException()
    {
        var model = new UnavailableScamModel(["model.onnx"]);

        await Assert.ThrowsAsync<ScamModelUnavailableException>(
            () => model.PredictAsync("xin chào", CancellationToken.None));
    }

    [Fact]
    public void IsAvailable_Always_ReturnsFalse()
    {
        var model = new UnavailableScamModel(["model.onnx"]);

        Assert.False(model.IsAvailable);
    }
}
