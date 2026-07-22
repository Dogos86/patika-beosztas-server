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

### LEG-007 – Dolgozói időablakok átfedési szemantikája

A Phase 1.5 nem változtatott a meglévő szabályon: az egymást metsző általános
és naphoz kötött időablakok, illetve a különböző típusú metsző időablakok is
ütközésnek számítanak. A preferred/forbidden ablakok jövőbeli prioritás- vagy
felülírási szabálya üzleti döntést igényel; addig ezt nem szabad önkényesen
módosítani.

## Generálás-központú beosztás

A generálás-központú termékirány, a három admin projekció, a korlátozott
korrekciók, valamint a `Generating` → `Draft` → `UnderReview` → `Approved` →
`Published` → `Archived` állapotkészlet eldöntött. Az alábbi részletszabályokat
a Phase 3 érintett szelete előtt kell lezárni.

### GEN-001 – Hard korlátok és soft célok

A közös döntés felsorolja a generátor bemeneteit és a megjelenítendő
problémákat, de nem mondja meg minden elem súlyosságát. Dönteni kell legalább:

- a napi és havi keret hard vagy soft jellegéről;
- a határozott elérhetetlenség megsértésének megengedhetőségéről;
- mely coverage-szabályok blokkolók, illetve csak figyelmeztetők;
- mely hibák blokkolják már a generált Draft létrehozását, és melyek csak a
  review/jóváhagyás/közzététel átmenetet.

### GEN-002 – Függő kérelmek kezelési módja

A generátor konfigurált módon vegye figyelembe a függő kérelmeket, de a módok
nincsenek definiálva. Döntés kell, hogy kizárás, soft büntetés, figyelmen kívül
hagyás vagy más policy választható-e, ki állíthatja, és a beállítás
szervezet-, futás- vagy beosztásszintű-e.

### GEN-003 – Fairness célfüggvény és reprodukálhatóság

Meg kell határozni a hétvégi, délutáni/esti és telephelyi terhelés mérését, a
vizsgált történeti időablakot, a célok egymáshoz és a preferenciákhoz képesti
súlyát, valamint az azonos pontszámú jelöltek determinisztikus tie-break
szabályát.

### GEN-004 – Részleges újragenerálási scope

Nyitott a nap, hét, telephely, szerepkör és kijelölt problémák scope-jának
pontos metszési szabálya. Dönteni kell, mi változhat a scope határán kívül, mi
történik egymást átfedő kijelöléseknél, illetve miként jelez a rendszer, ha a
rögzített műszakok miatt a kiválasztott probléma nem oldható meg.

### GEN-005 – Elutasított javaslat élettartama

Meg kell határozni, hogy egy elutasítás csak az aktuális futásban, az aktuális
Draftban, az adott időszakban vagy tartósabban érvényes-e; kötelező-e indok;
ki vonhatja vissza; és a generátor mikor javasolhatja újra ugyanazt a műszakot.

### GEN-006 – Published verzió és állapotátmenetek

A dolgozó csak Published beosztást láthat, és a munkatérnek az utolsó
közzétett változathoz képesti diffet kell mutatnia. Dönteni kell:

- egy Schedule több immutable revisiont tart-e, vagy az új Draft külön
  Schedule;
- új közzétételkor mi lesz az előző Published verzió státusza;
- módosítható-e Approved beosztás Draftba visszaléptetéssel;
- milyen értesítés jár új közzétételhez és közzétett műszak változásához.

### GEN-007 – Magyarázat és alternatívák megőrzése

Nyitott, hogy a strukturált döntési indokokat és az alternatív jelöltek
pontozását teljesen tároljuk-e, determinisztikusan újraszámoljuk-e, vagy
snapshotolt részhalmazt őrzünk. Rögzíteni kell a megőrzési időt, a jogosult
olvasókat és az auditban tárolható részletességet.

### GEN-008 – Időszak és naptári határok

A hét kezdőnapja, a „két hét” pontos intervalluma, a hónapnézet tört heteinek
kezelése, valamint a hétvégi és délutáni/esti kategóriák szervezeti definíciója
nincs rögzítve. Ezek nélkül a frontend intervallumválasztása, a generátor és a
sorösszesítések eltérően számolhatnak.

## Phase 2A preferenciák és távollétek

### P2A-001 – Átfedő munkapreferenciák prioritása

A runtime az Available/Preferred/Avoid/Unavailable/Fixed rekordok alakját és
tenant-határát validálja, de nem talál ki prioritást egymást átfedő konkrét,
ismétlődő, telephelyes vagy telephelyfüggetlen rekordokra. Ezt a generátor
előtt termékdöntéssel kell lezárni.

### P2A-002 – Távollét hatása és értesítések

A Phase 2A a kérelmet, státusztörténetet, auditot és jogosultságokat
valósítja meg. Az Approved/Cancelled vagy betegállomány-változás meglévő
beosztásra, generátorra és dolgozói értesítésre gyakorolt tranzakciós
hatását a beosztási vertikális szeletben kell meghatározni.

## Phase 2B tervezési alapok

### P2B-001 – Átfedő könyvelési szegmensek

Az érintkező Work és Overtime szegmensek egy jelenléti blokkban egyértelműen
megőrizhetők. Nyitott, hogy időben átfedő, eltérő TimeType szegmenseknél
melyik típus élvez prioritást, megengedett-e a felosztás, és milyen payroll
kódokat/pótléktageket kell képezni. A Phase 2B normalizáló addig stabil
`OVERLAPPING_TIME_TYPES_NOT_ALLOWED` hibával elutasítja ezt az alakot.

### P2B-002 – Nyitvatartási kivételek

A heti ismétlődéshez elkészült a jövőbeli exception draft contract, de dönteni
kell az ünnepnapok, rendkívüli zárások, dátumtartományok, prioritások és az
egymást átfedő kivételek felülírási szabályáról, mielőtt perzisztált entitás és
endpoint készül.

### P2B-003 – CountsAsPharmacist kivezetése

A migráció explicit Pharmacist capabilityre képezi a meglévő igaz értékeket és
a PharmacyManager szerepet, de a kompatibilitási mező még megmarad. Külön
verziózott kliens- és adatmigráció után dönthető el, mikor válik kizárólag a
capability-kapcsolat forrássá, és mikor távolítható el a régi oszlop.

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
