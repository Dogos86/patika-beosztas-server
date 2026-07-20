# Célarchitektúra

## Repository

Külön frontend és backend repó:
- `patika-beosztas-web`
- `patika-beosztas-server`

## Backend solution

```text
src/
  PatikaBeosztas.Domain/
  PatikaBeosztas.Application/
  PatikaBeosztas.Contracts/
  PatikaBeosztas.Infrastructure/
  PatikaBeosztas.Api/
tests/
  PatikaBeosztas.Domain.Tests/
  PatikaBeosztas.Application.Tests/
  PatikaBeosztas.Api.IntegrationTests/
tools/
  PatikaBeosztas.LegacyImporter/
legacy/
  current-winforms/
```

## Rétegek

### Domain
Entitások, value objectek, domain invariánsok, domain események. Nincs EF Core, HTTP vagy AI SDK függőség.

### Application
Use case-ek, parancsok/lekérdezések, authorization policy-k, tranzakcióhatárok, interfészek.

### Contracts
Publikus request/response DTO-k és verziózott AI-sémák. Ne szivárogjanak ki EF entitások.

### Infrastructure
EF Core, PostgreSQL, identity/auth adapter, audit tárolás, értesítés, AI/STT adapterek.

### API
HTTP végpontok, auth, OpenAPI, validation problem mapping, idempotency middleware, SignalR később.

## Integráció

A backend OpenAPI dokumentuma generálja a frontend TypeScript klienst. A `contracts/api-contract-draft.yaml` kezdeti egyeztetési vázlat, nem végleges forrás.

## Telepítés

Első cél: konténerizálható API + PostgreSQL, ugyanazon szervezeti domain mögötti PWA. A helyi AI külön szolgáltatásként fut, és az API csak adapteren keresztül éri el.
