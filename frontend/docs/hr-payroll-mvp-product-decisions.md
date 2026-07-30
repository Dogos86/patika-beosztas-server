# Patika Beosztás – MVP-, HR- és bérszámfejtési termékdöntések

## 1. Cél

A rendszer három, egymáshoz kapcsolódó, de külön kezelt területet fed le:

1. **Beosztás és távollét** – dolgozói igények, szabadság, betegállomány, automatikus beosztás, közzététel.
2. **Belépési/HR-adatok** – új dolgozó rögzítése, munkaviszony és munkaprofil, belépési folyamat.
3. **Bérszámfejtési előkészítés** – belépési adatok átadása, adókedvezmény-felmérő, szükséges NAV-nyilatkozatok követése, később havi jelenléti és munkaidő-export.

## 2. Alapvető különbség: beosztás és tényleges jelenlét

A beosztás terv. Bérszámfejtési exportot nem szabad kizárólag a tervezett beosztásból készíteni.

A későbbi helyes folyamat:

```text
Közzétett beosztás
→ tényleges jelenléti adatok / eltérések
→ admini ellenőrzés
→ havi időszak lezárása
→ bérszámfejtési export
```

A havi export alapja ezért egy lezárt **Attendance/Payroll Period**, nem pusztán a Schedule.

## 3. A feltöltött dokumentum szerepe

A `2026_Belepesi_adokedvezmeny_igenyfelmero_egyszerusitett.docx` egy belső adókedvezmény-felmérő. Nem hivatalos NAV adóelőleg-nyilatkozat és nem teljes körű munkavállalói belépési adatlap.

A rendszerben külön modul legyen:

- **Bérszámfejtési törzsadatok**;
- **2026-os adókedvezmény-felmérő**;
- **szükséges hivatalos nyilatkozatok ellenőrzőlistája**;
- **hivatalos nyilatkozat státusza**.

A felmérő alapján a rendszer feladatot/listát állít elő, de nem állapít meg végleges jogosultságot és nem helyettesíti a hivatalos NAV-nyilatkozatot.

## 4. Új dolgozó folyamat

Javasolt varázsló:

1. **Dolgozói alapadatok**
   - teljes név;
   - megjelenítési név;
   - szakmai szerepkör;
   - születési dátum;
   - munkaviszony kezdete;
   - munkavállalói azonosító;
   - adóazonosító jel;
   - külső bérszámfejtési azonosító.

2. **Munkaviszony és beosztási profil**
   - telephelyek;
   - havi/heti szerződéses idő;
   - műszakhossz;
   - túlóra, ügyelet, készenlét, hétvége;
   - kompetenciák;
   - preferenciák és kvóták.

3. **Adókedvezmény-felmérő**
   - a 2026-os kérdőív teljes tartalma;
   - verzió és hatálydátum;
   - munkavállalói nyilatkozat;
   - HR/bérszámfejtői felülvizsgálat.

4. **Szükséges nyilatkozatok**
   - szükséges-e;
   - kiküldve;
   - beérkezett papíron vagy ONYA-n;
   - ellenőrizve;
   - alkalmazva;
   - hatály/megjegyzés.

5. **Belépési fiók**
   - most nem kap fiókot;
   - fiók létrehozása;
   - permissionök.

## 5. Adókedvezmény-felmérő mezőcsoportjai

### Alapadat és igénybevétel

- társaság;
- munkavállalói azonosító;
- név;
- adóazonosító jel;
- születési dátum;
- munkaviszony kezdete;
- havi számfejtésben kéri / nem kéri / egyeztetést kér.

### Családi állapot és első házasok

- családi állapot;
- házasság dátuma;
- legalább egyik fél első házassága – igen/nem/nem tudom.

### Gyermekek és eltartottak

- családi pótlékra jogosító gyermekek száma;
- eltartotti létszámba beszámító tanuló/hallgató;
- 91. napot elért magzat és jogosultsági hónap;
- tartósan beteg/súlyosan fogyatékos gyermek vagy eltartott – csak szükséges jelző, diagnózis nélkül;
- felváltva gondozott gyermek;
- családi kedvezmény igénybevételi módja;
- másik jogosulttal közös érvényesítés.

### Anyák kedvezményei

- vér szerinti vagy örökbefogadó anya;
- jogosító gyermekek száma;
- 2026-ban aktuális saját/örökbefogadott gyermek vagy 91. napot elért magzat.

### Egyéb körülmények

- személyi kedvezményre jogosító igazolás/ellátás fennállása;
- jogosultság kezdő hónapja;
- másik munkáltató vagy rendszeres kifizető;
- 25 év alatti kedvezmény részleges/teljes mellőzése;
- külföldi adóügyi illetőség vagy külföldi hasonló kedvezmény.

## 6. Nyilatkozatkövetés

Első készlet:

- 25 év alatti kedvezmény mellőzése;
- 30 év alatti anyák kedvezménye;
- ANYACSKA;
- külön 2/3/4+ gyermekes anyakedvezmény;
- családi kedvezmény / családi járulékkedvezmény;
- első házasok kedvezménye;
- személyi kedvezmény.

Státuszok:

```text
NotRequired
Required
ToSend
Sent
ReceivedOnya
ReceivedPaper
Verified
Applied
Rejected
Expired
```

## 7. Verziózott szabálymotor

Az adókedvezmény-szabályok adóévenként változhatnak. A kérdőív és a döntési logika legyen verziózott:

- TaxYear;
- FormVersion;
- RuleSetVersion;
- EffectiveFrom;
- EffectiveTo;
- SourceMetadata.

A rendszer csak a szükséges hivatalos nyilatkozatok listáját javasolja. Adóösszeget és végleges jogosultságot ebben a modulban nem számol.

## 8. Adatvédelem és jogosultság

A beosztáskészítő jogosultság nem jelent automatikusan hozzáférést adó- és családi adatokhoz.

Új permissionök:

- `ManagePayrollOnboarding`;
- `ViewPayrollSensitiveData`;
- `ReviewTaxAllowanceSurvey`;
- `ExportPayrollData`;
- később `ApproveAttendance`;
- később `ClosePayrollPeriod`.

Követelmények:

- érzékeny érték ne kerüljön logba vagy általános audit payloadba;
- listákban adóazonosító csak maszkolva;
- teljes adat csak külön permissionnel;
- diagnózis és orvosi dokumentum ne legyen mező vagy feltöltés;
- minden megtekintés/módosítás auditálható legyen;
- dolgozó csak a saját kérdőívét lássa;
- admini beosztásszerkesztő ne lássa ezeket az adatokat külön payroll permission nélkül.

## 9. Bérszámfejtési kimenetek

### A. Belépési csomag – hamarabb megvalósítható

- dolgozói törzsadat;
- munkaviszony kezdete;
- munkaprofil;
- adókedvezmény-felmérő összefoglaló;
- szükséges nyilatkozatok és státuszok;
- export: JSON + CSV/Excel-kompatibilis táblázat + nyomtatható PDF később.

### B. Havi bérszámfejtési adatszolgáltatás – a jelenléti modul után

Részletes sorok:

- dolgozó azonosító;
- dátum;
- telephely;
- kezdés/vége;
- perc;
- Work / Overtime / OnCallDuty / Standby;
- AnnualLeave / SickLeave / UnpaidLeave / ParentalLeave;
- szombat/vasárnap/ünnepnap/éjszaka jelző;
- payroll code;
- megjegyzés;
- jóváhagyási és zárási státusz.

A rendszer először vendor-neutral exportot készítsen. Később külön Novitax-adapter készülhet.

## 10. MVP-kiadási sorrend

### MVP-A – valós adatbázisos törzs és távollét

- auth;
- dolgozó és fiók;
- telephely;
- munkaidőprofil;
- szabadság/betegállomány;
- belépési és adókedvezmény-felmérő.

### MVP-B – a feleség valós beosztási pilotja

- Schedule és Shift backend persistence;
- automatikus generátor backend;
- eredményellenőrző munkatér;
- jóváhagyás/közzététel;
- dolgozói saját beosztás.

### MVP-C – bérszámfejtési hónapzárás

- tényleges jelenlét;
- eltérések;
- túlóra jóváhagyás;
- hónapzárás;
- havi export;
- később Novitax-specifikus adapter.
