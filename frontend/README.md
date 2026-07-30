# Patika Beosztás – frontend

A gyógyszertári beosztás-, szabadságigénylő- és távollétkezelő rendszer
funkcionális React frontendje. A zárt Railway pilotban az ASP.NET Core API-val,
azonos publikus originen működik; a `/api/*` kéréseket a `gateway.mjs` a Railway
privát hálózatán továbbítja.

## Helyi fejlesztés

```bash
npm ci
npm run dev
```

A részletes frontend-architektúra:
[`docs/frontend-architecture.md`](docs/frontend-architecture.md). Az API
integráció leírása: [`docs/api-integration.md`](docs/api-integration.md).

## Adatforrás

`VITE_DATA_SOURCE=api` használja a valós backendet cookie + CSRF védelemmel;
relatív `VITE_API_URL` esetén azonos originen. A közös kiadás service locatora
API-only, ezért a repositoryban referenciaként megőrzött mock implementáció és
demo rekordok nem kerülnek a production bundle-be.

API módban nincs csendes mock fallback. A pilot build fail-fast módon csak az
alábbi biztonságos beállítással készülhet el:

```dotenv
VITE_APP_ENV=pilot
VITE_DATA_SOURCE=api
VITE_API_URL=
VITE_ENABLE_DEMO_LOGIN=false
VITE_ENABLE_AI=false
VITE_ENABLE_NOTIFICATIONS=false
```

A pilot bejelentkezési oldala nem tartalmaz demo fiókot vagy előre kitöltött
hitelesítő adatot. Az AI és az értesítési modul a pilotban ki van kapcsolva.

## Minőségi kapuk

```bash
npm run format:check
npm run typecheck
npm run lint -- --quiet
npm test
npm run build
npm audit --audit-level=high
```

A Railway image a repository gyökeréből, a
[`Dockerfile`](Dockerfile) alapján épül. A teljes `web` + `api` + `postgres`
telepítési és üzemeltetési folyamatot a
[`deployment/railway/README_RAILWAY_PILOT.md`](../deployment/railway/README_RAILWAY_PILOT.md)
írja le.

## Alapelvek

- Az alkalmazás-jogosultság és a gyógyszertári szakmai szerepkör külön fogalom.
- Egy admin beosztható dolgozó és gyógyszerész is lehet.
- A kliensoldali route guard csak felhasználói élmény; az engedélyezést minden
  érzékeny lekérdezésnél és módosításnál a backend kényszeríti ki.
- Az AI nem írhat közvetlenül adatbázisba.
- Titkos kulcs, jelszó vagy production konfiguráció nem kerülhet a frontend
  bundle-be.

## Technológia

- React 19, strict TypeScript és Vite
- TanStack Start, Router és Query
- Tailwind CSS v4 és shadcn/ui
- React Hook Form és Zod
- Vitest
- PWA-előkészített, mobil-first felület
