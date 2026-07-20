# Codex feladat – 3. fázis: beosztásmotor migrációja

Előfeltétel: `docs/CODEBASE_AUDIT.md` és legacy karakterizációs tesztek.

1. A legkisebb, domainfüggetlen validációs részt emeld át először.
2. Tartsd meg vagy dokumentáld a régi viselkedést; eltéréshez külön döntés és teszt kell.
3. Implementáld a Schedule, Shift, CoverageRule és employee scheduling settings tárolást.
4. 30 perces műszakrács; percre pontos preferenciák.
5. Több telephelyes átfedés blokkoló.
6. Inaktív telephely kizárása az aktív coverage/autofill számításból.
7. Gyógyszertárvezető külön `Schedulable`, `IncludeInAutoFill`, `CountsAsPharmacist` beállításai.
8. Figyelmeztető/blokkoló hibák és jóváhagyási logika.
9. Csak utána migráld az autofillt összehasonlító tesztekkel.
10. Készíts dry-run legacy JSON importer tervet vagy eszközt, de ne írj felül adatot automatikusan.
11. Frissítsd OpenAPI-t, teszteket és dokumentációt.
