# Patika Beosztás szerver

ASP.NET Core .NET 10 + PostgreSQL backend gyógyszertári szervezet, felhasználók,
jogosultságok, dolgozók és telephelyek kezeléséhez. A felület és a
felhasználónak szánt hibaüzenetek magyarok; az API- és kódazonosítók angolok.

## Előfeltételek

- .NET SDK 10.0.300 vagy újabb 10.0.x patch;
- Docker Desktop vagy más Docker-kompatibilis runtime;
- PowerShell 7 vagy egy POSIX shell.

A `legacy/current_winforms/` referenciaforrás read-only. A canonical,
buildelhető legacy solution:
`legacy/current_winforms/PharmacySchedulerWinForms/PharmacySchedulerWinForms/PharmacyScheduler.sln`.
A felső, hibás `legacy/current_winforms/PharmacyScheduler.sln` és a megőrzendő
`legacy/current_winforms/PharmacySchedulerWinForms.zip` archívum nem része az
új solution buildjének.

## Helyi API indítása PowerShellből

A repository gyökerében:

```powershell
Copy-Item .env.example .env
# Szerkeszd a .env fájlt, és cseréld le az összes CHANGE_ME értéket.
.\eng\start-local-api.ps1
```

A script betölti a `.env` fájlt anélkül, hogy kiírná a titkos értékeket,
ellenőrzi a .NET 10 SDK-t és a Docker motort, majd pontosan ezt a PostgreSQL
szolgáltatást indítja vagy használja újra:

```powershell
docker compose --env-file .env up -d postgres
```

Ezután `Development` környezetben a `https://localhost:7180` címen, launch
profile nélkül indítja az API-t. Ha a fejlesztői tanúsítvány még nem
megbízható, a script jelzi az egyszer futtatandó parancsot:

```powershell
dotnet dev-certs https --trust
```

Az adatbázis a konténeren belüli 5432-es portot alapértelmezetten csak a helyi
`127.0.0.1:55432` címre publikálja. A `.env` fájlban a
`POSTGRES_HOST_PORT=55432` és a
`ConnectionStrings__DefaultConnection` `Port=55432` értéke maradjon azonos, ha
másik host portot választasz. A `.env` gitignore-olt; production secretet ne
adj a repositoryhoz. A script futása az API konzolát foglalja, leállítása
`Ctrl+C`.

## Development seed

Development módban az API migrálja az adatbázist, majd idempotensen létrehozza:

- `admin@example.invalid`: minden permission, kapcsolt és beosztható
  gyógyszertárvezető Employee;
- `dolgozo@example.invalid`: csak saját funkciók, kapcsolt Employee;
- egy belépési fiók nélküli Employee;
- egy aktív központi és egy inaktív fióktelep.

Mindkét demo fiók jelszava a `Seed__DemoPassword`. A fix demo szervezetet,
telephelyeket, dolgozókat és fiókokat a seeder saját stabil azonosítójuk alapján
egyenként biztosítja. Emiatt egy részben feltöltött régi fejlesztői adatbázis is
kiegészül, miközben a felhasználó által létrehozott dolgozók nem törlődnek és
nem íródnak felül. Ezek anonimizált, kizárólag Development adatok; Developmenten
kívül sem automatikus migráció, sem seed nem fut.

## Cookie és CSRF használat

A böngésző `__Host-PatikaSession` nevű `HttpOnly`, `Secure`, `SameSite=Lax`
Identity cookie-t kap; tokent nem kell és nem szabad `localStorage`-be tenni.
Az API és a frontend HTTPS-en, azonos site alatt fusson; a frontend minden
authentikált és CSRF-tokenes kérésnél használjon `credentials: "include"`
beállítást. Fejlesztéskor az engedélyezett pontos frontend origineket a
`Cors:AllowedOrigins` konfiguráció adja meg. Credential mellett
`AllowAnyOrigin` nem használható.

Minden állapotmódosító kérés előtt:

1. `GET /api/auth/csrf`;
2. az így kapott `requestToken` értéket küldd `X-CSRF-TOKEN` headerben;
3. a fetch kérés használja a `credentials: "include"` beállítást.

A tokenhez tartozó `__Host-PatikaCsrf` cookie `HttpOnly`, `Secure`,
`SameSite=Strict`. A login IP-particionált, percenként 5 kérésre korlátozott,
és az Identity 5 hibás próbálkozás után 15 percre zárol.

Fő auth endpointok:

- `GET /api/auth/csrf`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/session`

A session válasz a user és organization azonosítója mellett a szervezet nevét,
`OrganizationTimeZoneId` értékét, az exact permission listát és az opcionális
kapcsolt dolgozót adja vissza. Nincs összefoglaló `admin` role; a frontend a
permissionök alapján dönt.

Felhasználó-integrációnál a lista és a
`GET /api/admin/users/{id}` részletező végpont `version` mezőt ad. A permission-,
employee-link- és státuszmódosító PUT requestek ezt `expectedVersion` néven
visszakérik; stale verzió esetén `409 CONCURRENCY_CONFLICT` érkezik.

## Frontend-integráció

A támogatott alapértelmezett Development origin pontosan
`https://localhost:5173`. A `.env` fájlban felülírható, további pontos origineket
pedig sorszámozott konfigurációs kulccsal lehet hozzáadni:

```dotenv
Cors__AllowedOrigins__0=https://localhost:5173
# Cors__AllowedOrigins__1=https://masik-helyi-origin.example
```

Credentiales CORS mellett wildcard és `AllowAnyOrigin` nincs. A Production
konfiguráció nem kap automatikus Development origint.

A frontend minden cookie-s kérésnél `credentials: "include"` beállítást használ.
Mutáció előtt kérjen CSRF-tokent, majd ugyanazzal a cookie-konteksszel küldje a
headert, például:

```typescript
const apiBase = "https://localhost:7180";
const csrf = await fetch(`${apiBase}/api/auth/csrf`, {
  credentials: "include",
}).then((response) => response.json());

await fetch(`${apiBase}/api/admin/employees`, {
  method: "POST",
  credentials: "include",
  headers: {
    "Content-Type": "application/json",
    [csrf.headerName]: csrf.requestToken,
  },
  body: JSON.stringify(employee),
});
```

Az exact permissionöket a `GET /api/auth/session` adja; a frontend ne vezessen
le jogosultságot a dolgozó szakmai szerepéből. Employee és ApplicationUser két
külön erőforrás: dolgozó fiók nélkül is létrehozható, a fiók pedig később
`employeeId` értékkel hozható létre vagy az employee-link végponton kapcsolható.

## Runtime OpenAPI és frontend típusgenerálás

A futó dokumentum címe `https://localhost:7180/openapi/v1.json`. A commitolt,
kanonikus Phase 2D exportot mindig a runtime válaszból frissítsd; a JSON-t ne
szerkeszd kézzel:

```powershell
.\eng\export-openapi.ps1
Get-FileHash .\contracts\openapi.phase2d.json -Algorithm SHA256
```

Ha a megfelelő API már fut, a script azt használja. Egyébként ellenőrzi a
szükséges környezeti változókat, elindítja a helyi PostgreSQL-t, Release buildet
készít, ideiglenesen elindítja az API-t, validálja az API címét, a
`0.4.0-phase2d` verziót, a megvalósított modulok útvonalait és a publikus
string enumokat, majd csak az általa indított API-folyamatot állítja le. A
frontend generált típusainak forrása:
`contracts/openapi.phase2d.json`.

## Migráció és minőségi kapuk

Új migráció:

```powershell
$env:ConnectionStrings__DefaultConnection = "<fejlesztoi-kapcsolati-string>"
dotnet ef migrations add <Nev> `
  --project src/PatikaBeosztas.Infrastructure `
  --startup-project src/PatikaBeosztas.Infrastructure `
  --output-dir Persistence/Migrations
```

Ellenőrzés:

```powershell
dotnet restore .\PatikaBeosztas.slnx
dotnet build .\PatikaBeosztas.slnx --configuration Release --no-restore
dotnet test .\PatikaBeosztas.slnx --configuration Release --no-build
dotnet list .\PatikaBeosztas.slnx package --vulnerable --include-transitive
dotnet ef migrations has-pending-model-changes `
  --project .\src\PatikaBeosztas.Infrastructure `
  --startup-project .\src\PatikaBeosztas.Infrastructure
dotnet format .\PatikaBeosztas.slnx --verify-no-changes --no-restore --include src tests eng
git diff --check
```

A biztonsági integrációs tesztek valódi `postgres:17-alpine` Testcontainers
adatbázist használnak. A teljes tesztfuttatáshoz működő Docker runtime kötelező;
hiányában az integrációs teszt assembly indítása hibával leáll, nem `Skipped`
eredménnyel tesz úgy, mintha a PostgreSQL-specifikus működés ellenőrzött volna.

Részletes fázisleírás: `docs/PHASE_1_IMPLEMENTATION.md`.
A Phase 2B nyitvatartási, coverage- és munkaprofil runtime-szelete:
`docs/PHASE_2B_IMPLEMENTATION.md`.
A Phase 2C API-átadás, runtime export és helyi integráció:
`docs/PHASE_2C_API_HANDOFF.md`.
A Phase 2D HR/bérszámfejtési belépés és adókedvezmény-felmérő:
`docs/PHASE_2D_IMPLEMENTATION.md`.
A Phase 1.5 hardening, frontend-integráció és production checklist:
`docs/PHASE_1_5_HARDENING.md`.
A későbbi beosztásmotor frontend–backend közös termékdöntése:
`docs/13_GENERALAS_KOZPONTU_BEOSZTAS.md`; a végrehajtási sorrendet a
`prompts/03_FAZIS_BEOSZTAS_MIGRACIO.md` rögzíti.
