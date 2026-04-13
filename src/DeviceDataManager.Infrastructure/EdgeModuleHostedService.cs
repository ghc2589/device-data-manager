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
        => await HandleDirectMethodAsync(GetCountsByDayMethod, request, _countReadRepository.GetCountsByDayAsync);

    private async Task<MethodResponse> OnGetCountsByHourAsync(MethodRequest request, object userContext)
        => await HandleDirectMethodAsync(GetCountsByHourMethod, request, _countReadRepository.GetCountsByHourAsync);

    private async Task<MethodResponse> HandleDirectMethodAsync<TResponse>(
        string methodName,
        MethodRequest request,
        Func<DateOnly, string?, CancellationToken, Task<Result<TResponse>>> handler)
    {
        _logger.LogInformation(
            "Direct method {Method} received. Payload: {Payload}",
            methodName,
            ReadRequestBody(request));

        try
        {
            if (!TryParseDayQuery(request, out var day, out var timeZoneId, out var parseError))
            {
                return CreateLoggedResponse(methodName, HttpStatusCode.BadRequest, new { error = parseError ?? "Invalid request body." });
            }

            var result = await handler(day, timeZoneId, CancellationToken.None);
            if (!result.Success)
            {
                return CreateLoggedResponse(methodName, HttpStatusCode.ServiceUnavailable, new { error = result.ErrorMessage ?? "Unknown error." });
            }

            return CreateLoggedResponse(methodName, HttpStatusCode.OK, result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Method} failed.", methodName);
            return CreateLoggedResponse(methodName, HttpStatusCode.InternalServerError, new { error = "Internal error." });
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

    private MethodResponse CreateLoggedResponse<TPayload>(string methodName, HttpStatusCode status, TPayload payload)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload, JsonWriteOptions);

        _logger.LogInformation(
            "Direct method {Method} responded with status {StatusCode}. Body: {Body}",
            methodName,
            (int)status,
            json);

        return new MethodResponse(Encoding.UTF8.GetBytes(json), (int)status);
    }

    private static string ReadRequestBody(MethodRequest request)
    {
        if (request.Data is null || request.Data.Length == 0)
        {
            return "<empty>";
        }

        return Encoding.UTF8.GetString(request.Data);
    }
}
