# Codex feladat – 4. fázis: gépelt AI és diktálás

Előfeltétel: a normál API workflow-k és authorization stabilak.

1. Készíts providerfüggetlen interfészeket LLM és speech-to-text számára.
2. Implementáld a `contracts/ai-command.schema.json` verziózott, szigorú feldolgozását.
3. A modell csak intent/action javaslatot ad; neveket és dátumokat az alkalmazás oldja fel.
4. Implementáld az interpret → resolve → authorize → validate → preview folyamatot.
5. A preview rövid életű, confirmation tokennel és adatverziós snapshot-tal.
6. Implementáld az explicit execute végpontot újravalidálással, idempotenciával, tranzakcióval és audittal.
7. Első action allowlist: saját kérelem, saját betegállomány, saját kérelem visszavonása, saját beosztás lekérdezése; adminnak műszakmódosítás csak megfelelő permissionnel.
8. Kétértelmű név/dátum/telephely esetén ne találgass, adj clarifications listát.
9. Nyers hangfájl alapértelmezésben ne tárolódjon.
10. Helyi Ollama/Whisper adapter külön konfigurálható infrastruktúra legyen; az alkalmazás AI nélkül is működjön.
11. Adj schema, authorization, idempotency, expiry és concurrency teszteket.
