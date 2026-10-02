using ScamDetector.Core.Common;

namespace ScamDetector.Core.Tests.Common;

public sealed class DomainErrorTests
{
    private const string Code = "test.code";
    private const string Message = "Test message.";

    [Fact]
    public void Equals_SameCodeAndMessage_ReturnsTrue()
    {
        var first = new DomainError(Code, Message);
        var second = new DomainError(Code, Message);

        var areEqual = first == second;

        Assert.True(areEqual);
    }

    [Fact]
    public void Equals_DifferentCode_ReturnsFalse()
    {
        var first = new DomainError(Code, Message);
        var second = new DomainError("other.code", Message);

        var areEqual = first == second;

        Assert.False(areEqual);
    }
}
