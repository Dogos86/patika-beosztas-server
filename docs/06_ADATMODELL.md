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
- Active

## Employee
- Id
- OrganizationId
- UserId nullable
- FullName
- DisplayName
- BirthDate opcionális, csak indokolt exportigény esetén
- ProfessionalRole
- Active
- Schedulable
- IncludeInAutoFill
- CountsAsPharmacist
- MonthlyHours
- MaxDailyHours
- Version

## UserPermission
- UserId
- Permission

## Location
- Id
- OrganizationId
- Name
- Kind: Central/Branch
- Active
- Version

## EmployeeLocation
- EmployeeId
- LocationId
- Active

## EmployeeTimePreference
- EmployeeId
- DayOfWeek
- StartLocalTime
- EndLocalTime
- PreferenceType: Preferred/Forbidden

## Schedule
- Id
- OrganizationId
- Name
- PeriodStart
- PeriodEnd
- Status
- Version
- ApprovedBy/ApprovedAt

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
- Version

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
