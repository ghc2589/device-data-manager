using System.Net;
using System.Text;
using System.Text.Json;
using DeviceDataManager.Application;
using DeviceDataManager.Domain;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DeviceDataManager.Infrastructure;

public sealed class EdgeModuleHostedService : IHostedService
{
    private const string GetCountsMethodName = "GetCounts";

    private readonly ModuleConfigurationState _configurationState;
    private readonly ICountDataSource _countDataSource;
    private readonly ILogger<EdgeModuleHostedService> _logger;
    private ModuleClient? _moduleClient;

    public EdgeModuleHostedService(
        ModuleConfigurationState configurationState,
        ICountDataSource countDataSource,
        ILogger<EdgeModuleHostedService> logger)
    {
        _configurationState = configurationState;
        _countDataSource = countDataSource;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Edge module client from environment (IoT Hub).");
        _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(TransportType.Amqp_Tcp_Only);

        await ApplyTwinConfigurationAsync(cancellationToken);

        await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdatedAsync, null, cancellationToken);
        await _moduleClient.SetMethodHandlerAsync(GetCountsMethodName, OnGetCountsAsync, null, cancellationToken);

        _logger.LogInformation("Edge module registered direct method {Method} and twin callback.", GetCountsMethodName);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_moduleClient is null)
        {
            return;
        }

        await _moduleClient.SetMethodHandlerAsync(GetCountsMethodName, null, null, cancellationToken);
        await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(null, null, cancellationToken);
        await _moduleClient.CloseAsync();
        _moduleClient.Dispose();
        _moduleClient = null;
    }

    private async Task OnDesiredPropertyUpdatedAsync(TwinCollection desiredProperties, object userContext)
    {
        _logger.LogInformation("Module twin desired properties updated; reloading configuration.");
        await ApplyTwinConfigurationAsync(CancellationToken.None);
    }

    private async Task ApplyTwinConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_moduleClient is null)
        {
            return;
        }

        var twin = await _moduleClient.GetTwinAsync(cancellationToken);
        var json = JsonConvert.SerializeObject(twin.Properties.Desired);
        var deserialized = JsonConvert.DeserializeObject<ModuleTwinDesiredOptions>(json);
        var merged = MergeWithEnvironment(deserialized);
        _configurationState.Apply(merged);
    }

    private static ModuleTwinDesiredOptions MergeWithEnvironment(ModuleTwinDesiredOptions? twin)
    {
        var o = twin ?? new ModuleTwinDesiredOptions();
        if (string.IsNullOrWhiteSpace(o.PostgresConnectionString))
        {
            o.PostgresConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        }

        if (string.IsNullOrWhiteSpace(o.CountQuery))
        {
            o.CountQuery = Environment.GetEnvironmentVariable("COUNT_QUERY");
        }

        if (o.MaxRows <= 0 && int.TryParse(Environment.GetEnvironmentVariable("MAX_ROWS"), out var envMax))
        {
            o.MaxRows = envMax;
        }

        if (o.MaxRows <= 0)
        {
            o.MaxRows = 100;
        }

        return o;
    }

    private async Task<MethodResponse> OnGetCountsAsync(MethodRequest request, object userContext)
    {
        try
        {
            var outcome = await _countDataSource.GetCountsAsync(CancellationToken.None);
            if (!outcome.Success)
            {
                return ErrorResponse(HttpStatusCode.ServiceUnavailable, outcome.ErrorMessage ?? "Unknown error");
            }

            var items = outcome.Items ?? (IReadOnlyList<CountItem>)Array.Empty<CountItem>();
            var body = GetCountsResponse.FromItems(items);
            var json = System.Text.Json.JsonSerializer.Serialize(body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            return new MethodResponse(Encoding.UTF8.GetBytes(json), (int)HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCounts direct method failed.");
            return ErrorResponse(HttpStatusCode.InternalServerError, "Internal error.");
        }
    }

    private static MethodResponse ErrorResponse(HttpStatusCode status, string message)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { error = message });
        return new MethodResponse(Encoding.UTF8.GetBytes(json), (int)status);
    }
}
