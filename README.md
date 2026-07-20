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

## Helyi PostgreSQL

1. Másold a `.env.example` fájlt `.env` néven.
2. Minden `CHANGE_ME` értéket cserélj saját, kizárólag helyi jelszóra.
3. Indítsd el az adatbázist:

```powershell
docker compose --env-file .env up -d postgres
```

Az adatbázis csak a helyi `127.0.0.1:5432` címen publikálódik. A `.env`
gitignore-olt; production secretet ne adj a repositoryhoz.

## API indítása és Development seed

PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "https://localhost:7180"
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=patika_beosztas;Username=patika_app;Password=<helyi-adatbazis-jelszo>"
$env:Seed__DemoPassword = "<eros-kizarolag-helyi-demo-jelszo>"
dotnet dev-certs https --trust
dotnet run --project src/PatikaBeosztas.Api
```

Development módban az API migrálja az adatbázist, majd idempotensen létrehozza:

- `admin@example.invalid`: minden permission, kapcsolt és beosztható
  gyógyszertárvezető Employee;
- `dolgozo@example.invalid`: csak saját funkciók, kapcsolt Employee;
- egy belépési fiók nélküli Employee;
- egy aktív központi és egy inaktív fióktelep.

Mindkét demo fiók jelszava a `Seed__DemoPassword`. Ezek anonimizált,
kizárólag Development adatok, production használatra tilosak. Developmenten
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

Az OpenAPI futás közben az `/openapi/v1.json` címen érhető el.

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
dotnet restore PatikaBeosztas.slnx
dotnet build PatikaBeosztas.slnx --no-restore --configuration Release
dotnet test PatikaBeosztas.slnx --no-build --configuration Release
```

A biztonsági integrációs tesztek valódi `postgres:17-alpine` Testcontainers
adatbázist használnak. Docker hiányában ezek `Skipped` állapotúak; ilyenkor
PostgreSQL-specifikus működés nem tekinthető ellenőrzöttnek.

Részletes fázisleírás: `docs/PHASE_1_IMPLEMENTATION.md`.
A Phase 1.5 hardening, frontend-integráció és production checklist:
`docs/PHASE_1_5_HARDENING.md`.
