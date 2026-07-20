# Gyógyszertári Beosztás Készítő – WinForms prototípus

Ez a csomag egy **Visual Studio-ba betölthető WinForms prototípus**, amely a gyógyszertári beosztáskészítés fő elemeit mutatja be:

- telephelyek kezelése
- dolgozók kezelése
- távollétek kezelése
- coverage / erőforrás szabályok
- heti/kétheti/havi beosztás létrehozása
- 30 perces rácsra igazított bejegyzések
- overlap és limit ellenőrzés
- auto-fill / hiánypótlás
- jóváhagyás hard hibák esetén blokkolva
- Excel export (`.xlsx`)
- PDF export (`.pdf`)
- külön tesztprojekt a core logikára

## Projektstruktúra

- `PharmacyScheduler.Core` – domain modellek + validáció + auto-fill + export lekérdezések
- `PharmacyScheduler.WinForms` – a WinForms felület
- `PharmacyScheduler.Tests` – MSTest alapú unit tesztek

## Futtatás Visual Studio-ban

1. Nyisd meg a `PharmacyScheduler.sln` fájlt **Visual Studio 2022** alatt.
2. Várd meg a NuGet restore végét.
3. Állítsd startup projectnek a `PharmacyScheduler.WinForms` projektet.
4. Nyomj `F5`.

A program az első induláskor létrehoz egy `scheduler-data.json` fájlt az exe mappájában mintaadatokkal.

## Tesztek futtatása

A `PharmacyScheduler.Tests` projekt a következőket ellenőrzi:
- overlap detektálás
- coverage hiány jelzése
- napi limit figyelmeztetés
- auto-fill alap működése

Visual Studio-ban:
- `Test > Test Explorer`
- majd `Run All`

## Fontos megjegyzések

Ez **prototípus / MVP-alap**. A felület WinForms-os és nem drag&drop naptár, hanem listás szerkesztésű. A fő cél az volt, hogy:
- stabilan megnyitható legyen Visual Studio-ban,
- a domain logika külön projektben legyen,
- és a kollégák később könnyen át tudják írni modernebb UI-ra vagy szolgáltatásarchitektúrára.

## Jelenlegi egyszerűsítések

- egyedi jogosultsági rendszer még nincs
- adatbázis helyett JSON fájl tárol
- a coverage kivételek (ünnepnap, havi ismétlődések) még nincsenek külön UI-val modellezve
- az auto-fill heurisztikus, nem solver-alapú
- a beosztás nézet táblázatos, nem klasszikus naptárrács

## Következő logikus fejlesztések

- ünnepnap / kivétel kezelés
- szabályok finomabb soft/hard konfigurálása UI-ból
- nap/heti rács nézet
- SQLite perzisztencia
- könyvelői export formátumok
- import/export sablonok
