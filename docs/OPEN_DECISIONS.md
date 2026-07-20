# Nyitott döntések – audit kiegészítés

A termékszintű nyitott kérdések elsődleges listája:
`docs/12_NYITOTT_DONTESEK.md`. Ez a fájl az audit során talált, konkrét
kódbázis- és migrációs döntéseket rögzíti az `AGENTS.md` által előírt néven.

## Repository és provenance

### AUD-001 – Canonical legacy könyvtár és solution – lezárva az 1. fázisban

A tényleges `legacy/current_winforms/` név lett dokumentálva, átnevezés nélkül.
A canonical referencia a két szinttel beljebb található, buildelhető
`PharmacyScheduler.sln`. A hibás felső solution és a ZIP archívumként megmarad,
de az új build nem hivatkozik rá. Legacy fájl nem módosult.

### AUD-002 – A legacy példány „legfrissebb” státusza

A belső forrás időbélyege újabb a ZIP-nél, de nincs release tag,
forrás-repository hivatkozás vagy kiadói checksum. Tulajdonosi megerősítés kell,
hogy ez a migrálandó verzió.

## Legacy viselkedés

### LEG-001 – Szakmai szerep és coverage-képesség megfeleltetése

A legacy autofill `AutoScheduleRoleOverride`-ot, a validátor viszont pontos
`Employee.Role` egyezést használ. A célmodell külön `CountsAsPharmacist`
zászlót ír elő. Dönteni kell a régi szerepek cél-`ProfessionalRole` és coverage
képesség megfeleltetéséről.

### LEG-002 – Más beosztások beszámítása

A validáció és autofill minden más Schedule összes műszakát figyelembe veszi,
Draft/Approved különbség nélkül. Döntés kell, hogy alternatív piszkozatok,
jóváhagyott időszakok és archivált beosztások hogyan hassanak az átfedésre,
limitekre és autofillre.

### LEG-003 – Autofill ismert eltérései

Dönteni kell, hogy célhibának tekintendő-e:

- tiltott idősávra jelölt dolgozó kiválasztása, majd utólagos hard hiba;
- napi/havi limit átlépésének megengedése és utólagos jelzése;
- a visszatérési számláló slotot, nem összevont műszakot jelent.

### LEG-004 – Beosztásmásolás contract

Nyitott, hogy másoláskor:

- csak műszakok vagy kapcsolódó metaadatok is másolódnak;
- távollétek és új coverage szabályok azonnal blokkolnak-e;
- kell-e preview és részleges másolás;
- jóváhagyott forrásból mindig Draft cél keletkezik-e.

### LEG-005 – Egyetlen gyógyszertárvezető

A WinForms `MainForm.EnsureRoleConstraints` legfeljebb egy vezetőt enged, és a
továbbiakat csendben gyógyszerésszé alakítja. A cél dokumentumok ezt nem írják
elő. Döntésig ezt a szabályt nem szabad átvinni.

### LEG-006 – Exportok

Meg kell nevezni a megtartandó Excel/PDF formátumokat, az oszlopok stabilitását,
a könyvelői igényeket, valamint azt, hogy exportálható-e Draft beosztás.

## Hitelesítés és üzemeltetés

Az 1. fázis aktív promptja az első implementációra ASP.NET Core Identity +
same-origin cookie megoldást rögzített. Hosszabb távú production döntéshez
továbbra is válasz kell legalább ezekre:

- internetes vagy csak patikai hálózati elérés;
- API/PostgreSQL hosting;
- rendelkezésre álló OIDC provider és MFA;
- felhasználói provisioning/deprovisioning;
- vészhelyzeti admin hozzáférés;
- session megőrzési és visszavonási követelmények.
