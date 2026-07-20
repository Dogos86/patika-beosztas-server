# Phase 1.5 – hardening és frontend-integráció

## Hatókör

Ez a fázis a Phase 1 szervezet/auth/permission/employee/location szeletét
erősíti meg. Nem tartalmaz szabadságworkflow-t, beosztásmotort, coverage-et,
autofillt, AI-t, diktálást, exportot, meghívót, emailt vagy MFA-t. A
`legacy/current_winforms/` forrásai változatlanok.

## Konkurencia és utolsó user manager

- Az `ApplicationUser.Version` PostgreSQL `xmin` rowversion.
- A `UserResponse` mindig visszaadja a verziót.
- Permission-, employee-link- és státusz-PUT esetén az `ExpectedVersion`
  kötelező.
- Stale verzió vagy EF konkurenciahiba:
  `409 CONCURRENCY_CONFLICT`.
- A permission- és státuszmutáció tranzakcióban közös
  `Organization ... FOR UPDATE` sorzárat vesz fel. Így a két külön usert érintő,
  párhuzamos adminelvétel/deaktiválás is egy szervezeti kritikus szakaszon fut.
- Az ellenőrzés a sorzár megszerzése után olvassa újra az aktív
  `ManageUsers` fiókokat. Legfeljebb az egyik konkurens eltávolítás sikerülhet.
- PostgreSQL deadlock (`40P01`) vagy serialization failure (`40001`) biztonságos
  409 választ ad; a nem idempotens mutációt a szerver nem ismétli meg
  automatikusan.

## Tenant-integritás az adatbázisban

A `20260720160921_Phase15UserConcurrencyTenantIntegrity` migráció az eredeti
primary keyeket és az Identity működését megtartva kompozit
organization-scoped alternate keyeket és foreign keyeket ad:

- `ApplicationUser(OrganizationId, EmployeeId)` →
  `Employee(OrganizationId, Id)`;
- `EmployeeLocation(OrganizationId, EmployeeId)` →
  `Employee(OrganizationId, Id)`;
- `EmployeeLocation(OrganizationId, LocationId)` →
  `Location(OrganizationId, Id)`;
- `EmployeeTimeWindow(OrganizationId, EmployeeId)` →
  `Employee(OrganizationId, Id)`;
- `EmployeeAllowedTimeType(OrganizationId, EmployeeId)` →
  `Employee(OrganizationId, Id)`;
- `UserPermission(OrganizationId, UserId)` →
  `ApplicationUser(OrganizationId, Id)`.

Az API explicit organization-szűrése továbbra is kötelező; az adatbázis-korlát
egy második védelmi réteg.

## Frontend contract

`SessionResponse` mezői:

- `UserId`;
- `OrganizationId`;
- `OrganizationName`;
- `OrganizationTimeZoneId`;
- `DisplayName`;
- `Email`;
- `Permissions`;
- `LinkedEmployee`.

A session nem ad `admin` role-t. A frontend kizárólag az exact permission
listából dönt. Új részletező végpont:
`GET /api/admin/users/{id}`, `ManageUsers` policyvel és organization
isolationnel.

## Employee input

- `MonthlyMinutesLimit`: `null` vagy 1–44 640 perc;
- `MaxDailyMinutes`: `null` vagy 1–1 440 perc;
- `BirthDate`: 1900-01-01 és az aktuális `Europe/Budapest` szerinti nap közé
  kell esnie;
- `IncludeInAutoFill=true` csak aktív és beosztható dolgozónál fogadható el;
- a kötelező szövegek trimelve, az opcionális üres/whitespace szövegek
  `null` értékre normalizálva tárolódnak;
- a publikus enumok továbbra is stringként érkeznek; integer enumérték
  elutasított.

Az időablak-átfedés szabálya nem változott; a nyitott termékdöntés az
`OPEN_DECISIONS.md` `LEG-007` pontja.

## Identity hibák

A saját `HungarianIdentityErrorDescriber` magyar leírást ad többek között a
duplikált email/felhasználónév, invalid email/felhasználónév/token, jelszóhossz,
kisbetű, nagybetű, számjegy, speciális karakter és tipikus fióklétrehozási
hibákhoz. Az API Identity-validációja nem adhat angol leírást.

## Cookie, CSRF, CORS és HTTPS

- A frontend és az API HTTPS-en, azonos site alatt fusson.
- A frontend fetch beállítása minden cookie-s kérésnél:
  `credentials: "include"`.
- A session cookie `HttpOnly`, `Secure`, `SameSite=Lax`.
- Mutáció előtt `GET /api/auth/csrf`, majd a kapott request token
  `X-CSRF-TOKEN` headerben küldendő.
- A CSRF cookie `HttpOnly`, `Secure`, `SameSite=Strict`.
- A Development frontend origineket a `Cors:AllowedOrigins` pontos HTTPS
  allowlistje adja. Credential mellett `AllowAnyOrigin` tilos.
- Az OpenAPI `cookieAuth` security scheme-et és a védett mutációk
  `X-CSRF-TOKEN` headerét is dokumentálja.

## Production checklist

- **Forwarded Headers:** a reverse proxy által továbbított `X-Forwarded-For`
  és `X-Forwarded-Proto` feldolgozása a HTTPS redirect/auth előtt történjen.
- **Trusted proxy:** kizárólag a tényleges proxy IP-je vagy hálózata kerüljön
  `KnownProxies`/`KnownNetworks` alá; tetszőleges kliens forwarded headerét nem
  szabad megbízni.
- **TLS és host:** külsőleg csak HTTPS legyen elérhető; az `AllowedHosts`
  konkrét production hostneveket tartalmazzon, ne `*` értéket.
- **Data Protection:** a kulcsgyűrű tartós, hozzáférésvédett, mentett tárhelyen
  legyen; több API-példány ugyanazt a kulcsgyűrűt és application nevet
  használja. A kulcsok ne kerüljenek a repositoryba vagy konténer image-be.
- **Secretek:** connection string, seed-jelszó, proxy- és provider-secretek
  környezeti/üzemeltetői secret store-ból érkezzenek, rotációval és legkisebb
  jogosultsággal. Productionben Development seed nem futhat.
- **Adatbázis:** a production DB-user csak a szükséges sémára kapjon jogot; a
  migráció külön kontrollált deployment lépés legyen.
- **Naplózás:** cookie, CSRF-token, jelszó, connection string és teljes
  érzékeny request body ne kerüljön logba.

## Repository és ellenőrzés

A canonical legacy solution:
`legacy/current_winforms/PharmacySchedulerWinForms/PharmacySchedulerWinForms/PharmacyScheduler.sln`.
A hibás felső solution és a
`legacy/current_winforms/PharmacySchedulerWinForms.zip` archívum megmarad.
Az új repository két nem hordozható, ékezetes dokumentumfájlneve ASCII,
UTF-8-kompatibilis névre változott, és a `MANIFEST.txt` hivatkozásai frissültek.

A PostgreSQL/security tesztek `postgres:17-alpine` Testcontaineren futnak. Ha
Docker nem érhető el, `Skipped` eredményük pontosan ezt jelzi; a DB-specifikus
hardening ilyenkor kódszinten elkészült, de futtatva nem igazolt.

## Ellenőrzési eredmény – 2026-07-20

- restore: sikeres;
- Release build: sikeres, 0 warning, 0 hiba;
- tesztek: 36 összesen, 18 futva sikeres, 18 PostgreSQL/Testcontainers teszt
  skipped;
- Docker állapot: a `docker` parancs nincs telepítve/elérhető a futtatási
  környezetben, ezért a DB-specifikus tesztek nem tekinthetők futtatva
  igazoltnak;
- NuGet vulnerability audit: egyik projektben sincs ismert sérülékeny közvetlen
  vagy tranzitív csomag az aktuális források alapján;
- EF Core: nincs pending model change;
- az új `src/` és `tests/` fájlokra futtatott format check sikeres;
- a secret scan csak dokumentált placeholder és kizárólag teszt-fixture
  credential értékeket talált, private key/token mintát és trackelt `.env`
  fájlt nem.
