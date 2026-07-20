# Codex feladat – 0. fázis: audit és buildelő skeleton

Dolgozz a repositoryban az `AGENTS.md`, a `docs/` és a `contracts/` alapján.

Ebben a fázisban ne valósíts meg nagy üzleti funkciót.

## Feladat

1. Térképezd fel a repositoryt.
2. Ellenőrizd, hogy a `legacy/current-winforms/` alatt megtalálható-e a legfrissebb WinForms solution.
3. Buildeld a legacy solutiont változtatás nélkül, és rögzítsd a parancsot/eredményt.
4. Azonosítsd:
   - domain modellek;
   - validátorok;
   - coverage számítás;
   - autofill/generátor;
   - beosztásmásolás;
   - exportok;
   - JSON persistence;
   - UI-hoz kötött részek.
5. Készíts `docs/CODEBASE_AUDIT.md` fájlt konkrét fájl- és típusnevekkel, és osztályozd az elemeket: átvehető / adapterezhető / teszt után refaktor / eldobandó / újraírandó.
6. A legfontosabb legacy viselkedésekhez készíts karakterizációs teszttervet. Ha biztonságosan lehetséges, adj hozzá kis számú karakterizációs tesztet a legacy logika módosítása nélkül.
7. Hozd létre a .NET 10 solution skeletonját:
   - `src/PatikaBeosztas.Domain`
   - `src/PatikaBeosztas.Application`
   - `src/PatikaBeosztas.Contracts`
   - `src/PatikaBeosztas.Infrastructure`
   - `src/PatikaBeosztas.Api`
   - `tests/PatikaBeosztas.Domain.Tests`
   - `tests/PatikaBeosztas.Application.Tests`
   - `tests/PatikaBeosztas.Api.IntegrationTests`
8. Állíts be nullable reference types, warnings-as-errors ésszerű keretek között, analyzereket és közös build property-ket.
9. Készíts minimális health endpointot és OpenAPI-t, üzleti végpont nélkül.
10. Készíts ADR-javaslatot a hitelesítéshez, de ebben a fázisban ne építs saját auth rendszert.
11. Buildeld az új solutiont, futtasd a teszteket.
12. Ne migrálj adatot és ne módosítsd a legacy algoritmust.

## Válasz formátuma

- repository állapot;
- létrehozott/módosított fájlok;
- legacy build eredmény;
- új build eredmény;
- teszteredmény;
- audit legfontosabb megállapításai;
- nyitott döntések;
- következő legkisebb biztonságos feladat.

Ne állítsd, hogy valami sikerült, ha a parancskimenet ezt nem igazolja.
