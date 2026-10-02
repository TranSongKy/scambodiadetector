using ScamDetector.Core.Common;

namespace ScamDetector.Core.Classification;

public interface IMessageClassifier
{
    Task<Result<ClassificationResult>> ClassifyAsync(string text, CancellationToken cancellationToken);
}
