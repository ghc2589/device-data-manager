using System.Text.Json.Serialization;

namespace DeviceDataManager.Application;

/// <summary>Body for direct methods that scope data to a calendar day (UTC date).</summary>
public sealed class DirectMethodDayPayload
{
    /// <summary>Calendar day in <c>yyyy-MM-dd</c> (matches <c>day_date</c> in PostgreSQL).</summary>
    [JsonPropertyName("day")]
    public string? Day { get; set; }
}
