# Phase 3 – kötelező elfogadási és aranyteszt-forgatókönyvek

## S-001 Egyszerű egytelephelyes hét

- 1 aktív telephely, H–P 08:00–20:00;
- 2 gyógyszerész;
- 2 szakasszisztens;
- coverage: 1 gyógyszerész + 1 szakasszisztens teljes nyitvatartásban;
- délelőtti és délutáni sablon.

Elvárt:

- 100% blocking coverage;
- nincs ütközés;
- egy dolgozó/nap legfeljebb egy folyamatos blokk;
- Draft eredmény.

## S-002 Érintkező műszakok összevonása

Egy dolgozó ugyanazon a telephelyen:

- Work 08:00–14:00;
- Work 14:00–18:00.

Elvárt:

- egy 08:00–18:00 assignment;
- egy Work segment;
- nincs split issue.

## S-003 Work + Overtime

Profil:

- standard shift 480 perc;
- overtime allowed.

Eredmény:

- 08:00–18:00 jelenlét;
- Work 08:00–16:00;
- Overtime 16:00–18:00.

Elvárt:

- egy assignment;
- két segment;
- planned overtime summary 120 perc.

## S-004 Split shift tiltás

- 08:00–14:00;
- 15:00–18:00.

Elvárt:

- nincs ilyen generált eredmény;
- input/korrekció esetén `SPLIT_SHIFT_NOT_ALLOWED`.

## S-005 Több telephely ugyanazon a napon

- A telephely 08:00–14:00;
- B telephely 14:00–18:00.

Elvárt:

- nem generálható ugyanannak a dolgozónak;
- `MULTI_LOCATION_SAME_DAY_NOT_ALLOWED`.

## S-006 Jóváhagyott szabadság

Dolgozó Approved AnnualLeave státuszban.

Elvárt:

- nincs Work/Overtime shift;
- alternatívaként sem jelenik meg;
- leave marker projekcióban látszik.

## S-007 Betegállomány

Reported/Recorded sick leave a napon.

Elvárt:

- a konfigurált aktív státuszok szerint távollét;
- nincs generált műszak.

## S-008 Függő kérelem két üzemmódban

Pending leave:

1. IgnorePending;
2. TreatAsTemporaryAbsence.

Elvárt:

- elsőnél generálható, de pending overlap warning;
- másodiknál nem generálható.

## S-009 Unavailable és Preferred

- A dolgozó 14:00 után Unavailable;
- 08:00–14:00 Preferred.

Elvárt:

- délutáni műszak soha;
- délelőtti választás soft előny.

## S-010 Fixed szabály

Dolgozó kedden 08:00–16:00 A telephelyen Fixed.

Elvárt:

- a műszak szerepel vagy a generálás inputkonfliktussal megáll;
- más telephelyre nem osztható;
- magyarázatban FixedRule.

## S-011 Kompetencia-öröklés

- SpecialistPharmacist fed Pharmacist coverage-et;
- SpecialistAssistant fed Assistant coverage-et.

Elvárt:

- coverage teljesül;
- magyarázatban capability implication.

## S-012 Inaktív telephely

Telephely inaktív, de nyitvatartás és coverage történetileg megmaradt.

Elvárt:

- generátor nem használja;
- projekció Inactive;
- nincs assignment.

## S-013 Lehetetlen blocking coverage

Nincs elegendő gyógyszerész.

Elvárt:

- a generálás Succeeded/Feasible Draftot adhat;
- shortage slack;
- Blocking issue;
- Approve/Publish tiltott;
- nincs hard constraint megsértés dolgozónál.

## S-014 Havi időkeret-egyensúly

Két azonos kompetenciájú dolgozó eltérő már kiosztott órákkal.

Elvárt:

- alacsonyabb terhelésű dolgozó soft előnyt kap;
- magyarázatban HoursBalance;
- max limit soha nem sérül.

## S-015 Hétvégi szabály

- egyik dolgozó nem vállal szombatot;
- másik vállal, havi max 2.

Elvárt:

- első nincs szombaton;
- második legfeljebb 2;
- harmadik alkalomhoz Blocking quota/coverage issue, nem csendes limitátlépés.

## S-016 Ügyelet és készenlét

- coverage/time type explicit OnCallDuty vagy Standby;
- csak engedélyezett profilú dolgozó.

Elvárt:

- megfelelő assignment segment;
- havi alkalomlimit;
- normál Workkal nem tiltott módon fed át;
- magyarázat.

## S-017 Determinizmus

Ugyanaz:

- input snapshot;
- algorithm version;
- options;
- seed.

Elvárt:

- canonical shiftlista azonos;
- input hash azonos;
- explanation reason code-ok azonosak.

## S-018 Lock és részleges újragenerálás

- egy műszak locked;
- egy nap újragenerálása.

Elvárt:

- locked shift változatlan;
- scope többi része változhat;
- teljes időszak validáció újrafut;
- change projection helyes.

## S-019 Reject és alternatíva

- generált shift rejected egy dolgozóra;
- alternatív lekérdezés.

Elvárt:

- rejected dolgozó ugyanarra a scope-ra nem kerül vissza;
- csak hard-valid alternatíva;
- tradeoff score.

## S-020 Publish immutabilitás

- Draft → UnderReview → Approved → Published;
- módosításkísérlet Published planen.

Elvárt:

- elutasítás;
- új Draft clone szükséges;
- audit.

## S-021 Új közzététel archiválja a régit

Azonos szervezeti scope/period új Published verzió.

Elvárt:

- tranzakcióban régi Archived;
- dolgozó csak új Published verziót lát;
- changes projekció elérhető.

## S-022 Jogosultságok

- ManageSchedules, de nincs RunAutoFill;
- RunAutoFill, de nincs ApproveSchedules;
- ApproveSchedules, de nincs PublishSchedules.

Elvárt:

- minden művelet külön 403;
- frontend és backend permission azonos.

## S-023 Tenant boundary

Másik szervezet schedule/run/shift GUID.

Elvárt:

- 404;
- nincs adatkisivárgás.

## S-024 Konkurencia

Két admin ugyanazt a Draftot azonos expectedVersionnel módosítja.

Elvárt:

- egyik siker;
- másik 409;
- nincs silent overwrite.

## S-025 Performance smoke

- 8 telephely;
- 40 dolgozó;
- 31 nap;
- több capability, leave, quota és preference.

Elvárt:

- dokumentált candidate/variable/constraint count;
- max 60 másodperces limit;
- Feasible vagy Optimal, vagy jól kezelt Unknown;
- nincs process crash/native load hiba.
