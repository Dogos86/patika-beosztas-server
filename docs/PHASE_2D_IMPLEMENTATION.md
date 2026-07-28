# Phase 2D – HR/bérszámfejtési belépés

## Termékhatár

A 2026-os adókedvezmény-felmérő belső HR/bérszámfejtési igényfelmérő. Nem
hivatalos NAV adóelőleg-nyilatkozat, nem számít adót, és a szabálymotor
eredménye nem végleges jogosultsági döntés. A javasolt checklist minden
szükséges elemét hivatalos nyilatkozat és HR/bérszámfejtői ellenőrzés alapján
kell feldolgozni.

A részletes termékdöntések forrása:
`docs/HR_PAYROLL_MVP_PRODUCT_DECISIONS.md`.

## Runtime adatmodell

- `EmployeePayrollProfile`: szervezetenként egy profil dolgozónként,
  dolgozói törzsszám, titkosított adóazonosító, determinisztikus keresési
  lenyomat, munkaviszony kezdete, külső payroll ID, workflow-státusz,
  actor/időpont auditmezők és PostgreSQL `xmin`.
- `TaxAllowanceSurvey`: dolgozó-, adóév- és űrlapverzió szerinti aggregate,
  a 2026-os kérdéscsoportokkal, deklarációs és review actor/időpontokkal,
  hatály- és forrásmetaadattal, workflow-státusszal és `xmin` verzióval.
- `TaxDeclarationRequirement`: a hét támogatott nyilatkozattípus
  szükségességi döntése és teljes adminisztratív státuszfolyamata, kézi
  felülírás kötelező indokával, hatály-, audit- és `xmin` mezőkkel.

A `20260728165652_Phase2DPayrollOnboarding` migráció külön táblákat hoz létre.
A tenant-határt kompozit idegen kulcsok, az egyprofilos és
dolgozó/adóév/űrlapverzió egyediséget adatbázis-indexek is védik.

## Jogosultságok

- `ManagePayrollOnboarding`: profil, admin survey, workflow, checklist és
  onboarding lezárás.
- `ViewPayrollSensitiveData`: a teljes adóazonosító külön olvasási joga.
- `ReviewTaxAllowanceSurvey`: survey és checklist payroll review olvasás,
  valamint felülvizsgálat.
- `ExportPayrollData`: vendor-neutral belépési export.

A beosztási permissionök egyike sem adja meg ezeket automatikusan. A
self-service útvonalak a kapcsolt `EmployeeId` értéket a sessionből oldják
fel, kliensoldali dolgozóazonosítót nem fogadnak.

## Endpointok

### Saját dolgozó

- `GET /api/me/payroll-onboarding`
- `GET /api/me/tax-allowance-surveys/{taxYear}`
- `POST /api/me/tax-allowance-surveys`
- `PUT /api/me/tax-allowance-surveys/{id}`
- `POST /api/me/tax-allowance-surveys/{id}/submit`

### HR/bérszámfejtés

- `GET /api/admin/employees/{employeeId}/payroll-onboarding`
- `POST /api/admin/employees/{employeeId}/payroll-onboarding/complete`
- `GET|PUT /api/admin/employees/{employeeId}/payroll-profile`
- `GET /api/admin/employees/{employeeId}/payroll-onboarding/export`
- `GET|PUT /api/admin/employees/{employeeId}/tax-allowance-surveys/{taxYear}`
- `POST /api/admin/tax-allowance-surveys/{id}/submit|reopen|review|complete`
- `GET /api/admin/employees/{employeeId}/tax-declaration-requirements`
- `PUT /api/admin/tax-declaration-requirements/{id}/status|override`

Minden mutáció cookie-auth, CSRF, szerveroldali permission-, tenant-,
validáció- és optimista konkurencia-ellenőrzés után fut. Idegen szervezeti
GUID 404-et ad.

## Verziózott döntési szolgáltatás

- `FormVersion`: `internal-survey-2026.1`
- `RuleSetVersion`: `HU-2026.1`
- támogatott adóév: `2026`
- szabályhatály vége: `2026-12-31`
- forrásmetaadat: belső 2026-os felmérő és NAV 2026
  adóelőleg-nyilatkozat tájékoztatók

A tiszta domain szolgáltatás mind a hét checklist-elemről reprodukálható
javaslatot ad. A `NeedsConsultation`, `Unknown` és külföldi illetőségi jelzés
`NeedsClarification` állapotot eredményez. A javaslat adóösszeget nem
tartalmaz, a kézi felülírást pedig csak indoklással lehet elmenteni.

## Adatvédelem és audit

- Az adóazonosító titkosítása ASP.NET Core Data Protectionnel történik,
  elkülönített alkalmazásnévvel.
- A szervezeten belüli duplikációvizsgálat HMAC-SHA-256 lenyomatot használ. A
  legalább 32 bájtos kulcsot a
  `SensitiveData__TaxIdentifierHashKey` secret konfiguráció adja.
- Lista- és summary-válaszban csak maszkolt adóazonosító szerepel; teljes
  érték csak a profil részletes olvasásánál és külön
  `ViewPayrollSensitiveData` permissionnel jelenik meg.
- A nyers adóazonosító és annak lenyomata nem kerül alkalmazáslogba, általános
  audit payloadba vagy exportba.
- A profil-, summary-, survey- és checklist-megtekintések, minden módosítás és
  minden export auditált. Az audit csak redaktált összefoglalót tartalmaz.
- Nincs diagnózis-, betegségnév-, orvosi lelet- vagy dokumentumfeltöltési
  mező.

Production környezetben a Data Protection kulcstárat tartós, hozzáférés-védett
külső táron kell megosztani és menteni; a hashkulcsot secret store-ban kell
kezelni.

## Exportok

A `payroll-onboarding-export-v1` JSON és CSV kimenet dolgozói
alapazonosítókat, munkaviszony-kezdést, külső payroll ID-t, survey-verziót és
státuszt, valamint a nyilatkozat-checklistet tartalmazza. Adóazonosítót,
hitelesítési adatot vagy szükségtelen egészségügyi adatot nem tartalmaz.

A `contracts/monthly-payroll-export-v1-draft.yaml` kizárólag jövőbeli
vendor-neutral szerződéstervezet, runtime endpoint nélkül. A havi export
forrása később lezárt tényleges jelenléti időszak lesz, nem a nyers beosztási
terv.

## Runtime OpenAPI

A kanonikus, tényleges runtime dokumentum:
`contracts/openapi.phase2d.json`, API-verziója `0.4.0-phase2d`. A fájlt az
`eng/export-openapi.ps1` validálja és exportálja; kézzel nem szerkesztendő.
