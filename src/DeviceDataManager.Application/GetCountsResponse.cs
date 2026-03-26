using DeviceDataManager.Domain;

namespace DeviceDataManager.Application;

public sealed class GetCountsResponse
{
    public IReadOnlyList<CountItemDto> Items { get; init; } = [];

    public static GetCountsResponse FromItems(IReadOnlyList<CountItem> items) =>
        new()
        {
            Items = items.Select(i => new CountItemDto(i.Label, i.Value)).ToList(),
        };
}

public sealed record CountItemDto(string Label, long Value);
