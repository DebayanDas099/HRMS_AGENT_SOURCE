using Google.Protobuf.Collections;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace HRMS_CHATBOT_SOURCE.RAG.Services;

public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly QdrantClient _client;
    private readonly VectorStoreSettings _settings;
    private readonly ILogger<QdrantVectorStoreService> _logger;

    public QdrantVectorStoreService(
        QdrantClient client,
        IOptions<VectorStoreSettings> settings,
        ILogger<QdrantVectorStoreService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(_settings.Provider, VectorStoreProviders.Qdrant, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Vector store provider '{_settings.Provider}' is not implemented.");
        }

        var exists = await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken);
        if (exists)
        {
            return;
        }

        await _client.CreateCollectionAsync(
            _settings.CollectionName,
            new VectorParams
            {
                Size = (ulong)_settings.VectorSize,
                Distance = Distance.Cosine
            },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created Qdrant collection {CollectionName}.", _settings.CollectionName);
    }

    public async Task UpsertAsync(
        IReadOnlyList<VectorDocumentPoint> points,
        CancellationToken cancellationToken = default)
    {
        if (points.Count == 0)
        {
            return;
        }

        await EnsureCollectionAsync(cancellationToken);

        var qdrantPoints = points.Select(point => new PointStruct
        {
            Id = CreatePointId(point.DocumentId, point.ChunkIndex),
            Vectors = point.Vector,
            Payload =
            {
                ["document_id"] = point.DocumentId,
                ["chunk_index"] = point.ChunkIndex,
                ["category"] = point.Category,
                ["title"] = point.Title,
                ["content"] = point.Content,
                ["blob_path"] = point.BlobPath,
                ["is_active"] = point.IsActive
            }
        }).ToList();

        await _client.UpsertAsync(_settings.CollectionName, qdrantPoints, cancellationToken: cancellationToken);
    }

    public async Task DeleteByDocumentIdAsync(long documentId, CancellationToken cancellationToken = default)
    {
        var exists = await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken);
        if (!exists)
        {
            return;
        }

        await _client.DeleteAsync(
            _settings.CollectionName,
            new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "document_id",
                            Match = new Match { Integer = documentId }
                        }
                    }
                }
            },
            cancellationToken: cancellationToken);
    }

    private static PointId CreatePointId(long documentId, int chunkIndex)
    {
        var buffer = new byte[16];
        BitConverter.GetBytes(documentId).CopyTo(buffer, 0);
        BitConverter.GetBytes(chunkIndex).CopyTo(buffer, 8);
        return new PointId { Uuid = new Guid(buffer).ToString() };
    }
}
