# Termékvízió és scope

## Cél

Többfelhasználós rendszer gyógyszertári dolgozók és vezetők számára, amely egyesíti:
- a beosztás megtekintését és készítését;
- a szabadságigénylést;
- a betegállomány és más távollétek rögzítését;
- az adminisztrátori jóváhagyást;
- a több telephelyes lefedettség ellenőrzését;
- a gépelt és diktált AI-alapú adatbevitelt.

A beosztáskészítés generálás-központú: az admin a dolgozói igényekből,
távollétekből, telephelyi szabályokból és időkeretekből előállított teljes
időszakot ellenőrzi, magyarázza, célzottan újragenerálja, majd jóváhagyja és
közzéteszi. A közös termékdöntés részlete:
`docs/13_GENERALAS_KOZPONTU_BEOSZTAS.md`.

## Felhasználók

### Dolgozó
- saját beosztás;
- saját szabadság-/fizetés nélküli szabadságigény;
- saját betegállomány-bejelentés;
- saját kérelmek és értesítések.

### Jóváhagyó / beosztáskészítő / admin
- más nevében rögzítés;
- kérelmek jóváhagyása és elutasítása;
- dolgozók, telephelyek, lefedettségi szabályok;
- automatikus beosztásgenerálás, eredmény- és problémaellenőrzés;
- korlátozott korrekciók, részleges újragenerálás, jóváhagyás és közzététel;
- saját dolgozói funkciók is.

## Első kiadás

- egy szervezet több telephellyel;
- reszponzív web/PWA;
- több jogosultsági szint;
- szabadság- és távolléti workflow;
- régi beosztási mag migrációja;
- auditnapló;
- REST API;
- AI előnézetes gépelt utasítás;
- diktálás adaptere későbbi fázisban.

## Nem cél az első kiadásban

- teljes magyar munkajogi szabálymotor;
- bérszámfejtő rendszer;
- diagnózis vagy részletes egészségügyi dokumentáció;
- automatikus, emberi jóváhagyás nélküli AI-módosítás;
- teljes drag-and-drop kézi beosztásszerkesztő és tömeges kézi áthelyezés;
- mobilon teljes időrácsos admin szerkesztés;
- natív mobilalkalmazás külön kódbázissal;
- több ország jogszabályi kezelése.
