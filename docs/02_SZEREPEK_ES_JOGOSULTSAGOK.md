# Szerepek és jogosultságok

## Két külön dimenzió

### Application permissions
- ViewOwnSchedule
- ManageOwnLeaveRequests
- ManageWorkPreferences
- ManageAllLeaveRequests
- ApproveLeaveRequests
- RecordLeaveForOthers
- ManageEmployees
- ManageLocations
- ManageCoverageRules
- ManageSchedules
- RunAutoFill
- UseAiAssistant
- ManageUsers

Az 1. fázis API-ja a `ManageEmployees`, `ManageLocations` és `ManageUsers`
policy-ket használja. A Phase 2A-ban a saját távolléti műveletekhez
`ManageOwnLeaveRequests`, a szervezeti listához `ManageAllLeaveRequests`, a
jóváhagyáshoz/elutasításhoz `ApproveLeaveRequests`, a más nevében történő
rögzítéshez és a betegállomány adminisztratív életciklusához
`RecordLeaveForOthers`, a más dolgozó munkapreferenciáinak kezeléséhez
pedig `ManageWorkPreferences` szükséges. A saját munkapreferencia
hitelesített, kapcsolt dolgozóhoz kötött self-service művelet.

A generálás-központú beosztási fázisban a teljes és részleges generálás
`RunAutoFill`, a beosztás állapotátmenetei és korlátozott korrekciói
`ManageSchedules`, a coverage-szabályok karbantartása
`ManageCoverageRules` permissionhöz kötött. Ha egy use case több képességet
érint, minden szükséges permissiont a szerver ellenőriz.

A Phase 2B runtime-ban a heti nyitvatartás és a műszaksablonok kezelése
`ManageLocations`, a dolgozói capability, munkaprofil és műszakkvóta kezelése
`ManageEmployees`, a coverage-szabályok CRUD-ja pedig `ManageCoverageRules`
permissiont igényel. Ezek a permissionök továbbra sem következnek a dolgozó
`ProfessionalRole` értékéből.

A Phase 2D négy, a beosztási jogoktól független payroll permissiont vezet be:
`ManagePayrollOnboarding`, `ViewPayrollSensitiveData`,
`ReviewTaxAllowanceSurvey` és `ExportPayrollData`. A profil részletes
adóazonosítója csak az első és a második permission együttes meglétekor
olvasható; az onboarding summary mindig maszkolt értéket ad. A survey admin
olvasása/review-ja `ReviewTaxAllowanceSurvey`, az export
`ExportPayrollData` policyhez kötött.

### Professional roles
- PharmacyManager
- Pharmacist
- SpecialistAssistant
- Assistant
- PharmacistTrainee
- AssistantTrainee
- Cleaner
- FinanceHelper
- Other

Az egyik nem következik automatikusan a másikból. Az admin lehet gyógyszerész és beosztható dolgozó.

## Szerveroldali szabályok

- Saját műveletnél a dolgozó azonosítóját a hitelesített felhasználóból kell feloldani.
- Egy normál dolgozó nem adhat meg tetszőleges `employeeId`-t saját kérelemhez.
- Admin más nevében csak megfelelő jogosultsággal rögzíthet.
- Minden lekérdezést és mutációt szervezethez kell kötni.
- A felhasználó ne férjen hozzá más szervezet objektumához még ismert GUID esetén sem.
- Az admin saját kérelmének önjóváhagyása külön szervezeti beállítás, alapértelmezésben tiltott.

## Ajánlott jogosultságtesztek

- dolgozó nem olvassa más részletes beosztását;
- dolgozó nem hoz létre kérelmet más nevében;
- admin létrehozhat más nevében;
- approver dönthet, de jogosultság nélkül employee CRUD nem érhető el;
- más szervezet GUID-ja 404/403 eredményt ad, adatszivárgás nélkül;
- admin továbbra is lekérheti saját beosztását.
