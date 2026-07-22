# Codex feladat – 3. fázis: generálás-központú beosztásmotor

Előfeltétel:

- `docs/CODEBASE_AUDIT.md` elkészült;
- a `docs/LEGACY_CHARACTERIZATION_TEST_PLAN.md` releváns P0 esetei
  automatizáltak;
- a megvalósítandó szeletet érintő `LEG-*` és `GEN-*` döntések lezártak;
- a normál szabadság- és távolléti API biztosítja a generátorhoz szükséges
  organization-scoped query portot.

Közös termékforrás:
`docs/13_GENERALAS_KOZPONTU_BEOSZTAS.md`. Backend-invariánsok:
`docs/04_BEOSZTAS_ES_LEFEDETTSEG.md`.

## Végrehajtási elv

Kis, reviewzható vertikális szeletekben dolgozz. Ne kombináld a teljes legacy
migrációt, a generátort és az összes workspace read modelt egyetlen széles
refaktorba. Minden szelethez domain/application/integration teszt,
authorization- és organization-boundary teszt, OpenAPI-frissítés és audit
ellenőrzés tartozik.

## Szeletek

1. Egészítsd ki a releváns P0 legacy karakterizációs teszteket. A legacy
   forrás maradjon változatlan.
2. Implementáld a Schedule, Shift, CoverageRule és szükséges scheduling
   settings tárolást organization-scoped kompozit kapcsolatokkal,
   optimista konkurenciával és EF Core migrációval.
3. Implementáld a `Generating`, `Draft`, `UnderReview`, `Approved`,
   `Published`, `Archived` állapotgépet. A generátor eredménye Draft; dolgozó
   csak Published beosztást láthat.
4. Emeld át vagy írd újra a legkisebb domainfüggetlen validációs és coverage
   részeket összehasonlító tesztekkel: 30 perces műszakrács, percre pontos
   preferencia, több telephelyes átfedés, távollét, időkeret, inaktív
   telephely és `CountsAsPharmacist`.
5. Készíts közös, stabil kódú Warning/Blocking probléma-read modelt, amelyből
   az érintett nap, dolgozó, telephely és műszak feloldható.
6. Implementáld a teljes hét/két hét/hónap időszakot együtt optimalizáló,
   reprodukálható generálást. Ne emeld át változtatás nélkül a legacy
   heurisztika dokumentált hibáit.
7. A generálás legyen idempotens, auditált és megszakításbiztos. A futás
   státusza lekérdezhető; csak a sikeres, teljesen validált eredmény válik
   Drafttá.
8. Add vissza a generálási összefoglalót és műszakonként a tényleges döntési
   adatokból származó strukturált magyarázatot, valamint az alkalmazható
   alternatív jelölteket.
9. Implementáld a korlátozott korrekciókat: rögzítés/feloldás, generált
   javaslat elutasítása, alternatívakeresés és részleges újragenerálás nap,
   hét, telephely, szerepkör vagy kijelölt problémák szerint. A rögzített
   műszakok mindig megmaradnak.
10. Készíts konzisztens read modeleket a dolgozó × nap, telephely × nap és csak
    problémák nézethez, dolgozói sorösszesítőkkel és az utolsó Published
    változathoz képesti diffel.
11. Implementáld a review, jóváhagyás, közzététel és archiválás use case-eket.
    Blokkoló probléma mellett a konfigurált tiltott átmenet nem hajtható végre.
12. Készíts dry-run legacy JSON importer tervet vagy eszközt, de ne írj felül
    adatot automatikusan.
13. Frissítsd a futó OpenAPI-t, a `contracts/` vázlatokat és a dokumentációt.
    Futtasd a buildet, unit teszteket és PostgreSQL-integrációs teszteket.

## Nem része ennek a fázisnak

- teljes drag-and-drop műszakrajzolás;
- tömeges kézi áthelyezés;
- mobilon teljes időrácsos admin szerkesztés;
- AI- vagy beszédfelismerő provider integráció;
- emberi jóváhagyás nélküli automatikus közzététel.
