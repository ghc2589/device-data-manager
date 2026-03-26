using DeviceDataManager.Application;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DeviceDataManager.Infrastructure;

public sealed class ModuleConfigurationState : IDisposable
{
    private readonly object _gate = new();
    private NpgsqlDataSource? _dataSource;
    private ModuleTwinDesiredOptions? _options;
    private readonly ILogger<ModuleConfigurationState> _logger;

    public ModuleConfigurationState(ILogger<ModuleConfigurationState> logger)
    {
        _logger = logger;
    }

    public (NpgsqlDataSource? DataSource, ModuleTwinDesiredOptions? Options) Snapshot()
    {
        lock (_gate)
        {
            return (_dataSource, _options);
        }
    }

    public void Apply(ModuleTwinDesiredOptions merged)
    {
        lock (_gate)
        {
            _dataSource?.Dispose();
            _dataSource = null;
            _options = merged;

            if (string.IsNullOrWhiteSpace(merged.PostgresConnectionString))
            {
                _logger.LogWarning("postgresConnectionString is empty; PostgreSQL data source was not created.");
                return;
            }

            try
            {
                _dataSource = new NpgsqlDataSourceBuilder(merged.PostgresConnectionString).Build();
                _logger.LogInformation("PostgreSQL data source created from module configuration.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Npgsql data source.");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _dataSource?.Dispose();
            _dataSource = null;
        }
    }
}
