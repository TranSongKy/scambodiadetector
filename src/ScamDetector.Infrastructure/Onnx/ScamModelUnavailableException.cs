namespace ScamDetector.Infrastructure.Onnx;

public sealed class ScamModelUnavailableException : Exception
{
    public ScamModelUnavailableException()
    {
    }

    public ScamModelUnavailableException(string message)
        : base(message)
    {
    }

    public ScamModelUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
