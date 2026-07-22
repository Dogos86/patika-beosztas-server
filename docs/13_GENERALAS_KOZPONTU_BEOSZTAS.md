# Generálás-központú beosztás és eredmény-ellenőrző munkatér

## Dokumentum szerepe

Ez a dokumentum a frontend és a backend közös termékdöntése a beosztási
folyamatról. A részletes backend-invariánsokat a
`docs/04_BEOSZTAS_ES_LEFEDETTSEG.md`, a tervezett API-felületet a
`docs/07_API_SZERZODES.md` tartalmazza. Eltérés esetén ezt a dokumentumot és az
`AGENTS.md` biztonsági invariánsait együtt kell alkalmazni; a nem rendezett
részleteket a `docs/OPEN_DECISIONS.md` fájlban kell nyitva tartani.

## Alapelv

A rendszer elsődlegesen automatikusan generálja a beosztást. Az admin nem egy
üres naptárt tölt fel kézzel, hanem:

1. összegyűjti a dolgozói kéréseket, preferenciákat,
   elérhetetlenségeket és távolléteket;
2. kiválaszt egy hetet, két hetet vagy hónapot;
3. elindítja az automatikus generálást;
4. áttekinti és megérti az eredményt;
5. kezeli a problémákat és az eltéréseket;
6. szükség esetén részlegesen újragenerál;
7. jóváhagyja és közzéteszi a beosztást.

A teljes drag-and-drop kézi beosztásszerkesztő nem része ennek a fázisnak.

## A generátor működési szemlélete

A generátor belül dolgozhat napokra és 30 perces blokkokra bontva, de a teljes
kiválasztott időszakot egyben kell optimalizálnia.

Figyelembe veendő tényezők:

- aktív telephelyek nyitvatartása;
- telephelyenkénti és szerepkörönkénti lefedettségi igény;
- dolgozói szakmai szerepkör és `CountsAsPharmacist` jelölés;
- telephelyhez rendelhetőség;
- jóváhagyott távollétek;
- függő kérelmek konfigurált kezelési módja;
- konkrét és ismétlődő dolgozói preferenciák;
- határozott elérhetetlenségek;
- napi és havi időkeretek;
- több telephelyes időütközés;
- lehetőség szerint igazságos hétvégi, délutáni és telephelyi terhelés.

## Admin munkatér

Az admin alapnézete egy generált beosztást ellenőrző munkatér. A három
kapcsolható projekció ugyanannak a beosztásnak és ugyanannak a validációs
eredménynek a nézete:

1. **Dolgozói beosztás** – dolgozó × nap mátrix; ez az alapnézet.
2. **Telephelyi lefedettség** – telephely × nap állapot- és problématérkép.
3. **Csak problémák** – blokkoló hibák, figyelmeztetések, ütközések, függő
   kérelmek és nem teljesült kívánságok.

Választható időszak: hét, két hét vagy hónap.

### Dolgozó × nap mátrix

A sorok dolgozók, az oszlopok naptári napok. Egy cella megjelenítheti:

- a telephely rövid nevét;
- a kezdési és befejezési időt;
- az időtípust;
- több műszak esetén a műszakok számát vagy tömör listáját;
- szabadságot, betegállományt vagy más távollétet;
- figyelmeztetést vagy blokkoló hibát;
- eltérést a dolgozó kérésétől;
- rögzített állapotot;
- változást az utolsó közzétett beosztáshoz képest.

A bal oldali dolgozónév-oszlop és a felső napfejléc rögzített. A kétheti és
havi nézet vízszintesen görgethető.

A dolgozói sor összesítése legalább:

- beosztott idő / havi cél;
- teljesült kérések / összes kérés;
- hétvégi műszakok;
- délutáni/esti műszakok;
- telephelyváltások;
- figyelmeztetések száma.

### Telephelyi lefedettség

A sorok telephelyek, az oszlopok napok. A napi cella állapota:

- megfelelő;
- figyelmeztetés;
- blokkoló hiba;
- zárva;
- inaktív.

A részletes nézet idősávonként és szakmai szerepkörönként mutatja a szükséges
és tényleges létszámot, a hiányt vagy többletet és az érintett dolgozókat.

### Problémák

A közös problématípusok:

- nincs meg a szükséges gyógyszerész vagy szakasszisztens;
- a dolgozó két telephelyen van ugyanabban az időben;
- jóváhagyott távollét és műszak ütközik;
- napi vagy havi keret túllépése;
- határozott elérhetetlenség megsértése;
- dolgozói preferencia nem teljesült;
- függő kérelem érinti a generált műszakot;
- inaktív telephely hibás figyelembevétele;
- egyéb validációs figyelmeztetés.

A problémából az érintett nap, dolgozó, telephely és műszak egyértelműen
feloldható legyen, hogy a frontend a megfelelő részletpanelt nyithassa meg.

## Generálási összefoglaló

A generálás eredménye legalább a következő mutatókat adja vissza:

- telephelyi lefedettség százaléka;
- blokkoló hibák száma;
- figyelmeztetések száma;
- dolgozói kérések teljesülési aránya;
- havi kerettől eltérő dolgozók száma;
- függő kérelmek által érintett műszakok száma;
- több telephelyes ütközések száma;
- új, módosított és törölt műszakok száma.

## Magyarázhatóság

Minden generált műszakhoz elérhető a „Miért ezt választotta?” részlet. A
magyarázat legalább az alkalmazható indokokat és az érdemi alternatívákat
mutatja:

- megfelelő szakmai szerepkör;
- az adott telephelyhez rendelhető;
- nincs távollét;
- nincs időütközés;
- belefér az időkeretbe;
- lefedettségi hiányt old meg;
- teljesíti vagy megsérti a dolgozói preferenciát;
- alternatív jelöltek és azok hátrányai.

A magyarázat a generátor döntésének strukturált, visszaadható eredménye; nem
lehet utólag kitalált, nem reprodukálható szöveg.

## Korlátozott korrekciók

Ebben a fázisban támogatott:

- műszak rögzítése és feloldása;
- generált javaslat elutasítása;
- alternatív dolgozók keresése;
- részleges újragenerálás napra, hétre, telephelyre, szerepkörre vagy kijelölt
  problémákra;
- rögzített műszakok megtartása újrageneráláskor;
- összehasonlítás az utolsó közzétett változattal.

Ebben a fázisban nem támogatott:

- teljes drag-and-drop műszakrajzolás;
- tömeges kézi áthelyezés;
- mobilon teljes időrácsos admin szerkesztés.

## Állapotok és láthatóság

A beosztás állapotai:

- `Generating`;
- `Draft`;
- `UnderReview`;
- `Approved`;
- `Published`;
- `Archived`.

A generátor sikeres eredménye mindig `Draft`. A dolgozók csak `Published`
állapotú beosztást láthatnak. Az állapotátmeneteket, a közzétételt és az
archiválást a szerver jogosultsággal, optimista konkurenciával és audittal
kezeli.

## AI és diktálás

Az AI-asszisztens későbbi végrehajtási csatorna. Ugyanazokat az alkalmazási
műveleteket hívja, mint a normál felület; nem kap közvetlen adatbázis-írási
utat.

Példa: „Rögzítsd Éva keddi központi műszakját, és generáld újra csak a szerdai
gyógyszerészi hiányt.”

Kötelező folyamat: értelmezés → strukturált művelet → jogosultság → validáció
→ előnézet → megerősítés → végrehajtás → audit.

## Mobil

Dolgozói mobil fókusz:

- saját beosztás;
- következő műszak;
- kérelmek és távollétek;
- értesítések.

Admin mobil fókusz:

- időszak-összefoglaló;
- problémák;
- jóváhagyások;
- generálás állapota;
- gyors korrekciók;
- diktálás.

A teljes havi dolgozó × nap mátrix elsősorban asztali gépre és nagyobb
tabletre készül.
