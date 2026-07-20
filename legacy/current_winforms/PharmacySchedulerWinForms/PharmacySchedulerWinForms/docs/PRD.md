# Beosztás Készítő – Termék- és Követelmény Specifikáció (PRD)

Ez a dokumentum a gyógyszertári személyzet beosztáskészítő rendszerének üzleti követelményeit és funkcionális elvárásait foglalja össze.

## 1. Cél
Olyan alkalmazás készítése gyógyszertárak számára, amellyel a **munkaidő**, **szabadság**, **betegszabadság** és kapcsolódó időtípusok beosztása gyorsan elkészíthető, hatékonyan szerkeszthető, majd **Excel**-be és **PDF**-be exportálható. Később könyvelői rendszerek felé interfész is készül.

## 2. Kontextus – gyógyszertári működés
### 2.1 Nyitvatartás
A gyógyszertárak nyitvatartása szélsőségesen változatos lehet:
- csak bizonyos napokon, pár órában nyitva tartó fiókpatikák
- hétköznap nyitva tartó patikák
- hétvégi nyitvatartás
- 0–24 nyitvatartás
- ügyelet / készenlét kezelés

### 2.2 Szerepkörök (példák)
- gyógyszertárvezető (dedikált, 1 fő)
- gyógyszerész, helyettes gyógyszerész
- expediáló szakasszisztens, nem expediáló asszisztens
- helyettes szakasszisztens
- gyakornokok (asszisztens/szakasszisztens/gyógyszerész)
- takarító, pénzügyi kisegítő, egyéb kisegítők

### 2.3 Munkaidő típusok (példák)
- munkaidő
- túlóra
- ügyelet
- készenlét
- szabadság
- betegszabadság
- fizetetlen szabadság
- szülési szabadság (típusok + megjegyzés igény, exportban is megjelenjen)

> Elvárás: állítható legyen, hogy egyes szerepkörök milyen időtípusokra oszthatók be.

## 3. Felhasználók / dolgozók kezelése
### 3.1 Dolgozó adatok
- teljes név (exporthoz)
- megjelenítési név / felhasználói név (UI-ban)
- születési idő (exporthoz)
- beosztás / szerepkör
- havi munkaidő keret
- maximum napi munkaidő
- preferált napszak / idősáv (pl. délelőtt, délután, ügyelet vagy konkrét órától–óráig)
- választható időtípusok (munkaidő, túlóra, készenlét, szabadság, betegszabi, stb.)

### 3.2 Dolgozó státuszok
- aktív (beosztható)
- szabadságon van (dátumtól–dátumig)
- betegszabadságon van (dátumtól–dátumig)
- fizetetlen szabadságon van (dátumtól–dátumig)
- szülési szabadságon van (dátumtól–dátumig)

## 4. Erőforrás igény (coverage) beállítás
Telephelyenként, napokra és 30 perces idősávokra bontva állítható legyen:
- melyik nap melyik idősávjában
- melyik szerepkörből
- hány fő szükséges

További elvárások:
- naptári napokra egyedi eltérés (ünnepnap, rendkívüli nap)
- ismétlődő kivétel (pl. minden hónapban adott napokon – nyugdíj környékén)

## 5. Beosztás készítés és szerkesztés
- heti / kétheti / havi beosztás
- időszak másolás (heti/kétheti/havi intervallumok)
- szerkesztés közben folyamatos visszajelzés: hol mi hiányzik a beállított erőforrás igényhez képest
- „optimális beosztás” készítés:
  - dátumintervallum megadása
  - dolgozók kijelölése
  - automatikus kitöltés

## 6. Export és integráció
- export **Excel**-be és **PDF**-be
- később könyvelői interfész: tipikusan szükséges mezők
  - dolgozó azonosító
  - időtípus kód
  - dátum
  - óraszám
  - megjegyzés
  - telephely

## 7. Technológiai irány (későbbi átírás cél)
- BE: C# / .NET 10+
- Server DB: MySQL → PostgreSQL
- Local DB: SQLite
- FE: React WebApp, Vite
- State: Redux Toolkit (ha van)
- Fetching: Axios / React Query + SignalR
- UI: Material UI / Ionic UI
- Desktop: Electron, Mobile: Capacitor
- Auth: OAuth2, B2B/B2C szeparáció később

