using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qdrant.Client;

namespace HRMS_CHATBOT_SOURCE.Domain.Extensions;

public static class QdrantClientServiceCollectionExtensions
{
    private const int DefaultGrpcPort = 6334;

    public static IServiceCollection AddHrmsQdrantClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(_ => CreateClient(configuration));
        return services;
    }

    private static QdrantClient CreateClient(IConfiguration configuration)
    {
        var endpoint = FirstNonEmpty(
            configuration["Qdrant:Host"],
            configuration["Qdrant:Endpoint"]);

        var apiKey = configuration["Qdrant:ApiKey"];
        var grpcPortOverride = configuration.GetValue<int?>("Qdrant:GrpcPort");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Qdrant:Host or Qdrant:Endpoint must be configured.");
        }

        var (host, port, useHttps) = ParseQdrantEndpoint(endpoint.Trim(), grpcPortOverride);

        return string.IsNullOrWhiteSpace(apiKey)
            ? new QdrantClient(host, port, useHttps)
            : new QdrantClient(host, port, useHttps, apiKey: apiKey);
    }

    internal static (string Host, int Port, bool Https) ParseQdrantEndpoint(string value, int? grpcPortOverride)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var useHttps = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
            var port = grpcPortOverride ?? ResolveGrpcPort(uri);
            return (uri.Host, port, useHttps);
        }

        var host = value.Trim().TrimStart('/');
        var useCloudTls = host.Contains("cloud.qdrant.io", StringComparison.OrdinalIgnoreCase);
        return (host, grpcPortOverride ?? DefaultGrpcPort, useCloudTls);
    }

    private static int ResolveGrpcPort(Uri uri)
    {
        // Qdrant Cloud dashboard URLs often omit the gRPC port (443) or use REST port 6333.
        if (uri.IsDefaultPort || uri.Port is 443 or 6333)
        {
            return DefaultGrpcPort;
        }

        return uri.Port;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
