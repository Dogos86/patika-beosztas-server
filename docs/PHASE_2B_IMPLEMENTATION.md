# Phase 2B runtime implementáció

## Megvalósított vertikális szeletek

- telephelyenkénti heti nyitvatartás napi Closed/Open24Hours/CustomIntervals
  móddal és több intervallummal;
- telephelyi műszaksablon CRUD napmaszkkal, kategóriával és opcionális
  capabilityvel;
- ProfessionalRole-tól különálló dolgozói capability-k és implikációk;
- coverage követelmény CRUD, szűrés és explicit nyitvatartási/inaktív
  telephelyi figyelmeztetések;
- dolgozónként egy munkaprofil, valamint dimenzió- és periódusalapú
  műszakkvóták;
- újrahasznosítható napi munkablokk-normalizáló szervezeti időzónával;
- jövőbeli, összeget nem tartalmazó könyvelési szerződés;
- EF Core migráció, runtime OpenAPI `0.3.0-phase2b`, közös draft contract és
  PostgreSQL/Testcontainers integrációs lefedés.

## Entitások és perzisztencia

Új táblák: `LocationWeeklyOpenings`, `OpeningIntervals`,
`LocationShiftTemplates`, `EmployeeCapabilities`, `CoverageRequirements`,
`EmployeeWorkProfiles` és `EmployeeShiftQuotaRules`. Minden szervezeti
kapcsolat kompozit tenant-FK-t használ. A szerkeszthető fejléc/entitás
PostgreSQL `xmin` konkurenciaverzióval és Created/Updated UTC audittal
rendelkezik; a listákhoz organization/location/employee/day/active indexek
tartoznak.

A `20260722125050_Phase2BPlanningFoundations` migráció a korábbi
`CountsAsPharmacist=true` és `PharmacyManager` dolgozókat veszteség nélkül
explicit `Pharmacist` capabilityre képezi. A kompatibilitási mező ebben a
fázisban megmarad.

## Jogosultság és biztonság

- nyitvatartás és műszaksablon: `ManageLocations`;
- coverage követelmény: `ManageCoverageRules`;
- dolgozói capability, munkaprofil és kvóta: `ManageEmployees`.

Minden végpont cookie-auth és organization scope alatt működik. Minden
mutáció antiforgery tokent kér, az idegen szervezeti azonosító 404, stale
verzió 409. A létrehozás, módosítás, capability-csere és inaktiválás
immutable audit eseményt ír.

## Fontos invariánsok

- a CustomIntervals rendezett, nem átfedő és a napi módhoz illeszkedik;
- azonos capability átfedő coverage követelményei pontonkénti maximumot
  képeznek;
- SpecialistPharmacist magában foglalja Pharmacist, SpecialistAssistant az
  Assistant capabilityt;
- munkaprofilnál min ≤ standard ≤ normál max ≤ napi max, a feltételes
  engedélyek és limitek konzisztensen mozognak;
- IncludeInAutoFill csak aktív és beosztható dolgozónál igaz;
- kvótánál min ≤ target ≤ max, és egy dolgozó/dimenzió/periódus egyedi;
- a napi elsődleges blokk nem lehet hézagos vagy több telephelyes; azonos
  telephely érintkező/átfedő Work blokkjai összeolvadnak, az érintkező
  Work/Overtime szegmensek külön könyvelési típust tartanak meg;
- a napi maximumot UTC instantok közötti valós idő alapján ellenőrizzük,
  explicit szervezeti időzónával és DST-validációval.

## Ismert korlátok

- teljes Schedule aggregate és generátor csak Phase 3-ban készül, ezért az
  inaktív telephely kizárása jelenleg domain-jogosultság és warning, nem futó
  generálási eredmény;
- heti nyitvatartási kivételhez jövőbiztos draft contract van, de perzisztált
  exception és endpoint még nincs;
- nyitvatartáson kívüli coverage menthető warninggal; nem hard validation;
- eltérő TimeType átfedő szegmenseinek könyvelési prioritása nincs eldöntve,
  ezért a normalizáló elutasítja;
- a könyvelési contract összeget és bérszámfejtési képletet nem tartalmaz;
- a `CountsAsPharmacist` deprecated kompatibilitási mező eltávolítása későbbi
  adatmigráció és kliensátállás után lehetséges.
