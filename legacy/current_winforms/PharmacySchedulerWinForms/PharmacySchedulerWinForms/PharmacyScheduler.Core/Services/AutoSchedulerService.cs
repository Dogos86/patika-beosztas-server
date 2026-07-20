using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.Core.Services;

public sealed class AutoSchedulerService
{
    public int FillCoverageGaps(AppData data, SchedulePlan schedule)
    {
        var createdEntries = new List<ShiftEntry>();
        var allEntries = data.Schedules
            .Where(x => x.Id != schedule.Id)
            .SelectMany(x => x.Entries)
            .Concat(schedule.Entries)
            .ToList();

        // Csak aktív telephelyek Id-jeit gyűjtjük össze
        var activeLocationIds = new HashSet<Guid>(data.Locations.Where(x => x.IsActive).Select(x => x.Id));

        for (var date = schedule.PeriodStart; date <= schedule.PeriodEnd; date = date.AddDays(1))
        {
            var rules = data.CoverageRules
                .Where(x => x.DayOfWeek == date.DayOfWeek)
                .Where(x => activeLocationIds.Contains(x.LocationId)) // Inaktív telephely coverage szabályait kihagyjuk
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.Start)
                .ToList();

            foreach (var rule in rules)
            {
                foreach (var slot in HalfHourHelper.EnumerateSlots(rule.Start, rule.End))
                {
                    var slotEnd = slot.AddMinutes(30);
                    var currentCount = CountAssigned(data, allEntries, rule, date, slot, slotEnd);
                    var shortage = rule.RequiredCount - currentCount;

                    while (shortage > 0)
                    {
                        var candidate = SelectBestCandidate(data, allEntries, rule, date, slot, slotEnd);
                        if (candidate is null)
                        {
                            break;
                        }

                        var entry = new ShiftEntry
                        {
                            ScheduleId = schedule.Id,
                            EmployeeId = candidate.Id,
                            LocationId = rule.LocationId,
                            Date = date,
                            Start = slot,
                            End = slotEnd,
                            TimeType = TimeType.Work,
                            Note = "Automatikus kitöltés"
                        };

                        createdEntries.Add(entry);
                        schedule.Entries.Add(entry);
                        allEntries.Add(entry);
                        shortage--;
                    }
                }
            }
        }

        MergeAdjacentEntries(schedule);
        return createdEntries.Count;
    }

    /// <summary>
    /// Meghatározza, hogy egy dolgozó milyen szerepkörben vesz részt az automatikus beosztásban.
    /// Ha van AutoScheduleRoleOverride, azt használjuk, egyébként a saját Role-ját.
    /// </summary>
    private static EmployeeRole GetEffectiveAutoScheduleRole(Employee employee)
    {
      return employee.AutoScheduleRoleOverride ?? employee.Role;
    }

    private static int CountAssigned(AppData data, IReadOnlyList<ShiftEntry> allEntries, CoverageRule rule, DateOnly date, TimeOnly slot, TimeOnly slotEnd)
    {
        var employeeLookup = data.Employees.ToDictionary(x => x.Id);

        return allEntries.Count(entry =>
        {
            if (entry.Date != date || entry.LocationId != rule.LocationId || !entry.TimeType.IsWorkLike())
{
 return false;
     }

            if (!employeeLookup.TryGetValue(entry.EmployeeId, out var employee))
       {
       return false;
     }

            var effectiveRole = GetEffectiveAutoScheduleRole(employee);
  return effectiveRole == rule.Role && HalfHourHelper.Overlaps(entry.Start, entry.End, slot, slotEnd);
        });
    }

    private static Employee? SelectBestCandidate(AppData data, IReadOnlyList<ShiftEntry> allEntries, CoverageRule rule, DateOnly date, TimeOnly slot, TimeOnly slotEnd)
    {
        var candidates = data.Employees
            .Where(employee => employee.IsActive)
   .Where(employee => employee.IncludeInAutoSchedule) // Csak azokat akiknél be van kapcsolva
      .Where(employee => GetEffectiveAutoScheduleRole(employee) == rule.Role)
    .Where(employee => employee.AllowedTimeTypes.Count == 0 || employee.AllowedTimeTypes.Contains(TimeType.Work))
  .Where(employee => employee.AllowedLocationIds.Count == 0 || employee.AllowedLocationIds.Contains(rule.LocationId))
    .Where(employee => !data.Leaves.Any(x => x.EmployeeId == employee.Id && x.StartDate <= date && date <= x.EndDate))
   .Where(employee => !allEntries.Any(entry =>
       entry.EmployeeId == employee.Id &&
     entry.Date == date &&
   HalfHourHelper.Overlaps(entry.Start, entry.End, slot, slotEnd)))
     .ToList();

      if (candidates.Count == 0)
 {
         return null;
        }

        var scored = candidates
       .Select(candidate => new
   {
       Candidate = candidate,
         Score = ScoreCandidate(candidate, allEntries, date, slot, slotEnd, rule.LocationId)
            })
      .OrderByDescending(x => x.Score)
     .ThenBy(x => x.Candidate.DisplayName)
       .ToList();

        return scored.FirstOrDefault()?.Candidate;
 }

    private static decimal ScoreCandidate(Employee candidate, IReadOnlyList<ShiftEntry> allEntries, DateOnly date, TimeOnly slot, TimeOnly slotEnd, Guid locationId)
    {
        var preferredWindows = TimeWindowParser.ParseMany(candidate.PreferredWindows);
        var preferredBonus = preferredWindows.Any(window => window.Contains(slot, slotEnd)) ? 20m : 0m;

        var monthlyHours = allEntries
            .Where(x => x.EmployeeId == candidate.Id && x.TimeType.IsWorkLike() && x.Date.Year == date.Year && x.Date.Month == date.Month)
  .Sum(x => x.Hours);

        var dailyHours = allEntries
            .Where(x => x.EmployeeId == candidate.Id && x.TimeType.IsWorkLike() && x.Date == date)
  .Sum(x => x.Hours);

     var contiguousBonus = allEntries.Any(x =>
   x.EmployeeId == candidate.Id &&
   x.Date == date &&
         x.LocationId == locationId &&
 x.TimeType == TimeType.Work &&
            (x.End == slot || x.Start == slotEnd))
      ? 10m
        : 0m;

    var monthlyPenalty = Math.Max(0m, monthlyHours - candidate.MonthlyHoursLimit) * 100m;
        var dailyPenalty = Math.Max(0m, dailyHours - candidate.MaxDailyHours) * 100m;

        return preferredBonus + contiguousBonus - monthlyHours - (dailyHours * 2m) - monthlyPenalty - dailyPenalty;
    }

    private static void MergeAdjacentEntries(SchedulePlan schedule)
    {
        var merged = new List<ShiftEntry>();

        foreach (var group in schedule.Entries
    .OrderBy(x => x.Date)
            .ThenBy(x => x.EmployeeId)
    .ThenBy(x => x.LocationId)
  .ThenBy(x => x.TimeType)
            .ThenBy(x => x.Start)
     .GroupBy(x => new { x.Date, x.EmployeeId, x.LocationId, x.TimeType, x.Note }))
        {
   ShiftEntry? current = null;

            foreach (var entry in group)
  {
              if (current is null)
      {
         current = Clone(entry);
             continue;
     }

        if (current.End == entry.Start)
  {
        current.End = entry.End;
               continue;
  }

          merged.Add(current);
        current = Clone(entry);
    }

      if (current is not null)
 {
         merged.Add(current);
   }
  }

    schedule.Entries = merged;
  }

    private static ShiftEntry Clone(ShiftEntry entry) => new()
    {
        Id = entry.Id,
        ScheduleId = entry.ScheduleId,
  EmployeeId = entry.EmployeeId,
        LocationId = entry.LocationId,
        Date = entry.Date,
        Start = entry.Start,
        End = entry.End,
        TimeType = entry.TimeType,
        Note = entry.Note
    };
}
