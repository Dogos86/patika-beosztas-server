# Nyitott döntések

Ezeket ne találja ki a Codex; készítsen ADR-t vagy kérjen döntést.

1. A dolgozók otthonról/mobilinternetről is használják-e, vagy csak a patika hálózatáról?
2. Hol fut az API és a PostgreSQL?
3. Pontosan milyen hitelesítési megoldás legyen: same-origin cookie, OpenID Connect szolgáltató vagy más?
4. Van-e legalább két jóváhagyó, vagy szükséges auditált önjóváhagyás?
5. Betegállomány bejelentése jóváhagyandó, tudomásul veendő vagy automatikusan rögzített?
6. Szabadság egy vagy több jóváhagyási lépcsős?
7. Szabadságegyenleg és éves keret része-e az első kiadásnak?
8. Szükséges-e dokumentumfeltöltés? Ha igen, milyen típushoz és megőrzéssel?
9. Milyen értesítési csatornák: alkalmazáson belüli, email, push?
10. Mely régi exportformátumokat kell megtartani?
11. Az ügyelet/készenlét éjfélen átnyúló műszakot hogyan kezeljen?
12. Milyen hard/soft egyéni limitek konfigurálhatók szervezetenként?
13. A függő távolléti kérelmeket a generátor kizárásként, soft büntetésként
    vagy figyelmeztetésként kezelje, és hol konfigurálható ez?
14. Pontosan mely generálási problémák blokkolják a review, jóváhagyás és
    közzététel állapotátmeneteket?
15. Hogyan mérjük és súlyozzuk a hétvégi, délutáni/esti és telephelyi
    terhelés igazságosságát?
16. Részleges újrageneráláskor mi változhat a kijelölt scope-on kívül, és mi
    történjen, ha rögzített műszak miatt nincs megoldás?
17. Meddig érvényes egy generált javaslat elutasítása, és mikor ajánlható fel
    ismét ugyanaz a megoldás?
18. Hogyan verziózzuk a Published beosztást, és milyen értesítés jár egy már
    közzétett időszak változásához?
19. Milyen részletességgel és meddig őrizzük a generálási magyarázatot és az
    alternatív jelöltek pontozását?
20. Mi a hét/két hét/hónap pontos naptári szemantikája, és hogyan definiáljuk
    a hétvégi, délutáni és esti kategóriákat?
