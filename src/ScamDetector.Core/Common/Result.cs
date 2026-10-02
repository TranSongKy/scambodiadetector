namespace ScamDetector.Core.Common;

public class Result
{
    protected Result(DomainError? error)
    {
        Error = error;
    }

    public DomainError? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(DomainError error) => new(error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value);

    public static Result<TValue> Failure<TValue>(DomainError error) => new(error);
}
