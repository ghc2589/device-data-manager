namespace DeviceDataManager.Application;

public sealed class Result<T>
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public T? Value { get; init; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };

    public static Result<T> Fail(string message) => new() { Success = false, ErrorMessage = message };
}
