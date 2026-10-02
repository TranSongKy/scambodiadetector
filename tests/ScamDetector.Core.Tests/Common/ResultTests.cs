using ScamDetector.Core.Common;

namespace ScamDetector.Core.Tests.Common;

public sealed class ResultTests
{
    private static readonly DomainError SampleError = new("test.code", "Test message.");

    [Fact]
    public void Success_NoValue_IsSuccessWithoutError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_WithError_IsFailureWithSameError()
    {
        var result = Result.Failure(SampleError);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void SuccessOfT_WithValue_IsSuccessWithValue()
    {
        const int value = 42;

        var result = Result.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void FailureOfT_WithError_IsFailureWithSameError()
    {
        var result = Result.Failure<int>(SampleError);

        Assert.True(result.IsFailure);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        var result = Result.Failure<string>(SampleError);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_ReturnsSuccessWithValue()
    {
        const string value = "xin chào";

        Result<string> result = value;

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void SuccessOfT_NullReferenceValue_IsSuccessWithNullValue()
    {
        var result = Result.Success<string?>(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
