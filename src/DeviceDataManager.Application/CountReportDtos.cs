using System.Text.Json.Serialization;

namespace DeviceDataManager.Application;

public sealed class CountsByDayResponse
{
    public required string Day { get; init; }

    /// <summary>IANA id used for the query.</summary>
    public string? TimeZone { get; init; }

    public required IReadOnlyList<ZoneDayTotal> Zones { get; init; }
}

public sealed class ZoneDayTotal
{
    public required string ZoneName { get; init; }
    /// <summary>Traffic direction for this aggregate (e.g. <c>in</c>, <c>out</c>).</summary>
    public required string Direction { get; init; }
    /// <summary>Sum of events_count for this zone and direction on the requested day.</summary>
    public required long Count { get; init; }
}

public sealed class CountsByHourResponse
{
    public required string Day { get; init; }

    /// <summary>IANA id used for the query.</summary>
    public string? TimeZone { get; init; }

    /// <summary>Aggregated per zone, direction, and hour.</summary>
    public required IReadOnlyList<ZoneHourTotal> Rows { get; init; }
}

public sealed class ZoneHourTotal
{
    public required string ZoneName { get; init; }
    /// <summary>Traffic direction for this aggregate (e.g. <c>in</c>, <c>out</c>).</summary>
    public required string Direction { get; init; }

    /// <summary>Start of the hour bucket in the requested time zone.</summary>
    [JsonPropertyName("hourStart")]
    public required DateTimeOffset HourStart { get; init; }

    /// <summary>Sum of events_count for this zone, direction, and hour.</summary>
    public required long Count { get; init; }
}
