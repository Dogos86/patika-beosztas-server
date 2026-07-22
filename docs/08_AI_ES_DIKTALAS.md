# AI és diktálás architektúra

## Folyamat

```text
szöveg vagy hang
→ beszédfelismerés
→ szerkeszthető átirat
→ AI interpretáció strukturált sémába
→ név/dátum/telephely feloldás alkalmazáskódban
→ authorization + business validation
→ előnézet tárolása rövid lejárattal
→ explicit confirmation
→ ismételt validáció
→ tranzakció + audit
```

## Interfészek

- `ISpeechToTextProvider`
- `IAiCommandInterpreter`
- `IAiCommandResolver`
- `IAiCommandPreviewService`
- `IAiCommandExecutor`

Provider implementációk cserélhetők:
- helyi Whisper kompatibilis STT;
- helyi Ollama kompatibilis LLM;
- később más provider.

## Biztonság

- a modell nem kap közvetlen adatbázis-hozzáférést;
- modell által visszaadott GUID nem megbízható;
- action allowlist és JSON Schema;
- unknown mezők elutasítása;
- authorization actionönként;
- előnézet lejár és adatváltozáskor érvényét veszti;
- confirmation token szerveroldalon védett;
- idempotencia;
- audit;
- nyers hang alapértelmezésben nem tárolódik.

## Relatív dátum

Az interpretáció bemenete tartalmazza:
- aktuális abszolút dátum/idő;
- Europe/Budapest időzóna;
- felhasználó nyelve: hu-HU.

Az előnézet mindig konkrét dátumot ír ki.

## Kezdeti action allowlist

Dolgozó:
- saját szabadságigény;
- saját betegállomány bejelentése;
- saját függő kérelem visszavonása;
- saját beosztás lekérdezése.

Admin/beosztáskészítő megfelelő permissionnel:
- más nevében távollét;
- teljes időszak generálása;
- műszak rögzítése vagy feloldása;
- generált javaslat elutasítása;
- alternatív dolgozók keresése;
- részleges újragenerálás napra, hétre, telephelyre, szerepkörre vagy kijelölt
  problémákra;
- dolgozói preferencia módosítása.

Az AI ugyanazokat a generálási és korrekciós application use case-eket hívja,
mint az admin felület. A teljes kézi műszak-CRUD nem kerül be ebbe a fázisba
csak azért, mert AI-csatornán kérhető lenne. A műszak, dolgozó, telephely,
szerepkör és probléma azonosítóját a resolver szervezeten belül oldja fel; a
modell által visszaadott adatbázis-ID nem megbízható.

A `contracts/ai-command.schema.json` 1.1-es vázlata a fenti allowlist
fogalmait használja. A Phase 4-ben minden action paramétereit külön, zárt
sémával kell rögzíteni; addig a fájl nem tekinthető végrehajtható AI-API
szerződésnek.
