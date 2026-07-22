# Patika Beosztás – munkaidő-, nyitvatartás- és lefedettségi domain-specifikáció

## 1. Fogalmak

### Nyitvatartás

Egy telephely heti ismétlődő nyitvatartással rendelkezik. Egy nap lehet:

- zárva;
- 24 órán át nyitva;
- egy vagy több egyedi nyitvatartási intervallummal nyitva.

A tárolás és API támogassa a 00:00–24:00 esetet. Az éjfélen átnyúló intervallumot vagy explicit következő napi jelöléssel, vagy normalizált két részben kell kezelni; a választott megoldást dokumentálni kell.

Az inaktív telephely nyitvatartása és szabályai megmaradhatnak, de generálásnál nem vehetők figyelembe.

### Műszaksablon

Telephelyenként definiálható visszatérő minta:

- Délelőtt;
- Délután;
- Hosszú műszak;
- Egyedi.

Mezők:

- név;
- napok;
- kezdés;
- befejezés;
- aktív;
- opcionális kompetencia/szerepkör;
- minimum/maximum dolgozói létszám későbbi felhasználásra.

A generátor nem köteles kizárólag sablonokkal dolgozni, de lehetőség szerint ezeket használja.

### Lefedettségi követelmény

Telephely + nap + idősáv + kompetencia + szükséges létszám + súlyosság.

Átfedő követelmények megengedettek. Ugyanazon kompetenciánál az adott időpontban érvényes szükséges létszám az alkalmazható szabályok **maximuma**, nem az összege.

Példa:

- 08:00–20:00 között 1 gyógyszerész;
- 12:00–16:00 között 2 gyógyszerész.

Eredmény:

- 08:00–12:00: 1;
- 12:00–16:00: 2;
- 16:00–20:00: 1.

### Szakmai szerepkör

A dolgozó elsődleges munkaköre, például:

- gyógyszertárvezető;
- gyógyszerész;
- szakgyógyszerész;
- szakasszisztens;
- asszisztens;
- gyakornok;
- takarító;
- pénzügyi kisegítő;
- egyéb.

### Kompetencia

A coverage szempontjából használható képesség. Egy dolgozó több kompetenciával rendelkezhet.

Javasolt első készlet:

- Pharmacist;
- SpecialistPharmacist;
- SpecialistAssistant;
- Assistant;
- Cleaner;
- Finance;
- Other.

Öröklődő fedés:

- SpecialistPharmacist kielégíti a Pharmacist követelményt is;
- PharmacyManager megfelelő gyógyszerészi kompetencia esetén kielégíti a Pharmacist követelményt;
- SpecialistAssistant kielégítheti az Assistant követelményt.

Az öröklési szabály egy helyen, tesztelten legyen definiálva.

## 2. Dolgozói munkaprofil

A dolgozóhoz egy külön munkaidőprofil tartozik.

Mezők:

- szerződéses havi perc / cél;
- opcionális szerződéses heti perc;
- standard napi műszakhossz;
- minimum műszakhossz;
- maximum normál műszakhossz;
- maximum összes napi munkavégzés;
- hosszú műszak engedélyezett-e;
- hosszú műszak maximuma;
- túlóra vállalható-e;
- havi túlóra maximum;
- ügyelet vállalható-e;
- havi ügyeleti alkalom maximum;
- készenlét vállalható-e;
- havi készenléti alkalom maximum;
- szombat vállalható-e;
- havi szombatok maximuma;
- vasárnap vállalható-e;
- havi vasárnapok maximuma;
- egész nyitvatartást lefedő műszak vállalható-e;
- automatikus generálásba bevonható-e.

A munkajogi törvényi korlátok ebben a fázisban nem részei a motornak. Csak a szervezet/dolgozó által beállított paramétereket kezeljük.

## 3. Dolgozói munkaszabályok

Egy szabály lehet konkrét dátumos vagy ismétlődő.

Típusok:

- Available – dolgozhat;
- Preferred – szeretne dolgozni;
- Avoid – lehetőség szerint ne;
- Unavailable – nem dolgozhat;
- Fixed – előre leegyeztetett, rögzített alapminta.

Mezők:

- dátumtartomány;
- hét napja opcionális;
- teljes nap vagy kezdés–befejezés;
- telephely opcionális;
- megjegyzés;
- aktív;
- súlyosság;
- létrehozó és audit.

A self-service endpoint a dolgozót a sessionből oldja fel, nem fogad EmployeeId-t.

## 4. Műszakkvóta-szabályok

A „heti két délelőtt, három délután” jellegű megállapodások külön kvótaszabályok.

Dimenziók:

- MorningShift;
- AfternoonShift;
- EveningShift;
- LongShift;
- SaturdayShift;
- SundayShift;
- OnCallDuty;
- Standby.

Időszak:

- Week;
- Month.

Mezők:

- minimum;
- cél;
- maximum;
- Preferred vagy Required súlyosság.

## 5. Munkaidőtípusok

Elsődleges típusok:

- Work;
- Overtime;
- OnCallDuty;
- Standby;
- AnnualLeave;
- SickLeave;
- UnpaidLeave;
- ParentalLeave;
- Other.

A hétvége, ünnepnap és éjszaka körülmény/pótlékjelölés, nem külön TimeType.

A későbbi könyveléshez a rendszer elő legyen készítve:

- payroll code;
- prémium/pótlék tag;
- szegmensenkénti időtípus;
- telephely;
- dátum;
- időtartam.

Összegeket és bérszámfejtési képleteket ebben a fázisban nem implementálunk.

## 6. Egy folyamatos elsődleges munkablokk naponta

Hard invariáns:

- egy dolgozó egy szervezeti időzóna szerinti napon legfeljebb egy folyamatos elsődleges munkablokkot kaphat;
- osztott műszak nincs;
- ugyanazon napon több telephelyes elsődleges munkablokk alapból tiltott;
- egymáshoz érő vagy átfedő, ugyanazon telephelyű blokkok összevonandók;
- hézaggal elválasztott blokkok érvénytelenek;
- eltérő telephelyű blokkok nem vonhatók össze, konfliktusnak számítanak.

Példák:

- 08:00–14:00 + 14:00–18:00, azonos telephely → 08:00–18:00;
- 08:00–14:00 + 13:00–18:00, azonos telephely → 08:00–18:00;
- 08:00–14:00 + 15:00–18:00 → tiltott split shift;
- 08:00–14:00 A telephely + 14:00–18:00 B telephely → tiltott;
- 08:00–16:00 Work + 16:00–18:00 Overtime, azonos telephely → egy 08:00–18:00 jelenléti blokk két könyvelési szegmenssel.

Az ügyelet és készenlét külön szolgálati bejegyzés lehet, de nem fedhet át tiltott módon, és a dolgozói profilnak engednie kell.

## 7. Generátori prioritás

Hard sorrend:

1. inaktív telephely kizárása;
2. jóváhagyott távollét;
3. Unavailable/Fixed szabály;
4. kompetencia és telephely-jogosultság;
5. nincs időütközés;
6. egy folyamatos munkablokk/nap;
7. dolgozói napi/havi maximumok;
8. blokkoló coverage.

Soft optimalizálás:

- Preferred és Avoid;
- műszakkvóták;
- havi/ heti célegyensúly;
- hétvégi terhelés;
- ügyelet/készenlét igazságossága;
- telephelyváltások minimalizálása.

## 8. Első adatbázis-integráció

Valódi HTTP/PostgreSQL modulok első körben:

- telephelyek;
- nyitvatartás;
- műszaksablonok;
- coverage követelmények;
- dolgozói munkaidőprofil;
- kompetenciák;
- munkaszabályok és kvóták;
- szabadság és betegállomány.

A beosztásgenerátor maradhat mock addig, amíg a backend Phase 3 el nem készül.

## 9. Phase 2B runtime-döntések

- A `CustomIntervals` intervallum `EndTime = null` értéke 24:00-t jelent; az
  éjfélen átnyúló időszakot két napra normalizált két rekordként kell megadni.
- A nyitvatartáson kívüli aktív coverage-szabály menthető, de a runtime stabil
  `COVERAGE_OUTSIDE_OPENING_HOURS` figyelmeztetést ad. A nyitvatartás hiánya
  `OPENING_HOURS_NOT_CONFIGURED`, az inaktív telephely pedig
  `INACTIVE_LOCATION_EXCLUDED_FROM_PLANNING` figyelmeztetés.
- Azonos capability átfedő coverage-szabályainál a szükséges létszám pontonkénti
  maximuma érvényes; a szabályokat nem összegezzük.
- Egy dolgozóhoz ugyanazon kvótadimenzió és periódus csak egyszer vehető fel;
  a történeti megőrzést az `IsActive` biztosítja, nem párhuzamos duplikátumok.
- A `CountsAsPharmacist` átmeneti kompatibilitási mező marad. A migráció és a
  fejlesztési seed a meglévő igaz értékeket, illetve a `PharmacyManager` szerepet
  explicit `Pharmacist` capabilityvé képezi le; az új coverage-logika már a
  capability-kapcsolatot használja.
- A normalizáló az eltérő `TimeType` átfedést elutasítja
  `OVERLAPPING_TIME_TYPES_NOT_ALLOWED` kóddal. Az érintkező `Work` és `Overtime`
  blokkok egy jelenléti assignmentben, külön könyvelési szegmensként maradnak.
  Az átfedő könyvelési szegmensek prioritása a Phase 3 előtt külön döntést kér.
- Az időzónát a hívó explicit adja át. A runtime elfogadja az
  `Europe/Budapest` IANA-azonosítót, Windows alatt a megfelelő rendszerazonosítót
  is; DST miatt nem létező vagy kétértelmű lokális idő elutasított.
