# Szabadság- és távolléti folyamatok

## Szabadságigény

1. Dolgozó létrehozza saját kérelmét.
2. A rendszer konkrét időintervallumot és típust tárol.
3. Státusz `Pending`.
4. A jóváhagyó látja az érintett műszakokat és lefedettségi hatást.
5. Jóváhagyáskor a rendszer távollétet rögzít és jelzi a beosztási konfliktust.
6. Elutasításkor indok adható.
7. A dolgozó értesítést kap.
8. Függő kérelem visszavonható.

## Fizetés nélküli szabadság

Első körben ugyanaz a workflow, de külön `LeaveType`. A szükséges dokumentáció vagy többszintű jóváhagyás későbbi bővítés.

## Betegállomány

Nem diagnosztikai modul.
- Dolgozó bejelenti saját magára.
- Admin más nevében rögzítheti.
- Kezdő dátum kötelező, végdátum később is pontosítható.
- Diagnózist és részletes egészségügyi adatot nem tárolunk.
- A bejelentés érintett műszakokat és lefedettséget jelöl.
- Szervezeti döntés, hogy formális jóváhagyás vagy adminisztratív tudomásulvétel kell; ezt konfigurálható workflowként tervezzük.

## Résznap

- óra:perc pontosság;
- kezdés < befejezés;
- a business timezone Europe/Budapest;
- az API date-time értékei egyértelmű offsettel érkezzenek.

## Konkurencia

Döntés és visszavonás optimista zárolással történjen. Ugyanarra a verzióra két döntésből csak az egyik sikerülhet.
