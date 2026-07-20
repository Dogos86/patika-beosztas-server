# Beosztás és lefedettség

## Alap

- heti, kétheti és havi időszak;
- 30 perces szerkesztési rács;
- több telephely;
- egy dolgozó több telephelyen dolgozhat, de ismert telephelyek között időátfedés nem lehet;
- piszkozat és jóváhagyott állapot;
- blokkoló szabály csak jóváhagyáskor akadályoz, szerkesztés közben előnézhető.

## Dolgozói beállítások

- havi időkeret;
- maximum napi óraszám;
- preferált és tiltott idősáv percre pontosan;
- beosztható;
- automatikus kitöltésbe bevonható;
- gyógyszerész-lefedettségbe beleszámít;
- engedélyezett időtípusok;
- telephely-hozzárendelések.

## Gyógyszertárvezető

A szakmai szerepkör önmagában nem tiltja a beosztást. Külön zászlók döntik el:
- `Schedulable`;
- `IncludeInAutoFill`;
- `CountsAsPharmacist`.

## Lefedettségi szabály

Telephely + nap/dátumminta + idősáv + szakmai szerepkör + szükséges létszám + súlyosság.

Súlyosság:
- Warning – figyelmeztető;
- Blocking – jóváhagyást blokkoló.

Inaktív telephely:
- történeti adatai és szabályai megmaradhatnak;
- aktív hiányszámítás és automatikus kitöltés figyelmen kívül hagyja.

## Automatikus kitöltés

A régi heurisztika csak karakterizációs tesztek után emelhető át. Minimum feltételek:
- aktív telephely;
- dolgozó aktív, beosztható és bevonható;
- megfelelő szakmai szerep/kompetencia;
- nincs időütközés;
- távollét figyelembe vétele;
- egyéni limitek;
- preferenciák pontozása;
- determinisztikus tesztadat mellett reprodukálható eredmény.
