using System.Text.Json.Serialization;

namespace DeviceDataManager.Application;

/// <summary>
/// Maps to the module twin <c>properties.desired</c> JSON (see deployment/module-twin-desired.example.json).
/// Queries are fixed in code; only the connection string is configured here.
/// </summary>
public sealed class ModuleTwinDesiredOptions
{
    [JsonPropertyName("postgresConnectionString")]
    public string? PostgresConnectionString { get; set; }
}
