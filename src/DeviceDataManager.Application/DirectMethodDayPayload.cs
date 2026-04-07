using System.Text.Json.Serialization;

namespace DeviceDataManager.Application;

/// <summary>Body for direct methods that scope data to a calendar day.</summary>
public sealed class DirectMethodDayPayload
{
    /// <summary>Calendar day in <c>yyyy-MM-dd</c>.</summary>
    [JsonPropertyName("day")]
    public string? Day { get; set; }

    /// <summary>
    /// Required IANA time zone (e.g. <c>America/Lima</c>). <c>day</c> is interpreted in that zone
    /// and filters use <c>timezone(tz, bucket_start)</c>.
    /// </summary>
    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; set; }
}
