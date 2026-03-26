using System.Text.Json.Serialization;

namespace DeviceDataManager.Application;

/// <summary>
/// Maps to the module twin <c>properties.desired</c> JSON (see deployment/module-twin-desired.example.json).
/// </summary>
public sealed class ModuleTwinDesiredOptions
{
    [JsonPropertyName("postgresConnectionString")]
    public string? PostgresConnectionString { get; set; }

    /// <summary>
    /// Read-only SQL returning columns <c>label</c> (text) and <c>value</c> (bigint).
    /// Use the <c>@maxRows</c> parameter when you need a cap (recommended).
    /// </summary>
    [JsonPropertyName("countQuery")]
    public string? CountQuery { get; set; }

    [JsonPropertyName("maxRows")]
    public int MaxRows { get; set; }
}
