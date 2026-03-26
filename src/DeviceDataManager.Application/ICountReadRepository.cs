namespace DeviceDataManager.Application;

public interface ICountReadRepository
{
    Task<Result<CountsByDayResponse>> GetCountsByDayAsync(DateOnly day, CancellationToken cancellationToken);

    Task<Result<CountsByHourResponse>> GetCountsByHourAsync(DateOnly day, CancellationToken cancellationToken);
}
