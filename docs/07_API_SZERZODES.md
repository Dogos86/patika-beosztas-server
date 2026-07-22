# API szerződés elvei

## Saját műveletek

A `me` végpontok nem fogadnak el tetszőleges dolgozóazonosítót. A szerver a hitelesített felhasználóból oldja fel az Employee rekordot.

Példák:
- `GET /api/me/schedule`
- `GET /api/me/leave-requests`
- `POST /api/me/leave-requests`
- `POST /api/me/leave-requests/{id}/withdraw`

## Admin műveletek

- `GET /api/admin/leave-requests`
- `POST /api/admin/leave-requests`
- `POST /api/admin/leave-requests/{id}/decision`
- dolgozók, telephelyek, lefedettség CRUD megfelelő permissionnel.

Az 1. fázis megvalósított admin útvonalai:

- `/api/admin/employees` és `/api/admin/employees/{id}`;
- `/api/admin/locations` és `/api/admin/locations/{id}`;
- `/api/admin/users`;
- `/api/admin/users/{id}`;
- `/api/admin/users/{id}/permissions`;
- `/api/admin/users/{id}/employee-link`;
- `/api/admin/users/{id}/status`.

Az admin requestek nem tartalmaznak `organizationId` vagy aktorazonosítót.
A user lista és részletező válasz `version` mezőt tartalmaz; a permission-,
employee-link- és státusz-PUT `expectedVersion` értéket kér.

Az auth session a user/szervezet azonosítók mellett a szervezet nevét és
időzóna-azonosítóját, a pontos permission listát és az opcionális kapcsolt
dolgozót adja. Összefoglaló `admin` role nincs.

## Beosztás

A beosztási API generálás-központú; nem egy teljes kézi műszak-CRUD felületre
optimalizál. A Phase 3 OpenAPI-nak a következő alkalmazási műveleteket kell
elkülönítenie:

- teljes időszak generálásának indítása hétre, két hétre vagy hónapra;
- generálási futás állapotának és eredményének lekérése;
- a dolgozó × nap, telephely × nap és csak problémák projekció lekérése;
- generálási összefoglaló és az utolsó közzétett verzióhoz képesti diff;
- műszak strukturált generálási magyarázata;
- műszak rögzítése/feloldása és generált javaslat elutasítása;
- alternatív dolgozók lekérdezése;
- részleges újragenerálás nap, hét, telephely, szerepkör vagy kijelölt
  probléma scope-pal;
- átmenet `UnderReview`, `Approved`, `Published` és `Archived` állapotba.

Az alkalmazási műveletek pontos útvonalai a Phase 3 implementációval együtt
kerülnek a futó OpenAPI-ba. A `contracts/api-contract-draft.yaml` jelenleg az
implementált Phase 1.5 felület szerződése; jövőbeli útvonalat nem szabad
implementáltként feltüntetnie.

### Admin munkatér read model

Az admin válaszok ugyanazon beosztás- és validációverzióból származnak. A
frontend számára legalább a következő stabil fogalmak szükségesek:

- `ScheduleStatus`: `Generating`, `Draft`, `UnderReview`, `Approved`,
  `Published`, `Archived`;
- időszak: `Week`, `TwoWeeks`, `Month`;
- lefedettségi cella: `Ok`, `Warning`, `Blocking`, `Closed`, `Inactive`;
- probléma: stabil kód, súlyosság, dátum és az alkalmazható dolgozó-,
  telephely-, műszak- és beosztásreferenciák;
- közzétett verzióhoz képesti változás: `New`, `Modified`, `Deleted`,
  `Unchanged`;
- rögzített műszak állapota;
- generálási összefoglaló a közös termékdöntésben felsorolt mutatókkal.

A dolgozói mátrix szerveroldali projekciója sorösszesítéseket ad; a
lefedettségi projekció idősáv- és szerepkörszintű szükséges/tényleges
létszámot; a problémalista pedig közvetlen navigációhoz szükséges
hivatkozásokat.

### Generálási és korrekciós mutációk

- Teljes és részleges generálás `RunAutoFill`, beosztás-életciklus művelet
  `ManageSchedules` permissiont igényel.
- A szerver nem fogad el `organizationId` vagy aktorazonosítót a requestből.
- Fontos POST művelethez `Idempotency-Key`, módosított beosztáshoz vagy
  műszakhoz `expectedVersion` szükséges.
- Az újragenerálási scope csak ugyanazon szervezethez és beosztáshoz tartozó
  azonosítókat tartalmazhat; a neveket az AI-csatorna az alkalmazásban oldja
  fel.
- Részleges újrageneráláskor a rögzített műszakok megőrzését a szerver
  garantálja, nem a kliens által visszaküldött műszaklistára bízza.
- A generátor eredménye `Draft`; a kliens nem kérhet közvetlenül generálásból
  `Approved` vagy `Published` állapotot.
- Blokkoló problémával jóváhagyás vagy közzététel nem sikerülhet.

### Dolgozói láthatóság

`GET /api/me/schedule` a kapcsolt dolgozót a hitelesített sessionből oldja fel,
nem fogad el `employeeId`-t, és csak `Published` beosztást ad vissza. A Draft,
UnderReview és Approved tartalom, a generálási magyarázatok és más dolgozók
részletei ezen az útvonalon nem szivároghatnak ki.

## Hibamodellezés

- RFC 7807 Problem Details;
- üzleti hibáknál stabil hibakód;
- magyar felhasználói üzenet opcionálisan, de a frontend kód alapján is tudjon lokalizálni;
- 409 konkurencia/idempotencia;
- 422 üzleti validáció;
- 403 jogosultság;
- más szervezethez tartozó objektumnál ne szivárogjon adat.

## Idempotencia és konkurencia

- fontos POST mutációknál `Idempotency-Key`;
- editable entitásoknál verzió/ETag;
- generálási indítás ugyanazzal az idempotency key-jel nem hozhat létre újabb
  futást;
- részleges újragenerálás stale schedule-verzióval 409-et ad;
- AI execute egyszer hajtható végre;
- döntésnél expected version kötelező.

A részletes vázlat: `contracts/api-contract-draft.yaml`.
