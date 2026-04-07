using DeviceDataManager.Application;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DeviceDataManager.Infrastructure;

public sealed class PostgresCountReadRepository : ICountReadRepository
{
    private const string SqlTotalsByZoneForDayInTimeZone =
        """
        SELECT zone_name,
               direction,
               SUM(events_count)::bigint AS events_sum
        FROM "Count"
        WHERE (timezone(@tz, bucket_start))::date = @day
        GROUP BY zone_name, direction
        ORDER BY zone_name, direction;
        """;

    private const string SqlTotalsByZoneAndHourInTimeZone =
        """
        SELECT zone_name,
               direction,
               (date_trunc('hour', timezone(@tz, bucket_start)) AT TIME ZONE @tz) AS hour_bucket,
               SUM(events_count)::bigint AS events_sum
        FROM "Count"
        WHERE (timezone(@tz, bucket_start))::date = @day
        GROUP BY zone_name, direction, date_trunc('hour', timezone(@tz, bucket_start))
        ORDER BY zone_name, direction, hour_bucket;
        """;

    private readonly ModuleConfigurationState _state;
    private readonly ILogger<PostgresCountReadRepository> _logger;

    public PostgresCountReadRepository(ModuleConfigurationState state, ILogger<PostgresCountReadRepository> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task<Result<CountsByDayResponse>> GetCountsByDayAsync(
        DateOnly day,
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var (dataSource, options) = _state.Snapshot();
        if (dataSource is null || string.IsNullOrWhiteSpace(options?.PostgresConnectionString))
        {
            return Result<CountsByDayResponse>.Fail(
                "PostgreSQL is not configured. Set postgresConnectionString in module twin desired properties.");
        }

        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(SqlTotalsByZoneForDayInTimeZone, conn);
            cmd.Parameters.AddWithValue("day", day);
            cmd.Parameters.AddWithValue("tz", timeZoneId!);

            var zones = new List<ZoneDayTotal>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                zones.Add(new ZoneDayTotal
                {
                    ZoneName = reader.GetString(reader.GetOrdinal("zone_name")),
                    Direction = ReadDirection(reader),
                    Count = reader.GetInt64(reader.GetOrdinal("events_sum")),
                });
            }

            return Result<CountsByDayResponse>.Ok(new CountsByDayResponse
            {
                Day = day.ToString("yyyy-MM-dd"),
                TimeZone = timeZoneId,
                Zones = zones,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCountsByDay failed for {Day}.", day);
            return Result<CountsByDayResponse>.Fail("Database query failed.");
        }
    }

    public async Task<Result<CountsByHourResponse>> GetCountsByHourAsync(
        DateOnly day,
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var (dataSource, options) = _state.Snapshot();
        if (dataSource is null || string.IsNullOrWhiteSpace(options?.PostgresConnectionString))
        {
            return Result<CountsByHourResponse>.Fail(
                "PostgreSQL is not configured. Set postgresConnectionString in module twin desired properties.");
        }

        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(SqlTotalsByZoneAndHourInTimeZone, conn);
            cmd.Parameters.AddWithValue("day", day);
            cmd.Parameters.AddWithValue("tz", timeZoneId!);

            var rows = new List<ZoneHourTotal>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var hourOrd = reader.GetOrdinal("hour_bucket");
            var displayTz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!);
            while (await reader.ReadAsync(cancellationToken))
            {
                var instant = ReadHourUtc(reader, hourOrd);
                var hourStart = TimeZoneInfo.ConvertTime(instant, displayTz);
                rows.Add(new ZoneHourTotal
                {
                    ZoneName = reader.GetString(reader.GetOrdinal("zone_name")),
                    Direction = ReadDirection(reader),
                    HourStart = hourStart,
                    Count = reader.GetInt64(reader.GetOrdinal("events_sum")),
                });
            }

            return Result<CountsByHourResponse>.Ok(new CountsByHourResponse
            {
                Day = day.ToString("yyyy-MM-dd"),
                TimeZone = timeZoneId,
                Rows = rows,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCountsByHour failed for {Day}.", day);
            return Result<CountsByHourResponse>.Fail("Database query failed.");
        }
    }

    private static string ReadDirection(NpgsqlDataReader reader)
    {
        var ord = reader.GetOrdinal("direction");
        return reader.IsDBNull(ord) ? string.Empty : reader.GetString(ord);
    }

    private static DateTimeOffset ReadHourUtc(NpgsqlDataReader reader, int ordinal)
    {
        var v = reader.GetValue(ordinal);
        return v switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => throw new InvalidCastException($"Unexpected hour_bucket type: {v?.GetType().Name ?? "null"}"),
        };
    }
}
