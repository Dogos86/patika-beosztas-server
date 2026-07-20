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
- beosztás jóváhagyás;
- jogosultság módosítás;
- AI előnézet végrehajtása.

Az audit tartalmazza az aktort, időpontot, szervezetet, entitást, műveletet és korrelációs azonosítót, de ne másolja be korlátlanul az érzékeny szabad szöveget.
