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
