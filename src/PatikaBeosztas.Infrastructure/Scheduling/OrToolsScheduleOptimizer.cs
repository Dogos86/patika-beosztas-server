using Google.OrTools.Sat;
using PatikaBeosztas.Application.Scheduling;
using PatikaBeosztas.Domain;

namespace PatikaBeosztas.Infrastructure.Scheduling;

public sealed class OrToolsScheduleOptimizer : IScheduleOptimizer
{
    public const string AlgorithmVersion = "cp-sat-9.15-phase3a.1";

    public async Task<ScheduleOptimizationResult> OptimizeAsync(
        ScheduleOptimizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            return await SolveAsync(input, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                ScheduleSolverStatus.Cancelled,
                "SOLVER_CANCELLED",
                "Az optimalizálás megszakadt.",
                input.Candidates.Count);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException and
            not StackOverflowException)
        {
            return Failure(
                ScheduleSolverStatus.Failed,
                "SOLVER_RUNTIME_FAILURE",
                $"A solver futása sikertelen ({exception.GetType().Name}).",
                input.Candidates.Count);
        }
    }

    private static async Task<ScheduleOptimizationResult> SolveAsync(
        ScheduleOptimizerInput input,
        CancellationToken cancellationToken)
    {
        var model = new CpModel();
        var variables = input.Candidates.ToDictionary(
            candidate => candidate.Key,
            candidate => model.NewBoolVar($"option_{SafeName(candidate.Key)}"),
            StringComparer.Ordinal);
        var variableCount = variables.Count;
        var constraintCount = 0;
        var objective = LinearExpr.NewBuilder();

        foreach (var candidate in input.Candidates.Where(item => item.IsFixed || item.IsLocked))
        {
            model.Add(variables[candidate.Key] == 1);
            constraintCount++;
        }

        foreach (var group in input.Candidates
                     .Where(IsPrimaryWork)
                     .GroupBy(candidate => new { candidate.EmployeeId, candidate.Date }))
        {
            model.Add(LinearExpr.Sum(group.Select(item => variables[item.Key])) <= 1);
            constraintCount++;
        }

        foreach (var group in input.Candidates.GroupBy(candidate =>
                     new { candidate.EmployeeId, candidate.Date }))
        {
            var ordered = group
                .OrderBy(candidate => candidate.StartTime)
                .ThenBy(candidate => candidate.EndTime)
                .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
                .ToArray();
            for (var left = 0; left < ordered.Length; left++)
            {
                for (var right = left + 1; right < ordered.Length; right++)
                {
                    if (!Overlaps(ordered[left], ordered[right]))
                    {
                        continue;
                    }

                    model.Add(
                        variables[ordered[left].Key] +
                        variables[ordered[right].Key] <= 1);
                    constraintCount++;
                }
            }
        }

        var coverageSlack = new Dictionary<string, IntVar>(StringComparer.Ordinal);
        foreach (var slot in input.CoverageSlots)
        {
            var covering = input.Candidates
                .Where(candidate => Covers(candidate, slot))
                .Select(candidate => variables[candidate.Key])
                .ToArray();
            var slack = model.NewIntVar(
                0,
                slot.RequiredCount,
                $"coverage_slack_{SafeName(slot.Key)}");
            coverageSlack[slot.Key] = slack;
            variableCount++;
            model.Add(LinearExpr.Sum(covering) + slack >= slot.RequiredCount);
            constraintCount++;
            objective.AddTerm(
                slack,
                slot.Severity == CoverageSeverity.Blocking
                    ? input.Weights.BlockingShortage
                    : input.Weights.WarningShortage);
        }

        var quotaSlack = new Dictionary<Guid, IntVar>();
        foreach (var employee in input.Employees)
        {
            var employeeCandidates = input.Candidates
                .Where(candidate => candidate.EmployeeId == employee.EmployeeId)
                .ToArray();
            AddHardLimit(
                model,
                employeeCandidates,
                variables,
                candidate => candidate.OvertimeMinutes,
                employee.MaximumOvertimeMinutes,
                ref constraintCount);
            AddHardCountLimit(
                model,
                employeeCandidates.Where(candidate =>
                    candidate.Date.DayOfWeek == DayOfWeek.Saturday),
                variables,
                employee.MaximumSaturdayAssignments,
                ref constraintCount);
            AddHardCountLimit(
                model,
                employeeCandidates.Where(candidate =>
                    candidate.Date.DayOfWeek == DayOfWeek.Sunday),
                variables,
                employee.MaximumSundayAssignments,
                ref constraintCount);
            AddHardCountLimit(
                model,
                employeeCandidates.Where(candidate =>
                    candidate.Segments.Any(segment =>
                        segment.TimeType == TimeType.OnCallDuty)),
                variables,
                employee.MaximumOnCallAssignments,
                ref constraintCount);
            AddHardCountLimit(
                model,
                employeeCandidates.Where(candidate =>
                    candidate.Segments.Any(segment =>
                        segment.TimeType == TimeType.Standby)),
                variables,
                employee.MaximumStandbyAssignments,
                ref constraintCount);

            var assignedExpression = LinearExpr.WeightedSum(
                employeeCandidates.Select(candidate => variables[candidate.Key]),
                employeeCandidates.Select(candidate => (long)candidate.TotalMinutes));
            var maximumMinutes = Math.Max(
                employee.TargetMinutes,
                employeeCandidates.Sum(candidate => candidate.TotalMinutes));
            var deviation = model.NewIntVar(
                0,
                maximumMinutes,
                $"target_deviation_{employee.EmployeeId:N}");
            variableCount++;
            model.AddAbsEquality(deviation, assignedExpression - employee.TargetMinutes);
            constraintCount++;
            objective.AddTerm(deviation, input.Weights.TargetHoursDeviation);
        }

        foreach (var quota in input.Quotas)
        {
            var matching = input.Candidates
                .Where(candidate =>
                    candidate.EmployeeId == quota.EmployeeId &&
                    MatchesQuota(candidate, quota.Dimension))
                .Select(candidate => variables[candidate.Key])
                .ToArray();
            model.Add(LinearExpr.Sum(matching) <= quota.Maximum);
            constraintCount++;

            if (quota.Severity == QuotaSeverity.Required)
            {
                var slack = model.NewIntVar(
                    0,
                    quota.Minimum,
                    $"quota_slack_{quota.Id:N}");
                quotaSlack[quota.Id] = slack;
                variableCount++;
                model.Add(LinearExpr.Sum(matching) + slack >= quota.Minimum);
                constraintCount++;
                objective.AddTerm(slack, input.Weights.BlockingShortage);
            }

            var deviation = model.NewIntVar(
                0,
                Math.Max(quota.Target, matching.Length),
                $"quota_target_deviation_{quota.Id:N}");
            variableCount++;
            model.AddAbsEquality(deviation, LinearExpr.Sum(matching) - quota.Target);
            constraintCount++;
            objective.AddTerm(deviation, input.Weights.QuotaTarget);
        }

        AddFairnessObjective(
            model,
            input,
            variables,
            candidate => candidate.Date.DayOfWeek is
                DayOfWeek.Saturday or DayOfWeek.Sunday,
            "weekend",
            input.Weights.WeekendFairness,
            objective,
            ref variableCount,
            ref constraintCount);
        AddFairnessObjective(
            model,
            input,
            variables,
            candidate => candidate.StartTime >= new TimeOnly(14, 0),
            "evening",
            input.Weights.EveningFairness,
            objective,
            ref variableCount,
            ref constraintCount);
        AddLocationChangeObjective(
            model,
            input,
            variables,
            objective,
            ref variableCount,
            ref constraintCount);

        foreach (var candidate in input.Candidates)
        {
            var variable = variables[candidate.Key];
            if (candidate.HasPreferredMatch)
            {
                objective.AddTerm(variable, -input.Weights.PreferredWindowMatch);
            }

            if (candidate.HasAvoidViolation)
            {
                objective.AddTerm(variable, input.Weights.AvoidWindowViolation);
            }

            if (candidate.HasPendingLeaveOverlap)
            {
                objective.AddTerm(variable, input.Weights.PendingLeaveOverlap);
            }

            if (candidate.OvertimeMinutes > 0)
            {
                objective.AddTerm(
                    variable,
                    checked((long)candidate.OvertimeMinutes * input.Weights.Overtime));
            }

            if (candidate.IsLongShift)
            {
                objective.AddTerm(variable, -input.Weights.LongShiftPreference);
            }

            if (candidate.MatchesPreviousPublished)
            {
                objective.AddTerm(variable, -input.Weights.PreviousScheduleChange);
            }

            if (candidate.IsFixed || candidate.IsLocked)
            {
                objective.AddTerm(variable, -input.Weights.PreserveAcceptedDecision);
            }
        }

        model.Minimize(objective);
        using var solver = new CpSolver
        {
            StringParameters =
                $"max_time_in_seconds:{input.MaxSolveSeconds} " +
                $"num_search_workers:{input.WorkerCount} " +
                $"random_seed:{input.DeterministicSeed} " +
                "log_search_progress:false"
        };
        using var registration = cancellationToken.Register(solver.StopSearch);
        var solverStatus = await Task.Run(() => solver.Solve(model), CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
        var mappedStatus = Map(solverStatus);
        if (mappedStatus is not ScheduleSolverStatus.Optimal and
            not ScheduleSolverStatus.Feasible)
        {
            return new(
                mappedStatus,
                [],
                [],
                null,
                Statistics(
                    input.Candidates.Count,
                    variableCount,
                    constraintCount,
                    solver),
                mappedStatus switch
                {
                    ScheduleSolverStatus.Infeasible => "SOLVER_INFEASIBLE",
                    ScheduleSolverStatus.ModelInvalid => "SOLVER_MODEL_INVALID",
                    _ => "SOLVER_NO_ACCEPTED_SOLUTION"
                },
                "A solver nem adott elfogadható Optimal vagy Feasible eredményt.");
        }

        var selectedCandidates = input.Candidates
            .Where(candidate => solver.BooleanValue(variables[candidate.Key]))
            .OrderBy(candidate => candidate.Date)
            .ThenBy(candidate => candidate.StartTime)
            .ThenBy(candidate => candidate.EmployeeId)
            .ToArray();
        var assignments = selectedCandidates
            .Select(candidate => BuildAssignment(
                candidate,
                input,
                selectedCandidates))
            .ToArray();
        var issues = BuildSolutionIssues(
            input,
            solver,
            variables,
            coverageSlack,
            quotaSlack);
        return new(
            mappedStatus,
            assignments,
            issues,
            checked((long)Math.Round(solver.ObjectiveValue)),
            Statistics(
                input.Candidates.Count,
                variableCount,
                constraintCount,
                solver),
            null,
            null);
    }

    private static ScheduleSelectedAssignment BuildAssignment(
        ScheduleCandidateOption candidate,
        ScheduleOptimizerInput input,
        IReadOnlyList<ScheduleCandidateOption> selectedCandidates)
    {
        var reasonCodes = new List<string>
        {
            "LocationAllowed",
            "NoLeave",
            "HoursBalance"
        };
        if (input.CoverageSlots.Any(slot => Covers(candidate, slot)))
        {
            reasonCodes.Add("CapabilityMatch");
            reasonCodes.Add("CoverageContribution");
        }

        if (candidate.IsFixed)
        {
            reasonCodes.Add("FixedRule");
        }

        if (candidate.HasPreferredMatch)
        {
            reasonCodes.Add("PreferredWindowMatch");
        }

        if (candidate.HasAvoidViolation)
        {
            reasonCodes.Add("AvoidWindowViolation");
        }

        if (candidate.OvertimeMinutes > 0)
        {
            reasonCodes.Add("OvertimeUsed");
        }

        if (candidate.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            reasonCodes.Add("WeekendQuota");
        }

        if (candidate.MatchesPreviousPublished)
        {
            reasonCodes.Add("PreviousScheduleStability");
        }

        var selectedScore = Score(candidate, input.Weights);
        var alternatives = input.Candidates
            .Where(alternative =>
                alternative.EmployeeId != candidate.EmployeeId &&
                alternative.LocationId == candidate.LocationId &&
                alternative.Date == candidate.Date &&
                alternative.StartTime == candidate.StartTime &&
                alternative.EndTime == candidate.EndTime &&
                SameTimeTypes(alternative, candidate) &&
                IsHardValidAlternative(
                    alternative,
                    selectedCandidates,
                    input))
            .Select(alternative =>
            {
                var score = Score(alternative, input.Weights);
                return new ScheduleAlternativeScore(
                    alternative.EmployeeId,
                    alternative.EmployeeDisplayName,
                    selectedScore - score,
                    ScoreComponents(alternative, input.Weights),
                    TradeoffCodes(alternative));
            })
            .OrderBy(alternative => alternative.ScoreDifference)
            .ThenBy(alternative => alternative.EmployeeId)
            .Take(5)
            .ToArray();
        return new(
            candidate,
            reasonCodes.Distinct(StringComparer.Ordinal).ToArray(),
            ScoreComponents(candidate, input.Weights),
            alternatives);
    }

    private static bool IsHardValidAlternative(
        ScheduleCandidateOption alternative,
        IReadOnlyList<ScheduleCandidateOption> selected,
        ScheduleOptimizerInput input)
    {
        var employeeSelected = selected
            .Where(item => item.EmployeeId == alternative.EmployeeId)
            .ToArray();
        if (IsPrimaryWork(alternative) &&
            employeeSelected.Any(item =>
                item.Date == alternative.Date &&
                IsPrimaryWork(item)))
        {
            return false;
        }

        var limits = input.Employees.SingleOrDefault(item =>
            item.EmployeeId == alternative.EmployeeId);
        if (limits is null)
        {
            return false;
        }

        if (limits.MaximumOvertimeMinutes is { } overtimeLimit &&
            employeeSelected.Sum(item => item.OvertimeMinutes) +
            alternative.OvertimeMinutes > overtimeLimit)
        {
            return false;
        }

        if (!FitsCountLimit(
                employeeSelected,
                alternative,
                item => item.Date.DayOfWeek == DayOfWeek.Saturday,
                limits.MaximumSaturdayAssignments) ||
            !FitsCountLimit(
                employeeSelected,
                alternative,
                item => item.Date.DayOfWeek == DayOfWeek.Sunday,
                limits.MaximumSundayAssignments) ||
            !FitsCountLimit(
                employeeSelected,
                alternative,
                item => item.Segments.Any(segment =>
                    segment.TimeType == TimeType.OnCallDuty),
                limits.MaximumOnCallAssignments) ||
            !FitsCountLimit(
                employeeSelected,
                alternative,
                item => item.Segments.Any(segment =>
                    segment.TimeType == TimeType.Standby),
                limits.MaximumStandbyAssignments))
        {
            return false;
        }

        return input.Quotas
            .Where(quota => quota.EmployeeId == alternative.EmployeeId)
            .All(quota =>
                employeeSelected.Count(item =>
                    MatchesQuota(item, quota.Dimension)) +
                (MatchesQuota(alternative, quota.Dimension) ? 1 : 0) <=
                quota.Maximum);
    }

    private static bool FitsCountLimit(
        IEnumerable<ScheduleCandidateOption> selected,
        ScheduleCandidateOption alternative,
        Func<ScheduleCandidateOption, bool> predicate,
        int? maximum) =>
        maximum is null ||
        selected.Count(predicate) + (predicate(alternative) ? 1 : 0) <= maximum;

    private static List<ScheduleOptimizationIssue> BuildSolutionIssues(
        ScheduleOptimizerInput input,
        CpSolver solver,
        Dictionary<string, BoolVar> variables,
        Dictionary<string, IntVar> coverageSlack,
        Dictionary<Guid, IntVar> quotaSlack)
    {
        var issues = new List<ScheduleOptimizationIssue>();
        foreach (var slot in input.CoverageSlots)
        {
            var shortage = solver.Value(coverageSlack[slot.Key]);
            if (shortage <= 0)
            {
                continue;
            }

            issues.Add(new(
                "COVERAGE_SHORTAGE",
                slot.Severity == CoverageSeverity.Blocking
                    ? ScheduleIssueSeverity.Blocking
                    : ScheduleIssueSeverity.Warning,
                null,
                slot.LocationId,
                slot.Date,
                slot.StartTime,
                slot.EndTime,
                new Dictionary<string, object?>
                {
                    ["requiredCapability"] = slot.RequiredCapability.ToString(),
                    ["timeType"] = slot.TimeType.ToString(),
                    ["requiredCount"] = slot.RequiredCount,
                    ["shortage"] = shortage
                }));
        }

        foreach (var quota in input.Quotas.Where(item =>
                     item.Severity == QuotaSeverity.Required))
        {
            if (!quotaSlack.TryGetValue(quota.Id, out var variable))
            {
                continue;
            }

            var shortage = solver.Value(variable);
            if (shortage <= 0)
            {
                continue;
            }

            issues.Add(new(
                "REQUIRED_QUOTA_SHORTAGE",
                ScheduleIssueSeverity.Blocking,
                quota.EmployeeId,
                null,
                null,
                null,
                null,
                new Dictionary<string, object?>
                {
                    ["dimension"] = quota.Dimension.ToString(),
                    ["period"] = quota.Period.ToString(),
                    ["minimum"] = quota.Minimum,
                    ["shortage"] = shortage
                }));
        }

        foreach (var candidate in input.Candidates.Where(item =>
                     item.HasPendingLeaveOverlap &&
                     solver.BooleanValue(variables[item.Key])))
        {
            issues.Add(new(
                "PENDING_LEAVE_OVERLAP",
                ScheduleIssueSeverity.Warning,
                candidate.EmployeeId,
                candidate.LocationId,
                candidate.Date,
                candidate.StartTime,
                candidate.EndTime,
                new Dictionary<string, object?>()));
        }

        return issues;
    }

    private static void AddHardLimit(
        CpModel model,
        IEnumerable<ScheduleCandidateOption> candidates,
        Dictionary<string, BoolVar> variables,
        Func<ScheduleCandidateOption, int> coefficient,
        int? maximum,
        ref int constraintCount)
    {
        if (maximum is null)
        {
            return;
        }

        var applicable = candidates
            .Where(candidate => coefficient(candidate) > 0)
            .ToArray();
        model.Add(LinearExpr.WeightedSum(
            applicable.Select(candidate => variables[candidate.Key]),
            applicable.Select(candidate => (long)coefficient(candidate))) <= maximum.Value);
        constraintCount++;
    }

    private static void AddHardCountLimit(
        CpModel model,
        IEnumerable<ScheduleCandidateOption> candidates,
        Dictionary<string, BoolVar> variables,
        int? maximum,
        ref int constraintCount)
    {
        if (maximum is null)
        {
            return;
        }

        model.Add(LinearExpr.Sum(
            candidates.Select(candidate => variables[candidate.Key])) <= maximum.Value);
        constraintCount++;
    }

    private static void AddFairnessObjective(
        CpModel model,
        ScheduleOptimizerInput input,
        Dictionary<string, BoolVar> variables,
        Func<ScheduleCandidateOption, bool> predicate,
        string name,
        int weight,
        LinearExprBuilder objective,
        ref int variableCount,
        ref int constraintCount)
    {
        if (input.Employees.Count < 2)
        {
            return;
        }

        var counts = new List<IntVar>();
        foreach (var employee in input.Employees)
        {
            var matching = input.Candidates
                .Where(candidate =>
                    candidate.EmployeeId == employee.EmployeeId &&
                    predicate(candidate))
                .ToArray();
            var count = model.NewIntVar(
                0,
                matching.Length,
                $"{name}_count_{employee.EmployeeId:N}");
            variableCount++;
            model.Add(count == LinearExpr.Sum(
                matching.Select(candidate => variables[candidate.Key])));
            constraintCount++;
            counts.Add(count);
        }

        var maximum = model.NewIntVar(0, input.PeriodEnd.DayNumber -
            input.PeriodStart.DayNumber + 1, $"{name}_max");
        var minimum = model.NewIntVar(0, input.PeriodEnd.DayNumber -
            input.PeriodStart.DayNumber + 1, $"{name}_min");
        variableCount += 2;
        model.AddMaxEquality(maximum, counts);
        model.AddMinEquality(minimum, counts);
        constraintCount += 2;
        objective.AddTerm(maximum, weight);
        objective.AddTerm(minimum, -weight);
    }

    private static void AddLocationChangeObjective(
        CpModel model,
        ScheduleOptimizerInput input,
        Dictionary<string, BoolVar> variables,
        LinearExprBuilder objective,
        ref int variableCount,
        ref int constraintCount)
    {
        foreach (var employee in input.Employees)
        {
            var candidates = input.Candidates
                .Where(candidate => candidate.EmployeeId == employee.EmployeeId)
                .ToArray();
            foreach (var left in candidates)
            {
                foreach (var right in candidates.Where(right =>
                             right.Date == left.Date.AddDays(1) &&
                             right.LocationId != left.LocationId))
                {
                    var changed = model.NewBoolVar(
                        $"location_change_{SafeName(left.Key)}_{SafeName(right.Key)}");
                    variableCount++;
                    model.Add(changed <= variables[left.Key]);
                    model.Add(changed <= variables[right.Key]);
                    model.Add(changed >= variables[left.Key] + variables[right.Key] - 1);
                    constraintCount += 3;
                    objective.AddTerm(changed, input.Weights.LocationChange);
                }
            }
        }
    }

    private static bool MatchesQuota(
        ScheduleCandidateOption candidate,
        ShiftQuotaDimension dimension)
    {
        var startHour = candidate.StartTime.Hour;
        return dimension switch
        {
            ShiftQuotaDimension.MorningShift => startHour < 12 && IsPrimaryWork(candidate),
            ShiftQuotaDimension.AfternoonShift => startHour is >= 12 and < 18 &&
                                                   IsPrimaryWork(candidate),
            ShiftQuotaDimension.EveningShift => startHour >= 18 && IsPrimaryWork(candidate),
            ShiftQuotaDimension.LongShift => candidate.IsLongShift,
            ShiftQuotaDimension.SaturdayShift =>
                candidate.Date.DayOfWeek == DayOfWeek.Saturday,
            ShiftQuotaDimension.SundayShift =>
                candidate.Date.DayOfWeek == DayOfWeek.Sunday,
            ShiftQuotaDimension.OnCallDuty => candidate.Segments.Any(segment =>
                segment.TimeType == TimeType.OnCallDuty),
            ShiftQuotaDimension.Standby => candidate.Segments.Any(segment =>
                segment.TimeType == TimeType.Standby),
            _ => false
        };
    }

    private static bool IsPrimaryWork(ScheduleCandidateOption candidate) =>
        candidate.Segments.Any(segment =>
            segment.TimeType is TimeType.Work or TimeType.Overtime);

    private static bool Covers(
        ScheduleCandidateOption candidate,
        ScheduleCoverageSlot slot) =>
        candidate.LocationId == slot.LocationId &&
        candidate.Date == slot.Date &&
        candidate.EffectiveCapabilities.Contains(slot.RequiredCapability) &&
        candidate.Segments.Any(segment =>
            segment.TimeType == slot.TimeType &&
            segment.StartTime < slot.EndTime &&
            slot.StartTime < segment.EndTime);

    private static bool Overlaps(
        ScheduleCandidateOption left,
        ScheduleCandidateOption right) =>
        left.StartTime < right.EndTime && right.StartTime < left.EndTime;

    private static bool SameTimeTypes(
        ScheduleCandidateOption left,
        ScheduleCandidateOption right) =>
        left.Segments.Select(segment => segment.TimeType)
            .SequenceEqual(right.Segments.Select(segment => segment.TimeType));

    private static long Score(
        ScheduleCandidateOption candidate,
        ScheduleOptimizationWeights weights) =>
        ScoreComponents(candidate, weights).Values.Sum();

    private static Dictionary<string, long> ScoreComponents(
        ScheduleCandidateOption candidate,
        ScheduleOptimizationWeights weights) =>
        new Dictionary<string, long>
        {
            ["PreferredWindowMatch"] = candidate.HasPreferredMatch
                ? weights.PreferredWindowMatch
                : 0,
            ["AvoidWindowViolation"] = candidate.HasAvoidViolation
                ? -weights.AvoidWindowViolation
                : 0,
            ["Overtime"] = checked(
                -(long)candidate.OvertimeMinutes * weights.Overtime),
            ["PendingLeaveOverlap"] = candidate.HasPendingLeaveOverlap
                ? -weights.PendingLeaveOverlap
                : 0,
            ["PreviousScheduleStability"] = candidate.MatchesPreviousPublished
                ? weights.PreviousScheduleChange
                : 0,
            ["LongShiftPreference"] = candidate.IsLongShift
                ? weights.LongShiftPreference
                : 0
        };

    private static List<string> TradeoffCodes(
        ScheduleCandidateOption candidate)
    {
        var result = new List<string>();
        if (!candidate.HasPreferredMatch)
        {
            result.Add("PreferredWindowNotMatched");
        }

        if (candidate.HasAvoidViolation)
        {
            result.Add("AvoidWindowViolation");
        }

        if (candidate.OvertimeMinutes > 0)
        {
            result.Add("MoreOvertime");
        }

        if (!candidate.MatchesPreviousPublished)
        {
            result.Add("PreviousScheduleChanged");
        }

        return result;
    }

    private static ScheduleSolverStatistics Statistics(
        int candidateCount,
        int variableCount,
        int constraintCount,
        CpSolver solver) =>
        new(
            candidateCount,
            variableCount,
            constraintCount,
            solver.WallTime(),
            checked((long)Math.Round(solver.BestObjectiveBound)),
            solver.NumConflicts(),
            solver.NumBranches());

    private static ScheduleSolverStatus Map(CpSolverStatus status) =>
        status switch
        {
            CpSolverStatus.Optimal => ScheduleSolverStatus.Optimal,
            CpSolverStatus.Feasible => ScheduleSolverStatus.Feasible,
            CpSolverStatus.Infeasible => ScheduleSolverStatus.Infeasible,
            CpSolverStatus.ModelInvalid => ScheduleSolverStatus.ModelInvalid,
            CpSolverStatus.Unknown => ScheduleSolverStatus.Unknown,
            _ => ScheduleSolverStatus.Failed
        };

    private static ScheduleOptimizationResult Failure(
        ScheduleSolverStatus status,
        string errorCode,
        string error,
        int candidateCount) =>
        new(
            status,
            [],
            [],
            null,
            new(candidateCount, 0, 0, 0, null, null, null),
            errorCode,
            error);

    private static string SafeName(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash.AsSpan(0, 8));
    }
}
