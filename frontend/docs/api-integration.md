# API integráció — leendő ASP.NET Core .NET 10 Web API

Ez a dokumentum írja le, hogyan cseréljük a jelenlegi mock service réteget élő HTTP kliensre.

## Áttekintés

```
UI komponens → services (locator) → HTTP kliens → ASP.NET Core Web API → PostgreSQL/SQL Server
```

A frontend a `src/services/interfaces.ts` fájlban definiált szerződéshez kötődik. A backend
ennek megfelelő végpontokat szállít. A mock és a HTTP implementáció bármikor cserélhető
a `src/services/index.ts` locatorban.

## Kulcselvek

- **Session-alapú identitás**: a self-service metódusok (`getMySchedule`, `listMyRequests`,
  `createMyRequest`, `withdrawMyRequest`) nem fogadnak kliensről `employeeId` /
  `createdByUserId` / `actorUserId` mezőt. A backend a bearer tokenből azonosítja a
  hívót és feloldja a hozzá tartozó `Employee`-t.
- **Admin metódusok** külön interfészen (`adminLeaveRequest.*`) — a backend
  jogosultság-alapon fogja őket védeni (`ApproveLeaveRequests`, `ManageAllLeaveRequests`).
- **Frontend guard csak UX**: az `/app/admin/*` route-okon `admin` szerep szükséges, de a
  végleges hozzáférés-vezérlést a backend adja.
- **AI**: az `interpretCommand` → `answerClarification` → `executeCommand` folyamat
  előnézet-token alapú, a végrehajtás nem történhet meg megerősítés nélkül.

## Enum-leképezés (frontend ↔ backend)

| Domain           | Frontend (lowercase snake)                                                           | Backend (PascalCase)                                                                                                                                                                                    |
| ---------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ProfessionalRole | pharmacy_manager                                                                     | PharmacyManager                                                                                                                                                                                         |
|                  | pharmacist                                                                           | Pharmacist                                                                                                                                                                                              |
|                  | specialist_assistant                                                                 | SpecialistAssistant                                                                                                                                                                                     |
|                  | assistant                                                                            | Assistant                                                                                                                                                                                               |
|                  | pharmacist_trainee                                                                   | PharmacistTrainee                                                                                                                                                                                       |
|                  | assistant_trainee                                                                    | AssistantTrainee                                                                                                                                                                                        |
|                  | cleaner                                                                              | Cleaner                                                                                                                                                                                                 |
|                  | finance_helper                                                                       | FinanceHelper                                                                                                                                                                                           |
|                  | other                                                                                | Other                                                                                                                                                                                                   |
| ScheduleStatus   | draft / approved / archived                                                          | Draft / Approved / Archived                                                                                                                                                                             |
| LeaveType        | annual_leave                                                                         | AnnualLeave                                                                                                                                                                                             |
|                  | sick_leave                                                                           | SickLeave                                                                                                                                                                                               |
|                  | unpaid_leave                                                                         | UnpaidLeave                                                                                                                                                                                             |
|                  | parental_leave                                                                       | ParentalLeave                                                                                                                                                                                           |
|                  | other                                                                                | Other                                                                                                                                                                                                   |
| LeaveStatus      | draft, pending, approved, rejected, withdrawn, cancelled, reported, recorded, closed | Draft, Pending, Approved, Rejected, Withdrawn, Cancelled, Reported, Recorded, Closed                                                                                                                    |
| AppPermission    | (közvetlen PascalCase)                                                               | ViewOwnSchedule, ManageOwnLeaveRequests, ManageAllLeaveRequests, ApproveLeaveRequests, ManageEmployees, ManageLocations, ManageCoverageRules, ManageSchedules, RunAutoFill, UseAiAssistant, ManageUsers |

A leképezést a HTTP-kliens rétegben végezzük (ne szennyezze be a domain típusokat).

## Csere lépései

1. Backend generál egy OpenAPI specifikációt.
2. Létrehozzuk `src/services/http/*.ts` implementációkat az `interfaces.ts`-hez.
3. `src/services/index.ts`: `export const services: Services = httpServices;`
4. Auth: `AuthService.login/logout/getSession` bearer tokenre épül; Authorization header
   interceptor a HTTP kliensben.
5. A UI-t nem kell módosítani.

## Környezeti változók

- `VITE_API_URL` — a Web API bázis URL-je.
- Titkos kulcs SOHA nem kerülhet a frontendbe.

## Offline

A PWA szolgáltatás első körben csak olvasási cache. Jóváhagyás, kérelem beadás, AI művelet
offline állapotban le van tiltva.

## Phase 2E.7 — modul-státusz (API-mód)

| Modul                                                    | Állapot API-módban                                               |
| -------------------------------------------------------- | ---------------------------------------------------------------- |
| Auth, felhasználók                                       | Valódi API                                                       |
| Dolgozók, planning (kompetencia, munkaidőprofil, kvóták) | Valódi API                                                       |
| Telephelyek, nyitvatartás, műszaksablonok                | Valódi API (lapozott)                                            |
| Lefedettségi szabályok                                   | Valódi API                                                       |
| Távollét (saját + admin)                                 | Valódi API                                                       |
| HR / bérszámfejtés, adókedvezmény kérdőív                | Valódi API                                                       |
| Munkavégzési kérések                                     | Valódi API                                                       |
| Beosztás generálás / review / publish (Phase 3A)         | Valódi API                                                       |
| Legacy munkatér (`/app/admin/scheduler`)                 | Letiltva — átirányítás a `/app/admin/schedules` felületre        |
| AI asszisztens, Értesítések                              | Demóadat; API-módban explicit jelzés, **nincs csendes fallback** |

Szabály: API-módban egyetlen modul sem eshet vissza csendben mockra. Ahol a backend még
hiányzik, a UI `ModuleNotice` komponenssel jelzi, a szervizek pedig explicit hibát dobnak.

## Regressziós lefedettség

`src/services/http/regression.test.ts` — távollét életciklus (create → submit → decision,
táppénz record/close), payroll kérdőív (submit → review → complete), export URL képzés,
`expectedVersion` továbbadása, 409/422 hibák, és a „nincs csendes mock fallback" invariáns.
