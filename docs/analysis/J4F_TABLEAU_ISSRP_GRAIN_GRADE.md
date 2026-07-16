# J4.f — Tableau de validation ISSRP au grain GRADE + constats de conception

> **Statut :** v1.3 — 16/07/2026 — jalon B **validé intégralement** (§ 3bis) et Q-C3
> **résolue** (§ 3) : seed ISSRP complet, **185/185 grades**. Reste ouvert :
> exhaustivité de la matrice au regard de `ISSRP_Regles_Metier.md` (§ 9, recommandation,
> non engagée).
> **Rôle :** exécute le protocole de validation V-1/Q-C2 défini dans
> `docs/analysis/J4E_DOSSIER_CONCEPTION_AFFECTATION.md` § 6.4 : correspondance
> grade → code, au grain GRADE (validé jalon A), source = CSV réglementaire
> (`Reglementation/elements_paie_historique_14726/Cascade_Corps_Grades_30526.csv`).
> **Rien n'est seedé tant que ce document n'est pas validé.**

---

## 1. Résumé exécutif

En construisant la table de correspondance demandée, deux défauts ont été trouvés dans
l'outillage de seed existant — aucun n'a été corrigé silencieusement, conformément au
principe d'abstention (ADR-0009 proposé) :

| # | Défaut | Statut | Impact sur ce livrable |
|---|---|---|---|
| 1 | `CsvCascadeParser` : un champ entre guillemets contenant un retour chariot littéral (grade n° 134) scindait l'enregistrement en 2 fragments de mauvaise arité, tous deux silencieusement rejetés — le grade disparaissait du seed sans erreur. | **Corrigé** (§ 2) — reconstruction d'enregistrement logique consciente des guillemets + tests de régression. Suite complète verte (283/283). | Grade 134 récupéré ; table ci-dessous complète pour ce cas. |
| 2 | 4 grades ont `Categorie ∈ {HC-S1, HC-S2}` (« hors catégorie », échelle indiciaire distincte) alors que `CascadeRow.Categorie` est typé `int` — `int.Parse` échoue, la ligne est silencieusement écartée du seed (comportement volontaire du parseur pour les lignes parasites, mais ici il masque une vraie donnée). | **Résolu (§ 3, 16/07/2026)** — indices réels trouvés dans `Liste_Grades_Fr.csv`, seed supplémentaire ciblé (`Grades`/`Categories`/`GrilleIndiciaire`), `CascadeRow`/`CsvCascadeParser` non touchés. | Les 4 grades sont désormais **OK** dans le tableau § 5 et intégrés aux groupes DNF ISSRP § 6. Seed complet à 185/185. |

Un troisième constat, **non bloquant pour ce livrable** grâce à la décision Q-C2 (grain
GRADE), est documenté en § 4 pour mémoire : 11 collisions d'acronymes dans
`Corps.Id` faussent aujourd'hui la table `Corps` (mais pas `Grades`, dont l'Id inclut le
`NumOrd` et reste unique à 100 %).

## 2. Correctif CsvCascadeParser (mécanique, appliqué)

**Cause.** Le parseur lisait le CSV ligne physique par ligne physique
(`TextReader.ReadLineAsync`) puis découpait par `;` sans tenir compte des guillemets. Le
grade n° 134 est stocké ainsi dans la source (octets bruts) :

```text
134;...;"Inspecteur de l'orientation et de la
guidance scolaire et professionnelle";15;666;716;791;866
```

Le champ `Grades` est bien entre guillemets et contient un retour chariot littéral — valide
en CSV, mais la lecture ligne à ligne coupait l'enregistrement en deux fragments de 7 et 5
colonnes (au lieu de 12), tous deux silencieusement écartés par le garde-fou « mauvaise
arité = ligne ignorée ».

**Correctif** (`tools/PaieEducation.Tools/Seeding/CsvCascadeParser.cs`) :
- `ReadLogicalRecordAsync` fusionne les lignes physiques tant que le nombre cumulé de
  guillemets est impair (à l'intérieur d'un champ ouvert) — les guillemets doublés `""`
  (échappement RFC4180) restent neutres car ils contribuent toujours un nombre pair ;
- `SplitCsvRecord` remplace le `Split(';')` naïf par un découpage conscient des guillemets
  (un `;` entre guillemets n'est plus un délimiteur) ;
- `NormalizeField` remplace les retours chariot littéraux reconstruits par un espace avant
  de collapser les espaces multiples (comportement inchangé sinon).

**Tests ajoutés** (`tests/PaieEducation.Tests.Tools/CsvCascadeParserTests.cs`) :
régression exacte du cas n° 134, et un cas générique (`;` entre guillemets non scindé).
Aucune régression sur les tests existants (header multi-ligne, accents, espaces multiples,
arité invalide, CSV vide) — suite complète : **283/283 tests verts**, Domain toujours pur.

**Effet mesuré** : le seed nomenclature passe de **180 à 181 lignes lues** sur le CSV réel.

## 3. ✅ Q-C3 résolue (16/07/2026) — Grades « Hors Catégorie » (HC-S1 / HC-S2)

**Contexte.** 4 grades du corps des Inspecteurs de l'enseignement secondaire portent une
catégorie non numérique dans le CSV source (`Cascade_Corps_Grades_30526.csv`) — `HC-S1`
(n° 144, 145, 146) et `HC-S2` (n° 148). `CascadeRow.Categorie` étant typé `int`, le
parseur rejette silencieusement ces 4 lignes.

**Correction de ma recommandation initiale.** J'avais recommandé l'abstention (option c
ci-dessous) en affirmant qu'« aucune donnée d'indice hors-catégorie n'est présente dans le
dossier `Reglementation/` fourni ». C'était une recherche insuffisante, pas une absence
réelle : `Reglementation/Statuts particuliers/Liste_Grades_Fr.csv` (593 lignes, source
plus large que le Cascade CSV) contient les 4 mêmes grades avec des indices réels et la
mention explicite `Hors catégorie-subdivision1` / `subdivision2` :

| Grade | Subdivision | Indice avant 2022-03 | 2022-03 | 2023-01 | 2024-01 |
|---|---|---:|---:|---:|---:|
| Inspecteur ens. secondaire, spécialité disciplines | HC-S1 | 0 (n'existait pas) | 980 | 1055 | 1130 |
| Inspecteur ens. secondaire, spécialité administration lycées | HC-S1 | 0 | 980 | 1055 | 1130 |
| Inspecteur orientation/guidance aux lycées | HC-S1 | 0 | 980 | 1055 | 1130 |
| Inspecteur de l'Education nationale | HC-S2 | 0 | 1040 | 1115 | 1190 |

Les 4 libellés correspondent exactement (vérifié un à un) aux 4 grades bloqués. Trouvaille
supplémentaire : le schéma `Categories`/`GrilleIndiciaire` (V002/V003, antérieur à cette
session) était **déjà conçu** pour ce cas — `Categories.Id TEXT` porte le commentaire
`-- ex. "1".."17", "HC-S1", "HC-S2"`, `Niveau INTEGER CHECK BETWEEN 1 AND 19` réserve
exactement 2 crans au-delà des 17 catégories numériques, et un flag `HorsCategorie`
existe. Aucune migration n'a donc été nécessaire.

**Résolution retenue** (STOP & ASK, confirmée par l'utilisateur le 16/07/2026) :
**seed supplémentaire ciblé**, indépendant de `NomenclatureSeeder`/`CsvCascadeParser`
(non touchés) — nouvelle méthode `InsertGradesHorsCategorieAsync` dans
`ReglementaireSeeder` :
- `Categories` : `HC-S1` (Niveau 18), `HC-S2` (Niveau 19), `HorsCategorie = 1` ;
- `GrilleIndiciaire` : 3 lignes par catégorie (2022-03-01, 2023-01-01, 2024-01-01) — pas
  de ligne avant cette date, l'indice source vaut 0 et `IndiceMin > 0` l'interdit de toute
  façon (ces subdivisions n'existaient pas avant la réforme de 2022) ;
- `Grades` : les 4 lignes (`IDLS-G144`, `IDLS-G145`, `IDLS-G146`, `IDLS-G148`) ;
- `Filieres`/`Corps` (`INSPECTION`/`IDLS`) réutilisent les identifiants déjà dérivés par
  `NomenclatureSeeder` pour ce même corps (`IDLS-G147` existe déjà) — idempotents, aucun
  conflit si les deux seeders tournent ensemble.

**Effet sur le seed ISSRP (§ 6)** : les 4 grades sont intégrés aux groupes DNF —
`IDLS-G144`/`G145`/`G148` → `GE-ISSRP45-DIRECT` (45 %) ; `IDLS-G146` →
`GE-ISSRP30-DIRECT` (30 %) ; les 4 → `GE-ISSRP15-HIST` (historique 2008-2024, RM-042).
Le seed ISSRP est désormais **complet : 185/185 grades**, `GE-ISSRP45-DIRECT` passe à
50 grades, `GE-ISSRP30-DIRECT` à 20. Tests dédiés :
`Seed_insere_les_4_grades_hors_categorie_et_leur_grille`,
`Seed_ISSRP_complet_185_grades_apres_resolution_Q_C3`
(`tests/PaieEducation.Tests.Tools/ReglementaireSeederTests.cs`). 289/289 tests verts.

**Options écartées, pour mémoire :**
- **(a)** Migration formelle de `Categories.Id` vers TEXT — inutile, le schéma l'était déjà.
- **(b)** `CascadeRow.Categorie` nullable pour que le CSV principal gère HC-S1/HC-S2
  nativement — délibérément écarté par l'utilisateur (périmètre plus large pour un gain
  incertain, aucun autre grade « hors catégorie » connu à ce jour dans le CSV principal) ;
  reste possible plus tard si un nouveau cas apparaît.
- **(c)** Abstention — était ma recommandation initiale, devenue caduque dès la donnée
  trouvée (ADR-0009 : abstention seulement en l'absence de règle démontrable).

## 4. Constat non bloquant — collisions d'acronymes `Corps.Id`

`CodeFromCorpsLibelle` dérive un acronyme (1ʳᵉ lettre de chaque mot, max 10) du libellé de
corps. 11 groupes de collision existent sur les 59 corps distincts du CSV — ex. `S` pour
« Secretaires » et « Sous-intendants », `I` pour « Ingenieurs » et « Intendants », `A` pour
« Administrateurs » et « Appariteurs », `ADL` pour trois corps différents. `INSERT ... ON
CONFLICT(Id) DO NOTHING` fait que le second corps arrivé sur un acronyme est silencieusement
absorbé par le premier — la table `Corps` sous-représente aujourd'hui 14 identités réelles
(59 libellés → 45 lignes), et toute requête utilisant `Grades.CorpsId → Corps.Libelle`
peut afficher un corps erroné (ex. un Sous-intendant apparaîtrait comme Secrétaire).

**Sans impact sur ce livrable ni sur le seed ISSRP** : `Grades.Id` inclut le `NumOrd`
(`{CorpsAcronyme}-G{NumOrd:D3}`), donc reste unique à 100 % quelle que soit la collision de
l'acronyme — c'est précisément pourquoi votre décision Q-C2 (grain **GRADE**, pas CORPS)
protège le seed ISSRP de ce défaut. La table § 5 utilise le libellé de corps **de J3G**
(source déjà validée), pas la valeur jointe depuis `Corps` (qui serait parfois fausse).

**Recommandation** (hors périmètre de ce document, à traiter séparément si `Corps.Libelle`
est utilisé ailleurs — ex. UI, rapports) : dériver `Corps.Id` d'un compteur ou d'un hash
court plutôt que d'un acronyme de libellé, ou died-letter les collisions au lieu de `DO
NOTHING`. Je ne le corrige pas ici — c'est une refonte de `NomenclatureSeeder` hors
périmètre du module d'affectation, à valider séparément (§ E du prompt).

## 5. Table de correspondance (185 lignes, grain GRADE, source CSV + J3G)

Colonnes : **N°** = `Num_Ord` du CSV (clé de référence J3G) · **Code GRADE** = `Grades.Id`
tel que seedé (`{CorpsAcronyme}-G{NumOrd:D3}`) · **Grade** = libellé seedé (normalisé,
accents/apostrophes préservés) · **Corps** = libellé source J3G (fiable, voir § 4) ·
**Cat.** = catégorie CSV · **Groupe ISSRP proposé** = classification J3G (v1.0, non
re-tranchée ici) · **Statut** = OK (seedable) / **BLOQUÉ** (Q-C3).

| N° | Code GRADE | Grade (libellé) | Corps (source J3G) | Cat. | Groupe ISSRP proposé | Statut |
|---:|---|---|---|---|---|---|
| 1 | `ADL-G001` | Adjoint de l'Education | Corps des Adjoints de l'Education | 7 | 30 % | OK |
| 2 | `ADL-G002` | Adjoint principal de l'Education | Corps des Adjoints de l'Education | 8 | 30 % | OK |
| 3 | `SDL-G003` | Superviseur de l'Education | Corps des Superviseurs de l'Education | 10 | 30 % | OK |
| 4 | `SDL-G004` | Superviseur principal de l'Education | Corps des Superviseurs de l'Education | 11 | 30 % | OK |
| 5 | `SDL-G005` | Superviseur de l'Education en chef | Corps des Superviseurs de l'Education | 12 | 30 % | OK |
| 6 | `SDL-G006` | Superviseur general de l'Education | Corps des Superviseurs de l'Education | 13 | 30 % | OK |
| 7 | `SDL-G007` | Educateur specialise en soutien Educatif | Corps des Superviseurs de l'Education | 10 | 45 % ou 30 % selon ORIGINE_CORPS | OK |
| 8 | `SDL-G008` | Educateur specialise principal en soutien Educatif | Corps des Superviseurs de l'Education | 11 | 45 % ou 30 % selon ORIGINE_CORPS | OK |
| 9 | `SDL-G009` | Educateur specialise en chef en soutien Educatif | Corps des Superviseurs de l'Education | 12 | 45 % ou 30 % selon ORIGINE_CORPS | OK |
| 10 | `SDL-G010` | Educateur specialise general en soutien Educatif | Corps des Superviseurs de l'Education | 13 | 45 % ou 30 % selon ORIGINE_CORPS | OK |
| 11 | `CDL-G011` | Conseiller de l'Education | Corps des Conseillers de l'Education | 13 | 45 % ou 30 % selon ORIGINE_CORPS | OK |
| 12 | `CDL-G012` | Conseiller principal de l'Education | Corps des Conseillers de l'Education | 13 | 45 % ou 30 % selon ORIGINE_CORPS | OK |
| 13 | `CDL-G013` | Conseiller de l'Education en chef | Corps des Conseillers de l'Education | 14 | 45 % ou 30 % selon ORIGINE_CORPS | OK |
| 14 | `CDL-G014` | Censeur de lycee | Corps des Censeurs de lycée | 14 | 45 % | OK |
| 15 | `C-G015` | Censeur de l'enseignement primaire | Corps des Censeurs | 14 | 45 % | OK |
| 16 | `C-G016` | Censeur de l'enseignement moyen | Corps des Censeurs | 15 | 45 % | OK |
| 17 | `C-G017` | Censeur de l'enseignement secondaire | Corps des Censeurs | 16 | 45 % | OK |
| 18 | `CDLSEP-G018` | Conseiller de l'orientation scolaire et professionnelle (Cat 10) | Corps des Conseillers de l'orientation scolaire et professionnelle | 10 | 30 % | OK |
| 19 | `CDLEDLGSEP-G019` | Conseiller de l'orientation et de la guidance scolaire et professionnelle | Corps des Conseillers de l'orientation et de la guidance scolaire et professionnelle | 12 | 30 % | OK |
| 20 | `CDLEDLGSEP-G020` | Conseiller principal de l'orientation et de la guidance scolaire et professionnelle (Cat 13) | Corps des Conseillers de l'orientation et de la guidance scolaire et professionnelle | 13 | 30 % | OK |
| 21 | `CDLEDLGSEP-G021` | Conseiller analyste de l'orientation et de la guidance scolaire et professionnelle | Corps des Conseillers de l'orientation et de la guidance scolaire et professionnelle | 13 | 30 % | OK |
| 22 | `CDLEDLGSEP-G022` | Conseiller principal de l'orientation et de la guidance scolaire et professionnelle | Corps des Conseillers de l'orientation et de la guidance scolaire et professionnelle | 14 | 30 % | OK |
| 23 | `CDLEDLGSEP-G023` | Conseiller en chef de l'orientation et de la guidance scolaire et professionnelle | Corps des Conseillers de l'orientation et de la guidance scolaire et professionnelle | 16 | 30 % | OK |
| 24 | `CEAS-G024` | Conseiller en alimentation scolaire (Cat 11) | Corps des Conseillers en alimentation scolaire | 11 | 30 % | OK |
| 25 | `CEAS-G025` | Conseiller en alimentation scolaire | Corps des Conseillers en alimentation scolaire | 12 | 30 % | OK |
| 26 | `CEAS-G026` | Conseiller principal en alimentation scolaire | Corps des Conseillers en alimentation scolaire | 13 | 30 % | OK |
| 27 | `CEAS-G027` | Conseiller en chef en alimentation scolaire | Corps des Conseillers en alimentation scolaire | 14 | 30 % | OK |
| 28 | `DDEP-G028` | Assistant du directeur de l'Ecole primaire | Corps des Directeurs des Ecoles primaires | 12 | 45 % | OK |
| 29 | `DDEP-G029` | Directeur de l'Ecole primaire_Cat14 | Corps des Directeurs des Ecoles primaires | 14 | 45 % | OK |
| 30 | `DDC-G030` | Directeur de college_Cat15 | Corps des Directeurs de colleges | 15 | 45 % | OK |
| 31 | `DDL-G031` | Directeur de lycee_Cat16 | Corps des Directeurs de lycees | 16 | 45 % | OK |
| 32 | `DDÈP-G032` | Directeur de l'Ecole primaire_Cat15 | Corps des Directeurs des Ècoles primaires | 15 | 45 % | OK |
| 33 | `DDC-G033` | Directeur de college_Cat16 | Corps des Directeurs de colleges | 16 | 45 % | OK |
| 34 | `DDL-G034` | Directeur de lycee_Cat17 | Corps des Directeurs de lycees | 17 | 45 % | OK |
| 35 | `ADSE-G035` | Adjoint des services Economiques | Corps des Adjoints des services Economiques | 7 | 15 % | OK |
| 36 | `ADSE-G036` | Adjoint principal des services Economiques | Corps des Adjoints des services Economiques | 8 | 15 % | OK |
| 37 | `S-G037` | Sous-intendant | Corps des Sous-intendants | 10 | 15 % | OK |
| 38 | `S-G038` | Sous-intendant gestionnaire | Corps des Sous-intendants | 11 | 15 % | OK |
| 39 | `I-G039` | Intendant | Corps des Intendants | 13 | 15 % | OK |
| 40 | `I-G040` | Intendant principal | Corps des Intendants | 14 | 15 % | OK |
| 41 | `ATDL-G041` | Aide technique de laboratoire | Corps des Aides techniques de laboratoire | 4 | 15 % | OK |
| 42 | `ATDL-G042` | Agent technique de laboratoire | Corps des Agents techniques de laboratoire | 5 | 15 % | OK |
| 43 | `ATDL-G043` | Adjoint technique de laboratoire | Corps des Adjoints techniques de laboratoire | 7 | 15 % | OK |
| 44 | `ADL-G044` | Attache de laboratoire | Corps des Attaches de laboratoire | 8 | 15 % | OK |
| 45 | `ADL-G045` | Attache principal de laboratoire | Corps des Attaches de laboratoire | 10 | 15 % | OK |
| 46 | `ADL-G046` | Attache en chef de laboratoire | Corps des Attaches de laboratoire | 11 | 15 % | OK |
| 47 | `ADL-G047` | Attache superviseur de laboratoire | Corps des Attaches de laboratoire | 12 | 15 % | OK |
| 48 | `A-G048` | Administrateur | Corps des Administrateurs | 12 | NON ÉLIGIBLE | OK |
| 49 | `A-G049` | Administrateur analyste | Corps des Administrateurs | 13 | NON ÉLIGIBLE | OK |
| 50 | `A-G050` | Administrateur principal | Corps des Administrateurs | 14 | NON ÉLIGIBLE | OK |
| 51 | `A-G051` | Administrateur conseiller | Corps des Administrateurs | 16 | NON ÉLIGIBLE | OK |
| 52 | `AA-G052` | Assistant administrateur | Corps des Assistants administrateurs | 11 | NON ÉLIGIBLE | OK |
| 53 | `AD-G053` | Attache d'administration | Corps des Attaches d'Administration | 9 | NON ÉLIGIBLE | OK |
| 54 | `AD-G054` | Attache Principal d'administration | Corps des Attaches d'Administration | 10 | NON ÉLIGIBLE | OK |
| 55 | `AD-G055` | Agent de bureau | Corps des Agents d'Administration | 5 | NON ÉLIGIBLE | OK |
| 56 | `AD-G056` | Agent d'administration | Corps des Agents d'Administration | 7 | NON ÉLIGIBLE | OK |
| 57 | `AD-G057` | Agent Principal d'administration | Corps des Agents d'Administration | 8 | NON ÉLIGIBLE | OK |
| 58 | `AD-G058` | Agent de saisie | Corps des Agents d'Administration | 5 | NON ÉLIGIBLE | OK |
| 59 | `S-G059` | Secretaire | Corps des Secretaires | 6 | NON ÉLIGIBLE | OK |
| 60 | `S-G060` | Secretaire de direction | Corps des Secretaires | 8 | NON ÉLIGIBLE | OK |
| 61 | `S-G061` | Secretaire Principal de direction | Corps des Secretaires | 10 | NON ÉLIGIBLE | OK |
| 62 | `CA-G062` | Aide-comptable administratif | Corps des Comptables administratifs | 5 | NON ÉLIGIBLE | OK |
| 63 | `CA-G063` | Comptable administratif | Corps des Comptables administratifs | 8 | NON ÉLIGIBLE | OK |
| 64 | `CA-G064` | Comptable administratif principal | Corps des Comptables administratifs | 10 | NON ÉLIGIBLE | OK |
| 65 | `TI-G065` | Traducteur-interprète | Corps des Traducteurs interprètes | 12 | NON ÉLIGIBLE | OK |
| 66 | `TI-G066` | Traducteur-interprete spécialisé | Corps des Traducteurs interprètes | 13 | NON ÉLIGIBLE | OK |
| 67 | `TI-G067` | Traducteur-interprète principal | Corps des Traducteurs interprètes | 14 | NON ÉLIGIBLE | OK |
| 68 | `TI-G068` | Traducteur-interprete en chef | Corps des Traducteurs interprètes | 16 | NON ÉLIGIBLE | OK |
| 69 | `I-G069` | Ingenieur d'Etat en Informatique | Corps des Ingenieurs | 13 | NON ÉLIGIBLE | OK |
| 70 | `I-G070` | Ingenieur principal en Informatique | Corps des Ingenieurs | 14 | NON ÉLIGIBLE | OK |
| 71 | `I-G071` | Ingenieur en chef en Informatique | Corps des Ingenieurs | 16 | NON ÉLIGIBLE | OK |
| 72 | `AI-G072` | Assistant Ingenieur de niveau 1 en Informatique | Corps des Assistants Ingenieurs | 11 | NON ÉLIGIBLE | OK |
| 73 | `AI-G073` | Assistant Ingenieur de niveau 2 en Informatique | Corps des Assistants Ingenieurs | 12 | NON ÉLIGIBLE | OK |
| 74 | `T-G074` | Technicien en Informatique | Corps des Techniciens | 8 | NON ÉLIGIBLE | OK |
| 75 | `T-G075` | Technicien superieur en Informatique | Corps des Techniciens | 10 | NON ÉLIGIBLE | OK |
| 76 | `AT-G076` | Adjoint technique en Informatique | Corps des Adjoints techniques | 7 | NON ÉLIGIBLE | OK |
| 77 | `AT-G077` | Agent technique en Informatique | Corps des Agents techniques | 5 | NON ÉLIGIBLE | OK |
| 78 | `I-G078` | Ingenieur d'Etat en Statistiques | Corps des Ingenieurs | 13 | NON ÉLIGIBLE | OK |
| 79 | `I-G079` | Ingenieur principal en Statistiques | Corps des Ingenieurs | 14 | NON ÉLIGIBLE | OK |
| 80 | `I-G080` | Ingenieur en chef en Statistiques | Corps des Ingenieurs | 16 | NON ÉLIGIBLE | OK |
| 81 | `AI-G081` | Assistant Ingenieur de niveau 1 en Statistiques | Corps des Assistants Ingenieurs | 11 | NON ÉLIGIBLE | OK |
| 82 | `AI-G082` | Assistant Ingenieur de niveau 2 en Statistiques | Corps des Assistants Ingenieurs | 12 | NON ÉLIGIBLE | OK |
| 83 | `T-G083` | Technicien en Statistiques | Corps des Techniciens | 8 | NON ÉLIGIBLE | OK |
| 84 | `T-G084` | Technicien superieur en Statistiques | Corps des Techniciens | 10 | NON ÉLIGIBLE | OK |
| 85 | `AT-G085` | Adjoint technique en Statistiques | Corps des Adjoints techniques | 7 | NON ÉLIGIBLE | OK |
| 86 | `AT-G086` | Agent technique en Statistiques | Corps des Agents techniques | 5 | NON ÉLIGIBLE | OK |
| 87 | `DA-G087` | Documentaliste-archiviste | Corps des Documentalistes-archivistes | 12 | NON ÉLIGIBLE | OK |
| 88 | `DA-G088` | Documentaliste-archiviste analyste | Corps des Documentalistes-archivistes | 13 | NON ÉLIGIBLE | OK |
| 89 | `DA-G089` | Documentaliste-archiviste principal | Corps des Documentalistes-archivistes | 14 | NON ÉLIGIBLE | OK |
| 90 | `DA-G090` | Documentaliste-archiviste en chef | Corps des Documentalistes-archivistes | 16 | NON ÉLIGIBLE | OK |
| 91 | `AD-G091` | Assistant documentaliste-archiviste | Corps des Assistants documentalistes-archivistes | 10 | NON ÉLIGIBLE | OK |
| 92 | `AD-G092` | Assistant documentaliste-archiviste principal | Corps des Assistants documentalistes-archivistes | 11 | NON ÉLIGIBLE | OK |
| 93 | `ATEDEA-G093` | Agent technique en documentation et archives | Corps des Agents techniques en documentation et archives | 7 | NON ÉLIGIBLE | OK |
| 94 | `I-G094` | Ingenieur d'Etat-Laboratoire et maintenance | Corps des Ingenieurs | 13 | NON ÉLIGIBLE | OK |
| 95 | `I-G095` | Ingenieur principal-Laboratoire et maintenance | Corps des Ingenieurs | 14 | NON ÉLIGIBLE | OK |
| 96 | `I-G096` | Ingenieur en chef-Laboratoire et maintenance | Corps des Ingenieurs | 16 | NON ÉLIGIBLE | OK |
| 97 | `T-G097` | Assistant ingenieur niv.1-Laboratoire et maintenance | Corps des Techniciens | 11 | NON ÉLIGIBLE | OK |
| 98 | `T-G098` | Assistant ingenieur niv.2-Laboratoire et maintenance | Corps des Techniciens | 12 | NON ÉLIGIBLE | OK |
| 99 | `T-G099` | Technicien-Laboratoire et maintenance | Corps des Techniciens | 8 | NON ÉLIGIBLE | OK |
| 100 | `T-G100` | Technicien Superieur-Laboratoire et maintenance | Corps des Techniciens | 10 | NON ÉLIGIBLE | OK |
| 101 | `AT-G101` | Adjoint technique-Laboratoire et maintenance | Corps des Adjoints techniques | 7 | NON ÉLIGIBLE | OK |
| 102 | `AT-G102` | Agent technique-Laboratoire et maintenance | Corps des Agents techniques | 5 | NON ÉLIGIBLE | OK |
| 103 | `ADL-G103` | Agent de Laboratoire-Laboratoire et maintenance | Corps des Agents de laboratoire | 4 | NON ÉLIGIBLE | OK |
| 104 | `MDLP-G104` | Instructeur | Corps des Maitres de l'Ecole Primaire | 7 | 45 % | OK |
| 105 | `PDLP-G105` | Professeur de l'Ecole primaire | Corps des Professeurs de l'Ecole primaire | 11 | 45 % | OK |
| 106 | `PDLP-G106` | Professeur Principal de l'Ecole primaire | Corps des Professeurs de l'Ecole primaire | 12 | 45 % | OK |
| 107 | `PDLP-G107` | Professeur Formateur de l'Ecole primaire | Corps des Professeurs de l'Ecole primaire | 14 | 45 % | OK |
| 108 | `PDLP-G108` | Maitre de l'Ecole primaire | Corps des Professeurs de l'Ecole primaire | 10 | 45 % | OK |
| 109 | `PDLP-G109` | Professeur de l'enseignement primaire | Corps des Professeurs de l'Ecole primaire | 12 | 45 % | OK |
| 110 | `PDLP-G110` | Professeur de l'enseignement primaire classe 1 | Corps des Professeurs de l'Ecole primaire | 13 | 45 % | OK |
| 111 | `PDLP-G111` | Professeur de l'enseignement primaire classe 2 | Corps des Professeurs de l'Ecole primaire | 14 | 45 % | OK |
| 112 | `PDLP-G112` | Professeur Emérite de l'enseignement primaire | Corps des Professeurs de l'Ecole primaire | 15 | 45 % | OK |
| 113 | `PDLF-G113` | Professeur de l'enseignement fondamental | Corps des Professeurs de l'Enseignement Fondamental | 11 | 45 % | OK |
| 114 | `PDLM-G114` | Professeur de l'enseignement moyen | Corps des Professeurs de l'Enseignement Moyen | 12 | 45 % | OK |
| 115 | `PDLM-G115` | Prof.Coordinateur d'Enseign.Moyen | Corps des Professeurs de l'Enseignement Moyen | 12 | 45 % | OK |
| 116 | `PDLM-G116` | Professeur Principal de l'enseignement moyen | Corps des Professeurs de l'Enseignement Moyen | 13 | 45 % | OK |
| 117 | `PDLM-G117` | Professeur Formateur de l'enseignement moyen | Corps des Professeurs de l'Enseignement Moyen | 15 | 45 % | OK |
| 118 | `PDLM-G118` | Professeur de l'enseignement moyen classe 1 | Corps des Professeurs de l'Enseignement Moyen | 13 | 45 % | OK |
| 119 | `PDLM-G119` | Professeur de l'enseignement moyen classe 2 | Corps des Professeurs de l'Enseignement Moyen | 15 | 45 % | OK |
| 120 | `PDLM-G120` | Professeur Emerite de l'enseignement moyen | Corps des Professeurs de l'Enseignement Moyen | 16 | 45 % | OK |
| 121 | `PTDL-G121` | Professeur technique de lycee, chef d'atelier | Corps des Professeurs Techniques de Lycee | 11 | 45 % | OK |
| 122 | `PTDL-G122` | Professeur technique de lycee, chef de travaux | Corps des Professeurs Techniques de Lycee | 12 | 45 % | OK |
| 123 | `PDLS-G123` | Professeur de l'enseignement secondaire | Corps des Professeurs de l'Enseignement secondaire | 13 | 45 % | OK |
| 124 | `PDLS-G124` | Prof.Coordinateur d'Enseign.Secondaire | Corps des Professeurs de l'Enseignement secondaire | 13 | 45 % | OK |
| 125 | `PDLS-G125` | Professeur Principal de l'enseignement secondaire | Corps des Professeurs de l'Enseignement secondaire | 14 | 45 % | OK |
| 126 | `PDLS-G126` | Professeur Formateur de l'enseignement secondaire | Corps des Professeurs de l'Enseignement secondaire | 16 | 45 % | OK |
| 127 | `PDLS-G127` | Professeur de l'enseignement secondaire classe 1 | Corps des Professeurs de l'Enseignement secondaire | 14 | 45 % | OK |
| 128 | `PDLS-G128` | Professeur de l'enseignement secondaire classe 2 | Corps des Professeurs de l'Enseignement secondaire | 16 | 45 % | OK |
| 129 | `PDLS-G129` | Professeur Emerite de l'enseignement secondaire | Corps des Professeurs de l'Enseignement secondaire | 17 | 45 % | OK |
| 130 | `PDLP-G130` | Prof.Contractuel de l'école primaire | Prof.Contractuel de l'Enseign. Primaire | 12 | 45 % — confirmé (arrêté 6 primes, § 3bis) | OK |
| 131 | `PDLM-G131` | Prof.Contractuel de l'Enseign. Moyen | Prof.Contractuel de l'Enseign. Moyen | 12 | 45 % — confirmé (arrêté 6 primes, § 3bis) | OK |
| 132 | `PDLS-G132` | Prof.Contractuel de l'Enseign. Secondaire | Prof.Contractuel de l'Enseign. Secondaire | 13 | 45 % — confirmé (arrêté 6 primes, § 3bis) | OK |
| 133 | `IDLP-G133` | Inspecteur de l'enseignement primaire | Corps des Inspecteurs de l'enseignement primaire | 15 | 45 % — confirmé (`INSPENSEPRIM`, § 3bis) | OK |
| 134 | `IDLEDLGSEP-G134` | Inspecteur de l'orientation et de la guidance scolaire et professionnelle | Corps des Inspecteurs de l'orientation et de la guidance scolaire et professionnelle | 15 | 30 % | OK (récupéré, § 2) |
| 135 | `IDLM-G135` | Inspecteur de l'enseignement moyen | Corps des Inspecteurs de l'enseignement moyen | 16 | 45 % — confirmé (`INSPENSEMOYE`, § 3bis) | OK |
| 136 | `IDLN-G136` | Inspecteur de l'éducation nationale (Cat 17) | Corps des Inspecteurs de l'Education nationale | 17 | 45 % — confirmé (`INSPEDUCNATI`, § 3bis) | OK |
| 137 | `IDLP-G137` | Inspecteurs enseignement primaire, spécialité disciplines | Corps des Inspecteurs de l'enseignement primaire | 17 | 45 % | OK |
| 138 | `IDLP-G138` | Inspecteur ens. primaire, spécialité administration des écoles | Corps des Inspecteurs de l'enseignement primaire | 17 | 45 % | OK |
| 139 | `IDLP-G139` | Inspecteur ens. primaire, spécialité alimentation scolaire | Corps des Inspecteurs de l'enseignement primaire | 17 | 30 % | OK |
| 140 | `IDLM-G140` | Inspecteur ens. moyen, spécialité disciplines | Corps des Inspecteurs de l'enseignement moyen | 17 | 45 % | OK |
| 141 | `IDLM-G141` | Inspecteur ens. moyen, spécialité administration des collèges | Corps des Inspecteurs de l'enseignement moyen | 17 | 45 % | OK |
| 142 | `IDLM-G142` | Inspecteur orientation/guidance aux collèges | Corps des Inspecteurs de l'enseignement moyen | 17 | 30 % | OK |
| 143 | `IDLM-G143` | Inspecteur gestion financière/matérielle des collèges | Corps des Inspecteurs de l'enseignement moyen | 16 | 15 % | OK |
| 144 | `IDLS-G144` | Inspecteur ens. secondaire, spécialité disciplines | Corps des Inspecteurs de l'enseignement secondaire | HC-S1 | 45 % | OK — résolu Q-C3 |
| 145 | `IDLS-G145` | Inspecteur ens. secondaire, spécialité administration lycées | Corps des Inspecteurs de l'enseignement secondaire | HC-S1 | 45 % | OK — résolu Q-C3 |
| 146 | `IDLS-G146` | Inspecteur orientation/guidance aux lycées | Corps des Inspecteurs de l'enseignement secondaire | HC-S1 | 30 % | OK — résolu Q-C3 |
| 147 | `IDLS-G147` | Inspecteur gestion financière/matérielle des lycées | Corps des Inspecteurs de l'enseignement secondaire | 17 | 15 % | OK |
| 148 | `IDLS-G148` | Inspecteur de l'Education nationale | Corps des Inspecteurs de l'enseignement secondaire | HC-S2 | 45 % — confirmé (`INSPEDUCNATI`, § 3bis) | OK — résolu Q-C3 |
| 149 | `ADSP-G149` | Aide-soignants de santé publique | Corps des Aides-soignants de santé publique | 8 | NON ÉLIGIBLE | OK |
| 150 | `ADSP-G150` | Aide-soignant principal de santé publique | Corps des Aides-soignants de santé publique | 9 | NON ÉLIGIBLE | OK |
| 151 | `IDSP-G151` | Infirmier breveté | Corps des Infirmiers de santé publique | 9 | NON ÉLIGIBLE | OK |
| 152 | `IDSP-G152` | Infirmier de santé publique | Corps des Infirmiers de santé publique | 11 | NON ÉLIGIBLE | OK |
| 153 | `OP-G153` | Ouvrier Professionnel Cat 3 | Corps des Ouvriers professionnels | 1 | NON ÉLIGIBLE | OK |
| 154 | `OP-G154` | Ouvrier Professionnel Cat 2 | Corps des Ouvriers professionnels | 3 | NON ÉLIGIBLE | OK |
| 155 | `OP-G155` | Ouvrier Professionnel Cat 1 | Corps des Ouvriers professionnels | 5 | NON ÉLIGIBLE | OK |
| 156 | `OP-G156` | Ouvrier Professionnel Cat 1 - Chef de Parc | Corps des Ouvriers professionnels | 5 | NON ÉLIGIBLE | OK |
| 157 | `OP-G157` | Ouvrier Professionnel Cat 1 - Chef d'Atelier | Corps des Ouvriers professionnels | 5 | NON ÉLIGIBLE | OK |
| 158 | `OP-G158` | Ouvrier Professionnel Cat 1 - Chef Magasinier | Corps des Ouvriers professionnels | 5 | NON ÉLIGIBLE | OK |
| 159 | `OP-G159` | Ouvrier Professionnel Cat 1 - Chef de Cuisine | Corps des Ouvriers professionnels | 5 | NON ÉLIGIBLE | OK |
| 160 | `OP-G160` | Ouvrier Professionnel Cat 1 - Resp. Service Intérieur | Corps des Ouvriers professionnels | 5 | NON ÉLIGIBLE | OK |
| 161 | `OP-G161` | Ouvrier Professionnel Hors Categorie | Corps des Ouvriers professionnels | 6 | NON ÉLIGIBLE | OK |
| 162 | `OP-G162` | Ouvrier Professionnel Hors Cat. - Chef de Parc | Corps des Ouvriers professionnels | 6 | NON ÉLIGIBLE | OK |
| 163 | `OP-G163` | Ouvrier Professionnel Hors Cat. - Chef d'Atelier | Corps des Ouvriers professionnels | 6 | NON ÉLIGIBLE | OK |
| 164 | `OP-G164` | Ouvrier Professionnel Hors Cat. - Chef Magasinier | Corps des Ouvriers professionnels | 6 | NON ÉLIGIBLE | OK |
| 165 | `OP-G165` | Ouvrier Professionnel Hors Cat. - Chef de Cuisine | Corps des Ouvriers professionnels | 6 | NON ÉLIGIBLE | OK |
| 166 | `OP-G166` | Ouvrier Professionnel Hors Cat. - Resp. Service Intérieur | Corps des Ouvriers professionnels | 6 | NON ÉLIGIBLE | OK |
| 167 | `CD-G167` | Conducteur d'automobile Cat 2 | Corps des Conducteurs d'automobile | 2 | NON ÉLIGIBLE | OK |
| 168 | `CD-G168` | Conducteur d'automobile Cat 2 - Chef de Parc | Corps des Conducteurs d'automobile | 2 | NON ÉLIGIBLE | OK |
| 169 | `CD-G169` | Conducteur d'automobile Cat 1 | Corps des Conducteurs d'automobile | 3 | NON ÉLIGIBLE | OK |
| 170 | `CD-G170` | Conducteur d'automobile Cat 1 - Chef de Parc | Corps des Conducteurs d'automobile | 3 | NON ÉLIGIBLE | OK |
| 171 | `A-G171` | Appariteur | Corps des Appariteurs | 1 | NON ÉLIGIBLE | OK |
| 172 | `A-G172` | Appariteur Principal | Corps des Appariteurs | 2 | NON ÉLIGIBLE | OK |
| 173 | `OPC-G173` | Ouvrier Professionnel Niv 1 | Ouvriers Professionnels Contractuels | 1 | NON ÉLIGIBLE | OK |
| 174 | `OPC-G174` | Ouvrier Professionnel Niv 2 | Ouvriers Professionnels Contractuels | 3 | NON ÉLIGIBLE | OK |
| 175 | `OPC-G175` | Ouvrier Professionnel Niv 3 | Ouvriers Professionnels Contractuels | 5 | NON ÉLIGIBLE | OK |
| 176 | `OPC-G176` | Ouvrier Professionnel Niv 4 | Ouvriers Professionnels Contractuels | 6 | NON ÉLIGIBLE | OK |
| 177 | `ADSC-G177` | Agent de service Niv 1 | Agents de service Contractuels | 1 | NON ÉLIGIBLE | OK |
| 178 | `ADSC-G178` | Agent de service Niv 2 | Agents de service Contractuels | 3 | NON ÉLIGIBLE | OK |
| 179 | `ADSC-G179` | Agent de service Niv 3 | Agents de service Contractuels | 5 | NON ÉLIGIBLE | OK |
| 180 | `CDC-G180` | Conducteur d'automobile Niveau 1 | Conducteurs d'automobile Contractuels | 2 | NON ÉLIGIBLE | OK |
| 181 | `CDC-G181` | Conducteur d'automobile Niveau 2 | Conducteurs d'automobile Contractuels | 3 | NON ÉLIGIBLE | OK |
| 182 | `CDC-G182` | Conducteur d'automobile Niveau 3 et Chef de Parc | Conducteurs d'automobile Contractuels | 4 | NON ÉLIGIBLE | OK |
| 183 | `GC-G183` | Gardien | Gardiens Contractuels | 1 | NON ÉLIGIBLE | OK |
| 184 | `ADPC-G184` | Agent de prevention Niv 1 | Agents de prevention Contractuels | 5 | NON ÉLIGIBLE | OK |
| 185 | `ADPC-G185` | Agent de prevention Niv 2 | Agents de prevention Contractuels | 7 | NON ÉLIGIBLE | OK |

**185 lignes OK (seedables), 0 lignes BLOQUÉ — Q-C3 résolue (§ 3).**

## 3bis. Résolution jalon B (15/07/2026) et conflit de sources découvert

**Inspecteurs génériques (n° 133/135/136) — ✅ CONFIRMÉ 45 %.** La classification J3G
« à confirmer » est validée sans ambiguïté par
`Reglementation/elements_paie_historique_14726/ISSRP_Regles_Metier.md` § 2.1 (référence
déjà présente dans le dossier, statut « CLÔTURÉ — CONFORME — VERROUILLÉ CONTRE
RÉGRESSION », 97 tests, citée Décret 25-55 Art. 10) : « Inspecteurs discipline /
administration : `INSPEDUCNATI`, `INSPENSEMOYE`, `INSPENSEPRIM`, `INSPENSESECO` » —
correspondant exactement aux grades n° 136, 135, 133 (et 144/145, résolus par Q-C3, § 3).
Le n° 148 (`Inspecteur de l'Education nationale`, HC-S2) relève du même groupe 45 % —
également résolu et seedé (Q-C3, § 3).

**✅ Q-08 (contractuels, n° 130-132) — RÉSOLU (15/07/2026, texte réglementaire fourni).**
L'utilisateur a produit le texte tranchant : un arrêté fixant les **6 primes/indemnités**
servies aux enseignants contractuels des établissements du MEN — qualification, PAPP
(prime d'amélioration des performances pédagogiques), documentation pédagogique,
**indemnité de soutien scolaire et de remédiation pédagogique (ISSRP)**, indemnité
forfaitaire compensatoire, indemnité de zone — « calculées selon le diplôme, le grade
correspondant et le lieu d'exercice ». Conditions et modalités fixées aux **art. 3, 7, 8,
9 bis du décret exécutif 10-78** (24/02/2010) et aux **art. 2, 3, 4 du décret exécutif
08-70** (26/02/2008).

**Conclusion :** l'ISSRP fait partie des indemnités statutairement dues aux enseignants
contractuels — les grades n° 130-132 sont **inclus dans le seed**, au même taux que leur
équivalent titulaire (`GE-ISSRP45-DIRECT`, 45 %), l'arrêté ne prévoyant pas de taux
différencié et calant le calcul sur le grade comme pour les titulaires. Source retenue
pour le seed : `GE-ISSRP45-DIRECT`, `Source = 'Arrêté 6 primes enseignants contractuels ;
D.ex. 10-78 art. 3/7/8/9bis ; D.ex. 08-70 art. 2/3/4'`.

**Réconciliation avec `ISSRP_Regles_Metier.md`** : ce document exclut `CONTRACTUEL`/
`VACATAIRE` des 3 taux ISSRP (§ 2.4/§ 4.2) et notait pourtant, dans le même souffle
(§ 1.1), que les 3 corps « Prof.Contractuel » y sont « soumis » à cette exclusion malgré
leur présence en liste d'inclusion — la tension que j'avais signalée. Le texte
réglementaire ci-dessus, spécifique aux « enseignants contractuels » et régi par un couple
de décrets dédié (10-78 + 08-70), l'emporte pour cette population précise : c'est une
catégorie statutaire distincte du `Type_Contrat=CONTRACTUEL` générique (probablement
personnel administratif/support sur contrat de droit commun). **`ISSRP_Regles_Metier.md`
n'est donc pas invalidé globalement** — son exclusion `CONTRACTUEL`/`VACATAIRE` reste
vraisemblablement correcte pour cette population générique — mais son classement des 3
corps `PROFCONTENSEPRIM/MOYE/SECO` sous cette exclusion est probablement une erreur ou une
simplification du système source, à ne pas reproduire ici.

**Conséquence pour le constat structurel (§ 3bis précédente version)** : ma recommandation
d'ajouter une condition commune `TYPE_CONTRAT NOT IN (CONTRACTUEL, VACATAIRE)` sur les 3
groupes ISSRP est **retirée** — elle aurait exclu à tort les enseignants contractuels. Le
modèle GRADE seul (sans condition `TYPE_CONTRAT`) reste correct pour l'ISSRP, précisément
parce que le grain GRADE distingue déjà nativement `PDLP-G130` (Prof.Contractuel) de
`PDLP-G105` (titulaire) — la distinction titulaire/contractuel est portée par le grade
lui-même dans ce référentiel, pas par un attribut séparé.

**Correctif (16/07/2026)** : contrairement à ce qui était noté ici initialement, les 3
autres indemnités de l'arrêté (qualification, documentation pédagogique, forfaitaire
compensatoire = IFC) **sont déjà catalogées** — pas de découverte, erreur de vérification
de ma part. Voir l'audit complet `docs/analysis/J3B_CATALOGUE_REGLES_METIER.md` RM-045/046/052
et `docs/analysis/J3C_CATALOGUE_FORMULES.md` (codes `QUALIF`, `DOC_PEDAG`, IFC § 7) —
seul leur codage dans `ReglementaireSeeder` reste à faire, hors périmètre de ce lot.

## 6. Groupes DNF proposés — listes `GRADE IN (...)` prêtes pour le seed

Reprend la structure de `J4E § 6.2`, intégralement implémentée dans
`ReglementaireSeeder.cs` (16/07/2026). Les 4 grades hors catégorie (§ 3) sont désormais
inclus — plus aucune exclusion.

```sql
-- GE-ISSRP45-DIRECT (50 grades)
GRADE IN ('CDL-G014','C-G015','C-G016','C-G017','DDEP-G028','DDEP-G029','DDC-G030',
  'DDL-G031','DDÈP-G032','DDC-G033','DDL-G034','MDLP-G104','PDLP-G105','PDLP-G106',
  'PDLP-G107','PDLP-G108','PDLP-G109','PDLP-G110','PDLP-G111','PDLP-G112','PDLF-G113',
  'PDLM-G114','PDLM-G115','PDLM-G116','PDLM-G117','PDLM-G118','PDLM-G119','PDLM-G120',
  'PTDL-G121','PTDL-G122','PDLS-G123','PDLS-G124','PDLS-G125','PDLS-G126','PDLS-G127',
  'PDLS-G128','PDLS-G129','IDLP-G133','IDLM-G135','IDLN-G136','IDLP-G137','IDLP-G138',
  'IDLM-G140','IDLM-G141','PDLP-G130','PDLM-G131','PDLS-G132',
  'IDLS-G144','IDLS-G145','IDLS-G148')
  -- ✅ Q-08 résolu (§ 3bis) : PDLP-G130/PDLM-G131/PDLS-G132 (contractuels) — arrêté 6
  -- primes + D.ex. 10-78 art. 3/7/8/9bis + D.ex. 08-70 art. 2/3/4.
  -- ✅ Q-C3 résolue (§ 3) : IDLS-G144/G145/G148 (hors catégorie) — Liste_Grades_Fr.csv.

-- GE-ISSRP45-ORIGINE (7 grades conditionnels — groupe A du OU)
GRADE IN ('SDL-G007','SDL-G008','SDL-G009','SDL-G010','CDL-G011','CDL-G012','CDL-G013')
-- + condition ORIGINE_STATUTAIRE = ENSEIGNANT (groupe B du ET, Q-C1)

-- GE-ISSRP30-ORIGINE : mêmes 7 grades ci-dessus
-- + condition ORIGINE_STATUTAIRE = AUTRE (Q-C1 — jamais de <> : abstention si INCONNU)

-- GE-ISSRP30-DIRECT (20 grades)
GRADE IN ('ADL-G001','ADL-G002','SDL-G003','SDL-G004','SDL-G005','SDL-G006',
  'CDLSEP-G018','CDLEDLGSEP-G019','CDLEDLGSEP-G020','CDLEDLGSEP-G021','CDLEDLGSEP-G022',
  'CDLEDLGSEP-G023','CEAS-G024','CEAS-G025','CEAS-G026','CEAS-G027','IDLEDLGSEP-G134',
  'IDLP-G139','IDLM-G142','IDLS-G146')
  -- ✅ Q-C3 résolue : IDLS-G146 (orientation/guidance, hors catégorie) ajouté.

-- GE-ISSRP15-DIRECT (15 grades)
GRADE IN ('ADSE-G035','ADSE-G036','S-G037','S-G038','I-G039','I-G040','ATDL-G041',
  'ATDL-G042','ATDL-G043','ADL-G044','ADL-G045','ADL-G046','ADL-G047','IDLM-G143',
  'IDLS-G147')

-- GE-ISSRP15-HIST (2008-2024, taux unique 15 % — union des 4 groupes 2025+ ci-dessus,
-- CONDITIONNELS ET CONTRACTUELS ET HORS CATÉGORIE INCLUS — RM-042 : aucune distinction
-- d'origine avant D.ex. 25-55 ; décrets 10-78/08-70 antérieurs à la période historique) :
-- 50 + 7 + 20 + 15 = 92 grades ; liste = concaténation des 4 blocs GRADE IN ci-dessus.
```

**Remarque sur GE-ISSRP45-DIRECT** : la synthèse J3G annonçait 47 grades pour « 45 % »
avant résolution de Q-08/Q-C3 — la liste ci-dessus (50) intègre les 3 contractuels et les
3 grades hors catégorie 45 % (IDLS-G144/G145/G148), désormais tous résolus.

## 7. Points hérités de J3G — statut après jalon B

- **✅ Q-08** — grades 130-132, contractuels : **résolu** (§ 3bis) — ISSRP 45 % confirmé
  par arrêté + D.ex. 10-78/08-70. Inclus dans `GE-ISSRP45-DIRECT` (§ 6).
- **Inspecteurs génériques** (n° 133, 135, 136, 148) : **résolu** (§ 3bis) — 45 % confirmé
  par `ISSRP_Regles_Metier.md` pour 133/135/136 ; le n° 148 (`IDLS-G148`) également résolu
  et seedé (Q-C3, § 3) — plus aucune réserve sur ce groupe.
- **✅ Q-C3** — grades 144/145/146/148 (hors catégorie HC-S1/HC-S2) : **résolu** (§ 3,
  16/07/2026) — indices trouvés dans `Liste_Grades_Fr.csv`, seed supplémentaire ciblé.
  Seed ISSRP complet à 185/185 grades.

**Jalon B et Q-C3 intégralement levés** — plus aucun point J3G en suspens, hors
l'exhaustivité recommandée en § 9 (non engagée).

## 8. Prochaine étape — ✅ SEED LIVRÉ ET COMPLET (16/07/2026)

Les listes `GRADE IN (...)` du § 6 ont été intégrées telles quelles dans
`ReglementaireSeeder.InsertReglesEligibiliteAsync` (6 groupes DNF, 8 conditions) —
aucune retranscription, copie directe. Le contournement CORPS du test e2e
(`BulletinEndToEndTests`) a été retiré ; l'agent de test passe maintenant par un
`Grade` réel (`PDLP-G105`). **Q-C3 résolue** (§ 3, même jour) : les 4 grades
HC-S1/HC-S2 sont seedés (`Grades`/`Categories`/`GrilleIndiciaire`) et intégrés aux
groupes DNF — seed ISSRP **complet, 185/185 grades**. 289/289 tests verts (Domain
toujours pur).

## 9. Rapprochement exhaustif J3G ↔ `ISSRP_Regles_Metier.md` — ✅ FAIT (16/07/2026)

**Méthode.** Les 185 grades de J3G ont été regroupés par corps (56 corps distincts) et
comparés systématiquement aux **34 corps** explicitement listés dans
`ISSRP_Regles_Metier.md` § 1.1 (14 corps M116 + 20 corps M117 — la liste faisant foi pour
« qui touche l'ISSRP, tous taux confondus »), puis aux populations détaillées par taux
(§ 2.1–2.3 du même document).

### Verdict global

**33 corps sur 34 corroborés sans ambiguïté.** Chaque corps du groupe 45 % direct (15
corps), du groupe 30 % (9 corps) et du groupe 15 % (9 corps, hors doublons de corps
« Inspecteurs » multi-taux) trouve une correspondance exacte 1:1 avec un code de
`ISSRP_Regles_Metier.md` § 2.1–2.3. Le groupe conditionnel (`CONSEDUC` = « Corps des
Conseillers de l'Education ») est **explicitement nommé** dans la population 45 %
(« Conseillers d'éducation issus du corps enseignant »), confirmant directement le
mécanisme `ORIGINE_STATUTAIRE` de Q-C1.

### Deux constats positifs — J3G plus complet que le référentiel legacy

`ISSRP_Regles_Metier.md` note explicitement deux lacunes de son propre référentiel
source : « Inspecteurs EP alimentation : aucun corps précis n'existe actuellement » et
« Inspecteurs gestion financière/matérielle : aucun corps précis n'existe actuellement —
extension future réglementaire ». Le CSV pilote (J3G) **couvre déjà** ces deux cas :
- grade n° 139 (« Inspecteur de l'enseignement primaire spécialité alimentation
  scolaire ») → 30 %, cohérent avec la famille orientation/alimentation ;
- grades n° 143/147 (« Inspecteur de la gestion financière et matérielle des
  collèges/lycées ») → 15 %, cohérent avec la famille intendance.

Aucune contradiction : le système legacy anticipait ces cas sans les avoir encore codés,
le CSV pilote les a. Une **correction déjà présente dans le legacy est également
corroborée** : le grade n° 134 (« Inspecteur de l'orientation et de la guidance...») est
classé 30 % dans J3G — exactement la correction que `ISSRP_Regles_Metier.md` documente
lui-même (§ 2.2 : « corrigé par M115/E1 : ce corps était à tort classé ISSRP_45 »).

### Un point nuancé — couverture par catch-all, pas par code explicite

Les 4 grades « Educateur spécialisé en soutien Éducatif » (n° 7-10, corps « Superviseurs
de l'Education ») sont traités par J3G/J4E comme conditionnels à `ORIGINE_STATUTAIRE`
(comme `CONSEDUC`), mais **aucun code corps distinct ne les nomme explicitement** dans la
population 45 % de `ISSRP_Regles_Metier.md` — seule la clause générique « Grades de
promotion à origine statutaire pédagogique » peut les couvrir. Le corps `SUPEEDUC` n'est
listé qu'au taux 30 % **direct** (ses grades non spécialisés, n° 3-6), sans mention
explicite d'une variante conditionnelle. Ce n'est pas une contradiction — c'est une
zone que le référentiel legacy documente moins précisément que J3G. Le traitement actuel
(conditionnel, cohérent avec le principe de la clause générique) est **maintenu sans
changement** ; signalé pour mémoire, pas une ⛔.

### ⛔ Un point non résolu — `ADJOTECH (40)`

`ISSRP_Regles_Metier.md` liste `ADJOTECH (40)` comme un corps **à part entière** des 34
éligibles (M117, rubrique « Adjoints techniques », distincte de la rubrique
« Laboratoire » qui porte `ADJOTECHLABO (28)`). Aucun grade du CSV pilote ne s'y
rattache clairement : les 3 grades J3G nommés « Corps des Adjoints techniques »
(n° 76, 85, 101 — spécialités Informatique/Statistiques/« Laboratoire et maintenance »)
apparaissent dans un contexte de corps communs techniques (aux côtés d'Ingénieurs/
Techniciens sur les mêmes spécialités, grades 69-102), pas de corps spécifiques EN — et
sont donc correctement classés NON ÉLIGIBLE, distincts des grades 41-47 (« Adjoint
technique **de laboratoire** », `ADJOTECHLABO`, déjà 15 % éligible). `ADJOTECH (40)`
semble désigner un corps EN-spécifique **absent du CSV pilote actuel**
(`Cascade_Corps_Grades_30526.csv` couvre 185 grades, le référentiel legacy plus large
peut en couvrir davantage). **Aucun impact sur le seed actuel** — rien n'est
mal-classé — mais la couverture du CSV pilote face au référentiel legacy complet reste
une question ouverte, à vérifier si un futur corps EN nommé proche d'« Adjoint
technique » apparaît hors de ce périmètre pilote.

**Conclusion.** Le rapprochement ne révèle **aucune erreur de classification** dans le
seed ISSRP actuel (185/185 grades). Un point de nuance documentaire (Educateur
spécialisé) et un point de couverture potentiellement incomplète du CSV pilote
(`ADJOTECH`) sont signalés pour mémoire, sans action requise à ce stade.
