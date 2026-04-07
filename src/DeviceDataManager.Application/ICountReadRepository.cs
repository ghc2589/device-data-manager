namespace DeviceDataManager.Application;

public interface ICountReadRepository
{
    /// <param name="timeZoneId">Required IANA id (e.g. America/Lima).</param>
    Task<Result<CountsByDayResponse>> GetCountsByDayAsync(DateOnly day, string? timeZoneId, CancellationToken cancellationToken);

    /// <param name="timeZoneId">Required IANA id (e.g. America/Lima).</param>
    Task<Result<CountsByHourResponse>> GetCountsByHourAsync(DateOnly day, string? timeZoneId, CancellationToken cancellationToken);
}
