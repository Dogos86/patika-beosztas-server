# Phase 2D – HR- és bérszámfejtési belépés a valódi API alapján

A `contracts/openapi.phase2d.json` (11 576 sor) az egyetlen hiteles szerződés. Mock fallback nincs `api` módban. A munka nagy (~30+ új/módosított fájl), ezért iterációkra bontom, minden iteráció után format + lint + typecheck + vitest + build.

## Iteráció A – Contract és típusalap

1. `contracts/openapi.phase2d.json` bemásolva (kész), `docs/hr-payroll-mvp-product-decisions.md` mentve (kész).
2. Új permission enumok: `ManagePayrollOnboarding`, `ViewPayrollSensitiveData`, `ReviewTaxAllowanceSurvey`, `ExportPayrollData` — `types.ts`, `dto/enums.ts`, permission label map, users UI.
3. Új domain típusok (`services/types.ts`): `PayrollOnboarding`, `PayrollProfile`, `TaxAllowanceSurvey` (kérdés-, válasz-, státusz-modell), `TaxDeclarationRequirement`, `PayrollExport`.
4. DTO-k (`services/http/dto/`): payrollOnboarding, payrollProfile, taxAllowanceSurvey (Draft/Submitted/Reviewed/Completed), taxDeclarationRequirement, export CSV/JSON válaszok, `LeaveVersionRequest`.

## Iteráció B – HTTP service-ek

Új szerviz-modulok (`services/http/services/`):

- `payrollOnboarding` (get, complete, export JSON/CSV, /api/me/payroll-onboarding)
- `payrollProfile` (GET/PUT `expectedVersion`-nal, maszkolás client-oldalon)
- `taxAllowanceSurvey` (own CRUD + submit; admin submit/reopen/review/complete)
- `taxDeclarationRequirement` (list/create/status/override)
- `leaveRequest` teljes lifecycle: submit/record/close/decision/cancel/withdraw (Phase 2D leave endpointok)
- Mock oldalon determinisztikus válaszok autosave-hez.

Bővítjük a `Services` interfészt és a service locatort (`api` és `mock` mindkettőt tartalmazza).

## Iteráció C – Új dolgozó wizard 6 lépésre

`src/routes/app.admin.employees.new.tsx` átstrukturálása állapotgép + Stepper alapon:

1. Alapadatok (kiterjesztve: birthDate, employment start, employeeIdCode, taxIdentifier maszkolt input, externalPayrollId).
2. Munkaviszony és beosztási profil (a jelenlegi step 2/3 átvitele: locations, munkaidőprofil, kompetenciák, kvóták, preferenciák).
3. HR/bérszámfejtési profil (permission gate: `ManagePayrollOnboarding`; státusz = Draft engedélyezett).
4. 2026 adókedvezmény-felmérő (opcionális, később is felvehető; „Marad Draft" gomb).
5. Nyilatkozat-checklist (`taxDeclarationRequirement` CRUD lista + státusz).
6. Belépési fiók (mai lépés).

Részleges hibakezelés: employee mindig megmarad, minden további lépés önállóan újrapróbálható; tab-onkénti toast.

## Iteráció D – Adókedvezmény-felmérő

Új komponens: `src/components/payroll/TaxAllowanceSurveyForm.tsx`.

- Backend `survey` contract szerint dinamikus kérdéssor (kérdés id + típus: tri-state Yes/No/DontKnow, szám, dátum, feltételes csoportok).
- Autosave 800ms debounce PATCH-ekkel; Draft állapotban mindig.
- Mezőnkénti hibák a mező alatt (ProblemDetails.errors mapping).
- Beküldés előtti összefoglaló, munkavállalói nyilatkozat + dátum.
- Submitted után read-only; admin `reopen` gombbal engedélyezi újra.
- Se diagnózis mező, se orvosi feltöltés, se adószámítás.

Használat: wizard 4. lépés + saját nézet + admin review nézet.

## Iteráció E – Admin ellenőrző nézet és export

- `src/routes/app.admin.payroll.tsx` új admin oldal `ManagePayrollOnboarding` guarddal, sorlista dolgozók onboarding státuszaival.
- Részletnézet (`app.admin.payroll.$employeeId.tsx`): felmérő review, javasolt nyilatkozatok, manual override, HR megjegyzés, audit időpontok. `ReviewTaxAllowanceSurvey` szükséges a review műveletekhez, `ViewPayrollSensitiveData` a maszkolatlan adóazonosítóhoz.
- Export sáv: `JSON export` + `CSV export` gombok, csak `ExportPayrollData` permissionnel; a válaszblob letöltése.
- Nav bővítés `AppShell`-ben új „Bérszámfejtés" menüponttal (permission-szűrt).

## Iteráció F – Saját nézet

- `src/routes/app.me.payroll.tsx` a kapcsolt Employee-vel rendelkező felhasználóknak.
- Saját onboarding státusz, saját felmérő kitöltés/beküldés, nyilatkozatok korlátozott státuszlistája (csak érintett sorok, HR-jegyzet nélkül).

## Iteráció G – Leave-lifecycle bekötése

A `services/http` leave szolgáltatás átírása az új Phase 2D lifecycle-ra (submit/record/close/decision/cancel/withdraw) és a UI (`app.requests.tsx`, `app.admin.approvals.tsx`) átvezetése ezekre az akciókra. A UI szemantikailag nem változik jelentősen, csak az endpointok.

## Iteráció H – Permissionök és guardok

- `permissions.ts`: új permissionök label + description.
- `useRequirePermission` az új admin route-okon.
- `AppShell` menüszűrés.
- `ManageSchedules` önmagában NEM mutat payroll adatokat.

## Iteráció I – Tesztek és minőségi kapu

- Vitest: új mapperek (payrollOnboarding, taxAllowanceSurvey, taxDeclarationRequirement), permission guard, felmérő autosave (fake timers), leave lifecycle akciók, wizard részleges hiba, export permission.
- Zárás: `bun run format && tsgo && bun run lint && bunx vitest run && bun run build:dev`. Csak zöld futással jelzek „kész"-t.

## Technikai megjegyzések

- `VITE_DATA_SOURCE=mock`-ban minden új HR/payroll művelet determinisztikus mockot ad, hogy a UI végigjárható legyen backend nélkül.
- `api` módban semmilyen mock fallback nincs; a szerver hibája (400/409/500) látható hiba lesz.
- Adóazonosítót a UI mindig maszkolja, kivéve `ViewPayrollSensitiveData` permissionnel — akkor „Mutat" toggle-lel.
- Payroll onboarding **Draft** állapotban is befejezhető a wizard; nem blokkolja az employee létrehozását.

A terjedelem miatt több turnben szállítom (A→I sorrendben, összevonva ahol biztonságos). Iterációnként lint + typecheck + teszt.

Ha jóváhagyod, indulok az A + B iterációval (típusok + DTO-k + HTTP service-ek + mock alap), és a következő üzenetben szállítom a wizardot és a felmérőt.
