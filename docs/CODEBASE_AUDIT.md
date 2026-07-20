# Kódbázis-audit – 0. fázis

Audit dátuma: 2026-07-20

## Hatókör és módszer

Az audit az `AGENTS.md`, a `docs/`, a `contracts/` és a
`prompts/00_FAZIS_AUDIT_ES_SKELETON.md` alapján készült. A legacy forrásfájlok
nem módosultak. A build előtti, `bin/` és `obj/` könyvtárakat kizáró 47 fájlos
legacy SHA-256 összesítő:
`43e9155ab41dd49cdfd4a27f33429ca229378713d8642d22035e2a54642e064b`.

Az osztályozás jelentése:

- **átvehető**: tiszta, a célkövetelményekkel összhangban álló logika;
- **adapterezhető**: a viselkedés vagy külső könyvtár használható, de új
  interfész/adatmodell szükséges;
- **teszt után refaktor**: értékes üzleti logika, amelyet előbb
  karakterizációs tesztekkel kell rögzíteni;
- **eldobandó**: WinForms- vagy prototípus-specifikus rész;
- **újraírandó**: a célrendszer biztonsági vagy domain-invariánsait nem tudja
  megfelelően hordozni.

## Repository állapot

- A Git ág `main`, a kiinduló commit `d769008 first commit`.
- A kiinduló munkafában az importált csomag túlnyomó része nem követett fájl
  volt; az audit ezért nem tekinti a repositoryt tiszta baseline-nak.
- A dokumentációban előírt `legacy/current-winforms/` könyvtár nem létezik.
  Helyette `legacy/current_winforms/` található.
- A `legacy/current_winforms/PharmacyScheduler.sln` felső solution hibás,
  mert nem létező, közvetlen projektmappákra mutat.
- A repositoryn belül legfrissebbnek látszó, ténylegesen buildelhető solution:
  `legacy/current_winforms/PharmacySchedulerWinForms/PharmacySchedulerWinForms/PharmacyScheduler.sln`.
  A benne lévő források 2026-04-07/08-i módosításokat tartalmaznak, míg a felső
  solution és a ZIP 2026-03-30-i. Külső kiadási azonosító vagy ellenőrzőösszeg
  hiányában az nem bizonyítható, hogy ez a szervezeten kívül is a legfrissebb
  példány.

## Legacy build és teszt baseline

Hibás felső solution ellenőrzése:

```powershell
dotnet build legacy/current_winforms/PharmacyScheduler.sln --no-restore --configuration Debug
```

Eredmény: sikertelen, `0` warning, `3` `MSB3202` hiba; a solution által
hivatkozott három projektfájl nem található.

Működő belső solution buildje:

```powershell
dotnet build legacy/current_winforms/PharmacySchedulerWinForms/PharmacySchedulerWinForms/PharmacyScheduler.sln --no-restore --configuration Debug
```

Eredmény: sikeres, `1` warning (`NETSDK1137`), `0` hiba. A buildhez a sandboxon
kívüli felhasználói Windows SDK könyvtár olvasása kellett.

Meglévő legacy tesztek:

```powershell
dotnet test legacy/current_winforms/PharmacySchedulerWinForms/PharmacySchedulerWinForms/PharmacyScheduler.Tests/PharmacyScheduler.Tests.csproj --no-build --configuration Debug --logger "console;verbosity=normal"
```

Eredmény: `4/4` teszt sikeres. A meglévő tesztek csak jelenlétet vizsgálnak
néhány fontos eredménynél, nem rögzítik a teljes kimenetet vagy a szélső
eseteket.

A `dotnet list <legacy-solution> package --vulnerable --include-transitive
--no-restore` az aktuális NuGet források alapján egyik legacy projektben sem
talált ismert sérülékeny közvetlen vagy tranzitív csomagot.

## Legacy projektek

| Projekt | Célkeretrendszer | Felelősség |
| --- | --- | --- |
| `PharmacyScheduler.Core` | `net8.0` | modellek, validáció, coverage, autofill, export-projekció |
| `PharmacyScheduler.WinForms` | `net8.0-windows` | UI, JSON fájltár, Excel/PDF export |
| `PharmacyScheduler.Tests` | `net8.0` | 4 MSTest alapú teszt |

## Típus- és fájlszintű leltár

### Domain modellek és segédtípusok

| Fájl és típus | Megfigyelt szerep | Osztályozás | Indok |
| --- | --- | --- | --- |
| `PharmacyScheduler.Core/Enums.cs`: `EmployeeRole`, `TimeType`, `Severity`, `ScheduleStatus` | Legacy kódkészletek | teszt után refaktor | A cél-contract szerepei és státuszai eltérnek; a `TimeType` munka- és távolléttípusokat kever. |
| `Models/Employee.cs`: `Employee` | dolgozó, limitek, szöveges preferenciák, telephelyek, autofill-zászlók | újraírandó | Nincs `OrganizationId`, verzió, külön `Schedulable` és `CountsAsPharmacist`; a preferencia nem strukturált. Legacy import DTO-ként adapterezhető. |
| `Models/Location.cs`: `Location` | telephely és aktivitás | újraírandó | Hiányzik a szervezeti határ, `Kind` és konkurenciaverzió. |
| `Models/CoverageRule.cs`: `CoverageRule` | heti nap/idősáv/szerep/minimum/súlyosság | újraírandó | Hiányzik szervezet, aktivitás, verzió és dátum-/ismétlődési modell. |
| `Models/ShiftEntry.cs`: `ShiftEntry` | lokális dátum + idő, számított óraszám | újraírandó | Nem hordoz UTC/offset szemantikát, szervezetet vagy verziót; éjfélen átnyúlás nem lehetséges. |
| `Models/SchedulePlan.cs`: `SchedulePlan` | beosztási időszak és közvetlenül birtokolt műszaklista | újraírandó | Környezeti felhasználónév és lokális `DateTime.Now`, nincs szervezet/verzió/audit, csak Draft/Approved. |
| `Models/LeaveEntry.cs`: `LeaveEntry` | teljes napos, inclusive dátumtartomány | újraírandó | Nincs workflow, státusz, résznap, nyitott betegállomány, audit vagy diagnózis-mezőt explicit tiltó contract. |
| `Models/AppSettings.cs`: `AppSettings` | szabályok soft/hard súlyossága | adapterezhető | A konfigurációs jelentés hasznos, de szervezeti és verziózott célmodell kell. |
| `Models/AppData.cs`: `AppData` | teljes rendszer egyetlen in-memory/JSON aggregátuma | adapterezhető | Csak legacy import bemenetként használható; tranzakciós runtime modellnek alkalmatlan. |
| `Models/ValidationModels.cs`: `ValidationIssue`, `ValidationReport` | stabil kód, súlyosság és opcionális érintett ID-k | adapterezhető | Jó kiinduló eredménymodell, de a cél-contract `entityIds` listát és lokalizálható hibakezelést vár. |
| `Models/ExportModels.cs`: `ScheduleExportRow`, `ScheduleSummaryRow` | export-projekciók | adapterezhető | Export read modelként használható, miután az elvárt formátumokról döntés születik. |
| `Services/HalfHourHelper.cs`: `HalfHourHelper` | rácsillesztés, slotok, fél-nyitott intervallumátfedés, időtartam | átvehető | Tiszta és determinisztikus; az éjfélen átnyúló szabály döntéséig csak napon belül használható. |
| `Services/TimeWindowParser.cs`: `TimeWindow`, `TimeWindowParser` | pontosvesszős szöveg parse és intervallum-műveletek | teszt után refaktor | Az intervallum-műveletek értékesek, a laza, hibát csendben eldobó szöveges parse nem célmodell. |
| `DisplayTextExtensions.cs`: `DisplayTextExtensions` | magyar feliratok és `IsWorkLike` | adapterezhető | A feliratok UI/lokalizációs rétegbe valók; a munka-jelleg üzleti fogalmát külön domain policyként kell rögzíteni. |

### Validáció és coverage

`PharmacyScheduler.Core/Services/ScheduleValidationService.cs` egyetlen
`ScheduleValidationService.Validate` metódusból indul, és az alábbi viselkedést
tartalmazza:

- időszakon kívüli bejegyzés, hibás időtartomány és 30 perces rács;
- ismeretlen dolgozó/telephely;
- engedélyezett időtípus és telephely;
- teljes napos távolléttel ütköző, munka-jellegű műszak;
- preferált/tiltott idősáv;
- dolgozói átfedés minden telephely és minden más beosztás között;
- napi és havi órakeret;
- aktív telephelyek félórás coverage-hiánya.

Osztályozás: **teszt után refaktor**. A logika nagyrészt UI-független és stabil
hibakódokat ad, de adatbázis-aggregátum helyett teljes `AppData` objektumot kér,
és több célkövetelménnyel nincs összhangban:

- minden más beosztást figyelembe vesz státusztól függetlenül, így alternatív
  piszkozatok is okozhatnak átfedést és limit-túllépést;
- a coverage pontos `Employee.Role` egyezést használ, nem az autofill által
  használt `AutoScheduleRoleOverride`-ot és nem a cél `CountsAsPharmacist`
  képességet;
- a félórás slotot bármilyen részleges átfedés lefedettnek számítja;
- az `OUT_OF_DAY` ellenőrzés `TimeOnly` mellett gyakorlatilag nem modellezi az
  éjfélen átnyúlást;
- ugyanazon beosztáson belüli kölcsönös ütközésekből ismétlődő
  `EMPLOYEE_OVERLAP` hibák keletkezhetnek;
- nincs szervezet-, authorization-, konkurencia- vagy auditkontextus.

A coverage külön privát `ValidateCoverage` metódus, nem önálló szolgáltatás.
Kinyerés előtt a slot-, szerep-, aktivitás- és több-beosztásos viselkedést
karakterizálni kell.

### Autofill/generátor

Fájl/típus:
`PharmacyScheduler.Core/Services/AutoSchedulerService.cs`,
`AutoSchedulerService`.

Osztályozás: **teszt után refaktor**.

Megfigyelt algoritmus:

1. aktív telephelyek, majd súlyosság és kezdés szerint rendezett szabályok;
2. félórás slotonként hiányszámítás;
3. aktív, `IncludeInAutoSchedule=true`, megfelelő effektív szerepű,
   időtípusra/telephelyre engedélyezett, nem távollévő és nem ütköző dolgozók;
4. preferált idősáv, folytonosság, napi/havi terhelés szerinti pontozás;
5. pontegyenlőségnél `DisplayName` szerinti determinisztikus választás;
6. azonos dolgozó/telephely/típus/megjegyzés szomszédos slotjainak összevonása.

Fontos eltérések:

- a tiltott idősáv nem zárja ki a jelöltet, csak az utólagos validáció jelez;
- a limitbüntetés a már meglévő, nem a tervezett új slot utáni óraszámra épül,
  ezért a generátor túllépést hozhat létre;
- minden más beosztás számít, státusztól függetlenül;
- nincs külön `Schedulable` és `CountsAsPharmacist`;
- az effektív szerep override-ját a validátor coverage-logikája nem használja;
- a visszatérési érték létrehozott félórás slotok száma, miközben az eredményben
  ezek összevont műszakok lehetnek.

### Beosztásmásolás

Fájl/típus:
`PharmacyScheduler.WinForms/MainForm.cs`, privát `MainForm.CopySchedule`.

Osztályozás: **újraírandó** alkalmazási use case-ként. A jelenlegi logika
napeltolással másolja az időszakot és az összes bejegyzést, új schedule- és
shift-ID-kat ad, Draft státuszt állít be, majd azonnal fájlba ment. Nem végez
előnézetet, authorizationt, szervezeti ellenőrzést, távollét-/coverage-
validációt, idempotenciát vagy auditot. A megfigyelt eltolási és másolási
szemantikát teszttel kell rögzíteni, mielőtt új use case készül.

### Exportok

| Fájl és típus | Osztályozás | Megjegyzés |
| --- | --- | --- |
| `Core/Services/ScheduleQueryService.cs`: `ScheduleQueryService` | adapterezhető | Tiszta `FlattenSchedule` és `BuildSummary` projekció; új read model és szervezeti query szükséges. |
| `WinForms/Infrastructure/ExcelExportService.cs`: `ExcelExportService` | adapterezhető | ClosedXML alapú két munkalapos export; fájlstreames, cserélhető infrastruktúra-interfész mögé tehető. |
| `WinForms/Infrastructure/PdfExportService.cs`: `PdfExportService` | adapterezhető | QuestPDF alapú lista és legfeljebb 25 validációs tétel; szerveroldali font/licenc/erőforrás teszt kell. |
| `MainForm.ExportExcel`, `MainForm.ExportPdf`, `SaveFileDialog` | eldobandó | Desktop fájlválasztó és üzenetablak PWA/API környezetben nem használható. |

Az exportformátumok megtartása nyitott termékdöntés.

### JSON persistence

Fájl/típus:
`PharmacyScheduler.WinForms/Infrastructure/AppDataFileStore.cs`,
`AppDataFileStore`; indítás:
`PharmacyScheduler.WinForms/Program.cs`.

Osztályozás: **adapterezhető**, kizárólag későbbi, dry-run képes legacy import
bemeneteként. Runtime persistence-ként **újraírandó**.

Kockázatok:

- nincs séma- vagy adatverzió;
- nincs szervezeti határ, tranzakció, optimista konkurencia vagy audit;
- teljes fájlos, nem atomikus felülírás és nincs backup;
- sérült JSON olvasása kivételt dob, `null` eredmény viszont csendben
  mintaadatra esik vissza;
- hiányzó fájlnál automatikusan személynév-szerű mintaadat keletkezik;
- alapértelmezett `System.Text.Json` viselkedés miatt ismeretlen mezők nem
  jelentenek szigorú sémahibát;
- az adatfájl az alkalmazás bináris könyvtárába kerül.

Ebben a fázisban sem import, sem adatkonverzió nem történt.

### UI-hoz kötött és implicit üzleti logika

Az alábbi típusok **eldobandók** PWA nézetrétegként:

- `MainForm`;
- `Dialogs/CoverageRuleEditorForm`;
- `Dialogs/EmployeeEditorForm`;
- `Dialogs/LeaveEditorForm`;
- `Dialogs/LocationEditorForm`;
- `Dialogs/ScheduleCopyForm`;
- `Dialogs/ScheduleEditorForm`;
- `Dialogs/ShiftEditorForm`;
- `ViewModels/LocationGridRow`, `EmployeeGridRow`, `CoverageGridRow`,
  `LeaveGridRow`, `ShiftGridRow`;
- `Program` WinForms bootstrapja.

Az UI-ba ágyazott szabályokat viszont nem szabad észrevétlenül elveszíteni;
ezeket **újra kell írni szerveroldalon**, ha a termékdokumentum megerősíti:

- kötelező mezők és dátum-/időtartomány-validáció az editorok `SaveBack`
  metódusaiban;
- hivatkozott dolgozó/telephely törlésének tiltása a `MainForm` CRUD
  metódusaiban;
- jóváhagyás blokkolása `ValidationReport.HasBlockingIssues` esetén;
- módosítás után Draft státusz visszaállítása;
- `MainForm.EnsureRoleConstraints` legfeljebb egy vezetőt enged, és frissítéskor
  a további vezetőket csendben gyógyszerésszé módosítja. Ez nincs a cél
  dokumentumaiban, ezért nem vihető át üzleti szabályként.

Az in-memory `_data` állapot, a kézzel példányosított szolgáltatások,
`Environment.UserName`, `DateTime.Now/Today`, `MessageBox` és `SaveFileDialog`
erős desktop-kötést jelentenek.

## Biztonsági és architekturális hiányok

A legacy prototípusban nincs:

- hitelesítés és szerveroldali authorization;
- szervezeti adatelválasztás;
- alkalmazás-permission és szakmai szerep szétválasztása;
- optimista konkurencia, idempotencia és tranzakciókezelés;
- immutable audit;
- UTC/`Europe/Budapest` időkezelési határ;
- AI séma/előnézet/megerősítés;
- PostgreSQL/EF Core migration.

Ezeket adapterezéssel nem lehet pótolni; az új rétegekben kell megvalósítani a
későbbi fázisokban.

## 0. fázisban létrehozott cél-skeleton

- `PatikaBeosztas.slnx`;
- `src/PatikaBeosztas.Domain`;
- `src/PatikaBeosztas.Application`;
- `src/PatikaBeosztas.Contracts`;
- `src/PatikaBeosztas.Infrastructure`;
- `src/PatikaBeosztas.Api`;
- `tests/PatikaBeosztas.Domain.Tests`;
- `tests/PatikaBeosztas.Application.Tests`;
- `tests/PatikaBeosztas.Api.IntegrationTests`;
- külön, legacy forrást nem módosító
  `tests/PatikaBeosztas.Legacy.CharacterizationTests`.

A közös buildbeállítás az `eng/Common.Build.props`, amelyet csak az új `src/`
és `tests/` fák importálnak. Így a nullable, C# 14, beépített .NET analyzerek,
`latest-recommended` elemzési szint és warnings-as-errors nem változtatja meg a
legacy projektek buildkontextusát.

## Új solution ellenőrzése

Használt toolchain: .NET SDK `10.0.302`, .NET/ASP.NET Core runtime `10.0.10`.

```powershell
dotnet restore PatikaBeosztas.slnx
dotnet build PatikaBeosztas.slnx --no-restore --configuration Release
dotnet test PatikaBeosztas.slnx --no-build --configuration Release --logger "console;verbosity=normal"
dotnet list PatikaBeosztas.slnx package --vulnerable --include-transitive --no-restore
```

Eredmények:

- restore sikeres;
- Release build sikeres, `0` warning, `0` hiba;
- `7/7` új teszt sikeres: 1 domain-architektúra, 1 alkalmazás-architektúra,
  2 API-integrációs és 3 legacy karakterizációs teszt;
- az aktuális NuGet források alapján egyik projektnek sincs ismert sérülékeny
  közvetlen vagy tranzitív csomagja;
- a health endpoint `/health`, az OpenAPI dokumentum
  `/openapi/v1.json`; üzleti endpoint nincs;
- a csak új fájlokra futtatott `dotnet format --verify-no-changes` sikeres.

A restore először helyesen megállt a `Microsoft.AspNetCore.OpenApi` által
behozott sérülékeny `Microsoft.OpenApi 2.0.0` tranzitív verziónál. A javítás
explicit `Microsoft.OpenApi 2.11.0` referencia; a warning nincs elnémítva.
