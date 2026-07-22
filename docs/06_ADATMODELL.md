# Adatmodell vázlat

Minden üzleti entitás tartalmazzon `OrganizationId`-t, ahol releváns audit mezőket és optimista konkurenciaverziót.

## Organization
- Id
- Name
- TimeZoneId (`Europe/Budapest`)
- AllowSelfApproval
- Active

## ApplicationUser
- Id
- OrganizationId
- authentication provider fields
- DisplayName
- Active
- EmployeeId nullable, egyedi kapcsolat ugyanabban a szervezetben
- Version (`xmin`)

## Employee
- Id
- OrganizationId
- FullName
- DisplayName
- BirthDate opcionális, csak indokolt exportigény esetén
- ProfessionalRole
- Active
- Schedulable
- IncludeInAutoFill
- CountsAsPharmacist
- MonthlyMinutesLimit nullable
- MaxDailyMinutes nullable
- Version

## UserPermission
- UserId
- Permission

## Location
- Id
- OrganizationId
- Name
- Type: Central/Branch
- Active
- Version

## EmployeeLocation
- EmployeeId
- LocationId
- Active

## LocationWeeklyOpening és OpeningInterval
- OrganizationId, LocationId; telephelyenként egy heti fejléc
- hét nap külön `OpeningDayMode` értéke: Closed/Open24Hours/CustomIntervals
- OpeningInterval: DayOfWeek, StartTime, EndTime nullable; null végidő = 24:00
- CreatedAtUtc/UpdatedAtUtc, Version (`xmin`)

## LocationShiftTemplate
- OrganizationId, LocationId
- Name, Category, WeekdayMask, StartTime/EndTime
- RequiredCapability opcionális
- IsActive, CreatedAtUtc/UpdatedAtUtc, Version (`xmin`)

## EmployeeCapability
- OrganizationId, EmployeeId, Capability
- kompozit kulcs; capability-implikáció alkalmazási/domain projekció

## EmployeeWorkProfile
- OrganizationId, EmployeeId; dolgozónként egy rekord
- szerződéses havi/heti perc, standard/minimum/maximum műszakpercek
- hosszú műszak, teljes nyitvatartás, túlóra, ügyelet, készenlét és hétvége
  engedélyei és korlátai
- IncludeInAutoFill, CreatedAtUtc/UpdatedAtUtc, Version (`xmin`)

## EmployeeShiftQuotaRule
- OrganizationId, EmployeeId, Dimension, Period
- MinimumCount, TargetCount, MaximumCount
- Severity, IsActive, CreatedAtUtc/UpdatedAtUtc, Version (`xmin`)
- egyedi EmployeeId + Dimension + Period

## WorkPreference
- Id
- OrganizationId
- EmployeeId
- Type: Available/Preferred/Avoid/Unavailable/Fixed
- DateFrom/DateTo
- DayOfWeek opcionális
- IsFullDay vagy StartTime/EndTime
- LocationId opcionális
- Note opcionális
- IsActive
- CreatedAtUtc/UpdatedAtUtc
- Version (`xmin`)

## Schedule
- Id
- OrganizationId
- Name
- PeriodStart
- PeriodEnd
- Status: Generating/Draft/UnderReview/Approved/Published/Archived
- Version
- ApprovedBy/ApprovedAt
- PublishedBy/PublishedAt
- ArchivedBy/ArchivedAt

A legutolsó közzétett állapottal való összehasonlításhoz meg kell őrizni a
közzétett verzió változatlan referenciáját vagy pillanatképét. Ennek pontos
normalizálása nyitott, de a későbbi Draft módosítás nem írhatja át azt az
állapotot, amelyet a dolgozók korábban láttak.

## ScheduleGenerationRun
- Id
- OrganizationId
- ScheduleId
- PeriodStart/PeriodEnd
- ScopeType és scope-hivatkozások
- Status
- AlgorithmVersion
- PendingRequestHandlingMode
- RequestedBy/RequestedAtUtc
- CompletedAtUtc nullable
- ResultSummary
- IdempotencyKey hash vagy referencia
- Version

A generálási futás a kiválasztott teljes időszakot optimalizálja. A scope azt
írja le, hogy teljes vagy részleges újragenerálást kértek; nem lazítja fel az
időszak egészére vonatkozó validációt.

## Shift
- Id
- OrganizationId
- ScheduleId
- EmployeeId
- LocationId
- StartsAtUtc
- EndsAtUtc
- TimeType
- Note
- IsLocked
- GeneratedByRunId nullable
- Version

## ScheduleIssue
- Id vagy stabil, egy eredményen belüli kulcs
- OrganizationId
- ScheduleId
- GenerationRunId nullable
- IssueType
- Severity: Warning/Blocking
- Date és az alkalmazható EmployeeId/LocationId/ShiftId hivatkozások
- strukturált részletek

A problémák származtatott read modelként is előállíthatók. Ha tároltak, nem
válhatnak el a beosztás és a validáció verziójától.

## ShiftGenerationExplanation
- OrganizationId
- ScheduleId
- ShiftId
- GenerationRunId
- strukturált választási indokok
- alternatív jelöltek és strukturált hátrányaik

A magyarázat a tényleges generálási döntésből származik. A pontos tárolási vagy
újraszámítási stratégia nyitott, de auditálhatóan az algoritmusverzióhoz és a
bemeneti snapshothoz kell kötnie.

## GeneratedSuggestionDecision
- OrganizationId
- ScheduleId
- ShiftId vagy generált javaslat stabil azonosítója
- DecisionType: Rejected
- ActorUserId
- OccurredAtUtc
- Reason nullable

Az elutasítás hatókörének és megőrzési idejének szabálya nyitott; enélkül a
generátor nem feltételezheti, hogy egy korábbi elutasítás örökre tiltást jelent.

## CoverageRequirement
- Id
- OrganizationId
- LocationId
- DayOfWeek
- StartTime/EndTime
- RequiredCapability
- RequiredCount
- Severity: Warning/Blocking
- IsActive, CreatedAtUtc/UpdatedAtUtc
- Version (`xmin`)

## LeaveRequest
- Id
- OrganizationId
- EmployeeId
- CreatedByUserId
- Type: AnnualLeave/SickLeave/UnpaidLeave/ParentalLeave/Other
- DateFrom/DateTo; a `DateTo` kizárólag betegállománynál lehet nyitott
- IsFullDay vagy StartTime/EndTime
- Status
- EmployeeNote
- DecisionReason
- DecidedBy/DecidedAt
- Version

A normál állapotgép `Draft` → `Pending` → `Approved`/`Rejected`,
`Draft`/`Pending` → `Withdrawn`, illetve `Approved` → `Cancelled`. A
betegállomány állapotgépe `Reported` → `Recorded` → `Closed`.

## LeaveStatusHistory
- LeaveRequestId
- FromStatus
- ToStatus
- ActorUserId
- OccurredAtUtc
- Reason

## AuditEvent
- OrganizationId
- ActorUserId
- Action
- EntityType
- EntityId
- OccurredAtUtc
- CorrelationId
- Redacted change summary

## AiCommandPreview
- Id
- OrganizationId
- UserId
- SchemaVersion
- InputText
- ResolvedActionsJson
- ValidationSnapshotJson
- ConfirmationTokenHash
- ExpiresAtUtc
- ExecutedAtUtc nullable

Ne tároljunk diagnózist és alapértelmezésben nyers hangfájlt.

## Tenant-kapcsolatok

Az organization-scoped kapcsolatok kompozit idegen kulcsot használnak, így a
kapcsoló rekord `OrganizationId` értékének egyeznie kell mindkét hivatkozott
rekord szervezetével. Ez különösen kötelező az ApplicationUser–Employee,
EmployeeLocation–Employee/Location, EmployeeTimeWindow–Employee,
EmployeeAllowedTimeType–Employee, UserPermission–ApplicationUser,
WorkPreference–Employee/Location, LeaveRequest–Employee/User és
LeaveStatusHistory–LeaveRequest/User, továbbá a LocationWeeklyOpening–Location,
OpeningInterval–LocationWeeklyOpening, LocationShiftTemplate–Location,
CoverageRequirement–Location, EmployeeCapability–Employee,
EmployeeWorkProfile–Employee és EmployeeShiftQuotaRule–Employee kapcsolatoknál.
