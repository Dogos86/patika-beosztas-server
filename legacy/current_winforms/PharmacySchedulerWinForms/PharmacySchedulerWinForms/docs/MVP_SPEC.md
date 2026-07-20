# Beosztás Készítő – MVP Specifikáció (v0)

Ez a dokumentum az első működő prototípus (MVP) scope-ját rögzíti.

## 0. MVP döntések
- Időkezelés: **30 perces rács** (tetszőleges hossz, de 30 percenként „áll meg”).
- Hard szabályok: **mentéskor / jóváhagyáskor** blokkolnak (szerkesztés közben jelzés lehet).

## 1. Képernyők és UX flow
### A) Telephelyek
- telephely lista / szerkesztés
- mezők: név, cím (opcionális), aktív
- nyitvatartás sablon (hét napjaira)
- (opcionális) ügyelet/készenlét idősávok jelölése

### B) Dolgozók
- dolgozó adatlap:
  - teljes név, megjelenítési név
  - születési idő (ha kell exporthoz)
  - szerepkör
  - telephelyhez rendelhetőség
  - havi keret, max napi óra
  - preferált / tiltott idősávok
  - beosztható időtípusok

### C) Szabályok (Coverage)
- telephelyenként idősávokra bontva (30 perc)
- min. X fő szerepkör/kompetencia szerint
- szabályonként **soft/hard** kapcsoló

### D) Beosztás készítés
- nézetek: heti / kétheti / havi
- bal oldalt dolgozó lista (szűrők: telephely, szerepkör, kereső)
- rács: 30 perc tick, intervallum kijelölés húzással
- bejegyzés: időtípus, telephely, megjegyzés
- oldalsó panelek:
  - Coverage: hiány/túltöltés
  - Ütközés: ugyanaz a dolgozó más telephelyen ugyanabban az időben
  - Személy limitek: max napi/havi keret túllépés, preferált idősávon kívül

### E) Jóváhagyás
- státusz: Draft → Approved
- hard szabályok Approved-nál blokkolnak

### F) Export
- PDF export: nézet szerinti (heti/havi)
- Excel export:
  - „Sorlista” (intervallumonként)
  - „Összesítő” (dolgozó × időtípus × telephely)

## 2. Adatmodell v0 (entitások)
### Core
- Location (Telephely): id, name, address?, is_active
- Role (Szerepkör): id, name, is_unique_per_tenant (vezető: true)
- Employee (Dolgozó): id, full_name, display_name, birth_date?, role_id, is_active
- EmployeeLocation: employee_id, location_id, enabled
- EmployeeConstraints: employee_id, monthly_hours_limit, max_daily_hours, preferred_time_windows, forbidden_time_windows?
- TimeType: id, code, name
- Schedule: id, period_start, period_end, status, created_by, created_at, approved_by?, approved_at?
- ShiftEntry: id, schedule_id, location_id, employee_id, date, start_time, end_time, time_type_id, note

### Coverage
- CoverageRule: id, location_id, day_pattern, start_time, end_time, required_role_id, required_count, severity (soft/hard)

## 3. Ellenőrzések
### Hard (blokkol Approved)
- ütközés: egy dolgozó nem lehet két telephelyen ugyanarra az időre beosztva
- rács invariáns: start < end, 30 perces lépésköz, nap-határok

### Soft/Hard (állítható)
- coverage hiány (telephely + idősáv + szerepkör minimum)
- személy limitek: max napi/havi keret, preferált idősáv

## 4. Auto-schedule v1 (heurisztika)
Cél: a hiányzó coverage feltöltése kijelölt dolgozókkal.
- hiány számítás role-onként idősávonként
- priorizálás (legnagyobb hiány / hard szabályos sávok)
- jelöltek szűrése (role, ütközés, limitek)
- pontozás preferenciák alapján
- 30 perces blokkok összefűzése UI-ban

## 5. Export specifikáció (MVP)
### Excel „Sorlista” oszlopok
- Telephely, Dátum, Kezdés, Vége, Dolgozó (teljes név), Szerepkör, Időtípus kód, Óraszám, Megjegyzés

### Excel „Összesítő”
- dolgozó sorok, időtípus oszlopok, összesen
- opcionális telephely bontás

### PDF
- nyomtatható heti/havi rács nézet
