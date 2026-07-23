# Phase 2C – API-átadás és helyi integráció

## Hatókör

Ez a fázis nem vezet be új üzleti modult, Schedule aggregate-et vagy
beosztásgenerátort, és nem módosítja a `legacy/current_winforms/` forrást. A
Phase 2B runtime frontendnek történő átadását, reprodukálható OpenAPI-exportját
és helyi indítását teszi ellenőrizhetővé.

## Kanonikus runtime contract

- A tényleges `GET /openapi/v1.json` válasz commitolt példánya:
  `contracts/openapi.phase2b.json`.
- Az exportot az `eng/export-openapi.ps1` készíti, és validálja a
  `Patika Beosztás API` címet, a `0.3.0-phase2b` verziót, valamint az auth,
  employee, user, location, work preference, leave és minden Phase 2B modul
  reprezentatív útvonalát.
- A publikus enumok string enumként jelennek meg. Integer enumérték továbbra
  sem elfogadott.
- A kézzel karbantartott `contracts/api-contract-draft.yaml` egyeztetési vázlat;
  frontend típusgeneráláshoz a runtime export a forrás.

## Helyi indítás és CORS

Az `eng/start-local-api.ps1` a repository `.env` fájljából tölti be a helyi
PostgreSQL-, connection string-, seed- és CORS-konfigurációt. Ellenőrzi a .NET
10 SDK-t és a Docker motort, elindítja a `postgres` compose szolgáltatást, majd
Development környezetben a `https://localhost:7180` címen indítja az API-t.
Titkos konfigurációs értéket nem ír ki. A PostgreSQL konténer 5432-es portja
alapértelmezetten csak a host `127.0.0.1:55432` címén érhető el; a
`POSTGRES_HOST_PORT` és a connection string `Port` értéke együtt módosítandó.

A Development alap-origin `https://localhost:5173`, amely a
`Cors__AllowedOrigins__0` környezeti változóval felülírható. A policy pontos
origineket és credentialt használ; wildcard nincs. Productionben a Development
allowlist nem aktiválódik.

## Employee és ApplicationUser életciklus

Az erőforrások külön maradnak. A PostgreSQL/Testcontainers HTTP-integrációs
forgatókönyv bizonyítja, hogy az admin:

1. fiók nélkül létrehoz, listáz és módosít egy dolgozót;
2. később `employeeId` értékkel létrehoz hozzá egy ApplicationUser fiókot;
3. mindkét erőforrás projekciójában látja a kapcsolatot;
4. ugyanazt a dolgozót nem kapcsolhatja második userhez;
5. más szervezet dolgozóját nem kapcsolhatja;
6. a sikeres Employee- és User-mutációkhoz auditbejegyzést kap.

Az adatbázis egyedi indexe és kompozit tenant-idegen kulcsa az API-validáció
mellett második védelmi réteg.

## Development seed

A fix demo telephelyek és dolgozók nem szervezetszintű `Any` ellenőrzéssel,
hanem stabil rekordazonosítónként kerülnek biztosításra. A seeder így kijavítja
a részleges régi demo adatállapotot, de a már létező rekordot és a felhasználó
saját dolgozóit nem írja felül. A működés idempotenciáját és kizárólagos
Development aktiválását PostgreSQL-integrációs teszt ellenőrzi.

## Ismert korlátok

- Meghívó, email, jelszó-visszaállítás és MFA továbbra sincs.
- A runtime export a Phase 2B modulokat tartalmazza; Schedule és generátor csak
  a lezárt Phase 3 termékdöntések után kerülhet bele.
- A helyi script fejlesztői tanúsítványt nem bízik meg automatikusan; szükség
  esetén jelzi a `dotnet dev-certs https --trust` parancsot.
