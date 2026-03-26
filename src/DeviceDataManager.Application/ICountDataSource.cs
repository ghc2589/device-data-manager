using DeviceDataManager.Domain;

namespace DeviceDataManager.Application;

public interface ICountDataSource
{
    Task<CountQueryOutcome> GetCountsAsync(CancellationToken cancellationToken);
}

public sealed class CountQueryOutcome
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<CountItem>? Items { get; init; }

    public static CountQueryOutcome Ok(IReadOnlyList<CountItem> items) =>
        new() { Success = true, Items = items };

    public static CountQueryOutcome Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
