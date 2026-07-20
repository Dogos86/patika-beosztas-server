# Patika Beosztás – Codex szervercsomag

Ez a csomag a **VS Code + Codex** számára készült. A cél az ASP.NET Core .NET 10 backend és a régi WinForms logika biztonságos migrációja.

## Pontos használat

1. Hozz létre vagy klónozz egy üres GitHub repót, például `patika-beosztas-server` néven.
2. Csomagold ki ezt a csomagot a repository gyökerébe.
3. A legfrissebb működő WinForms projektet másold a `legacy/current-winforms/` mappába. Az eredeti forrás maradjon érintetlen.
4. Nyisd meg a repositoryt VS Code-ban.
5. A Codexnek először a `prompts/00_FAZIS_AUDIT_ES_SKELETON.md` teljes szövegét add át.
6. Az első feladat csak felmérés, karakterizációs tesztek és buildelő solution skeleton. Ne kérd rögtön az egész rendszer megírását.
7. Minden fázis után nézd át a diffet, futtasd a teszteket, commitolj és pusholj.

## Fázisok

1. Audit és skeleton
2. Szervezet, felhasználók, jogosultságok
3. Szabadság- és távollétkezelés
4. Régi beosztásmotor migrációja
5. AI- és diktálási adapterek

A `contracts/` mappában lévő fájlok a frontendcsomagban is azonos változatban szerepelnek.
