# Frontend architektúra

## Rétegek

```text
┌──────────────────────────────────────────────────────────┐
│ UI komponensek (src/components, src/routes)              │
│  - shadcn/ui alapok, mobile-first layout, magyar címkék  │
├──────────────────────────────────────────────────────────┤
│ Adat-hook réteg (TanStack Query useQuery/useMutation)    │
│  - komponensek innen kérnek/módosítanak adatot           │
├──────────────────────────────────────────────────────────┤
│ Service réteg (src/services/index.ts – lokátor)          │
│  - AuthService, ScheduleService, LeaveRequestService,    │
│    EmployeeService, LocationService, CoverageService,    │
│    NotificationService, AiAssistantService interfészek   │
├──────────────────────────────────────────────────────────┤
│ Implementáció: MOCK (src/services/mock)                  │
│  - in-memory store, seed adatokkal                       │
│  - CSAK ez a réteg importálja a seedet                   │
└──────────────────────────────────────────────────────────┘
```

## Fájlszervezés

```text
src/
  routes/                          TanStack file-based routing
    __root.tsx                     Shell + AuthProvider + Toaster
    index.tsx                      Redirect (/) → /login vagy /app
    login.tsx                      Bejelentkezés + jelszó-visszaállítás
    app.tsx                        Auth-gated layout, AppShell
    app.index.tsx                  Kezdőlap (szerep alapján)
    app.schedule.tsx               Saját beosztás
    app.requests.tsx               Saját kérelmek + új kérelem dialógus
    app.notifications.tsx          Értesítések
    app.settings.tsx               Beállítások
    app.ai.tsx                     AI asszisztens (mock)
    app.admin.approvals.tsx        Jóváhagyások
    app.admin.scheduler.tsx        Beosztásszerkesztő
    app.admin.employees.tsx        Dolgozók lista
    app.admin.employees.$id.tsx    Dolgozó szerkesztő
    app.admin.locations.tsx        Telephelyek
    app.admin.coverage.tsx         Lefedettségi szabályok
  components/
    layout/AppShell.tsx            Sidebar (desktop) + BottomNav (mobil) + Sheet
    common/                        PageHeader, states, StatusBadge
    ui/                            shadcn primitívek
  services/
    types.ts                       Domain típusok
    interfaces.ts                  Service interfészek
    mock/                          Mock implementáció + seed
    index.ts                       Service locator — komponensek CSAK innen
  hooks/
    use-auth.tsx                   AuthProvider + useAuth + useIsAdmin
  lib/format.ts                    Magyar dátumformátumok, címkék
```

## Backend csere (később)

A `src/services/index.ts` exportálja a `services` konstansot. Amikor kész a .NET 10 Web API:

1. Készíts új implementációt (`src/services/http/index.ts`) az `interfaces.ts`-ben definiált interfészekre — minden metódus `fetch`-hez hívást csomagol.
2. Cseréld a lokátort: `export const services: Services = httpServices;`
3. Nem kell komponenst módosítani, mert azok csak az interfészt ismerik.

A base URL-t és tokent érdemes `import.meta.env.VITE_API_URL` és auth interceptor mögé tenni.

## Domain modell — kulcs döntések

- **User vs Employee** különválasztva: egy user opcionálisan hivatkozhat egy employee rekordra (`user.employeeId`). Így egy admin lehet nem-beosztható rendszergazda vagy egyszerre beosztható gyógyszerész.
- **appRoles**: alkalmazásjogosultság (`admin`, `employee`) — az UI ez alapján dönt a route-hozzáférésről.
- **professionalRole**: szakmai kategória (`pharmacist`, `assistant`, `technician`, `intern`).
- **countsAsPharmacist**: külön kapcsoló — nem minden gyógyszerésznek számító dolgozó formális gyógyszerész (pl. asszisztens vezető, aki lefedettségi szempontból mégis annak minősül).
- **schedulable + includeInAutoFill**: két külön kapcsoló, mert lehet valakit beosztani, de az auto-fillből kihagyni.

## Route guard

Csak UX. `src/routes/app.tsx` `useEffect`-tel ellenőrzi a session-t és `/login`-ra irányít, ha nincs. Az admin route-okat jelenleg az AppShell menüjével rejtjük (a `useIsAdmin` alapján); a route-fájlok maguk nem védettek — a majdani backend adja a valódi authorizációt.

## AI asszisztens

A `AiAssistantService.interpret()` mock — kulcsszavak alapján előre elkészített művelet-előnézeteket ad vissza. A UI `Alkalmazás` gombja csak toastot ad, nem módosít adatot. Ez tudatos: az AI-alapú műveletet mindig felhasználói megerősítéshez kötjük.

## PWA

Csak metaadat előkészítés (`theme-color`, mobile viewport). Nincs service worker regisztrálva — a Lovable preview stabilitása miatt sem, és mert a use case (backend nélküli váz) még nem indokolja. Amikor kellenek: home-screen ikonok + manifest, később offline cache.

## Modul-jelzések és guardok (Phase 2E.7)

- `src/components/common/ModuleNotice.tsx` — egységes „demóadat" / „modul nem elérhető"
  figyelmeztetések. Minden még nem éles modul ezt használja API-módban.
- `useRequirePermission` (`src/components/common/PermissionGate.tsx`) minden védett route-on
  fut: beosztás, kérelmek, jóváhagyások, admin beosztás, AI, értesítések.
- A legacy `/app/admin/scheduler` munkatér API-módban le van tiltva, és a valódi
  `/app/admin/schedules` felületre irányít.
