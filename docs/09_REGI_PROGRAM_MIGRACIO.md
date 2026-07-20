# Régi WinForms program migrációja

## Alapelv

A régi program nem kidobandó, de a UI és a JSON persistence nem kerül át változtatás nélkül. A működő domainlogikát tesztekkel kell körbevenni.

## Audit során keresendő

- domain modellek;
- műszakátfedés ellenőrzés;
- napi/havi limit validátor;
- coverage számítás;
- autofill algoritmus;
- beosztás másolás;
- export logika;
- JSON adattárolás és adatverziók;
- implicit UI-függőségek és statikus állapot.

## Osztályozás

Minden elem kerüljön egyik kategóriába:
1. változtatás nélkül emelhető át;
2. adapterrel újrafelhasználható;
3. karakterizációs teszt után refaktorálandó;
4. UI-specifikus, eldobandó;
5. hibás vagy elavult, újraírandó.

## Migrációs sorrend

1. Legacy solution build és teszt.
2. Reprezentatív demo adat rögzítése.
3. Karakterizációs tesztek.
4. Domainfüggetlen logika kinyerése.
5. Új domain modellekhez adapter.
6. JSON → PostgreSQL import eszköz dry-run móddal.
7. Összehasonlító teszt: régi és új validáció/generálás.
8. Csak ezután UI-integráció.

A `legacy/current-winforms` forrását az audit első fázisában ne módosítsd.
