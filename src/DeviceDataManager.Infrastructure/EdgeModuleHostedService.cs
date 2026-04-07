using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using DeviceDataManager.Application;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DeviceDataManager.Infrastructure;

public sealed class EdgeModuleHostedService : IHostedService
{
    private const string GetCountsByDayMethod = "GetCountsByDay";
    private const string GetCountsByHourMethod = "GetCountsByHour";

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ModuleConfigurationState _configurationState;
    private readonly ICountReadRepository _countReadRepository;
    private readonly ILogger<EdgeModuleHostedService> _logger;
    private ModuleClient? _moduleClient;

    public EdgeModuleHostedService(
        ModuleConfigurationState configurationState,
        ICountReadRepository countReadRepository,
        ILogger<EdgeModuleHostedService> logger)
    {
        _configurationState = configurationState;
        _countReadRepository = countReadRepository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating Edge module client from environment (IoT Hub).");
        _moduleClient = await ModuleClient.CreateFromEnvironmentAsync(TransportType.Amqp_Tcp_Only);

        await ApplyTwinConfigurationAsync(cancellationToken);

        await _moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdatedAsync, null, cancellationToken);
        await _moduleClient.SetMethodHandlerAsync(GetCountsByDayMethod, OnGetCountsByDayAsync, null, cancellationToken);
        await _moduleClient.SetMethodHandlerAsync(GetCountsByHourMethod, OnGetCountsByHourAsync, null, cancellationToken);

        _logger.LogInformation(
            "Registered direct methods {Day} and {Hour}.",
            GetCountsByDayMethod,
            GetCountsByHourMethod);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_moduleClient is null)
        {
            return;
        }

        await _moduleClient.SetMethodHandlerAsync(GetCountsByDayMethod, null, null, cancellationToken);
        await _moduleClient.SetMethodHandlerAsync(GetCountsByHourMethod, null, null, cancellationToken);
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

        return o;
    }

    private async Task<MethodResponse> OnGetCountsByDayAsync(MethodRequest request, object userContext)
    {
        try
        {
            if (!TryParseDayQuery(request, out var day, out var timeZoneId, out var parseError))
            {
                return ErrorResponse(HttpStatusCode.BadRequest, parseError ?? "Invalid request body.");
            }

            var result = await _countReadRepository.GetCountsByDayAsync(day, timeZoneId, CancellationToken.None);
            if (!result.Success)
            {
                return ErrorResponse(HttpStatusCode.ServiceUnavailable, result.ErrorMessage ?? "Unknown error.");
            }

            var json = System.Text.Json.JsonSerializer.Serialize(result.Value, JsonWriteOptions);
            return new MethodResponse(Encoding.UTF8.GetBytes(json), (int)HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Method} failed.", GetCountsByDayMethod);
            return ErrorResponse(HttpStatusCode.InternalServerError, "Internal error.");
        }
    }

    private async Task<MethodResponse> OnGetCountsByHourAsync(MethodRequest request, object userContext)
    {
        try
        {
            if (!TryParseDayQuery(request, out var day, out var timeZoneId, out var parseError))
            {
                return ErrorResponse(HttpStatusCode.BadRequest, parseError ?? "Invalid request body.");
            }

            var result = await _countReadRepository.GetCountsByHourAsync(day, timeZoneId, CancellationToken.None);
            if (!result.Success)
            {
                return ErrorResponse(HttpStatusCode.ServiceUnavailable, result.ErrorMessage ?? "Unknown error.");
            }

            var json = System.Text.Json.JsonSerializer.Serialize(result.Value, JsonWriteOptions);
            return new MethodResponse(Encoding.UTF8.GetBytes(json), (int)HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Method} failed.", GetCountsByHourMethod);
            return ErrorResponse(HttpStatusCode.InternalServerError, "Internal error.");
        }
    }

    private static bool TryParseDayQuery(MethodRequest request, out DateOnly day, out string? timeZoneId, out string? error)
    {
        day = default;
        timeZoneId = null;
        error = null;

        if (request.Data == null || request.Data.Length == 0)
        {
            error = "Body must be JSON with day (yyyy-MM-dd) and required timeZone (IANA id, e.g. America/Lima).";
            return false;
        }

        DirectMethodDayPayload? payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<DirectMethodDayPayload>(request.Data, JsonReadOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            error = "Body must be valid JSON.";
            return false;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.Day))
        {
            error = "Property 'day' is required (yyyy-MM-dd).";
            return false;
        }

        if (!DateOnly.TryParse(payload.Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            error = "Property 'day' must be a calendar date (yyyy-MM-dd).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.TimeZone))
        {
            error = "Property 'timeZone' is required and must be a valid IANA id (e.g. America/Lima).";
            return false;
        }

        var tz = payload.TimeZone.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(tz);
        }
        catch (TimeZoneNotFoundException)
        {
            error = "Property 'timeZone' must be a valid IANA id (e.g. America/Lima).";
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            error = "Property 'timeZone' must be a valid IANA id (e.g. America/Lima).";
            return false;
        }

        timeZoneId = tz;

        return true;
    }

    private static MethodResponse ErrorResponse(HttpStatusCode status, string message)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { error = message }, JsonWriteOptions);
        return new MethodResponse(Encoding.UTF8.GetBytes(json), (int)status);
    }
}
