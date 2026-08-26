namespace HRMS_CHATBOT_SOURCE.RAG.Abstractions;

public interface IEmbeddingService
{
    Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}
