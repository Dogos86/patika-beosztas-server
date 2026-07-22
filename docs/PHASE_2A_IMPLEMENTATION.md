# Phase 2A runtime implementáció

## Megvalósított vertikális szeletek

- WorkPreference self-service és admin CRUD/inaktiválás;
- LeaveRequest self-service, admin rögzítés, normál és betegállomány
  állapotgép;
- immutable LeaveStatusHistory és minden fontos mutáció auditja;
- PostgreSQL `xmin` optimista konkurencia;
- szervezethez kötött kompozit idegen kulcsok;
- cookie auth, szerveroldali permission/ownership és CSRF minden mutáción;
- EF Core migráció és futó OpenAPI 0.2.0-phase2a;
- domain-, application- és PostgreSQL/Testcontainers tesztek.

## Adatminimalizálás

A public contractban nincs diagnózismező. Betegállományhoz az
`EmployeeNote` sem fogadható el, így az általános megjegyzésmezőn keresztül
sem kerülhet egészségügyi szabad szöveg az adatbázisba. Nyers hang vagy
melléklet nem része ennek a szeletnek.

## Ismert korlátok

- A preferenciák egymás közötti prioritása és átfedéskezelése nyitott
  termékdöntés.
- A távollét még nem módosít beosztást, nem indít újragenerálást és nem
  küld értesítést.
- Az admin saját kérelmének jóváhagyására vonatkozó szervezeti policy még
  nincs implementálva; ezt a jogosultság és tenant-határ önmagában nem
  helyettesíti.
- Melléklet- és dokumentumkezelés nincs.
