# Legacy karakterizációs tesztterv

## Cél és korlátok

A tesztek a legacy megfigyelt viselkedését rögzítik, nem nyilvánítják azt
helyes célviselkedésnek. A `legacy/current_winforms` alatt sem forrás, sem
projektfájl nem módosítható. Az új tesztek kívülről, projektreferencián
keresztül hivatkoznak a `PharmacyScheduler.Core` assemblyre.

Tesztadat csak determinisztikus, anonimizált fixture lehet. A `DateTime.Today`,
`DateTime.Now` és `Environment.UserName` függő tesztadatokat kerülni kell.

## Meglévő legacy tesztek

`PharmacyScheduler.Tests`:

- `ValidationServiceTests.Validate_ShouldFlagOverlapAsHardIssue`;
- `ValidationServiceTests.Validate_ShouldFlagCoverageShortage`;
- `ValidationServiceTests.Validate_ShouldFlagDailyHoursExceeded`;
- `AutoSchedulerServiceTests.FillCoverageGaps_ShouldCreateEntriesWhenEligibleEmployeeExists`.

Ezek smoke tesztek: többnyire csak azt ellenőrzik, hogy legalább egy adott kódú
hiba vagy legalább egy generált elem létezik.

## A 0. fázisban hozzáadott tesztek

`tests/PatikaBeosztas.Legacy.CharacterizationTests`:

1. `AutoFillReturnsCreatedSlotCountAndMergesAdjacentEntries` – rögzíti, hogy
   08:00–10:00 között a visszatérési érték 4 félórás slot, miközben az
   eredmény egy összevont műszak.
2. `InactiveLocationCoverageRuleIsIgnored` – rögzíti, hogy inaktív telephely
   coverage-szabálya nem hoz létre hiányt.
3. `AutoScheduleRoleOverrideIsNotUsedByCoverageValidation` – rögzíti a jelenlegi
   inkonzisztenciát: az autofill használja a szerep-override-ot, a coverage
   validáció nem.

A harmadik teszt ismert eltérést dokumentál. Az új rendszer célviselkedését a
`CountsAsPharmacist` és a szakmai szerepkör szabályainak tisztázása után külön
tesztnek kell meghatároznia.

## Következő tesztmátrix

| Prioritás | Terület | Rögzítendő esetek |
| --- | --- | --- |
| P0 | Átfedés | azonos dolgozó azonos/eltérő telephelyen; érintkező végpont nem átfedés; másik Draft/Approved beosztás hatása; duplikált hibák pontos száma |
| P0 | Coverage | pontos slotlefedés; részleges slotátfedés; `RequiredCount > 1`; több munka-jellegű időtípus; inaktív telephely; ismeretlen dolgozó |
| P0 | Autofill | `IncludeInAutoSchedule=false`; távollét; engedélyezett telephely/időtípus; determinisztikus név tie-break; tiltott idősáv jelenlegi figyelmen kívül hagyása |
| P0 | Limitek | pontosan limiten; fél órával felette; másik beosztás beszámítása; autofill új slot utáni túllépése |
| P0 | Beosztásmásolás | időszakhossz és napeltolás; új ID-k; Draft státusz; note/időtípus megtartása; jóváhagyási adatok eldobása |
| P1 | 30 perces rács | `:00`, `:30`, más perc, másodperc, start=end, start>end |
| P1 | Távollét | inclusive kezdő/záró nap; nem munka-jellegű bejegyzés; többnapos tartomány |
| P1 | Preferált/tiltott idősáv | több pontosvesszős ablak; hibás token csendes eldobása; contains és fél-nyitott overlap |
| P1 | Havi összesítés | hónaphatár; több beosztás; Work/Overtime/OnCall/StandBy beszámítása |
| P1 | Export-projekció | rendezés; ismeretlen ID-k helyettesítő szövege; csoportosítás; tizedes órák |
| P1 | JSON | ismert fixture round-trip; hiányzó fájl; `null`; sérült JSON; enum és dátum formátum; ismeretlen mező |
| P2 | Excel/PDF | lap- és oszlopnevek; dátum-/óraábrázolás; validációs lista 25-ös korlátja; üres beosztás |

## Tesztelési stratégia a UI-ba ágyazott logikához

`MainForm.CopySchedule`, `ApproveSchedule` és a törlési szabályok privát,
WinForms-hoz kötött metódusok. Reflectionös UI-teszt helyett először:

1. rögzítsünk anonimizált bemenet/kimenet fixture-t a működő programból;
2. írjunk jóváhagyott expected-output tesztet az új, UI-független use case-re;
3. csak ezután hasonlítsuk össze a legacy és az új eredményt;
4. eltérésnél ne módosítsuk a legacy algoritmust, hanem dokumentáljuk a célzott
   változást és kérjünk üzleti döntést.

## Kilépési feltétel a migráció megkezdéséhez

A coverage és autofill kinyerése csak akkor kezdhető el, ha minden P0 eset
automatizált, a jelenlegi ismert hibák külön meg vannak jelölve, és az új
célviselkedés nyitott döntései lezárultak.

