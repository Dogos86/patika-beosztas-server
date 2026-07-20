# ADR-0001 – Hitelesítési és munkamenet-kezelési javaslat

- Állapot: Javaslat
- Dátum: 2026-07-20
- Érintett fázis: 1 – szervezet, auth és jogosultság

## Kontextus

A rendszer PWA frontendből és ASP.NET Core API-ból áll, érzékeny munkaügyi
adatokat kezel, több jogosultsági szinttel és szervezeti adatelválasztással.
Saját tokenformátum és böngészőnek átadott admin/service secret nem
megengedett. Még nyitott, hogy a rendszer internet felől elérhető-e, hol fut,
és rendelkezésre áll-e vállalati OpenID Connect szolgáltató.

Ez az ADR nem vezet be hitelesítést a 0. fázisban; a következő fázis döntését
készíti elő.

## Döntési szempontok

- szabványos, karbantartott protokoll és ASP.NET Core integráció;
- a böngészőben tárolt bearer tokenek és XSS-kitettség minimalizálása;
- CSRF, session-visszavonás és kijelentkezés kezelhetősége;
- szervezet- és permission-claimet nem szabad vakon megbízni;
- helyi/patikai hálózatos és internetes telepítés támogatási költsége;
- auditálható felhasználói identitás és fiókéletciklus.

## Vizsgált lehetőségek

### A. Szerveroldali OpenID Connect + same-origin session cookie

Az API az OIDC Authorization Code flow résztvevője, a provider tokenjei
szerveroldalon maradnak. A PWA csak biztonságos, host-only, `HttpOnly` session
cookie-t kap. A permissionök és a szervezeti tagság az alkalmazás
adatbázisából származik.

Előny: alacsonyabb böngészőoldali tokenkitettség, szabványos SSO/MFA,
központosított fiókéletciklus. Hátrány: elérhető és üzemeltetett IdP kell.

### B. ASP.NET Core Identity + same-origin cookie

Az alkalmazás maga kezeli a helyi fiókokat a támogatott Identity
komponensekkel, saját tokenformátum nélkül.

Előny: zárt hálózatban külső IdP nélkül is működhet. Hátrány: jelszó-reset,
MFA, lockout, incidenskezelés és fiókéletciklus üzemeltetési terhe a
rendszerre kerül.

### C. PWA-ban kezelt OIDC bearer token

Authorization Code + PKCE után a frontend bearer tokent küld az API-nak.

Előny: külön domainen futó SPA/API esetén gyakori. Hátrány: a tokenkezelés és
XSS-kitettség összetettebb, a same-origin céltelepítésnél nem ad szükséges
előnyt.

### D. Saját jelszó- vagy tokenrendszer

Elutasítandó. Ellentétes a biztonsági invariánsokkal.

## Javaslat

Alapértelmezett irány az **A lehetőség**:

- PWA és API azonos eredet mögött;
- szabványos OIDC Authorization Code flow, szerveroldali tokenkezeléssel;
- `__Host-` prefixű, `Secure`, `HttpOnly`, megfelelő `SameSite` beállítású,
  rövid életű és rotálható munkamenet-cookie;
- cookie-auth mutációknál antiforgery/CSRF védelem;
- ASP.NET Core policy-based authorization minden érzékeny végponton;
- permission és `OrganizationId` alkalmazásoldali feloldása minden kérésnél;
- önkiszolgáló végpontokon az `EmployeeId` kizárólag a hitelesített
  felhasználóhoz kötött rekordból származhat;
- session-, bejelentkezési és jogosultságváltozási audit események;
- rate limit a bejelentkezési felületen;
- sem access token, sem service secret nem kerül böngésző storage-ba.

Ha igazoltan nincs használható IdP és a rendszer zárt hálózaton működik, a
**B lehetőség** lehet a jóváhagyott fallback, ugyanazzal a cookie-, CSRF- és
authorization-modellel.

## Következmények

- A szakmai `ProfessionalRole` nem lesz auth role.
- A provider claimjei önmagukban nem helyettesítik a helyi
  szervezet-/permission-ellenőrzést.
- Az API contract `cookieAuth` vázlata megtartható, de a cookie neve és session
  tárolása csak az ADR elfogadása után véglegesíthető.
- CORS alapértelmezésben nem szükséges same-origin esetben; fejlesztői kivétel
  csak szűk allowlisttel adható.
- Az auth megvalósítása előtt dönteni kell a hálózati elérésről, hostingról,
  IdP-ről, felhasználói provisioningről és vészhelyzeti admin hozzáférésről.

