using ScamDetector.Core.Classification;
using ScamDetector.Core.Common;

namespace ScamDetector.Bot.Tests.Fakes;

public sealed class FakeMessageClassifier : IMessageClassifier
{
    private readonly Result<ClassificationResult>? _result;
    private readonly Exception? _exception;

    public FakeMessageClassifier(Result<ClassificationResult> result)
    {
        _result = result;
    }

    public FakeMessageClassifier(Exception exception)
    {
        _exception = exception;
    }

    public List<string> ReceivedTexts { get; } = [];

    public Task<Result<ClassificationResult>> ClassifyAsync(string text, CancellationToken cancellationToken)
    {
        ReceivedTexts.Add(text);
        return _exception is null ? Task.FromResult(_result!) : throw _exception;
    }
}
