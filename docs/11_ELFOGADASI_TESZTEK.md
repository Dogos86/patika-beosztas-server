# Elfogadási és tesztforgatókönyvek

## Jogosultság

1. Dolgozó saját szabadságigényt létrehoz – siker.
2. Dolgozó más `employeeId`-jával próbál létrehozni – a szerver figyelmen kívül hagyja/megtagadja.
3. Dolgozó más részletes beosztását kéri – megtagadva.
4. Admin más nevében rögzít – auditálva sikeres.
5. Admin saját beosztása lekérhető.
6. Más szervezet GUID-ja nem szivárogtat adatot.

## Kérelem

1. Teljes napos szabadság → Pending.
2. Résznapos kérelem percre pontosan.
3. Függő kérelem visszavonás.
4. Jóváhagyás és értesítés.
5. Elutasítás indokkal.
6. Két párhuzamos döntésből egy kap 409-et.
7. Betegállománynál nincs diagnózis mező/adat.

## Beosztás

1. Generálás hétre, két hétre és hónapra indítható; sikeres eredménye Draft.
2. A generátor a teljes kiválasztott időszak napi/havi korlátait együtt
   értékeli, nem egymástól független napi beosztásokat fűz össze.
3. 30 perces rácshoz nem illeszkedő generált műszak nem keletkezhet.
4. Azonos dolgozó két ismert telephelyen átfed – blokkoló hiba.
5. Jóváhagyott távolléttel ütköző generált műszak problémaként megjelenik.
6. Inaktív telephely coverage-e nem kerül aktív hiányszámításba vagy
   generálásba.
7. Gyógyszertárvezető `CountsAsPharmacist=true` esetén a gyógyszerészi
   lefedettségbe számít, ha egyébként beosztható.
8. Blokkoló hiba mellett jóváhagyás és közzététel sikertelen.
9. Figyelmeztetés mellett jóváhagyás a konfigurált szabály szerint engedhető.
10. Részleges újragenerálás nap, hét, telephely, szerepkör és kijelölt probléma
    scope-pal kérhető, és a rögzített műszakokat minden esetben megtartja.
11. Stale beosztásverzióval kért korrekció vagy újragenerálás 409-et ad.
12. Ugyanazzal az idempotency key-jel ismételt generálás nem hoz létre második
    futást.
13. A dolgozó Draft, UnderReview vagy Approved beosztást nem lát; Published
    beosztását igen.
14. Az állapotátmenetek közül jogosulatlan, szervezetidegen vagy tiltott
    átmenet elutasított és nem szivárogtat adatot.
15. Műszak rögzítése/feloldása, javaslat elutasítása, generálás,
    újragenerálás, jóváhagyás és közzététel auditált.

## Munkatér és magyarázhatóság

1. A dolgozói, telephelyi és problémaprojekció ugyanarra a schedule- és
   validációverzióra hivatkozik.
2. A dolgozó × nap cella több műszakot, távollétet, problémát, rögzített
   állapotot és közzétett verzióhoz képesti változást is vissza tud adni.
3. A dolgozói sorösszesítés a beosztott időt, havi célt, kérés-teljesülést,
   hétvégi és délutáni/esti műszakokat, telephelyváltásokat és
   figyelmeztetésszámot tartalmazza.
4. A lefedettségi részlet idősávonként és szerepkörönként visszaadja a
   szükséges/tényleges létszámot, eltérést és érintett dolgozókat.
5. Minden probléma az érintett napra és az alkalmazható dolgozóra,
   telephelyre vagy műszakra navigálható stabil referenciát ad.
6. A generálási összefoglaló minden, a
   `docs/13_GENERALAS_KOZPONTU_BEOSZTAS.md` fájlban felsorolt mutatót
   tartalmazza.
7. Minden generált műszak magyarázata a tényleges döntési okokat és az
   alkalmazható alternatív jelölteket strukturáltan adja vissza.
8. A legutolsó Published változathoz képest az új, módosított és törölt
   műszakok konzisztensen azonosíthatók.

## AI

1. Relatív dátum konkrét dátummal jelenik meg előnézetben.
2. Azonos név esetén tisztázást kér.
3. Jogosulatlan action nem hajtható végre.
4. Ismeretlen action/mező sémahibát ad.
5. Előnézet nélkül nincs végrehajtás.
6. Lejárt előnézet 409.
7. Ugyanaz az idempotency key nem duplikál műveletet.
8. Hangfájl alapértelmezésben nem marad meg.
