using Grpc.Core;
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
    private const string DocumentIdPayloadField = "document_id";

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
        if (!exists)
        {
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

        await EnsureDocumentIdIndexAsync(cancellationToken);
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
                [DocumentIdPayloadField] = point.DocumentId,
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

    public async Task<bool> ExistsByDocumentIdAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (!await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken))
        {
            return false;
        }

        await EnsureDocumentIdIndexAsync(cancellationToken);

        var count = await _client.CountAsync(
            _settings.CollectionName,
            filter: BuildDocumentIdFilter(documentId),
            exact: true,
            cancellationToken: cancellationToken);

        return count > 0;
    }

    public async Task DeleteByDocumentIdAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (!await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken))
        {
            return;
        }

        await EnsureDocumentIdIndexAsync(cancellationToken);

        await _client.DeleteAsync(
            _settings.CollectionName,
            BuildDocumentIdFilter(documentId),
            cancellationToken: cancellationToken);
    }

    private async Task EnsureDocumentIdIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.CreatePayloadIndexAsync(
                _settings.CollectionName,
                fieldName: DocumentIdPayloadField,
                schemaType: PayloadSchemaType.Integer,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Ensured integer payload index on {FieldName} for collection {CollectionName}.",
                DocumentIdPayloadField,
                _settings.CollectionName);
        }
        catch (RpcException ex) when (IsPayloadIndexAlreadyExists(ex))
        {
            _logger.LogDebug(
                "Payload index on {FieldName} already exists for collection {CollectionName}.",
                DocumentIdPayloadField,
                _settings.CollectionName);
        }
    }

    private static Filter BuildDocumentIdFilter(long documentId)
    {
        return new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = DocumentIdPayloadField,
                        Match = new Match { Integer = documentId }
                    }
                }
            }
        };
    }

    private static bool IsPayloadIndexAlreadyExists(RpcException exception)
    {
        return exception.StatusCode == StatusCode.AlreadyExists
            || exception.Status.Detail.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    }

    private static PointId CreatePointId(long documentId, int chunkIndex)
    {
        var buffer = new byte[16];
        BitConverter.GetBytes(documentId).CopyTo(buffer, 0);
        BitConverter.GetBytes(chunkIndex).CopyTo(buffer, 8);
        return new PointId { Uuid = new Guid(buffer).ToString() };
    }
}
