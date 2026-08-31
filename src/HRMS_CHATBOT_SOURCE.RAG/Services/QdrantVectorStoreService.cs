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
    private const int ScrollPageSize = 512;

    /// <summary>
    /// Collection setup is idempotent but costs several gRPC round-trips, so the
    /// result is cached process-wide rather than repeated on every upsert.
    /// </summary>
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);
    private static volatile bool _collectionEnsured;

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
        if (_collectionEnsured)
        {
            return;
        }

        await EnsureLock.WaitAsync(cancellationToken);

        try
        {
            if (_collectionEnsured)
            {
                return;
            }

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

            await EnsurePayloadIndexAsync(VectorPayloadFields.DocumentId, PayloadSchemaType.Integer, cancellationToken);
            await EnsurePayloadIndexAsync(VectorPayloadFields.Category, PayloadSchemaType.Keyword, cancellationToken);
            await EnsurePayloadIndexAsync(VectorPayloadFields.IsActive, PayloadSchemaType.Bool, cancellationToken);

            _collectionEnsured = true;
        }
        finally
        {
            EnsureLock.Release();
        }
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
                [VectorPayloadFields.DocumentId] = point.DocumentId,
                [VectorPayloadFields.ChunkIndex] = point.ChunkIndex,
                [VectorPayloadFields.Category] = point.Category,
                [VectorPayloadFields.Title] = point.Title,
                [VectorPayloadFields.Content] = point.Content,
                [VectorPayloadFields.BlobPath] = point.BlobPath,
                [VectorPayloadFields.SectionPath] = point.SectionPath,
                [VectorPayloadFields.StartOffset] = point.StartOffset,
                [VectorPayloadFields.EndOffset] = point.EndOffset,
                [VectorPayloadFields.IsActive] = point.IsActive
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

        await EnsureCollectionAsync(cancellationToken);

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

        await EnsureCollectionAsync(cancellationToken);

        await _client.DeleteAsync(
            _settings.CollectionName,
            BuildDocumentIdFilter(documentId),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<RetrievalCandidate>> SearchAsync(
        float[] queryVector,
        int limit,
        string? category,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        if (queryVector.Length == 0 || limit <= 0)
        {
            return [];
        }

        if (!await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken))
        {
            return [];
        }

        await EnsureCollectionAsync(cancellationToken);

        var points = await _client.SearchAsync(
            _settings.CollectionName,
            queryVector,
            filter: BuildSearchFilter(category, activeOnly),
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: cancellationToken);

        var results = new List<RetrievalCandidate>(points.Count);

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var candidate = ToCandidate(point.Payload);

            candidate.DenseRank = index + 1;
            candidate.DenseScore = point.Score;
            results.Add(candidate);
        }

        return results;
    }

    public async Task SetActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (!await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken))
        {
            return;
        }

        await EnsureCollectionAsync(cancellationToken);

        var payload = new Dictionary<string, Value>
        {
            [VectorPayloadFields.IsActive] = isActive
        };

        await _client.SetPayloadAsync(
            _settings.CollectionName,
            payload,
            BuildDocumentIdFilter(documentId),
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Set is_active={IsActive} on vector payloads for document {DocumentId}.",
            isActive,
            documentId);
    }

    public async Task<IReadOnlyList<LexicalDocument>> ScrollAllAsync(CancellationToken cancellationToken = default)
    {
        if (!await _client.CollectionExistsAsync(_settings.CollectionName, cancellationToken))
        {
            return [];
        }

        var documents = new List<LexicalDocument>();
        PointId? offset = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await _client.ScrollAsync(
                _settings.CollectionName,
                payloadSelector: true,
                vectorsSelector: false,
                limit: ScrollPageSize,
                offset: offset,
                cancellationToken: cancellationToken);

            foreach (var point in page.Result)
            {
                documents.Add(ToLexicalDocument(point.Payload));
            }

            if (page.NextPageOffset is null)
            {
                break;
            }

            offset = page.NextPageOffset;
        }

        return documents;
    }

    private async Task EnsurePayloadIndexAsync(
        string fieldName,
        PayloadSchemaType schemaType,
        CancellationToken cancellationToken)
    {
        try
        {
            await _client.CreatePayloadIndexAsync(
                _settings.CollectionName,
                fieldName: fieldName,
                schemaType: schemaType,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Ensured {SchemaType} payload index on {FieldName} for collection {CollectionName}.",
                schemaType,
                fieldName,
                _settings.CollectionName);
        }
        catch (RpcException ex) when (IsPayloadIndexAlreadyExists(ex))
        {
            _logger.LogDebug(
                "Payload index on {FieldName} already exists for collection {CollectionName}.",
                fieldName,
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
                        Key = VectorPayloadFields.DocumentId,
                        Match = new Match { Integer = documentId }
                    }
                }
            }
        };
    }

    private static Filter? BuildSearchFilter(string? category, bool activeOnly)
    {
        var filter = new Filter();

        if (!string.IsNullOrWhiteSpace(category))
        {
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = VectorPayloadFields.Category,
                    Match = new Match { Keyword = category.Trim() }
                }
            });
        }

        if (activeOnly)
        {
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = VectorPayloadFields.IsActive,
                    Match = new Match { Boolean = true }
                }
            });
        }

        return filter.Must.Count == 0 ? null : filter;
    }

    private static RetrievalCandidate ToCandidate(IReadOnlyDictionary<string, Value> payload)
    {
        var documentId = ReadInt64(payload, VectorPayloadFields.DocumentId);
        var chunkIndex = (int)ReadInt64(payload, VectorPayloadFields.ChunkIndex);

        return new RetrievalCandidate
        {
            DocumentKey = VectorPayloadFields.BuildDocumentKey(documentId, chunkIndex),
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Category = ReadString(payload, VectorPayloadFields.Category),
            Title = ReadString(payload, VectorPayloadFields.Title),
            Content = ReadString(payload, VectorPayloadFields.Content),
            SectionPath = ReadString(payload, VectorPayloadFields.SectionPath)
        };
    }

    private static LexicalDocument ToLexicalDocument(IReadOnlyDictionary<string, Value> payload)
    {
        return new LexicalDocument
        {
            DocumentId = ReadInt64(payload, VectorPayloadFields.DocumentId),
            ChunkIndex = (int)ReadInt64(payload, VectorPayloadFields.ChunkIndex),
            Category = ReadString(payload, VectorPayloadFields.Category),
            Title = ReadString(payload, VectorPayloadFields.Title),
            Content = ReadString(payload, VectorPayloadFields.Content),
            SectionPath = ReadString(payload, VectorPayloadFields.SectionPath),
            StartOffset = (int)ReadInt64(payload, VectorPayloadFields.StartOffset),
            EndOffset = (int)ReadInt64(payload, VectorPayloadFields.EndOffset),
            IsActive = ReadBool(payload, VectorPayloadFields.IsActive, defaultValue: true)
        };
    }

    private static string ReadString(IReadOnlyDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) && value.KindCase == Value.KindOneofCase.StringValue
            ? value.StringValue
            : string.Empty;
    }

    private static long ReadInt64(IReadOnlyDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) && value.KindCase == Value.KindOneofCase.IntegerValue
            ? value.IntegerValue
            : 0;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, Value> payload, string key, bool defaultValue)
    {
        return payload.TryGetValue(key, out var value) && value.KindCase == Value.KindOneofCase.BoolValue
            ? value.BoolValue
            : defaultValue;
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
