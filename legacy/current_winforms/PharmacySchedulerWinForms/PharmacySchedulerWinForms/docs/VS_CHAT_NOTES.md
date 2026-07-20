# Beosztás Készítő – Fejlesztői jegyzet (VS Chat-hez)

## Hova tedd, hogy a Visual Studio / GitHub Copilot Chat könnyen „felzabálja”?
Ajánlott repo-struktúra:

- **README.md** – 10–20 soros „mi ez a projekt” + quickstart
- **/docs/PRD.md** – üzleti/követelmény specifikáció (miért, kinek, mit tudjon)
- **/docs/MVP_SPEC.md** – MVP scope, képernyők, entitások, ellenőrzések
- **/docs/DOMAIN_MODEL.md** – opcionális: részletesebb entitások/mezők, később API-k
- **/docs/EXPORT_FORMATS.md** – opcionális: Excel/PDF formátumok

A VS chat általában jól dolgozik:
- **Markdown (.md)**
- sima **.txt**
- a kódbázisban lévő kommentekkel és README-vel

Word (.docx) is mehet, de a legjobb a **.md** a repóban, mert:
- verziózható
- diffelhető
- a fejlesztők is könnyen olvassák

## Ajánlott minimál csomag most
- docs/PRD.md
- docs/MVP_SPEC.md

Ezt a két fájlt tudod betallózni / megnyitni a VS-ben, és a chatnek azt mondani:
„Use the docs/PRD.md and docs/MVP_SPEC.md as the source of truth.”
