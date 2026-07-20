# Codex feladat – 1. fázis: szervezet, felhasználó és jogosultság

Előfeltétel: a 0. fázis buildel és az audit elkészült.

1. Készíts ADR-t és implementációs tervet a hitelesítéshez. Ne írj saját tokenformátumot.
2. Valósítsd meg a szervezeti határt, ApplicationUser–Employee elkülönítést és additív permission modellt.
3. Készíts `GET /api/auth/session` és minimális fejlesztői seed/profil megoldást kizárólag development környezetre.
4. Készíts authorization policy-ket az AGENTS és docs szerint.
5. Adj unit/integration tesztet saját vs. más dolgozó és más szervezet elérésére.
6. Készíts audit infrastruktúra alapot.
7. EF Core PostgreSQL migráció; integration teszthez izolált adatbázis.
8. Frissítsd az OpenAPI-t.
9. Futtasd build/testet és dokumentáld az eredményt.

Ne kezdj még beosztásgenerátor-migrációba vagy AI-integrációba.
