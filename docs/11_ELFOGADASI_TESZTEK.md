# Elfogadási és tesztforgatókönyvek

## Jogosultság

1. Dolgozó saját szabadságigényt létrehoz – siker.
2. Dolgozó más `employeeId`-jával próbál létrehozni – a szerver figyelmen kívül hagyja/megtagadja.
3. Dolgozó más részletes beosztását kéri – megtagadva.
4. Admin más nevében rögzít – auditálva sikeres.
5. Admin saját beosztása lekérhető.
6. Más szervezet GUID-ja nem szivárogtat adatot.

## Kérelem

1. Teljes napos szabadság → Draft → Pending.
2. Résznapos kérelem percre pontosan.
3. Függő kérelem visszavonás.
4. Jóváhagyás és értesítés.
5. Elutasítás indokkal.
6. Két párhuzamos döntésből egy kap 409-et.
7. Betegállománynál nincs diagnózis mező/adat.
8. Nyitott betegállomány `Reported` → `Recorded` → `Closed`, lezáráskor
   végdátummal.
9. Saját munkapreferencia employee azonosítója a sessionből származik;
   idegen dolgozó és idegen szervezet határa tesztelt.
10. WorkPreference- és LeaveRequest-mutáció CSRF nélkül sikertelen.
11. Stale `xmin` verzió 409-et ad, az audit és a státusztörténet megmarad.

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

## Phase 2B tervezési alapok

1. Heti nyitvatartás Closed, Open24Hours és több CustomIntervals napot ment és
   olvas vissza; átfedő vagy rendezetlen intervallum 422.
2. Stale nyitvatartás, műszaksablon, coverage, capability-aggregate,
   munkaprofil vagy kvóta verzió 409.
3. Műszaksablon CRUD napmaszkot, kategóriát és opcionális capabilityt kezel;
   inaktiválás auditált.
4. SpecialistPharmacist → Pharmacist és SpecialistAssistant → Assistant
   implikáció érvényes; a meglévő gyógyszertárvezető/CountsAsPharmacist adat
   explicit Pharmacist capabilityt kap.
5. Átfedő coverage-szabályok szükséges létszáma azonos capabilitynél maximum,
   nem összeg; nyitvatartáson kívül explicit warning jelenik meg.
6. Inaktív telephely rekordjai tárolhatók, de a tervezési jogosultság hamis és
   a coverage válasz kizárási figyelmeztetést ad.
7. Munkaprofil min/standard/max, feltételes limitek és autofill-feltételek
   szerveroldalon validáltak.
8. Kvótánál min ≤ target ≤ max és a dolgozó/dimenzió/periódus egyediség
   érvényes.
9. Ismert idegen szervezeti GUID 404, hiányzó CSRF token 400, jogosulatlan
   permission 403; sikeres mutáció auditált.
10. Napi blokknál 08–14 + 14–18 és az átfedés összeolvad; hézag
    `SPLIT_SHIFT_NOT_ALLOWED`, más telephely
    `MULTI_LOCATION_SAME_DAY_NOT_ALLOWED`.
11. 08–16 Work + 16–18 Overtime egy 08–18 assignmentet és két könyvelési
    szegmenst ad; napi maximum túllépése és DST-hibás lokális idő elutasított.

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

## Phase 2D HR/bérszámfejtési belépés

1. A payroll profil CRUD titkosított adóazonosítót tárol, API-lista/summary
   csak maszkolt értéket ad, a teljes érték külön permissiont igényel.
2. Beosztási permission önmagában nem ad hozzáférést payroll profilhoz,
   survey-hez vagy exporthoz.
3. A self-service survey dolgozóját a sessionből oldja fel; más szervezet
   ismert GUID-ja 404.
4. CSRF nélküli mutáció 400, stale `xmin` verzió 409.
5. A 2026-os validáció kezeli a darabszámokat, hatálydátumokat, YYYY-MM
   hónapokat és az egymásnak ellentmondó mezőket.
6. Beküldött survey csak admin visszanyitás után szerkeszthető; a
   submit/review/reopen/complete állapotgép tiltja a rövidítéseket.
7. A `HU-2026.1` döntési motor mind a hét nyilatkozattípusról reprodukálható
   javaslatot ad; ismeretlen vagy külföldi válasz tisztázást kér.
8. Kézi szükségességi felülírás csak indoklással menthető, és újraértékeléskor
   megmarad.
9. A nyilatkozat státuszfolyamata nem ugorható át a Required állapottól az
   Applied állapotig.
10. A JSON/CSV belépési export permission-védett, auditált, és nem tartalmaz
    adóazonosítót, hitelesítési vagy szükségtelen egészségügyi adatot.
11. Profil-, summary-, survey- és checklist-megtekintés, minden mutáció és
    export redaktált auditbejegyzést hoz létre.
12. A public contractban nincs diagnózis-, betegségnév-, orvosi lelet- vagy
    dokumentumfeltöltési mező.
