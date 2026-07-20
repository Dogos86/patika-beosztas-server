namespace PharmacyScheduler.Core.Services;

public static class HalfHourHelper
{
    public static bool IsHalfHourAligned(TimeOnly time) => time.Minute is 0 or 30 && time.Second == 0;

    public static IEnumerable<TimeOnly> EnumerateSlots(TimeOnly start, TimeOnly end)
    {
        for (var current = start; current < end; current = current.AddMinutes(30))
        {
            yield return current;
        }
    }

    public static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && startB < endA;

    public static decimal DurationHours(TimeOnly start, TimeOnly end) =>
        (decimal)(end.ToTimeSpan() - start.ToTimeSpan()).TotalHours;
}
