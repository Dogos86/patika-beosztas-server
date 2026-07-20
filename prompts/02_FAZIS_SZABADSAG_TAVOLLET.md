# Codex feladat – 2. fázis: szabadság és távollét

Előfeltétel: authorization és organization isolation tesztelt.

1. Implementáld a saját szabadságigényt, fizetés nélküli szabadságot és betegállomány-bejelentést.
2. Saját végpontnál ne fogadj el megbízhatóként employeeId-t.
3. Implementáld admin rögzítést más nevében.
4. Implementáld jóváhagyás/elutasítás/visszavonás státuszátmeneteket optimista konkurenciával.
5. Betegállománynál ne legyen diagnózis mező.
6. Hozz létre státusztörténetet, audit eseményeket és értesítési outbox alapot.
7. Ellenőrizd az érintett műszakokat absztrakt schedule query porton keresztül, még a teljes migráció előtt.
8. Frissítsd OpenAPI-t és a szerződésvázlatot.
9. Adj domain, application és integration teszteket.
10. Futtasd build/testet és dokumentáld.
