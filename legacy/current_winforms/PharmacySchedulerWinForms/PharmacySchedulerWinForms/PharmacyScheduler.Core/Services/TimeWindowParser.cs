namespace PharmacyScheduler.Core.Services;

public readonly record struct TimeWindow(TimeOnly Start, TimeOnly End)
{
    public bool Contains(TimeOnly start, TimeOnly end) => Start <= start && end <= End;
    public bool Overlaps(TimeOnly start, TimeOnly end) => start < End && Start < end;
}

public static class TimeWindowParser
{
    public static IReadOnlyList<TimeWindow> ParseMany(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<TimeWindow>();
        }

        var windows = new List<TimeWindow>();
        var parts = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var range = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (range.Length != 2)
            {
                continue;
            }

            if (TimeOnly.TryParse(range[0], out var start) && TimeOnly.TryParse(range[1], out var end) && start < end)
            {
                windows.Add(new TimeWindow(start, end));
            }
        }

        return windows;
    }
}
