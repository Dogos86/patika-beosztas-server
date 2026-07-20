# 1. fázis – implementációs döntések

## Hatókör

Ez a fázis kizárólag a szervezet, Identity-fiók, permission, dolgozó,
telephely, session és audit vertikális szeletét valósítja meg. Szabadság-,
beosztás-, coverage-, autofill-, AI-, diktálás- és exportfolyamat nincs benne.
A `legacy/current_winforms/` tartalma nem módosult.

## Rétegek

- Domain: szervezet, dolgozó, telephely, beoszthatósági beállítások, enumok,
  permission-kapcsolat, audit és időablak-invariánsok.
- Application: permission policy-nevek és bemeneti üzleti validáció.
- Contracts: kizárólag publikus request/response DTO-k; nincs EF entitás.
- Infrastructure: ASP.NET Core Identity, EF Core/Npgsql DbContext,
  konfiguráció, migráció, permission handler, audit writer és Development seed.
- API: cookie/session, CSRF, rate limit, tenant-szűrt admin endpointok,
  ProblemDetails és OpenAPI.

## Tenant és authorization

Az OrganizationId egyik admin request DTO-ban sem szerepel. Az aktort az
Identity session `NameIdentifier` claimje alapján az adatbázisból tölti be az
API. Minden Employee, Location és ApplicationUser query explicit
`OrganizationId == actor.OrganizationId` feltételt tartalmaz; nincs kizárólagos
global query filterre hagyatkozás. Más szervezet ismert GUID-ja 404-et ad.

A permission policy handler minden kérésnél az adatbázisból ellenőrzi, hogy:

- a user aktív;
- a szervezet aktív;
- a permission rekord ugyanahhoz a szervezethez tartozik.

Az aktív-user middleware a cookie élettartamától függetlenül azonnal 401-et ad
deaktivált user vagy szervezet esetén.

## Cookie, CSRF és CORS

Az aktív fázisprompt döntése alapján ASP.NET Core Identity email/jelszó +
same-origin cookie készül. A session cookie `__Host-PatikaSession`, `Secure`,
`HttpOnly`, `SameSite=Lax`, 8 órás és sliding. A CSRF az ASP.NET Core
antiforgery megoldása: a request token a `GET /api/auth/csrf` válaszában jön,
és minden POST/PUT mutáció `X-CSRF-TOKEN` headert validál.

A Development CORS allowlist konfigurációból érkezik, credentialst enged, és
nem használ `AllowAnyOrigin` beállítást.

## Konkurencia és inaktív telephely

Employee és Location a PostgreSQL `xmin` rendszeroszlopot használja
concurrency tokenként. A PUT request `expectedVersion` értéket kér; eltérés és
EF concurrency exception 409 ProblemDetails választ ad.

Az inaktív telephely nem törlődik: listázható `includeInactive=true` mellett és
szerkeszthető marad. A későbbi coverage/autofill rétegnek kötelező lesz
kizárnia az aktív számításból.

## CountsAsPharmacist

A dokumentumok külön szakmai szerepet és coverage-képességet írnak elő, de
nem rögzítenek merev megfeleltetést. Ezért az eltérés engedélyezett, az
Employee válasz `warnings` listája jelzi. A következő coverage-fázis előtt a
pontos képességmátrix továbbra is nyitott döntés.

## Audit és érzékeny adatok

Dolgozó-, telephely-, user-, permission-, link- és státuszmódosítás, valamint
login/logout auditált. Az audit összefoglalója rövid; nem tartalmaz jelszót,
tokent, teljes request body-t vagy diagnózist. A DbContext tiltja az AuditLog
módosítását és törlését.

## Fióklétrehozás korlátja

Az első fázis admin API-ja kezdeti jelszóval hoz létre helyi Identity-fiókot.
Meghívó, email-küldés, jelszó-visszaállítás és MFA még nincs. Production
bevezetés előtt ezeket, a provisioninget és a vészhelyzeti admin eljárást
külön dönteni és implementálni kell.
