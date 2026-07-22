# Szerepek és jogosultságok

## Két külön dimenzió

### Application permissions
- ViewOwnSchedule
- ManageOwnLeaveRequests
- ManageAllLeaveRequests
- ApproveLeaveRequests
- ManageEmployees
- ManageLocations
- ManageCoverageRules
- ManageSchedules
- RunAutoFill
- UseAiAssistant
- ManageUsers

Az 1. fázis API-ja ezek közül a `ManageEmployees`, `ManageLocations` és
`ManageUsers` policy-ket használja. A többi permission a következő
vertikális szeletek stabil neve.

A generálás-központú beosztási fázisban a teljes és részleges generálás
`RunAutoFill`, a beosztás állapotátmenetei és korlátozott korrekciói
`ManageSchedules`, a coverage-szabályok karbantartása
`ManageCoverageRules` permissionhöz kötött. Ha egy use case több képességet
érint, minden szükséges permissiont a szerver ellenőriz.

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
