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

## EmployeeTimePreference
- OrganizationId
- EmployeeId
- DayOfWeek vagy konkrét dátumtartomány
- StartLocalTime
- EndLocalTime
- PreferenceType: Preferred/Forbidden
- Version

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

## CoverageRule
- Id
- OrganizationId
- LocationId
- recurrence/date selector
- StartLocalTime
- EndLocalTime
- RequiredProfessionalRole or capability
- RequiredCount
- Severity
- Active
- Version

## LeaveRequest
- Id
- OrganizationId
- EmployeeId
- CreatedByUserId
- LeaveType
- StartsAtUtc
- EndsAtUtc nullable for open sick leave if required
- FullDay
- Status
- EmployeeNote
- DecisionReason
- DecidedBy/DecidedAt
- Version

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
EmployeeAllowedTimeType–Employee és UserPermission–ApplicationUser
kapcsolatoknál.
