using DeviceDataManager.Application;
using DeviceDataManager.Domain;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DeviceDataManager.Infrastructure;

public sealed class PostgresCountDataSource : ICountDataSource
{
    private readonly ModuleConfigurationState _state;
    private readonly ILogger<PostgresCountDataSource> _logger;

    public PostgresCountDataSource(ModuleConfigurationState state, ILogger<PostgresCountDataSource> logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task<CountQueryOutcome> GetCountsAsync(CancellationToken cancellationToken)
    {
        var (dataSource, options) = _state.Snapshot();
        if (dataSource is null || string.IsNullOrWhiteSpace(options?.PostgresConnectionString))
        {
            return CountQueryOutcome.Fail(
                "PostgreSQL is not configured. Set module twin desired properties (or dev environment variables).");
        }

        if (string.IsNullOrWhiteSpace(options!.CountQuery))
        {
            return CountQueryOutcome.Fail("countQuery is not set in module twin desired properties.");
        }

        var maxRows = options.MaxRows;
        if (maxRows <= 0)
        {
            maxRows = 100;
        }

        if (maxRows > 10_000)
        {
            maxRows = 10_000;
        }

        try
        {
            await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(options.CountQuery, conn);
            if (options.CountQuery.Contains("@maxRows", StringComparison.OrdinalIgnoreCase))
            {
                cmd.Parameters.AddWithValue("maxRows", maxRows);
            }

            var items = new List<CountItem>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var label = reader.GetString(reader.GetOrdinal("label"));
                var value = reader.GetInt64(reader.GetOrdinal("value"));
                items.Add(new CountItem(label, value));
            }

            return CountQueryOutcome.Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Count query failed.");
            return CountQueryOutcome.Fail("Database query failed.");
        }
    }
}
