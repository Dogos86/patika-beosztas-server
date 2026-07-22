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

Az audit tartalmazza az aktort, időpontot, szervezetet, entitást, műveletet és korrelációs azonosítót, de ne másolja be korlátlanul az érzékeny szabad szöveget.

A generálási audit ezen felül az algoritmus verzióját, a kért időszakot és
scope-ot, a bemeneti snapshot stabil referenciáját, az idempotency-referenciát
és az eredmény összefoglalóját rögzíti. A magyarázhatósági adat és az
alternatív jelöltek listája érzékeny dolgozói adat: csak a szükséges admin
jogosultsággal és szervezeti határon belül kérdezhető le, teljes tartalma nem
kerül általános alkalmazásnaplóba.
