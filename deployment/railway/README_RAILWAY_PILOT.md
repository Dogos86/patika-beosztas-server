# Railway zárt pilot üzemeltetési kézikönyv

Ez a csomag egy 4–6 dolgozós, egyetlen szervezetet kiszolgáló zárt pilothoz
készült. A topológia egy Railway projekten belül:

```text
internet
   |
   v
web  (egyetlen publikus *.up.railway.app cím)
   |
   | /api, /health, /openapi a Railway private networkön
   v
api  (nincs publikus domain)
   |
   v
postgres + tartós adat-volume

api + külön /app/keys Data Protection volume
```

Nem része ennek a kiadásnak a Kubernetes, több API-példány, automatikus
failover, saját domain, MFA, self-service jelszó-visszaállítás, emailes meghívás,
AI vagy értesítési szolgáltató.

## 1. Előfeltételek

- a repository fel van pusholva a Git szolgáltatóhoz;
- van Railway projekt- és service-kezelési jogosultság;
- a pilot adminhoz külön, erős, jelszókezelőben tárolt jelszó készült;
- a `SensitiveData__TaxIdentifierHashKey` legalább 32 kriptográfiailag véletlen
  bájtból, Base64 formában készült;
- a pilot indulása előtt a jelen dokumentumban leírt restore-próba megtörtént.

Titkot ne tegyél `.env` fájlba, Docker image-be, build argumentumba, commitba
vagy Railway service-névbe.

## 2. Railway projekt létrehozása

Hozz létre egy üres Railway projektet, majd benne három service-t pontosan ezekkel
a nevekkel:

1. `postgres`: a Railway PostgreSQL sablonból;
2. `api`: ugyanabból a Git repositoryból;
3. `web`: ugyanabból a Git repositoryból.

Az `api` és a `web` service Repository Root Directory értéke `/`. A Config as
Code fájl:

- `api`: `/railway.json`;
- `web`: `/frontend/railway.json`.

Az automatikusan felismert vagy dashboardon megadott külön Build/Start Command
override-okat töröld; a verziózott Railway-konfiguráció és a Dockerfile legyen a
forrás. A két szolgáltatás figyelt útvonalai külön vannak választva, ezért egy
csak-backend módosítás nem építi újra szükségtelenül a web image-et és fordítva.

## 3. Hálózat és egyetlen publikus cím

Az `api` service Variables lapján állítsd a `PORT` értékét `8080`-ra, a `web`
service-nél `3000`-re. Ezután:

1. kizárólag a `web` service Networking lapján válaszd a **Generate Domain**
   műveletet;
2. az `api` és `postgres` service-hez ne készíts publikus domaint;
3. a pilot felhasználóknak csak a generált
   `https://<név>.up.railway.app` címet add meg.

A böngésző azonos originen marad. A web gateway a `/api/*`, `/health/*` és
`/openapi/*` kéréseket az
`http://${{api.RAILWAY_PRIVATE_DOMAIN}}:${{api.PORT}}` belső címre továbbítja.
Az átjáró a `Cookie` és a különálló `Set-Cookie` fejléceket változtatás nélkül
továbbítja. Az `/api/auth/csrf` válasz cache-elését `Cache-Control: no-store`
fejléccel tiltja akkor is, ha egy köztes réteg ettől eltérő upstream
cache-fejlécet adna.
Az `/openapi/v1.json` továbbítása szándékos: így a smoke teszt a publikus
útvonalon is bizonyítja, hogy Production módban `404` érkezik.

## 4. Környezeti változók

A `deployment/railway/.env.railway.example` csak név- és értékminta. A blokkokat
szolgáltatásonként, a Railway Variables lapon vedd fel.

### `web`

| Változó | Érték |
|---|---|
| `PORT` | `3000` |
| `NODE_ENV` | `production` |
| `VITE_APP_ENV` | `pilot` |
| `VITE_DATA_SOURCE` | `api` |
| `VITE_API_URL` | üres |
| `VITE_ENABLE_DEMO_LOGIN` | `false` |
| `VITE_ENABLE_AI` | `false` |
| `VITE_ENABLE_NOTIFICATIONS` | `false` |
| `API_INTERNAL_URL` | `http://${{api.RAILWAY_PRIVATE_DOMAIN}}:${{api.PORT}}` |

A `VITE_*` értékek buildidőben kerülnek a frontendbe. A production build
szándékosan hibával leáll, ha az adatforrás nem `api`, az API URL nem relatív,
vagy bármely demo/AI/értesítési kapcsoló engedélyezett.

### `api`

| Változó | Érték |
|---|---|
| `PORT` | `8080` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `${{postgres.DATABASE_URL}}` |
| `Seed__Enabled` | `false` |
| `OpenApi__Enabled` | `false` |
| `DataProtection__KeysPath` | `/app/keys` |
| `DataProtection__ApplicationName` | `PatikaBeosztas` |
| `SensitiveData__TaxIdentifierHashKey` | Railway secret, legalább 32 véletlen bájt Base64-kódolva |

Az API Production módban fail-fast módon ellenőrzi az adatbázis-kapcsolat
formátumát, a seed/OpenAPI tiltását, a hash kulcs erősségét és a tartós
kulcskönyvtár írhatóságát. A jelszó, az adóazonosító és a kulcs nem kerülhet
logba.

## 5. Tartós volume-ok és adatmegőrzés

### PostgreSQL

A Railway PostgreSQL sablon volume-ját hagyd a sablon által megadott
`/var/lib/postgresql/data` útvonalon. Az `api` mindig a
`${{postgres.DATABASE_URL}}` service reference-t használja; ne másold át kézzel
egy ideiglenes adatbázis címét.

### Data Protection

Az `api` service-hez adj egy külön Railway volume-ot, mount path:

```text
/app/keys
```

Ide kerül a cookie- és CSRF-védelem key ringje. A volume nem része az image-nek,
és restart/redeploy után is ugyanaz marad. Ne skálázd az API-t több replikára:
ez a pilot egyetlen API-példányra és egyetlen csatolt volume-ra készült.

Frontend- vagy API-redeploy nem törli a PostgreSQL és a kulcs-volume tartalmát.
Volume törlése, service újrakötése vagy másik environmentre mutató referencia
azonban adatvesztést okozhat; ezeket normál frissítés közben ne módosítsd.

## 6. Migráció

Az API deployment `preDeployCommand` lépése ezt futtatja:

```sh
sh /app/scripts/railway-migrate.sh
```

A script a már publikált API assembly `migrate` parancsát indítja. Az EF Core
csak a hiányzó, verziózott migrációkat alkalmazza ugyanarra a PostgreSQL-re,
majd ellenőrzi, hogy nem maradt függő migráció. A script nem töröl adatbázist,
nem futtat seedet és nem módosít korábbi migrációt. Hiba esetén nem lesz
sikeres az új deployment.

Kötelező kiadási sorrend:

```text
kézi PostgreSQL backup
→ api Redeploy és pre-deploy migráció
→ api readiness
→ web Redeploy
→ pilot smoke teszt
```

Adatbázis-migrációt tartalmazó kiadás előtt különösen fontos a backup: egy
korábbi Docker image visszaállítása nem vonja automatikusan vissza az adatbázis
sémáját.

## 7. Első szervezet és admin

Először legyen sikeres az API deployment és a `/health/ready`. Ezután:

1. az `api` Variables lapon ideiglenesen add hozzá a
   `BootstrapAdmin__Password` secretet;
2. alkalmazd a staged variable change-et, hogy a futó deployment megkapja;
3. a Railway dashboardon az `api` service helyi menüjéből másold ki az SSH
   parancsot, csatlakozz, majd a konténerben futtasd:

```sh
dotnet /app/PatikaBeosztas.Api.dll bootstrap-admin \
  --organization-name "Pilot Patika" \
  --email "admin@pelda.hu" \
  --display-name "Pilot Admin"
```

4. ellenőrizd a sikeres kimenetben az `OrganizationId` és `UserId` értéket;
5. futtasd le ugyanazt a parancsot még egyszer: módosítás nélkül azt kell
   jeleznie, hogy az admin már létezik;
6. azonnal töröld a `BootstrapAdmin__Password` változót, és alkalmazd a staged
   változást.

A parancs explicit emailt és szervezetnevet kér, az ASP.NET Identity erős
jelszó-szabályait használja, tranzakcióban adja hozzá az összes alkalmazás-
jogosultságot és immutable audit eseményt ír. A jelszó nem command-line argument,
nem kerül a kimenetbe és nem kerül adatbázisba olvasható formában.

Az első admin ezután a normál felületen hozza létre és kapcsolja a tényleges
dolgozókat/felhasználókat. Development demo-fiók és demo seed Production módban
nincs.

## 8. Backup

Mindkét volume-hoz (`postgres` adat-volume, `api` `/app/keys`) a service
**Backups** lapján:

1. kapcsold be a napi ütemezést;
2. javasolt kiegészítésként kapcsold be a heti és havi ütemezést is;
3. minden éles frissítés előtt indíts kézi backupot;
4. várd meg, amíg mindkét kézi backup sikeres állapotú.

A PostgreSQL backup az üzleti adatokat, a Published beosztásokat és a HR-adatokat
őrzi. A kulcs-volume backupja a meglévő munkamenetek és Data Protection által
védett payloadok olvashatóságához szükséges. A Railway napi volume-backup
megőrzése korlátozott, ezért a heti/havi ütemezés nem helyettesíthető pusztán
napi mentéssel.

## 9. Restore és kötelező restore-próba

Restore előtt állítsd le a felhasználói írásokat, és jegyezd fel a kiválasztott
backup időpontját.

1. A `postgres` Backups lapján válaszd ki a mentést, majd **Restore**.
2. Railway egy új volume-ot állít be ugyanarra a mount pathra, a régit
   lecsatolva megőrzi. Nézd át a staged changes részleteit, majd **Deploy**.
3. Az `api` `/app/keys` volume-ján lehetőleg ugyanahhoz a kiadáshoz tartozó
   mentést restore-olj, nézd át, majd deployold a staged változást.
4. Várd meg az API `ready` állapotát és futtasd a smoke tesztet.
5. Adminnal ellenőrizd legalább:
   - a visszaállítási időpont előtt közzétett Published beosztást;
   - egy jogosultsággal elérhető HR-profilt és az adóazonosító maszkolását;
   - egy normál dolgozó saját közzétett beosztását;
   - azt, hogy más tenant/illetéktelen felhasználó nem lát érzékeny adatot.
6. Csak az elfogadás után nyisd vissza a pilotot.

Figyelem: a volume restore ugyanabban a Railway projektben és environmentben
használható; a kiválasztott időpontnál újabb backupok eltűnhetnek a listából.

A pilot indulása előtt kötelező egy külön próbakörnyezetben vagy előre egyeztetett
karbantartási ablakban végrehajtani a teljes backup → restore → ellenőrzés
folyamatot. Az éles pilot kezdését csak dátummal és ellenőrző személy nevével
dokumentált sikeres restore-próba után hagyd jóvá.

## 10. Kézi frissítés, Redeploy és rollback

A `web` és `api` service Settings/Source részén kapcsold ki a branchhez tartozó
**GitHub Autodeploys** funkciót. A push így nem kerül automatikusan productionbe.

Kézi kiadás:

1. a felülvizsgált commitot pushold;
2. készíts kézi backupot mindkét volume-ról;
3. az `api` Deployments lapján válaszd az új commit deploymentjét, és indíts
   kézi **Redeploy**-t;
4. ellenőrizd a pre-deploy migrációt és a `/health/ready` állapotot;
5. redeployold a `web` service-t ugyanarra a commitra;
6. futtasd a smoke tesztet;
7. jegyezd fel a commit SHA-t, deploymentazonosítókat és az eredményt.

Alkalmazáskód-rollback:

1. a service Deployments lapján keresd meg az előző bizonyítottan jó
   deploymentet;
2. válaszd a **Rollback** műveletet;
3. ismételd meg külön az `api` és `web` service-en;
4. futtasd újra a smoke tesztet.

Ha az új kiadás migrációt alkalmazott, a Docker image rollbackja önmagában nem
adatbázis-rollback. Ilyenkor az előre elkészített backup restore-ját csak
karbantartási ablakban, az adatvesztési időablak elfogadása után végezd el.

## 11. Health és smoke teszt

Publikus ellenőrzések:

```text
GET /health/live   → az API folyamat él
GET /health/ready  → az API él és a PostgreSQL elérhető
```

A Railway API healthcheck a `/health/ready` útvonalat használja. A kiadás utáni
smoke teszt PowerShellből:

```powershell
$credential = Get-Credential -UserName "dolgozo@pelda.hu"
.\scripts\pilot-smoke-test.ps1 `
  -BaseUrl "https://<generalt-nev>.up.railway.app" `
  -Email "dolgozo@pelda.hu" `
  -Credential $credential
```

Olyan aktív, Employee rekordhoz kapcsolt pilot felhasználót használj, akinek már
van Published beosztása. A script csak a publikus web címet hívja, cookie-jarban
tartja a munkamenetet, CSRF-védett login/logout mutációt végez, és ellenőrzi:

- frontend `200`;
- liveness `200`;
- readiness/PostgreSQL `200`;
- login és hitelesített session;
- saját Published schedule endpoint `200`;
- production OpenAPI `404`;
- CSRF-védett kijelentkezés `204`.

A jelszó nincs a scriptben és nem kerül kiírásra. A smoke futását, időpontját,
felhasználóját és a deploy commit SHA-ját az üzemeltetési naplóban rögzítsd.

## 12. Pilot előtti ellenőrzőlista

- [ ] Csak a `web` service publikus.
- [ ] A generált `*.up.railway.app` HTTPS címen betölt a frontend.
- [ ] `VITE_DATA_SOURCE=api`, demo/AI/notifications kapcsolók `false`.
- [ ] `Seed__Enabled=false`, `OpenApi__Enabled=false`.
- [ ] A PostgreSQL és `/app/keys` volume csatolt és napi backupja aktív.
- [ ] Az első admin idempotens parancsa kétszer ellenőrizve.
- [ ] Nincs `BootstrapAdmin__Password` a service változói között.
- [ ] Teljes backend és frontend minőségi kapu sikeres.
- [ ] Mindkét Docker image lokálisan felépült.
- [ ] Kötelező restore-próba dokumentáltan sikeres.
- [ ] A pilot felhasználós smoke teszt sikeres.
- [ ] GitHub Autodeploys ki van kapcsolva, a kiadás kézi jóváhagyású.

## 13. Ismert korlátok

- Egyetlen régió, egyetlen API-példány, Railway volume-ok; nincs magas
  rendelkezésre állás.
- Frissítéskor rövid leállás lehetséges.
- Nincs automatikus adatbázis down-migráció.
- Nincs emailes meghívás, jelszó-visszaállítás vagy MFA; a fiókokat az admin
  kezeli, a jelszavakat külön biztonságos csatornán adja át.
- AI és értesítések ki vannak kapcsolva; mock képernyő nem jelenhet meg valós
  szolgáltatásként.
- Az OpenAPI Production módban nem publikus; a verziózott contract a
  repository `contracts/` könyvtárában marad.
