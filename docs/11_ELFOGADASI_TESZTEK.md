# Elfogadási és tesztforgatókönyvek

## Jogosultság

1. Dolgozó saját szabadságigényt létrehoz – siker.
2. Dolgozó más `employeeId`-jával próbál létrehozni – a szerver figyelmen kívül hagyja/megtagadja.
3. Dolgozó más részletes beosztását kéri – megtagadva.
4. Admin más nevében rögzít – auditálva sikeres.
5. Admin saját beosztása lekérhető.
6. Más szervezet GUID-ja nem szivárogtat adatot.

## Kérelem

1. Teljes napos szabadság → Pending.
2. Résznapos kérelem percre pontosan.
3. Függő kérelem visszavonás.
4. Jóváhagyás és értesítés.
5. Elutasítás indokkal.
6. Két párhuzamos döntésből egy kap 409-et.
7. Betegállománynál nincs diagnózis mező/adat.

## Beosztás

1. 30 perces rácshoz nem illeszkedő műszak elutasítva vagy normalizálási szabály szerint kezelve.
2. Azonos dolgozó két ismert telephelyen átfed – blokkoló hiba.
3. Inaktív telephely coverage-e nem kerül aktív hiányszámításba.
4. Gyógyszertárvezető `CountsAsPharmacist=true` esetén lefedettségbe számít.
5. Blokkoló hiba mellett jóváhagyás sikertelen.
6. Figyelmeztetés mellett jóváhagyás engedhető.

## AI

1. Relatív dátum konkrét dátummal jelenik meg előnézetben.
2. Azonos név esetén tisztázást kér.
3. Jogosulatlan action nem hajtható végre.
4. Ismeretlen action/mező sémahibát ad.
5. Előnézet nélkül nincs végrehajtás.
6. Lejárt előnézet 409.
7. Ugyanaz az idempotency key nem duplikál műveletet.
8. Hangfájl alapértelmezésben nem marad meg.
