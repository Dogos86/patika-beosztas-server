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

- időszak és műszak CRUD;
- validáció;
- automatikus kitöltés;
- jóváhagyás;
- export később.

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
- AI execute egyszer hajtható végre;
- döntésnél expected version kötelező.

A részletes vázlat: `contracts/api-contract-draft.yaml`.
