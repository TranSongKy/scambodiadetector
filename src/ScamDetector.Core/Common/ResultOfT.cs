namespace ScamDetector.Core.Common;

public sealed class Result<TValue> : Result
{
    private const string FailedResultValueMessage = "Cannot access the value of a failed result.";

    private readonly TValue? _value;

    internal Result(TValue value)
        : base(null)
    {
        _value = value;
    }

    internal Result(DomainError error)
        : base(error)
    {
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(FailedResultValueMessage);

    public static implicit operator Result<TValue>(TValue value) => new(value);
}
