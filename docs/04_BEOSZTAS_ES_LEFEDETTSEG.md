# Beosztás és lefedettség – backend célviselkedés

A frontend és backend közös munkatér-döntésének kanonikus leírása:
`docs/13_GENERALAS_KOZPONTU_BEOSZTAS.md`. Ez a dokumentum az abból következő
szerveroldali viselkedést rögzíti.

## Generálás-központú folyamat

- A támogatott időszak hét, két hét vagy hónap.
- A generátor a teljes kiválasztott időszakot egyben optimalizálja, akkor is,
  ha belül napokra és 30 perces blokkokra bontja.
- A generálás induló állapota `Generating`; sikeres eredménye `Draft`.
- A normál adminfolyamat: generálás → ellenőrzés → korlátozott korrekció vagy
  részleges újragenerálás → `UnderReview` → `Approved` → `Published`.
- A dolgozói lekérdezés kizárólag `Published` beosztást adhat vissza.
- A teljes kézi drag-and-drop szerkesztő nem része ennek a fázisnak.

Az állapotátmeneteket a szerver ellenőrzi. Minden fontos mutáció szervezethez
kötött, jogosultság-ellenőrzött, optimista konkurenciával védett és auditált.

## Dolgozói beállítások és bemenetek

- havi időkeret;
- maximum napi percszám;
- konkrét és ismétlődő, percre pontos preferencia vagy elérhetetlenség;
- `Schedulable`;
- `IncludeInAutoFill`;
- `CountsAsPharmacist`;
- engedélyezett időtípusok;
- aktív telephely-hozzárendelések;
- jóváhagyott távollétek;
- a függő kérelmek konfigurált kezelési módja.

A szakmai szerepkör önmagában nem tiltja a beosztást. Külön zászlók döntik el,
hogy a gyógyszertárvezető beosztható-e, részt vesz-e a generálásban, illetve
gyógyszerészi lefedettségbe számít-e.

## Lefedettségi szabály

A lefedettségi szabály legalább telephelyet, nap- vagy dátummintát, idősávot,
szakmai szerepkört vagy képességet, szükséges létszámot, súlyosságot és aktív
állapotot tartalmaz.

Súlyosság:

- `Warning` – figyelmeztető;
- `Blocking` – a jóváhagyást vagy közzétételt blokkoló.

Az inaktív telephely történeti adatai és szabályai megmaradhatnak, de az aktív
hiányszámítás és a generálás nem veheti figyelembe. A lefedettségi projekció
naponként `Ok`, `Warning`, `Blocking`, `Closed` vagy `Inactive` állapotot ad,
részleteiben pedig idősávonként és szerepkörönként a szükséges és tényleges
létszámot, az eltérést és az érintett dolgozókat.

## Generátor kötelező korlátai és céljai

A régi heurisztika csak a karakterizációs tesztek után emelhető át. A
generátor legalább a következőket értékeli:

- aktív telephely és annak nyitvatartása;
- aktív, beosztható, generálásba bevont dolgozó;
- megfelelő szakmai szerepkör vagy coverage-képesség;
- telephelyhez rendelhetőség;
- dolgozói és több telephelyes időütközés hiánya;
- jóváhagyott távollét;
- függő kérelem konfigurált kezelése;
- határozott elérhetetlenség;
- konkrét és ismétlődő preferenciák;
- napi és havi időkeret;
- szükséges telephelyi és szerepköri lefedettség;
- hétvégi, délutáni/esti és telephelyi terhelés lehetőség szerinti
  igazságossága.

A hard korlátok és soft célok pontos osztályozása, valamint a pontozási súlyok
a `docs/OPEN_DECISIONS.md` fájlban maradnak nyitva. Determinisztikus bemenet,
azonos konfiguráció és azonos algoritmusverzió mellett az eredménynek
reprodukálhatónak kell lennie.

## Validáció és problémák

A generálás és minden újragenerálás ugyanazt a szerveroldali validációt futtatja.
A közös problématípusok:

- szükséges gyógyszerész vagy szakasszisztens hiánya;
- dolgozó egyidejű beosztása két telephelyre;
- jóváhagyott távollét és műszak ütközése;
- napi vagy havi keret túllépése;
- határozott elérhetetlenség megsértése;
- dolgozói preferencia nem teljesülése;
- generált műszakot érintő függő kérelem;
- inaktív telephely hibás figyelembevétele;
- egyéb validációs figyelmeztetés.

Minden probléma stabil kódot, súlyosságot és az érintett beosztás-, nap-,
dolgozó-, telephely- és műszakreferenciák közül az alkalmazhatóakat adja vissza.
Így mindhárom munkatér-projekció ugyanarra a problémára tud navigálni.

## Eredményprojekciók

Az admin API ugyanabból a konzisztens eredményből szolgálja ki:

- a dolgozó × nap mátrixot és dolgozói sorösszesítőket;
- a telephely × nap lefedettségi térképet és idősávos részleteit;
- a szűrhető problémalistát;
- a generálási összefoglalót;
- az utolsó közzétett változathoz képesti műszakonkénti `New`, `Modified`,
  `Deleted` vagy `Unchanged` eltérést.

A generálási összefoglaló legalább lefedettségi százalékot, blokkoló és
figyelmeztető darabszámot, kérés-teljesülési arányt, havi kerettől eltérő
dolgozók számát, függő kérelmek által érintett műszakokat, több telephelyes
ütközéseket, valamint az új/módosított/törölt műszakok számát tartalmazza.

## Magyarázhatóság

Minden generált műszakhoz strukturált magyarázat tartozik. Az indoklás az
algoritmus tényleges döntési adataiból származik, és az alkalmazható okok mellett
az érdemi alternatív jelölteket és hátrányaikat is visszaadhatja. Nem elegendő
egy utólag generált, a döntéstől független szabad szöveg.

## Korlátozott korrekció és részleges újragenerálás

Támogatott műveletek:

- műszak rögzítése vagy feloldása;
- generált javaslat elutasítása;
- alternatív dolgozók lekérdezése;
- újragenerálás napra, hétre, telephelyre, szerepkörre vagy kijelölt
  problémákra;
- utolsó közzétett változattal való összehasonlítás.

A részleges újragenerálás minden esetben megtartja a rögzített műszakokat. Az
újragenerálás is teljes üzleti validációt futtat; a szűkített scope nem ad
felmentést az időszak egészét érintő korlátok ellenőrzése alól.

Az elutasítás megőrzési ideje, az alternatívák rangsora és az egymást metsző
újragenerálási scope-ok pontos szemantikája nyitott termékdöntés.
