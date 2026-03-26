namespace DeviceDataManager.Application;

public sealed class CountsByDayResponse
{
    public required string Day { get; init; }
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
    /// <summary>Aggregated per zone, direction, and hour (UTC); minute-level buckets are rolled up.</summary>
    public required IReadOnlyList<ZoneHourTotal> Rows { get; init; }
}

public sealed class ZoneHourTotal
{
    public required string ZoneName { get; init; }
    /// <summary>Traffic direction for this aggregate (e.g. <c>in</c>, <c>out</c>).</summary>
    public required string Direction { get; init; }
    public required DateTimeOffset HourUtc { get; init; }
    /// <summary>Sum of events_count for this zone, direction, and hour.</summary>
    public required long Count { get; init; }
}
