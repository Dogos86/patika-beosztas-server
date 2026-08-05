# Phase 3A – tartós beosztásgenerálás és közzététel

## Megvalósított runtime szelet

A Phase 3A valódi Domain, Application, Infrastructure, Contracts és API
implementáció. A legacy WinForms forrás változatlan maradt; a régi motor csak
karakterizációs referenciaként szolgál.

Az optimalizáló a `Google.OrTools` `9.15.6755` NuGet csomag CP-SAT solverét
használja. Az algoritmus verzióazonosítója
`cp-sat-9.15-phase3a.1`. A solver bemenete véges, folytonos napi
műszakopció-lista; az egymáshoz érő normál sablonok egy napi assignmentté
egyesülnek, a köztük lévő rés viszont nem képez split shiftet.

Hard feltételek többek között:

- aktív, beosztható és autofillbe bevont dolgozó és telephely;
- telephely-hozzárendelés, effektív kompetencia és nyitvatartás;
- jóváhagyott/rögzített távollét és `Unavailable` kizárás;
- legfeljebb egy folytonos normál napi blokk és egy napi telephely;
- napi, túlóra-, hétvégi, ügyeleti, készenléti és kvótamaximumok;
- `Fixed` és locked assignment megőrzése;
- reject-exclusion és a részleges scope-on kívüli assignment rögzítése.

A blocking és warning coverage hiánya explicit slack és `ScheduleIssue`;
a blocking issue Draftot eredményezhet, de az approve és publish tiltott.
A súlyozott célfüggvény kezeli a preferenciát, avoid ablakot, célórát,
túlórát, hétvégi/esti igazságosságot, telephelyváltást, kvótacélt,
pending leave-et és az előző közzétett beosztás stabilitását.

## Perzisztencia és migráció

Az új tartós entitások:

- `SchedulePlan`;
- `ScheduleGenerationRun`;
- `ShiftAssignment`;
- `ShiftSegment`;
- `ScheduleIssue`;
- `ShiftExplanation`;
- `GeneratedSuggestionDecision`.

Minden üzleti rekord szervezeti határral rendelkezik. Az aktív runok
idempotencia- és scope-indexei PostgreSQL filtered unique indexek; az
szerkeszthető rekordok `xmin` optimista konkurenciát használnak. A migráció két
lépésből áll:

1. `20260729083000_Phase3ATimeTypeCoverage` – kompatibilis `Work` defaulttal
   hozzáadja a `TimeType` mezőt a sablonhoz és coverage-hez;
2. `20260729084051_Phase3ASchedulePersistence` – létrehozza a teljes
   beosztási sémát, tenant-kompozit idegen kulcsokkal és indexekkel.

## Háttérfeldolgozás

A generálás persistent `Queued → Running → Succeeded/Failed/Cancelled`
állapotgépet használ. A worker:

- kanonikus JSON input snapshotot és SHA-256 hash-t ment;
- determinisztikus seedet és alapértelmezetten egy solver workert használ;
- csak `Optimal` vagy `Feasible` eredményt perzisztál;
- menti a solver statisztikákat, magyarázatokat, alternatívákat és issue-kat;
- cancel esetén nem ír részleges eredményt;
- restartkor a korábban `Running` futást
  `RECOVERED_AFTER_RESTART` hibakóddal lezárja;
- minden fontos állapotot auditál.

## API és jogosultság

A runtime OpenAPI tartalmazza a generálási lifecycle, schedule lista/részlet,
employee matrix, location coverage, issue/change, explanation/alternative,
lock/unlock/reject/replace, részleges regenerate, workflow, clone és saját
published beosztás végpontjait.

Jogosultságok:

- `ManageSchedules`: Draft olvasás és korrekció;
- `RunAutoFill`: generálás és újragenerálás;
- `ApproveSchedules`: jóváhagyás;
- `PublishSchedules`: közzététel és archiválás;
- `ViewOwnSchedule`: kizárólag a kapcsolt dolgozó legfrissebb Published nézete.

Az admin szerep önmagában nem ad hozzáférést. Minden mutáció cookie authot,
CSRF-et, tenant szűrést, auditot és – ahol állapotot módosít –
`expectedVersion` ellenőrzést használ. A Published terv immutábilis; módosításhoz
idempotens Draft clone szükséges. Az új Published revision ugyanabban a
tranzakcióban archiválja az előzőt.

## Automatizált elfogadás

Az `S-001`–`S-019` és `S-025` optimizer/golden eseteket a
`ScheduleGoldenScenarioTests`, az `S-020`–`S-024` és a PostgreSQL runtime
lifecycle eseteket a `Phase3ARuntimeTests` ellenőrzi. Az integrációs tesztek
valódi `postgres:17-alpine` Testcontainers adatbázist és EF migrációt használnak;
nincs in-memory vagy SQLite helyettesítés, és Docker-hiány esetén nem skipelnek.

Az S-025 smoke adathalmaza 8 telephely, 40 dolgozó és 31 nap. A rögzített
Debug mérés:

- candidate opció: `1240`;
- változó: `5332`;
- constraint: `5372`;
- solver wall time: `20,011 s`;
- eredmény: `Feasible`;
- konfigurált limit: `20 s` (az acceptance maximuma `60 s`).

## OpenAPI

A dokumentum runtime export:

- verzió: `0.5.0-phase3a`;
- fájl: `contracts/openapi.phase3a.json`;
- SHA-256:
  `35d940513265f6b61d01949ad25495d2f49b585a3c5296f1d465940533e85131`.

Frissítés és ellenőrzés:

```powershell
.\eng\export-openapi.ps1
Get-FileHash .\contracts\openapi.phase3a.json -Algorithm SHA256
```

## Railway pilot CSRF-hardening

A frontend összes védett schedule-mutációja ugyanazt a cookie-s, központi HTTP
klienst használja. A kliens `credentials: "include"` beállítással küld, a
CSRF-tokent memóriában tartja, login/logout után törli, párhuzamos
tokenfrissítéskor közös Promise-t használ, és `INVALID_CSRF_TOKEN` után csak
egyszer küldi újra az eredeti kérést. A generálás, újragenerálás és Draft-klón
idempotenciakulcsa az újrapróbálás során változatlan.

## Munkaidőprofil és generálási preflight

A munkaidőprofil frontendje az óra/perc bevitelt minden mezőnél egész percre
alakítja. A feltételes korlátokat a request mapper normalizálja: kikapcsolt
vállalásnál `null`, bekapcsolt vállalásnál kötelező pozitív érték kerülhet a
kérésbe. A frontend és az OpenAPI kanonikus mezőnevei többek között
`allowsLongShift`, `maximumLongShiftMinutes` és
`allowsFullOpeningHoursShift`; kompatibilitási cast vagy alternatív mezőnév
nincs. Sikeres mentés és 409-es verzióütközés után a kliens újratölti a profilt,
így az `id` és `version` mindig a backend állapotát követi.

A generálási preflight az összesítések mellett telephely- és dolgozószintű
ellenőrzéseket is visszaad. A felület külön jelzi a nyitvatartás, műszaksablon,
lefedettség, telephely-hozzárendelés, munkaidőprofil, pozitív szerződéses idő,
kompetencia és blokkoló elérhetőség állapotát. A blokkoló hiányok letiltják az
indítást, név szerinti magyar üzenetet és közvetlen javítási hivatkozást adnak;
nulla jelölt esetén a `NO_CANDIDATE_OPTIONS` továbbra is blokkoló.

Az API és a Railway web gateway az `/api/auth/csrf` választ `no-store`
fejléccel adja; a gateway a bejövő `Cookie` és a különálló kimenő `Set-Cookie`
fejléceket veszteség nélkül továbbítja. A session-életciklus integrációs teszt
a login → CSRF → employee mutation → generation → regeneration → logout/login
→ új generation folyamatot PostgreSQL felett ellenőrzi.

## Railway pilot generálási életciklus

A generálási run statisztikája `Queued` és `Running` állapotban szándékosan
nullable. A frontend ilyenkor pollolja a runt, „Az optimalizáló még dolgozik.”
állapotot mutat, majd terminális siker esetén frissíti a listát, a részletet, a
dolgozói mátrixot, a lefedettséget, az issue-kat és a változásokat.

Az indítás előtti preflight strukturált bemeneti darabszámokat és blokkoló
issue-kat ad. Nulla jelölt esetén a `NO_CANDIDATE_OPTIONS` issue tartalmazza a
telephely-, nyitvatartás-, sablon-, lefedettség-, jogosult dolgozó-,
munkaidőprofil-, telephely-hozzárendelés- és képességdarabszámokat. Blokkoló
preflight mellett nem jön létre üres Draft. A felhasználó a hiányzó
munkaidőprofilt és lefedettséget közvetlen beállítási hivatkozással látja.

Az indítás és az újragenerálás kliensoldali in-flight deduplikációt és stabil
idempotenciakulcsot használ. Az újragenerálás mindig a legfrissebb tervverziót
kéri le; `409` esetén bezárja és alaphelyzetbe állítja a modalt, majd teljesen
újratölti a beosztást. A `ManageSchedules` jogosultságú admin az igazoltan üres,
verzióegyező Draftot külön, auditált végponton archiválhatja.
