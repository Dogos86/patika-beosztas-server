# Szabadság- és távolléti folyamatok

## Szabadságigény

1. Dolgozó létrehozza saját kérelmét.
2. A rendszer konkrét időintervallumot és típust tárol.
3. A létrehozott kérelem `Draft`, beküldéskor `Pending`.
4. A jóváhagyó `Approved` vagy indoklással `Rejected` állapotba viszi.
5. `Draft` vagy `Pending` kérelem a dolgozó által `Withdrawn` állapotba
   vonható vissza.
6. A jóváhagyott kérelem indoklással `Cancelled` állapotba zárható.
7. Minden állapotátmenet immutable státusztörténetet és auditbejegyzést
   hoz létre.

A műszakokra és lefedettségre gyakorolt hatás, valamint az értesítés a
későbbi beosztási vertikális szelet feladata.

## Fizetés nélküli szabadság

Első körben ugyanaz a workflow, de külön `LeaveType`. A szükséges dokumentáció vagy többszintű jóváhagyás későbbi bővítés.

## Betegállomány

Nem diagnosztikai modul.
- Dolgozó bejelenti saját magára.
- Admin más nevében rögzítheti.
- Kezdő dátum kötelező, végdátum később is pontosítható.
- Diagnózist és részletes egészségügyi adatot nem tárolunk.
- A runtime workflow `Reported` → `Recorded` → `Closed`; lezáráskor a
  végdátum kötelező.
- Betegállományhoz a public contract szabad szöveges dolgozói megjegyzést
  sem fogad el, hogy egészségügyi részlet ne kerülhessen az általános
  távolléti mezőbe.
- A bejelentés műszakokra és lefedettségre gyakorolt hatása későbbi
  beosztási integráció.

## Résznap

- óra:perc pontosság;
- kezdés < befejezés;
- a business timezone Europe/Budapest;
- az API date-time értékei egyértelmű offsettel érkezzenek.

## Konkurencia

Döntés és visszavonás optimista zárolással történjen. Ugyanarra a verzióra két döntésből csak az egyik sikerülhet.
