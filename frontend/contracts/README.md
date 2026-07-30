# `contracts/`

Ez a mappa gyűjti a frontend ↔ backend közti szerződéseket:

- **API-szerződés** — a leendő ASP.NET Core .NET 10 Web API OpenAPI leírója
  (`openapi.yaml` / `openapi.json`), amint elkészül. A frontend HTTP kliens réteg
  ebből generálódik.
- **AI parancs-szerződés** — az `AiCommandPreview`, `AiResolvedAction`,
  `AiClarification` típusok, valamint az `interpretCommand → answerClarification →
executeCommand` folyamat leírása.

A domain enum-értékek leképezése (lowercase snake ↔ PascalCase) a
`docs/api-integration.md` fájlban található.

> Amíg nincs végleges OpenAPI, a `src/services/interfaces.ts` a hatályos szerződés.
