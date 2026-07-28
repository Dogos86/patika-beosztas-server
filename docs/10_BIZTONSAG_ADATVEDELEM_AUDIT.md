# Biztonság, adatvédelem és audit

## Minimum

- szerveroldali authorization;
- szervezeti adatelválasztás;
- TLS;
- biztonságos hitelesítés, saját tokenformátum nélkül;
- rate limit a bejelentkezés és AI végpontok előtt;
- CSRF-védelem, ha cookie auth;
- szigorú CORS dev kivétellel;
- naplóban ne szerepeljen secret, teljes hang vagy érzékeny szabad szöveg;
- titkos kulcsok konfigurációból/secret store-ból;
- audit fontos módosításokra;
- mentési/visszaállítási terv.

Cookie-auth frontend-integrációnál minden kérés `credentials: "include"`
beállítást használ, a frontend és az API HTTPS-en, azonos site alatt fut, a
mutációk pedig `X-CSRF-TOKEN` headert kérnek. Credentiales CORS csak
konfigurált pontos origin allowlisttel engedhető; `AllowAnyOrigin` tilos.

A reverse proxy, Forwarded Headers, tartós Data Protection kulcstár,
`AllowedHosts` és secret store production követelményeit a
`docs/PHASE_1_5_HARDENING.md` checklistje részletezi.

## Egészségügyi adat minimalizálása

- betegállomány ténye és időtartama kezelhető;
- diagnózis nem tárolható;
- mellékletkezelés csak későbbi külön döntéssel;
- hozzáférés csak szükséges jogosultságoknak;
- megőrzési szabály szervezeti és jogi egyeztetés tárgya.

A Phase 2A public request/response típusai nem tartalmaznak diagnózismezőt.
Nyitott betegállomány tárolható, de betegállományhoz dolgozói szabad szöveg
nem fogadható el. A szervezetidegen azonosítók 404-et adnak; a self-service
employee azonosítóját a szerver a sessionből oldja fel.

## Audit események

Legalább:
- kérelem létrehozása/visszavonása;
- jóváhagyás/elutasítás;
- admin más nevében rögzít;
- műszak létrehozás/módosítás/törlés;
- teljes vagy részleges generálás indítása és befejezése;
- műszak rögzítése/feloldása és generált javaslat elutasítása;
- beosztás review, jóváhagyás, közzététel és archiválás állapotátmenete;
- jogosultság módosítás;
- AI előnézet végrehajtása.

A Phase 2A ezen felül auditálja a WorkPreference létrehozását,
módosítását és inaktiválását, valamint minden LeaveRequest
létrehozást, módosítást és státuszátmenetet. A LeaveStatusHistory és az
AuditLog meglévő sorai alkalmazási mentésen keresztül nem módosíthatók és
nem törölhetők.

A Phase 2B auditálja a heti nyitvatartás upsertjét, a műszaksablon és coverage
követelmény létrehozását, módosítását és inaktiválását, továbbá a dolgozói
capability-készlet, munkaprofil és műszakkvóta minden változását. A rekordok
szervezeti határát kompozit idegen kulcsok is védik; ismert idegen szervezeti
GUID 404-et ad. Minden Phase 2B mutáció cookie-auth, permission policy és
antiforgery ellenőrzés után fut, stale `xmin` esetén 409-cel és auditmutáció
nélkül áll meg.

Az audit tartalmazza az aktort, időpontot, szervezetet, entitást, műveletet és korrelációs azonosítót, de ne másolja be korlátlanul az érzékeny szabad szöveget.

A generálási audit ezen felül az algoritmus verzióját, a kért időszakot és
scope-ot, a bemeneti snapshot stabil referenciáját, az idempotency-referenciát
és az eredmény összefoglalóját rögzíti. A magyarázhatósági adat és az
alternatív jelöltek listája érzékeny dolgozói adat: csak a szükséges admin
jogosultsággal és szervezeti határon belül kérdezhető le, teljes tartalma nem
kerül általános alkalmazásnaplóba.

## Phase 2D payroll-adatvédelem

Az adóazonosítót ASP.NET Core Data Protection titkosítja, a tenanton belüli
duplikációt secret kulcsos HMAC-SHA-256 lenyomat ellenőrzi. A summary/lista
mindig maszkol, teljes értéket kizárólag külön
`ViewPayrollSensitiveData` permissionnel ad a profil részletes végpontja.
Nyers adóazonosító és lenyomat nem kerül auditösszefoglalóba vagy exportba.

Auditált a profil, onboarding summary, belső survey és
nyilatkozat-checklist megtekintése; továbbá minden mutáció, kézi felülírás,
státuszváltás, lezárás és JSON/CSV export. A survey csak jelzőt tárolhat a
személyi kedvezményhez és érintett eltartotthoz: diagnózis, betegségnév,
orvosi dokumentum és feltöltés nincs a public contractban.
