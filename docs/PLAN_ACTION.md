# PaieEducation ERP — Plan d'Action de Conception et d'Implémentation

> **Statut :** v0.1 — En attente de validation du jalon J0.
> **Nature :** Document vivant. Mis à jour à chaque décision, ADR et fin de phase.
> **Référentiels :** Documentation V3.0 (métier/fonctionnel/architecture) + V4.0 (spécification d'implémentation, tomes A–F). En cas de conflit, la V4.0 prévaut pour l'implémentation.
> **Principe cardinal :** ZÉRO hardcoding des règles et valeurs réglementaires. Tout ce qui varie (point indiciaire, grilles, indices, corps/grades/échelons/catégories, taux de cotisation, barèmes IRG, rubriques, formules, priorités, dépendances, régimes indemnitaires, dates d'effet) vit en base SQLite, **versionné par période de validité** et **personnalisable par l'utilisateur**.

---

## A. Journal des décisions métier validées (réponses Q1–Q10)

| Réf | Décision validée |
|-----|------------------|
| **Q1** | `traitement de base = indice_min_catégorie × valeur_point` ; `traitement = (indice_min + indice_échelon) × valeur_point`. Valeur point = 45 DA (paramétrable, versionné). |
| **Q2** | ⚠ **Révisée le 14/07/2026 (voir Q2-rev).** Formulation initiale : « IEP = 4 % × échelon × TBASE » — cette formule est en réalité celle de l'indemnité d'expérience **pédagogique**. |
| **Q3** | Cotisations (sécurité sociale/retraite part ouvrière, œuvres sociales éventuelles), **taux + assiette + rubriques entrantes/sortantes** = **paramétrés en base, éditables par l'utilisateur**. Aucun taux codé en dur. La BD et les écrans doivent supporter ce paramétrage. |
| **Q4** | Barème IRG fourni (`CALCUL IRG ALGERIE.txt`). Barème 2008 : `≤10 000 → 0 %` ; `10 001–30 000 → 20 %` ; `30 001–120 000 → 30 %` ; `>120 000 → 35 %`. Périodes/lissages 2020, 2021, 2022+ avec coefficients. **Tout stocké en base (tranches + règles de période).** |
| **Q5** | Abattement **40 % appliqué sur l'IRG brut** (borné [1000 ; 1500 DA]). Exonération totale si imposable ≤ 30 000. Caractère imposable/cotisable = **flag paramétrable par rubrique**. |
| **Q6** | Matrice éligibilité rubrique×corps **construite en base** à partir des décrets. Le client fournira/validera un tableau récapitulatif corps→rubriques (⏳ attendu). |
| **Q7** | Rappels/régularisations rétroactifs **gérés dès la V1**, règle = **date d'effet réglementaire**. |
| **Q8** | Ancienneté = **années de service effectif, sans interruptions déductibles** (disponibilité, suspension non déduites). |
| **Q9** | Arrondi **centralisé unique**. Au dinar le plus proche / à la dizaine → **à préciser (voir ⛔Q9b)**. Paramétrable. |
| **Q10** | Périmètre V1 = **corps pilote « enseignants » d'abord** pour valider le moteur, puis extension aux autres corps. |
| **Q3b** | Seed cotisation **sécurité sociale part ouvrière = 9 %** (éditable). **Œuvres sociales** = retenues **temporaires/facultatives en montants fixes**, au choix de l'employé (pas un % obligatoire). **Cotisations mutuelles** = **facultatives**. → modélisées comme *retenues optionnelles à montant fixe pilotées par l'agent*, distinctes des cotisations obligatoires. |
| **Q4b** | ⚠ **Révisée le 14/07/2026 (voir Q-01).** Formulation initiale (« pas de table de tranches distincte pour 2022+ ») invalidée par l'analyse J3 : la LF 2022 est une refonte du barème. |
| **Q9b** | Arrondi **centralisé, uniforme partout** (rubriques + net). Défaut retenu : **au dinar le plus proche**, paramétrable (dinar/dizaine). |
| **Q11** | Bulletins réels de référence fournis **après mise en marche** → validation réglementaire (Phase 8) itérative post-livraison. |
| **Q12** | **Mode autonome** (sans authentification) en V1. Modèle de rôles prévu mais désactivé. |
| **Q13** | Documents V1 (au-delà du bulletin) précisés **après mise en marche**. Architecture Reporting extensible dès la Phase 7. |
| **Q6** | Matrice corps→rubriques construite depuis : `Cascade_Corps_Grades_30526.csv` (**source faisant foi**, remplace 11526), `elements_paie_historique_corrige`, `ISSRP_Corrige`, `Prime-Rendement_historique`, `IFC 2008+2015`, `Grille_indiciaire_2007_2024`. **ISSRP** : 45 % = groupe pédagogique **élargi** (enseignants + directeurs + inspecteurs + censeurs + conseillers issus du corps enseignant + grades de promotion d'origine enseignante) ; 30 % ; 15 %. |

### Décisions du 14/07/2026 (validation de l'analyse préalable J3, cf. `docs/analysis/`)

| Réf | Décision validée |
|-----|------------------|
| **Q-01** (révise Q4b) | **Barème IRG 2022+ = nouveau barème à 6 tranches** (mensuel : ≤ 20 000 → 0 % ; 20 001–40 000 → 23 % ; 40 001–80 000 → 27 % ; 80 001–160 000 → 30 % ; 160 001–320 000 → 33 % ; > 320 000 → 35 %), conformément à `IRG_Algerie_2008_2026_PseudoCode.txt` (« à partir de 2022 ⇒ nouveau barème »). Seed `IRG-2022` + repointage de `IRG-PER-2022`. Lissages conservés : général `137/51 − 27925/8` (30–35 k), spécial `93/61 − 81213/41` (≤ 42 500). |
| **Q-02** | **Tableau des flags imposable/cotisable par rubrique validé** (J3D §4) : PAPP/PAPG/rendement/qualification/doc. pédagogique/exp. pédagogique/ISSRP/IEP/IFC = imposables **et** cotisables. Correction du seed PAPP (`EstCotisable = 1`). |
| **Q-03** | **Méthode matrice ISSRP validée** : (a) affectation des 185 grades du CSV aux groupes 45/30/15 générée pour validation ligne à ligne (`docs/analysis/J3G_MATRICE_ISSRP_PROPOSITION.md`) ; (b) critère d'éligibilité `ORIGINE_CORPS` + attribut agent `OrigineStatutaire` pour les grades dont le taux dépend de l'origine statutaire (V008). |
| **Q2-rev** | **Deux indemnités d'expérience distinctes.** (1) **IEP** (expérience professionnelle, **tous** les employés) : `IEP_FONC = IE × VPI` pour les fonctionnaires (indice d'échelon × valeur du point ⇒ `TRT = TBASE + IEP_FONC`) ; `IEP_CONT = TBASE × min(ANC_PUB × 1,4 % + ANC_PRIV × 0,7 % ; 60 %)` pour les contractuels — le plafond 60 % s'applique au **taux composite**, jamais aux années (ex. 30+30 ans → 63 % → retenu 60 %). Paramètres versionnés : `IEP_TAUX_PUBLIC_PCT = 1,4`, `IEP_TAUX_PRIVE_PCT = 0,7`, `IEP_PLAFOND_PCT = 60`. (2) **EXP_PEDAG** (expérience pédagogique) : `TBASE × 4 % × ECH`, fonctionnaires des corps EN **hors Intendance et Laboratoire** ; bénéficiaires versionnés (v1 2008 ; v2 + direction/inspection au 29/05/2012 ; v3 2025 inchangée). |
| **J3-plan** | **Plan de correction J3A §5 et extensions de schéma V008 (J3E) approuvés** : colonne `PeriodiciteVersement`, table `RubriqueBaremes`, critères d'éligibilité étendus, correctifs seed P0. |
| **Q3b-rev** | **Révise la modélisation Q3b.** Les retenues optionnelles existent sous **deux formes** : (a) **montant fixe** au choix de l'agent (œuvres sociales) ; (b) **taux versionné appliqué à une assiette** — ex. mutuelle **MUNATEC** (secteur éducation nationale) = **1 % du salaire soumis aux cotisations**, taux modifiable dans le temps. Modélisation sans migration : Nature `RETENUE`, `BaseCalcul = ASSIETTE_COTISABLE`, taux dans `RubriqueParametres` versionné, `EstAffectableManuellement = 1` (D1), occurrence unique par organisme (un organisme = une rubrique). Toujours distinctes des cotisations obligatoires et hors déduction d'assiette IRG sauf paramétrage contraire (RM-068 révisée). |

### Précisions post-livraison 22/07/2026 (P22, P23)

| Réf | Précision validée |
|-----|-------------------|
| **P22** (clôture Lot 2.2) | La matrice scénario × rubrique × assertion du bulletin pilote est désormais verrouillée dans `docs/analysis/Lot_2_2_HYPOTHESES.md` (9 scénarios S1–S9, couverture ligne par ligne, 6 tests d'intégration dédiés `Lot22ClotureTests` verts). Livré commit `1c3b34e`. |
| **P23** (correction assiette SS) | L'assiette de la cotisation SS (obligatoire salariale, 9 %, Q3b) est `AssietteCotisable` = Σ des gains marqués `EstCotisable = 1` (Q-02), **pas** `TBASE`. En pilote tous les gains sont cotisables → `AssietteCotisable = TotalGains = 75 325` → SS = 9 % × 75 325 = **6 779 DA** (et non 9 % × 26 010 = 2 341, qui était l'indication erronée du doc P22 §3.2 originelle). Le moteur (`CalculationPipeline.cs:102-103`) et le seed (`referentiel_reglementaire_v1.json`, `assietteRef: "ASSIETTE_COTISABLE"`) sont cohérents ; seule la documentation P22 a été corrigée et le test S6 renforcé en assert strict. |

### Décisions J3H — Affectation assistée des rubriques (14/07/2026, J3H lot 1)

| Réf | Décision validée |
|-----|------------------|
| **Q-J3H-1** | **Rubriques suggérées pré-affectées** dès la création de l'agent (utilisateur libre de les retirer). Évite les oublis sources de paie incomplète. |
| **Q-J3H-2** | **`GroupesEligibilite`** (DNF : ET dans groupe, OU entre groupes) créés pour mutualiser les critères entre rubriques. |
| **Q-J3H-3** | **Statut professionnel** ∈ {ENSEIGNANT, AUTRE, INCONNU}. `INCONNU` = pas de suggestion + avertissement non bloquant. |
| **Q-J3H-4** | MUNATEC 1 % effectif au 01/01/2008. Le système **ne seed rien automatiquement** : l'utilisateur saisit l'historique via l'UI `RubriqueParametres`. Onglet générique « Paramètres historisés » sur chaque fiche rubrique. |

### Décisions J3I — Workbench réglementaire (14/07/2026, J3I — voir ADR-0007)

| Réf | Décision validée |
|-----|------------------|
| **D5** | **Adoption `GroupesEligibilite` (DNF) — abandon de la limite V1 « pas de conditions composées »** (J3H §C.5). Conditions d'un groupe ETées, groupes OUés. Exigé par P6 et P7. (ADR-0007) |
| **D6** | **Introduction `SourcesValeur`** — catalogue extensible (`NOTATION_AGENT`, `ANCIENNETE_PUBLIQUE`, `ANCIENNETE_PRIVEE`, `INDICE_ECHELON`, `POINT_INDICIAIRE`, `BASE_ASSIETTE`, `CONSTANTE_REGLEMENTAIRE`…) + colonne `Rubriques.SourceValeurCode`. Remplace les calculateurs typés pour P3 (% indexé sur une source externe). Nouvelle source = ligne de catalogue + calculateur via DI (Open/Closed). (ADR-0007) |
| **D7** | **Workbench réglementaire = arborescence d'écrans spécialisés** par pattern (Rubriques / Cotisations / Fiscalité / Carrière & grilles / Simulation / Évolution / Audit / Matrice), pas un écran fourre-tout. (ADR-0007) |
| **D8** | **Dry-run obligatoire avant tout commit** d'évolution réglementaire. Preview d'impact (agents × montant × période). Aucun commit sans dry-run, sauf bypass admin documenté dans `AuditLog`. (ADR-0007) |
| **D9** | **Rétroactif = nouvelle version + génération de rappels** (lignes additionnelles), pas de modification des bulletins validés. RM-103 respecté, intégrité comptable préservée. (ADR-0007) |
| **D10** | **Migration unique V009** regroupant L-M1 (`SourcesValeur` + `Rubriques.SourceValeurCode`), L-M2 (`GroupesEligibilite` + `ReglesEligibilite.GroupeId`), L-M3 (audit `RubriqueBaremes` : `CreatedBy`/`UpdatedBy`/`UpdatedAt`) et le socle J3H (`CriteresEligibilite`, `MessagesRegles`, amorces `AgentAttributs`/`AgentRubriques`/`AvertissementsHistorique`). Une seule migration, un seul test d'upgrade V008→V009. (ADR-0007) |
| **D11** | **Matrice de couverture (`corps × rubriques`) en V1**, pas post-V1. Code couleur : vert = règle active, orange = inactive, rouge = pas de règle, gris = non applicable. Sert d'outil de validation admin et d'audit. (ADR-0007) |

---

## B. Questions ouvertes restantes

✅ **Toutes les questions P1/P2/P3 sont résolues** (voir section A). Les décisions J3H (Q-J3H-1 à Q-J3H-4) et J3I (D5-D11) sont validées par l'utilisateur le 14/07/2026 et intégrées aux Phases 3bis/4/5/6/8 (cf. §D et `docs/analysis/J3I_WORKBENCH_REGLEMENTAIRE.md`).

**Questions closes par J3I** :
- Q-04 (DNF vs ET plat) → **D5** adopte la DNF
- Q-08 (notation comme source paramétrable) → **D6** adopte `SourcesValeur`
- Q-09 (périodicité de versement) → supporté par `Rubriques.PeriodiciteVersement` (V008) + Workbench (D7)

Éléments reportés **après mise en marche** du logiciel, sans impact sur le démarrage :
- Fourniture des **bulletins réels de référence** (Q11) → validation réglementaire itérative (Phase 8).
- Précision des **documents officiels V1** additionnels (Q13) → l'architecture Reporting reste extensible (Phase 7).
- **Matrice corps→rubriques** : bases fournies (Q6) ; affinage au fil de l'extension aux autres corps ; visualisation V1 (D11).

Aucun point n'est actuellement bloquant. Toute nouvelle règle métier douteuse rencontrée en cours d'implémentation déclenchera une question (STOP & ASK).

---

## C. Contraintes d'environnement

| Constat | Action |
|---------|--------|
| Pas de dépôt git | `git init` + `.gitignore` .NET en Phase 0 |
| SDK installé = .NET **9.0.315** ; requis = **.NET 10 LTS** | **Installer le SDK .NET 10** avant tout build. Bloquant technique Phase 0. |
| Dossiers `Documentation de Référence du Projet`, `Reglementation` | Conservés hors solution ; référencés en lecture. |

---

## D. Vue d'ensemble des phases

| Phase | Intitulé | Effort | Dépend de | Jalon validation |
|-------|----------|:------:|-----------|------------------|
| 0 | Cadrage & fondations | M | — | **J0** |
| 1 | Référentiel paramétrable (schéma SQLite) | L | 0 | **J1** |
| 2 | Ingestion & seed des données de référence | L | 1 | **J2** |
| 3 | Couche Domaine (DDD) | L | 1 | **J3** |
| **3bis** | **Workbench réglementaire — modèle V009 + usages** (ADR-0007, J3I) | **M** | **3** | **J3bis** |
| 4 | Moteur de calcul de paie (pilote enseignants) | XL | 2, 3, 3bis | **J4** |
| 5 | Application & Persistence | L | 3, 3bis, 4 | **J5** |
| 6 | Présentation WPF/MVVM (Workbench + UI bulletin) | XL | 5 | **J6** |
| 7 | Reporting & documents officiels | L | 5,6 | **J7** |
| 8 | Qualité & validation réglementaire | L | 4→7 | **J8** |
| 9 | Déploiement, doc & finalisation | M | 8 | **J9** |
| 10 | Extension aux autres corps | L | 8 | itératif |

Effort relatif : S < M < L < XL.

---

## Phase 0 — Cadrage & fondations
**Objectif.** Poser un socle technique buildable et une base documentaire partagée, sans code métier.
**Périmètre.** Outillage, structure de solution, conventions, glossaire, squelette de tests.

**Tâches.**
1. Installer/valider le SDK **.NET 10 LTS** ; figer la version via `global.json`.
2. `git init`, `.gitignore` .NET, structure de dossiers `src/`, `tests/`, `docs/`, `data/`.
3. Créer `PaieEducation.sln` et les **projets vides** conformes V4 Tome A : `Domain`, `Application`, `Infrastructure`, `Persistence`, `Reporting`, `Shared`, `Presentation`, `Bootstrapper`, `Tests.Unit`, `Tests.Integration`, `Tools`.
4. Câbler les **références inter-projets** selon la matrice V4 (Domain ne référence rien ; Presentation→Application ; etc.) + test d'architecture (garde-fou de dépendances).
5. Ajouter les paquets NuGet imposés (CommunityToolkit.Mvvm, Microsoft.Extensions.*, Microsoft.Data.Sqlite, Dapper, QuestPDF, ClosedXML, System.Text.Json, xUnit, Moq).
6. Rédiger `docs/GLOSSAIRE.md` (Ubiquitous Language) et `docs/CONVENTIONS.md` (nommage, style, ADR).
7. Initialiser le registre **ADR** (`docs/adr/`) en reprenant ADR-001…ADR-130 de la V4.
8. Mettre en place `Result<T>`, exceptions de base, `IClock`, logging — squelettes dans `Shared`/`Infrastructure`.
9. Pipeline de build local (script `build`, `test`) + 1 test « fumée » par projet.

**Livrables.** Solution qui compile ; tests fumée verts ; glossaire ; conventions ; ADR initiaux.
**Critères d'acceptation.** `dotnet build` et `dotnet test` OK ; test d'architecture des dépendances vert ; aucune référence interdite.
**Tests.** Test d'architecture (dépendances) + fumée.
**Risques.** SDK .NET 10 absent → *installation préalable*. Incohérence numérotation doc V4 → *on fige « tomes A–F / vol. 1–25 » comme référence*.
**Jalon J0.** Validation de la structure et des conventions.

---

## Phase 1 — Référentiel paramétrable (schéma SQLite)
**Objectif.** Modéliser en base **toutes** les données réglementaires versionnées — cœur du principe « zéro hardcoding ».
**Périmètre.** Schéma + migrations, sans logique métier.

**Tâches (tables clés, toutes avec `DateEffet`/`DateFin`, `Version`, `Source`, `Hash` d'audit) :**
1. **Structure organique & carrière :** `Etablissements`, `Corps`, `Grades`, `Echelons`, `Categories`, `Fonctions`, `TypesContrat`, `TypesPersonnel`, `Filieres`.
2. **Grille indiciaire :** `ValeurPoint(date_effet, valeur)` ; `GrilleIndiciaire(categorie, indice_min, date_effet)` ; `IndicesEchelon(echelon, indice)`. (Seed depuis grilles 2007/2022/2023/2024.)
3. **Rubriques & formules :** `Rubriques(code, libellé, nature[Gain/Retenue/Cotisation/Impôt], base_calcul[TRAITEMENT/TBASE/TBASE_ECHELON/FORFAIT/ASSIETTE], periodicite, ordre_calcul, est_imposable, est_cotisable, actif)` ; `RubriqueFormules(rubrique, expression, date_effet)` ; `RubriqueParametres(rubrique, clé, valeur, date_effet)` ; `RubriqueDependances(rubrique, depend_de)`.
4. **Éligibilité :** `ReglesEligibilite(rubrique, critère[corps/grade/catégorie/fonction/type_contrat/spécialité/…], opérateur, valeur, date_effet)` — permet les taux différenciés (soutien scolaire 45/30/15, spécialités d'inspection).
5. **Cotisations (paramétrables — Q3) :** `Cotisations(code, libellé, taux, assiette_ref, date_effet)` ; `CotisationAssietteRubriques(cotisation, rubrique, inclus)`.
6. **IRG (paramétrable — Q4/Q5) :** `BaremeIRG(version, date_effet)` ; `BaremeIRGTranches(bareme, borne_inf, borne_sup, taux)` ; `IRGReglesPeriode(date_debut, date_fin, exoneration_seuil, abattement_taux, abattement_min, abattement_max, coef_general, const_general, coef_special, const_special, plafond_special)`.
7. **Régimes indemnitaires :** import de `IndemniteHistorique` (fichier TXT) comme table de faits.
8. **Paramètres système :** `Parametres(clé, valeur, type)` (dont règle d'arrondi Q9).
9. **Technique :** `SchemaVersions`, `AuditLog`.
10. Écrire les **migrations versionnées** (`V001_…`) + activation `PRAGMA foreign_keys=ON`, mode `WAL`.

**Livrables.** Schéma SQLite complet + dictionnaire de données `docs/DICTIONNAIRE_DONNEES.md` + migrations rejouables depuis zéro.
**Critères d'acceptation.** Base reconstructible par migration ; intégrité référentielle ; requête « résolution par date » démontrée sur ≥ 2 rubriques ; **aucune valeur réglementaire dans le code**.
**Tests.** Tests d'intégration migration (base vide + base existante) ; test de résolution temporelle.
**Risques.** Sous-modélisation de l'éligibilité → *revue avec la matrice client (Q6)*.
**Jalon J1.** Validation du schéma paramétrable.

---

## Phase 2 — Ingestion & seed des données de référence
**Objectif.** Peupler le référentiel depuis les fichiers fournis, de façon **reproductible et validée**.
**Périmètre.** Outils dans `PaieEducation.Tools` + jeux de seed.

**Tâches.**
1. Parsers/importeurs par format (CSV/Excel via ClosedXML ; TXT/MD ; PDF → extraction assistée/manuelle contrôlée).
2. Importer : grilles indiciaires + point ; **cascade corps/grades (185 lignes)** ; barème & règles IRG (fichier txt) ; historique des éléments de paie ; IFC (08-70).
3. **Contrôles de cohérence** : unicité codes, continuité des périodes (pas de chevauchement/trou), correspondance indices/catégories, totaux.
4. Seed **cotisations** avec valeurs par défaut documentées (⛔Q3b) et **flags imposable/cotisable** par rubrique.
5. Intégrer la **matrice corps→rubriques** dès réception (⛔Q6) — sinon seed provisoire pour le corps pilote enseignants.
6. Rapport d'import (lignes lues/insérées/rejetées + anomalies).

**Livrables.** Base seedée reproductible ; rapports d'import ; scripts idempotents.
**Critères d'acceptation.** Rejeu identique → même base ; 0 anomalie bloquante ; échantillon vérifié manuellement (indices catégorie 7 = 348/398/473/548, etc.).
**Tests.** Tests d'intégration des importeurs + contrôles de cohérence.
**Risques.** Extraction PDF imparfaite → *validation humaine des tables sensibles (barèmes)*.
**Jalon J2.** Validation des données seedées (au moins périmètre enseignants).

---

## Phase 3 — Couche Domaine (DDD)
**Objectif.** Implémenter le cœur métier pur, indépendant de toute techno (V4 Tomes B/C).
**Tâches.**
1. **Value Objects** : `Money` (DZD + arrondi centralisé), `Percentage`, `Indice`, `Echelon`, `Anciennete`, `PayrollPeriod`, `Matricule`, `RubriqueCode`, `DateRange`.
2. **Entités/Agrégats** : `Agent` (racine : identité, carrière, contrat, affectation), `Bulletin` (racine : lignes, totaux, statut), `Rubrique`, `Grade`, `Echelon`, `Periode`.
3. **Invariants** (matricule unique, 1 bulletin/agent/période, immuabilité après validation…).
4. **Services de domaine & spécifications** : `SeniorityService` (Q8), `EligibilityService`, spécifications composables.
5. **Domain Events** & **exceptions métier**.
6. **Interfaces repository** (contrats dans Domain).

**Livrables.** Projet `Domain` testé (couverture ≥ 95 % des comportements critiques).
**Critères d'acceptation.** Aucune dépendance technique dans Domain ; invariants garantis ; `Money` = seul porteur des montants.
**Tests.** Unitaires exhaustifs (VO, agrégats, spécifications).
**Jalon J3.**

---

## Phase 3bis — Workbench réglementaire : modèle V009 + services de base (ADR-0007, J3I, J3J v1.0)
**Objectif.** Traduire opérationnellement les décisions D5-D11 du Workbench réglementaire (J3I §11) en schéma V009, services de domaine et use cases minimaux — avant la Phase 4 pour que le moteur étendu puisse s'appuyer dessus, et avant la Phase 5 pour que les use cases Workbench soient disponibles au démarrage de la couche Application.

**Périmètre.** Migration V008 → V009 (refactorée R1-R5) ; Value Objects et services de domaine du Workbench ; use cases de simulation ; rien d'UI (UI = Phase 6).

**Tâches.**
1. **Migration V009** (D10, J3I §9 refactoré) — un seul fichier SQL idempotent :
   - `SourcesValeur` (**PK `Id`**, catalogue technique, R2/R4 révisé) + `Rubriques.SourceValeurId` (FK)
   - `CriteresEligibilite` (**PK `Id`**, catalogue technique, R2/R3/R4 révisé) + `ReglesEligibilite.CritereId` (FK, **remplace** `Critere` TEXT+CHECK)
   - `MessagesRegles` (**PK `Id`**, **texte réglementaire — audit complet**, R4 révisé)
   - `GroupesEligibilite` (PK `Id`, règle réglementaire, audit complet) + `ReglesEligibilite.GroupeId` (FK, NULL = condition commune)
   - `RubriqueBaremes` : ajout `CreatedBy` / `UpdatedBy` / `UpdatedAt` (audit barème, L-M3)
   - **PAS d'amorces de tables de gestion** — `AgentAttributs`, `AgentRubriques`, `AvertissementsHistorique` sont créées **avec `Agents` en Phase 5** (R1, design preview J3J § 8.3-8.5)
2. **Tests d'upgrade V008 → V009** : base de test V008 complète, application V009, vérification ligne à ligne + invariants + non-régression des 117 tests existants. Test d'audit allégé (R4 révisé) inclus.
3. **Value Objects domaine** (Domain) :
   - `BaremeValue` (rubrique + dimension + clé de tranche) avec résolution par date
   - `SourceValeur` (enum ↔ catalogue `SourcesValeur`) avec méthode d'évaluation typée
   - `CritereEligibilite` (libellé + `TypeValeur` + `SourceResolution`) pour piloter l'évaluateur
   - `MessageRegle` (avec versioning R4 révisé : `Source` + `DateEffet/Fin`)
   - `GroupeEligibilite` (en-tête) + `ConditionEligibilite` (membre) avec `Operateur` (DNF supported)
   - `PeriodeReglementaire` (DateEffet, DateFin, validation chevauchement/trou)
4. **Services de domaine** (Domain) :
   - `BaremeResolver` (lit `RubriqueBaremes`, applique `Dimension`/`Borne`/`TypeValeur`, versionné)
   - `SourceValeurResolver` (lit `SourcesValeur` + `Rubriques.SourceValeurId`, délègue au calculateur enregistré)
   - `RegleEligibiliteEvaluator` (étend l'évaluateur V008 : ET plat **+ DNF** via `GroupesEligibilite`)
   - `ContinuiteTemporelle` (validation : pas de chevauchement, pas de trou, une seule ouverte par clé)
5. **Use cases de simulation** (Application) :
   - `SimulerEvolutionReglementaire` (D8) : produit un rapport d'impact (agents × montant × période) **sans** toucher à la base — c'est une lecture.
   - `DetecterChevauchement` / `DetecterTrou` : utilisés par le simuler et par l'UI Workbench (Phase 6).
6. **Catalogue de sources seed** (Domain + Tools) : enregistrer les `SourcesValeur` V1 (`NOTATION_AGENT`, `ANCIENNETE_PUBLIQUE`, `ANCIENNETE_PRIVEE`, `INDICE_ECHELON`, `POINT_INDICIAIRE`, `BASE_ASSIETTE`, `CONSTANTE_REGLEMENTAIRE`) avec leur `Calculateur` DI enregistré. Pattern Open/Closed : une nouvelle source = ligne catalogue + classe enregistrée, **sans modification du moteur**.

**Livrables.** V009 appliqué sur une base V008, suite de tests d'upgrade verte, services de domaine testés, simulateur fonctionnel.
**Critères d'acceptation.**
- Migration reproductible depuis V008 ; aucun downgrade involontaire
- DNF couverte par l'évaluateur (test : règle `ISSRP_45` avec 2 groupes, Grade direct vs Grade conditionnel + Origine)
- `BaremeResolver` : 1 000 résolutions en < 50 ms (perf cible V4)
- `SimulerEvolutionReglementaire` retourne un rapport complet (nb agents, delta min, delta max, montant total, période touchée) sans écrire en base
**Tests.** Upgrade V008→V009 ; DNF nominal + bornes ; barème nominal + chevauchement + trou ; simulation régression (résultat identique à 2 simulations successives) ; non-régression des 117 tests existants.
**Risques.** Régression sur l'existant → *tests d'upgrade systématiques avant tout commit V009*. DNF subtile → *relecture croisée J3G (matrice ISSRP) et J3I §13.2*.

**Jalon J3bis.** Validation de V009, de l'évaluateur DNF et du simulateur.

---

## Phase 4 — Moteur de calcul de paie (pilote : enseignants)
**Objectif.** Implémenter le pipeline orienté-moteur et les calculateurs du corps pilote, **alimentés par la base** (V4 Tome C, vol. 9–10), étendu par les capacités du Workbench (V009 + J3I).
**Périmètre pilote (rubriques enseignants) :** Traitement de base → IEP (4 %/échelon) → Indemnité de qualification (40/45 %) → Documentation pédagogique (forfait 2000/2500/3000) → PAPP (0–40 %, source `NOTATION_AGENT`) → Soutien scolaire (45 %/30 %/15 %, DNF) → Assiette cotisable/imposable → Cotisations (paramétrables) → IRG (barème + lissages) → Net.

**Tâches.**
1. `PayrollContext` (immuable) + `ContextBuilder` — étendu pour résoudre `valeurSource(RUB)` et les barèmes à la date demandée.
2. `EligibilityEngine` — étendu pour évaluer la **DNF** (`GroupesEligibilite` ET dans groupe, OU entre groupes) en plus de l'ET plat V008.
3. `VariableEngine` (indice, traitement de base, ancienneté, n° échelon…).
4. `FormulaEngine` + `FormulaParser`/`ExpressionEvaluator` (formules **lues en base**, aucune formule codée en dur) — expose `bareme(RUB, dim)` et `valeurSource(RUB)`.
5. `DependencyResolver` (graphe DAG, détection de cycles).
6. `CalculationPipeline` + calculateurs `IPayrollCalculator` (un par rubrique/famille), priorisés — les calculateurs P3 dépréciés (`PAPPCalculator` typé) sont remplacés par la voie générique `valeurSource('NOTATION_AGENT')`.
7. `IrgCalculator` : tranches + règles de période **depuis la base** (barème 2008 + lissages 2020/2021/2022+), abattement sur IRG brut, exonération ≤ 30 000.
8. `ContributionCalculator` : taux/assiette **depuis la base**.
9. `TotalsEngine`, `ValidationEngine`, `ExplainabilityEngine` (justification de chaque montant), `AuditEngine`, `SnapshotEngine`.
10. **Rappels rétroactifs** (Q7 / D9) : recalcul à la réglementation en vigueur à la **date d'effet** — la rétroactivité déclenche la création de **lignes de rappel** (lignes additionnelles), jamais la ré-écriture d'un bulletin validé.
11. Service d'**arrondi centralisé** (Q9).
12. **Générateur de rappels** (D9) : prend en entrée un commit rétroactif (V009) et produit la liste des lignes de rappel à émettre pour les bulletins validés impactés, en préservant l'immutabilité des bulletins.

**Livrables.** Moteur exécutant un bulletin enseignant de bout en bout + journal d'explication + simulateur d'évolution + générateur de rappels.
**Critères d'acceptation.** Déterminisme (mêmes entrées → mêmes sorties) ; chaque montant explicable (ExplainabilityEngine consomme la DNF + les sources de valeur) ; dépendances résolues sans cycle ; **0 règle réglementaire dans le code** ; perfs cibles V4 (bulletin < 300 ms) ; simulation d'évolution < 2 s pour 200 agents.
**Tests.** Unitaires par calculateur (cas nominal, limites, erreurs) + DNF + sources de valeur + tests de reproductibilité + tests IRG par tranche/période + **test de parité** : un bulletin calculé avant rétroactivité reste identique après ; les rappels sont des lignes additionnelles, pas des modifications. Comparaison aux montants attendus dès réception des bulletins de référence (⛔Q11).
**Risques.** Ambiguïté barème 2022+ (⛔Q4b) → *paramétrable, ajustable sans recompilation*. DNF subtile → *test systématique contre la matrice ISSRP*.
**Jalon J4 — CRITIQUE.** Validation d'un bulletin enseignant complet et de son explicabilité.

---

## Phase 5 — Application & Persistence
**Objectif.** Orchestrer les cas d'usage et brancher la persistance réelle, y compris les use cases du Workbench réglementaire (D5-D11).

**Jalon D — Fondations Agent/Carrière/Période : ✅ FAIT (16/07/2026).** Migration
`V011__agents_carriere.sql` (`Agents`, `Carrieres` versionnée point-in-time, `Periodes`
avec cycle de vie ADR-0008, `AgentAttributs`/`AgentRubriques`/`AgentRubriqueParametres`/
`AvertissementsHistorique` — DDL portée depuis J3H §4/7/8) + `AgentCarriereRepository`
(Infrastructure) qui résout un `AgentContext` réel depuis la base (remplace la
construction à la main utilisée en Phase 4). 328 tests verts (301 + 27 nouveaux).
Conception discutée avec Tome B vol. 8 §4-§12, 4 écarts validés STOP&ASK (Agent =
identité pure, Carrière unique poste+affectation, Contrat différé, 4 tables J3H
incluses dès V011). **Reste explicitement hors périmètre** de ce jalon : le
`VariableEngine` (résolution `INDICE_MIN`/`TRT`/... depuis la grille réelle) et les
use cases d'écriture (`CréerAgent`, Workbench D5-D11 ci-dessous) — tâches 1-6
restent donc entières.

**VariableEngine : ✅ FAIT (16/07/2026).** `VariableRepository` (Infrastructure,
`Repositories/Payroll/`) résout `INDICE_MIN`/`INDICE_ECH`/`VPI`/`TBASE`/`TRT`/`ECH`/
`CAT` depuis `GrilleIndiciaire`/`IndicesEchelon`/`ValeurPoint` à une date de paie,
point-in-time (même schéma que `AgentCarriereRepository`), à partir des seuls
`Categorie`/`Echelon` (int) déjà portés par `AgentContext` — pas besoin d'exposer
les ID texte `CategorieId`/`EchelonId` (résolus via `Categories.Niveau`/
`Echelons.Numero`, uniques). `BulletinEndToEndTests.Bulletin_enseignant_depuis_un_agent_reel_seede_en_base`
n'a plus aucune valeur fournie à la main : agent, carrière et variables de base
sont tous résolus depuis SQLite. Tâches 1-6 (use cases, DI, Workbench D5-D11)
restent entières.

**`CalculerBulletin` (1er use case pilote, tâche 4) : ✅ FAIT (16/07/2026).**
Premier point d'entrée `Application` réel : `Domain/Calcul/Repositories/`
définit trois ports (`IAgentCarriereRepository`, `IVariableRepository`,
`IPayrollReadRepository`) — nécessaires car `DependencyRulesTests` interdit à
`Application` de référencer `Infrastructure` directement ; les 3 repositories
existants les implémentent sans changement de comportement.
`Application/Payroll/UseCases/CalculerBulletin.cs` orchestre ces ports +
`CalculationPipeline` (instancié directement, service Domain pur). Lecture
seule — pas de persistance (`ValiderBulletin`, à venir). 336 tests verts
(334 + 2). Câblage DI dans `Bootstrapper`, persistance, et les 4 autres use
cases pilotes restent entiers.

**`CréerAgent` (2e use case pilote, tâche 4) : ✅ FAIT (16/07/2026).** Premier
use case d'**écriture** : `Domain/Agents/` (nouveau bounded context) définit
`NouvelAgent` (DTO partagé, réutilisé tel quel comme paramètre du use case —
pas de duplication) et le port `IAgentRepository.CreerAsync`.
`AgentRepository` (Infrastructure) crée `Agents`+`Carrieres` en une seule
transaction Dapper (`BeginTransaction`/`ExecuteAsync`/`Commit`, patron déjà
utilisé par `ReglementaireSeeder`), Id généré en GUID (ADR-0004 : tables de
gestion = GUID, contrairement aux référentiels = code métier), matricule
vérifié unique avant insertion (`Error.Conflict` explicite, pas d'exception).
`Application/Agents/UseCases/CreerAgent.cs` valide les champs requis et les
valeurs énumérées (Sexe/SituationFamiliale/TypeContrat) avant d'appeler le
port — première utilisation réelle d'`IClock` (jusque-là défini mais jamais
injecté) pour `CreatedAt`. FK (Grade/Catégorie/Échelon/Fonction/
Établissement) non vérifiées proactivement — hors périmètre V1, cohérent
avec le reste des repositories. Test bout-en-bout : un agent créé par
`CreerAgent` est immédiatement résoluble par `AgentCarriereRepository`
(lecture) — les deux use cases pilotes sont cohérents sur le même schéma.
340 tests verts (336 + 4). Câblage DI, persistance du bulletin, et les 3
autres use cases pilotes (`ValiderBulletin`, `ConsulterBulletin`,
`GérerRéférentiels`) restent entiers.

**`ValiderBulletin` (3e use case pilote, tâche 4) : ✅ FAIT (16/07/2026).**
Fige le bulletin via le Snapshot Engine (Phase 4) et le persiste — prérequis
bloquant d'ADR-0008 pour tout futur rappel. Migration `V012__bulletins.sql`
(`Bulletins`, unicité `(AgentId, DatePaie)` — un bulletin validé n'est jamais
réécrit). Obstacle technique découvert et résolu : 5 Value Objects du
sous-arbre calc (`Fraction`, `PeriodeReglementaire`, `BaremeValue`,
`ConditionEligibilite`, `CritereEligibilite`) ont un constructeur privé +
fabrique `.Creer(...)`, non désérialisables par défaut par
`System.Text.Json` — 5 `JsonConverter<T>` sur mesure
(`Infrastructure/Serialization/BulletinSnapshotJsonConverters.cs`). Un test
dédié prouve l'exigence dure d'ADR-0008 : sérialiser puis désérialiser un
snapshot et rejouer `CalculationPipeline.Calculer` sur l'Input désérialisé
reproduit le bulletin à l'identique (net 57 739 DA, pas une réévaluation du
passé). `IBulletinRepository`/`BulletinRepository` suivent le même patron
que `AgentRepository` (vérification d'unicité avant écriture →
`Error.Conflict` explicite). `ValiderBulletin` duplique l'orchestration de
lecture de `CalculerBulletin` plutôt que de la factoriser (celui-ci ne
renvoie pas le `PayrollInput` nécessaire au snapshot — changer son contrat
aurait cassé ses tests). Transition d'état `Periodes`
(`OUVERTE`→`VALIDEE`/`CLOTUREE`) explicitement hors périmètre — l'unicité
`(AgentId, DatePaie)` suffit à garantir l'immutabilité de base. 345 tests
verts (340 + 5). Câblage DI, `ConsulterBulletin`, `GérerRéférentiels`, et les
use cases Workbench D5-D11 restent entiers.

**`ConsulterBulletin` (4e use case pilote, tâche 4) : ✅ FAIT (16/07/2026).**
Symétrique en lecture de `ValiderBulletin` : `IBulletinReadRepository`/
`BulletinReadRepository` (`Repositories/Payroll/`, même dossier, nom en écho
à `PayrollReadRepository`) relisent `SnapshotJson` et le désérialisent via
les mêmes convertisseurs que l'écriture (`BulletinSnapshotJson.Options`,
tranche précédente) — aucun nouveau problème de sérialisation. Le use case
projette `Bulletin` depuis le `BulletinSnapshot` complet (pas de
recalcul : ADR-0008, le snapshot est la seule source de vérité une fois
validé). Testé bout-en-bout en enchaînant `ValiderBulletin` puis
`ConsulterBulletin` dans le même test — le bulletin relu est identique à
celui validé (net 57 739 DA). Tranche volontairement petite (aucune
recherche/décision nouvelle, tout le travail dur avait été fait pour
`ValiderBulletin`) — passée directement sans repasser par Plan Mode. 349
tests verts (345 + 4). Il ne reste de la tâche 4 que `GérerRéférentiels`.
Câblage DI `Bootstrapper` et use cases Workbench D5-D11 restent entiers.

**`GérerRéférentiels` — grille indiciaire (5e et dernier use case pilote,
tâche 4) : ✅ FAIT (16/07/2026).** Q3 couvre plusieurs entités
(rubriques/cotisations/barèmes/grille) ; périmètre choisi avec l'utilisateur :
grille indiciaire (`ValeurPoint`/`GrilleIndiciaire`/`IndicesEchelon`, V003) —
symétrique en écriture de `VariableRepository` (VariableEngine). Écart
corrigé avant implémentation en relisant ADR-0004 : ces tables sont des
tables de **référentiel** (PK = code métier, ex. `"GI-13-2024-01-01"`), pas
des tables de gestion (GUID) comme `Agents`/`Bulletins` — le réflexe des
tranches précédentes (GUID) aurait été faux ici. `IGrilleIndiciaireRepository`/
`GrilleIndiciaireRepository` (3 méthodes, même algorithme de versionnement :
rejet si la date d'effet existe déjà — `Conflict` — ou n'est pas postérieure
à la version en vigueur — `Validation` — sinon ferme la version courante
(`DateFin` = veille exacte, `DateOnly`) et insère la nouvelle en transaction).
3 use cases Application distincts (`DefinirValeurPoint`,
`DefinirIndiceMinGrille`, `DefinirIndiceEchelon`, nouveau dossier
`Application/Referentiels/UseCases/`) plutôt qu'une classe à 3 méthodes —
ce sont 3 actions utilisateur distinctes même si l'implémentation
Infrastructure est mutualisée. Test bout-en-bout : écrire deux versions
successives d'un indice de catégorie puis relire via `VariableRepository`
(déjà livré) résout la bonne valeur selon la date — preuve que l'écriture et
la lecture point-in-time restent cohérentes. 363 tests verts (349 + 14).
**Les 5 use cases pilotes de la tâche 4 sont complets.** Reste : câblage DI
`Bootstrapper`, use cases Workbench D5-D11 (tâche 5), et les autres
entités de `GérerRéférentiels` (rubriques/cotisations/barèmes) si besoin.

**Tâche 3 — Composition Root (câblage DI) : ✅ FAIT (16/07/2026),
partiellement.** `Application`/`Infrastructure` référençaient déjà
`Microsoft.Extensions.DependencyInjection.Abstractions` sans jamais l'avoir
utilisé — scaffolding anticipé, jamais câblé. `AddInfrastructure(services,
connectionString)` (`Infrastructure/DependencyInjection/`) enregistre
`IClock`→`SystemClock` (Singleton), `SqliteConnection` (Scoped, `PRAGMA
foreign_keys=ON` à l'ouverture) et les 7 repositories déjà livrés (Scoped).
`AddApplication(services)` enregistre les 7 use cases pilotes +
`SimulerEvolutionReglementaire` (Transient). Testé en résolvant
`CalculerBulletin` depuis un vrai conteneur DI (`ServiceCollection` +
`BuildServiceProvider`) et en l'exécutant contre une base SQLite migrée et
seedée — même résultat (net 57 739 DA) que tous les tests qui instanciaient
les classes à la main. **Volontairement laissé de côté** : `App.xaml.cs`/
`MainWindow.xaml` (ouvrir une fenêtre vide sur `Presentation` — toujours un
projet vide — ne prouverait rien de plus) et `appsettings.json`/liaison
`IConfiguration` réelle (aucune convention de chemin `.db` de production
n'existe encore, pas inventée ici). Ce sera à câbler par Bootstrapper quand
Phase 6 aura un Shell réel. Écart doc/code relevé en passant, non traité :
`docs/CONVENTIONS.md` §5 prescrit un objet valeur `Money`, jamais utilisé
depuis Phase 4 (`decimal` nu partout) — hors périmètre de cette tranche.
365 tests verts (363 + 2). Rien commité.

**Tâche 5 — Use cases Workbench (D5-D11) : `SuggererRubriques` (D5) livré
(16/07/2026), 1/7.** Pour un agent et une date, évalue les rubriques
affectables (`EstAffectableManuellement=1`) via le moteur DNF
(`RegleEligibiliteEvaluator`, déjà livré) et crée une ligne `AgentRubriques
(Statut=SUGGEREE, Origine=GROUPE:<Id>@<DateEffet>)` pour chaque rubrique dont
un groupe DNF est satisfait — cycle ISSRP de J3H §10(a). Portée volontairement
limitée aux rubriques dont l'éligibilité repose sur un groupe DNF (`GroupeId`
non nul) : une rubrique affectable éligible seulement par conditions communes
(sans groupe) n'a pas de `GroupeId` à citer dans `Origine` et n'a aucun
exemple documenté — ignorée dans cette tranche, pas une erreur. Nouveaux
ports `IWorkbenchReadRepository`/`IAgentRubriqueRepository`
(`Domain/Workbench/Repositories/`, 3e bounded context de ports après
`Agents`/`Calcul`) ; `WorkbenchReadRepository` (déjà livré, jamais interfacé)
gagne `ListerRubriquesAffectablesAsync` + implémente le nouveau port.
Écriture idempotente (`AgentRubriqueRepository.SuggererAsync` : `Result<string?>`,
`null` = déjà suggérée/affectée, no-op silencieux, jamais de doublon). Câblé
dans `AddInfrastructure`/`AddApplication` (Composition Root déjà livré).
Test bout-en-bout rejoue exactement le cycle ISSRP avec les groupes réels
`GE-ISSRP45-DIRECT`/`GE-ISSRP45-ORIGINE` (J4F) déjà utilisés par
`BulletinEndToEndTests` — même grades (`SDL-G007` éligible conditionnel,
`A-G048` hors groupe), preuve que suggestion d'affectation et éligibilité au
calcul restent cohérentes. N'écrit jamais `AvertissementsHistorique` (émis à
l'acceptation, pas à la suggestion). 371 tests verts (365 + 6).

**`AccepterSuggestion`/`SupprimerAffectation`/`SuspendreAffectation` (D5)
livrés (17/07/2026), 4/7.** Implémentent la machine à états J3H §7
(`SUGGEREE ⇄ ACCEPTEE ⇄ SUSPENDUE`, tout état non-`SUPPRIMEE` →
`SUPPRIMEE` terminal). « Aucune transition n'est bloquée par une règle » →
aucune précondition sur l'état de départ ; seule la sortie de l'état
terminal `SUPPRIMEE` est refusée explicitement. `IAgentRubriqueRepository`
étendu (pas un nouveau port) avec `ListerParAgentAsync` (nouvelle projection
`AffectationRubrique`, traçabilité complète y compris les lignes
`SUPPRIMEE`) et **une seule méthode générique** `ChangerStatutAsync` pour
les 3 transitions (mécaniquement identiques) — mais **3 use cases
Application séparés** (même choix que `DefinirValeurPoint`/`...Grille`/
`...Echelon` : mécaniquement proches, sémantiquement distincts, nommage
déjà fixé par ce document). `AccepterSuggestion` gère à la fois
`SUGGEREE→ACCEPTEE` et la réactivation `SUSPENDUE→ACCEPTEE` (même cible sur
le diagramme). `AvertissementsHistorique` toujours **hors périmètre**
(nécessite de résoudre `MessageAffiche` depuis `GroupeId`→`MessageId`→
`MessagesRegles.TexteFr`, machinerie non construite). Test bout-en-bout
enchaîne `SuggererRubriques` (cycle ISSRP réel) → transition → vérifie le
nouveau `Statut`, y compris suspension puis réactivation et le rejet d'une
transition sur une ligne déjà `SUPPRIMEE`. Pas de câblage UI dans cette
tranche (retour Application après plusieurs tranches Phase 6 ; l'écran
`SuggererRubriques` sera enrichi plus tard). 392 tests verts (385 + 7).

**`ListerMatriceCouverture` (D11) livré (17/07/2026), 5/7.** Vue tabulaire
`corps × rubriques` (J3I §5.5) : indique par couple si une règle
d'éligibilité couvre ce corps pour cette rubrique (`Couverte`) et si elle
est actuellement en vigueur (`Active`). Découverte structurante avant
conception : les données réelles (ISSRP) référencent **exclusivement**
`CritereId='GRADE'`, jamais `CORPS` directement — une matrice ignorant ça
afficherait zéro couverture pour le seul exemple détaillé du projet. Résout
donc les conditions `GRADE` vers leur corps via `Grades.CorpsId` en plus des
conditions `CORPS` directes ; seuls `=`/`IN` sont résolus (`NOT_IN` ignoré,
limite documentée). `IWorkbenchReadRepository` étendu avec 4 méthodes,
dont `ListerConditionsCorpsGradeAsync` qui **ne filtre pas par date**
(contrairement à toutes les autres méthodes de lecture) — la matrice a
besoin de l'historique complet pour distinguer Vert (actif) d'Orange
(expiré), calcul fait en Application, jamais en SQL. Aucune couleur
calculée côté backend : renvoie des faits bruts (`Couverte`/`Active`), la
4e nuance du mockup (« Gris = non applicable ») n'a pas de définition
précise dans J3I et sera une règle d'affichage Phase 6. Tests bout-en-bout
sur les données ISSRP réelles (seedées par `ReglementaireSeeder`, aucune
donnée inventée) : le corps `IDLS` (seul corps avec des grades ISSRP
réellement seedés, via les 4 grades hors catégorie IDLS-G144/145/146/148)
est couvert et actif pour `ISSRP_45`, couvert mais inactif pour `ISSRP_15`
(uniquement via le groupe historique expiré), non couvert pour `QUALIF`
(barème, pas d'éligibilité). Pas de câblage UI (vue matricielle = tâche 9
Phase 6). 402 tests verts (395 + 7).

**17/07/2026 — `GenererRappels` (D9) livré, portée volontairement
réduite.** La conception documentée (J3C §11, J4E §8) décrit le rappel
comme une ligne d'un **futur bulletin ouvert** (rubrique de rappel), pas
une table à part — mais `Bulletins` (V012) stocke un `SnapshotJson` figé
en un seul `INSERT` par `ValiderBulletin`, sans aucun mécanisme pour
ajouter des lignes à un futur bulletin ; implémenter le design complet
demanderait de modifier le moteur de calcul lui-même, hors périmètre
d'une tranche. **Simplification assumée** : nouvelle table dédiée
`Rappels` (V013, `IRappelRepository`/`RappelRepository`) qui persiste les
`LigneRappel` produites par `RappelCalculator` (Phase 4, jusqu'ici jamais
appelé hors de ses propres tests) — un agent + un bulletin validé à la
fois (pas de balayage multi-agents par période, cette énumération
n'existe toujours pas). `GenererRappels` recalcule « à droit constant
actuel » à la **même** `DatePaie` que le bulletin d'époque : le patron de
résolution point-in-time déjà en place (`WHERE DateEffet <= @date ORDER
BY DateEffet DESC LIMIT 1`) fait automatiquement remonter une évolution
réglementaire rétroactive, sans logique de détection dédiée. Garde
d'idempotence en Application (`IRappelRepository.ExisteAsync` avant tout
recalcul, `Error.Conflict` si des rappels existent déjà pour cet agent à
cette date) ; aucun delta réel → succès avec liste vide, rien persisté.
Test bout-en-bout : agent pilote `A-PILOTE` déjà utilisé par
`ValiderBulletinTests`/`CalculerBulletinTests`, évolution rétroactive
simulée par l'insertion directe d'une nouvelle `ValeurPoint` avec
`DateEffet` antérieure à la `DatePaie` du bulletin validé — les lignes
renvoyées par le use case sont vérifiées contre les valeurs persistées en
base, pas de montant deviné à l'avance. 408 tests verts (402 + 6). Reste
`AppliquerEvolutionReglementaire`, `CloreVersion`/`DupliquerVersion` —
dépendent toujours de briques non construites (`AuditLog` jamais câblé en
code, pas de cible unique pour « clôturer/dupliquer une version
réglementaire »). **Dette explicite** : le rattachement des rappels à un
véritable futur bulletin (le design documenté) reste à faire — cette
tranche les rend seulement calculables et traçables en base.

**17/07/2026 — `DupliquerVersion` livré ; `CloreVersion` clarifié comme
déjà couvert.** Recherche dédiée avant conception : `CloreVersion` au
sens « clôture + nouvelle version » (mode 2, J3I §7.4) est en fait déjà
ce que fait `DefinirValeurPoint`/`DefinirIndiceMinGrille`/`DefinirIndiceEchelon`
(Phase 5 tâche 4) — `GrilleIndiciaireRepository.DefinirXxxAsync` ferme
atomiquement la version courante avant d'insérer la nouvelle valeur ; il
n'y a aucun scénario métier documenté pour « clôturer un point d'indice
sans le remplacer ». **Aucun nouveau code pour `CloreVersion`** — le
sous-item est donc considéré clos par ce qui existe déjà. `DupliquerVersion`
(mode 3, « Duplication »), en revanche, n'avait aucun précédent de code :
livré, scopé à `ValeurPoint` (même périmètre que `GérerRéférentiels`) —
`IGrilleIndiciaireRepository.DupliquerValeurPointAsync` clone la valeur
en vigueur (lue, pas redemandée) vers une nouvelle `DateEffet`/`Version`/`Source`,
en réutilisant telles quelles `ValiderContinuite`/`FermerVersionAsync`
(les mêmes méthodes privées que `DefinirValeurPointAsync`) ; échoue
`NotFound` si aucune version n'est en vigueur à cloner (distinct de
`DefinirValeurPointAsync`, qui accepte l'absence de version courante
comme une première définition normale). 414 tests verts (408 + 6). Reste
`AppliquerEvolutionReglementaire` — dépend toujours d'`AuditLog`, jamais
câblé en code.

**17/07/2026 — `AppliquerEvolutionReglementaire` (D8) livré — tâche 5
complète, 7/7 use cases Workbench.** Premier câblage applicatif
d'`AuditLog` (V001) : jamais utilisée en code jusqu'ici (confirmé par 2
recherches successives), désormais écrite par
`IAuditLogRepository`/`AuditLogRepository` (un seul `INSERT`, pas de
transaction nécessaire). Portée limitée à `ValeurPoint`, aux 2 modes de
J3I §7.4 qui ont un chemin d'écriture réel : **clôture + nouvelle
version** (délègue à `DefinirValeurPointAsync`) et **duplication**
(délègue à `DupliquerValeurPointAsync`, livré à la tranche précédente) —
« Modification en place » (qualifiée de « rare » par la doc elle-même)
reste hors périmètre, sans précédent d'écriture. **Deux limites
assumées et documentées, pas cachées** : (1) aucune notion d'identité
utilisateur n'existe dans tout le projet (pas d'authentification, de
session, ni d'`IUserContext`) — `Actor` reste un paramètre fourni par
l'appelant, à brancher sur un vrai système d'auth plus tard ; (2)
l'écriture réglementaire et la ligne `AuditLog` ne sont **pas**
atomiques entre elles — aucun `IUnitOfWork` n'existe dans le projet et
chaque méthode de `GrilleIndiciaireRepository` gère sa propre
transaction interne sans en exposer une externe ; si l'audit échoue
après un commit réglementaire réussi, le use case renvoie un échec
explicite (pas un succès menteur) mais le changement reste en base.
`Demande` exige un `RapportImpact` (le DTO déjà produit par
`SimulerEvolutionReglementaire`, jusqu'ici jamais consommé par personne)
— une convention de forme d'API pour la porte « dry-run vu », pas une
preuve cryptographique ; il est sérialisé dans le `Payload` `AuditLog`.
420 tests verts (414 + 6). **Tâche 5 (use cases Workbench, D5-D11)
terminée.** Reste tâche 6 Phase 5 (validation applicative transverse) et
le câblage UI des use cases Workbench (Phase 6, tâches 4-10).

**17/07/2026 — Tâche 6 Phase 5 (validation applicative transverse)
terminée.** Spec réduite à une ligne (« rejet de tout commit Workbench
sans dry-run préalable, sauf bypass admin (`AuditLog`記录) ») — dans le
contexte du seul use case qui « commit » réellement une évolution
réglementaire (`AppliquerEvolutionReglementaire`), c'était en fait déjà
**plus strict** que la spec : `RapportImpact` était non-nullable, donc
aucun bypass n'était possible du tout. Corrigé : `Demande.RapportImpact`
devient nullable, `BypassDryRun`/`RaisonBypass` ajoutés — sans bypass,
`RapportImpact` reste obligatoire (`Validation` sinon) ; avec bypass,
`RaisonBypass` devient obligatoire à la place (`Validation` sinon) ;
`AuditLog.Action` distingue `APPLIQUER_EVOLUTION` de
`APPLIQUER_EVOLUTION_BYPASS` — filtrable sans désérialiser le `Payload`,
pour un vrai usage de conformité (« qui a bypassé, quand, pourquoi »).
3 nouveaux tests (sans dry-run ni bypass → échec ; bypass sans raison →
échec ; bypass avec raison → succès, action tracée). 425 tests verts
(422 + 3). **Phase 5 est maintenant complète dans son intégralité**
(tâches 1-6). Reste uniquement le câblage UI des use cases Workbench
(Phase 6, tâches 2 et 4-10).

**Tâches.**
1. **Application** : Use Cases (CQRS léger), Commands/Queries, DTO, mapping, validation applicative, `IUnitOfWork`, notifications, gestion d'erreurs normalisée.
2. **Persistence** : `Persistence Models` distincts des entités (ADR-066), mappers, **repositories spécialisés** par agrégat (Dapper), transactions, migrations, backup/restore — incluant les nouveaux repositories V009 (`BaremeRepository`, `SourceValeurRepository`, `GroupeEligibiliteRepository`, `CriteresEligibiliteRepository`).
3. Câblage DI complet dans `Bootstrapper` (Composition Root) — y compris l'enregistrement des `SourceValeurCalculator` (pattern Open/Closed).
4. Use cases pilotes : CréerAgent, CalculerBulletin, ValiderBulletin, ConsulterBulletin, GérerRéférentiels (paramétrage).
5. **Use cases Workbench** (D5-D11) :
   - `SuggererRubriques` (J3H lot 3) — sur création d'agent, basé sur l'évaluateur DNF
   - `AccepterSuggestion` / `SupprimerAffectation` / `SuspendreAffectation` — avec historisation
   - `SimulerEvolutionReglementaire` (D8) — appel du `SimulerEvolutionReglementaire` de Phase 3bis via l'orchestrateur applicatif ; retourne un DTO `RapportImpact`
   - `AppliquerEvolutionReglementaire` (D8) — orchestre le dry-run → validation explicite utilisateur → commit transactionnel → audit
   - `CloreVersion` / `DupliquerVersion` — workflow d'évolution §7 J3I
   - `GenererRappels` (D9) — pour une version rétroactive, génère les lignes de rappel pour les bulletins validés impactés
   - `ListerMatriceCouverture` (D11) — vue `corps × rubriques` avec code couleur
6. **Validation applicative transverse** : rejet de tout commit Workbench sans dry-run préalable, sauf bypass admin (`AuditLog`记录).

**Livrables.** Chaîne complète Application→Domain→Persistence→SQLite fonctionnelle (hors UI) — incluant tous les use cases Workbench.
**Critères d'acceptation.** Domaine sans dépendance Dapper/SQLite ; transactions atomiques ; calcul + persistance d'un bulletin via use case ; **simulation sans écriture** ; **commit gated par dry-run** ; **rappels = lignes additionnelles immuables** ; perfs d'accès conformes V4.
**Tests.** Intégration repositories (SQLite temporaire) + tests de use cases (mocks) + **test d'immutabilité** : bulletin validé ne change pas après une évolution rétroactive.
**Jalon J5.**

---

## Phase 6 — Présentation WPF/MVVM (Workbench + UI bulletin)
**Objectif.** Interface professionnelle MVVM strict (V4 Tome E) — incluant le Workbench réglementaire complet (D7).
**Tâches.**
1. **Shell** (fenêtre principale, navigation centralisée `INavigationService`, `IDialogService`, notifications) — avec entrée « Workbench réglementaire » dans le menu principal. **✅ FAIT (16/07/2026)**, notifications exceptées. `Presentation` (`Navigation/`, `Dialogs/`, `Shell/`, `Payroll/`, `Workbench/`, `DependencyInjection/`) porte tout — `Bootstrapper` reste une pure Composition Root (`App.xaml.cs` : `Host.CreateApplicationBuilder()`, migrations avant affichage, résolution DI de `ShellWindow`, plus de `MainWindow`/`StartupUri`). Navigation ViewModel-first (`DataTemplate` implicites, `Presentation/Resources/ViewTemplates.xaml`) — aucune logique en code-behind. Écran réel livré : « Calculer un bulletin » (`CalculerBulletinViewModel`, appelle le use case `CalculerBulletin` de Phase 5) — preuve de la chaîne complète WPF→ViewModel→Application→Infrastructure→SQLite ; l'entrée « Workbench réglementaire » reste un panneau placeholder (tâche 4). Chemin de base par défaut fixé (`%LOCALAPPDATA%\PaieEducation\paie.db`, overridable via `appsettings.json`). Nouveau projet `Tests.Presentation` (`net10.0-windows`, seul moyen de référencer `Presentation` — cf. `Tests.Architecture`) pour les tests de ViewModel (ports mockés). Vérifié par `dotnet run` réel (fenêtre affichée, DB créée, aucun crash) — **pas** une vérification visuelle du rendu, honnêtement signalé comme limite de l'environnement. 373 tests verts (371 + 2). Notifications explicitement hors périmètre (aucune spécification, aucun consommateur réel). Reste : tâches 2-10 (Design System, écrans référentiels/agents/bulletin, arborescence Workbench complète, FormulaEditor, éditeurs barème/DNF, assistant d'évolution, vues matricielles).
2. Design System + Business Controls (MoneyTextBox, sélecteurs corps/grade/échelon/période, `ExplainabilityPanel`, `ERPDataGrid`).
3. Écrans : **paramétrage des référentiels** (rubriques, taux cotisations, barèmes, grilles — édition par l'utilisateur, Q3) ; gestion agents/carrière ; lancement/contrôle des calculs ; **consultation bulletin + panneau d'explication**.
   - **Créer un agent : ✅ FAIT (16/07/2026)**, 2e écran réel du Shell. `CreerAgentViewModel`/`CreerAgentView` (`Presentation/Agents/`), appelle le use case `CreerAgent` (Phase 5). `Sexe`/`SituationFamiliale`/`TypeContrat` en `ComboBox` (listes fermées, mêmes valeurs que les `CHECK` V011 — pas besoin d'une source de données) ; `GradeId`/`CategorieId`/`EchelonId`/`FonctionId`/`EtablissementId` en code référentiel brut (texte) — aucun sélecteur métier construit encore (nécessiterait un use case de liste des référentiels, hors périmètre ; relève de la tâche 2 Design System). Menu Shell étendu (`Agents` → `Créer un agent`). Vérifié par `dotnet run` réel (fenêtre affichée, aucun crash au chargement du nouveau `DataTemplate`). 375 tests verts (373 + 2).
   - **Valider un bulletin + Consulter un bulletin : ✅ FAIT (17/07/2026)**,
     3e et 4e écrans réels. `ValiderBulletinViewModel`/`View` et
     `ConsulterBulletinViewModel`/`View` (`Presentation/Payroll/`), même
     patron que `CalculerBulletin` (mêmes valeurs fixes `ClesBareme`/
     `SourcesValeur`, même dette connue). Menu `Paie` complet : Calculer /
     Valider / Consulter — cycle de vie du bulletin entièrement navigable
     depuis le Shell. 4 tests de ViewModel (ports mockés, dont un
     `PayrollInput` minimal réel pour prouver que le pipeline/Snapshot
     Engine s'exécute vraiment). Vérifié par `dotnet run` réel. 379 tests
     verts (375 + 4).
   - **Grille indiciaire (paramétrage référentiels, Q3) : ✅ FAIT
     (17/07/2026)**, 5e écran réel. `GrilleIndiciaireViewModel`/`View`
     (`Presentation/Referentiels/`) — **un seul écran, 3 onglets**
     (Valeur du point / Indice min. catégorie / Indice d'échelon), un par
     use case déjà livré (`DefinirValeurPoint`/`DefinirIndiceMinGrille`/
     `DefinirIndiceEchelon`, Phase 5) — regroupés parce qu'ils partagent le
     même repository et le même concept métier, pas 3 écrans séparés.
     Validation du format numérique côté ViewModel avant l'appel au use
     case (`decimal`/`int.TryParse`, message d'erreur explicite si invalide
     — jamais une exception de parsing qui fuit). Menu Shell étendu
     (`Référentiels` → `Grille indiciaire`). 4 tests de ViewModel. Vérifié
     par `dotnet run` réel. 383 tests verts (379 + 4).
     **4e onglet « Dupliquer » ajouté le 17/07/2026** après la livraison
     Phase 5 de `DupliquerVersion` — même patron exact que les 3 autres
     onglets (formulaire simple, use case déjà existant), extension
     mécanique d'un patron déjà validé (pas de repassage par Plan Mode).
     2 nouveaux tests de ViewModel. Vérifié par `dotnet run` réel. 422
     tests verts (420 + 2).
   - **Suggérer des rubriques : ✅ FAIT (17/07/2026)**, 6e écran réel — le
     premier consommant un use case **Workbench** (`SuggererRubriques`, D5,
     Phase 5). `SuggererRubriquesViewModel`/`View`
     (`Presentation/Workbench/`), menu étendu (`Agents` → `Suggérer des
     rubriques` — action agent-centrique, distincte du placeholder
     `Workbench réglementaire` qui reste réservé à l'arborescence complète,
     tâche 4). Ferme la boucle Créer un agent → Suggérer ses rubriques →
     Calculer/Valider son bulletin, entièrement navigable depuis le Shell.
     2 tests de ViewModel. Vérifié par `dotnet run` réel. 385 tests verts
     (383 + 2). **Les 5 use cases Application non-Workbench + 1 use case
     Workbench (sur 9 au total) ont maintenant tous un écran** — restent
     `SimulerEvolutionReglementaire` (plus complexe, relève plutôt de la
     tâche 8 « Assistant d'évolution réglementaire ») et les 6 autres use
     cases Workbench pas encore construits (tâche 5 Phase 5).
   - **Écran enrichi (17/07/2026)** : après la livraison Phase 5 des 3 use
     cases de transition d'état (`AccepterSuggestion`/`SupprimerAffectation`/
     `SuspendreAffectation`), l'écran devient un vrai écran de gestion —
     premier `DataGrid` de la session (les 6 écrans précédents n'étaient que
     des formulaires simples). Nouveau use case `ListerAffectationsAgent`
     (enveloppe `IAgentRubriqueRepository.ListerParAgentAsync`, même patron
     que `ConsulterBulletin`/`IBulletinReadRepository`) — créé pour préserver
     la frontière Presentation→Application (aucun écran ne référence
     directement un port Domain, seulement des use cases). Liste les
     affectations de l'agent (y compris `SUPPRIMEE`, traçabilité complète) ;
     boutons Accepter/Suspendre/Supprimer par ligne (`DataGridTemplateColumn`,
     `CommandParameter="{Binding Id}"`) ; après chaque action, la liste est
     **rechargée depuis la base** (jamais de mise à jour optimiste). 3
     nouveaux tests. 395 tests verts (392 + 3).
   - **Sélecteurs référentiels réels (17/07/2026)** : `Grade`/`Catégorie`/
     `Échelon`, saisis en texte brut jusqu'ici sur `CréerAgent` et
     `Grille indiciaire` (Design System, tâche 2, en dépendait), sont
     maintenant des `ComboBox` réels. Nouveau port
     `IReferentielReadRepository` (`Domain/Calcul/Repositories/`, même
     dossier que `IGrilleIndiciaireRepository` — Grades/Categories/Echelons
     sont la nomenclature V002 consommée par le sous-arbre Calcul) et use
     case `ListerReferentiels` (`Application/Referentiels/UseCases/`) qui
     agrège les 3 listes en un seul aller-retour (même patron que
     `ListerMatriceCouverture` — pas 3 use cases séparés, ces lectures
     n'ont pas d'intention métier distincte). Nouveau patron de
     chargement : `[RelayCommand] ChargerReferentielsAsync` invoqué en
     fire-and-forget dans le constructeur du ViewModel (peuple les listes
     dès l'ouverture de l'écran, sans logique dans le code-behind XAML) —
     premier écran de la session à charger des données sans action
     utilisateur explicite. 6 nouveaux tests (3 repository, 1 use case, 2
     ViewModel). Vérifié par `dotnet run` réel. 431 tests verts (425 + 6).
     **Design System (tâche 2) partiellement débloqué** : sélecteurs
     simples livrés pour Grade/Catégorie/Échelon ; les contrôles
     réutisables (`MoneyTextBox`, `ERPDataGrid`, etc.) et les sélecteurs
     manquants (Fonction/Établissement — absents de `NouvelAgent`, donc
     jamais bloquants jusqu'ici) restent à faire.
4. **Workbench réglementaire — arborescence complète** (D7, J3I §5) :
   - `Rubriques` (catalogue) → fiches par rubrique avec onglets : Identité, **Formule** (FormulaEditor avec coloration + auto-complétion + simulation sur agent témoin), **Paramètres** (clé/valeur typé, P2/P9), **Barème** (P4/P5/P6/P12, éditeur de tranches avec garde-fous), **Éligibilité** (P7/P8, éditeur de groupes DNF), **Audit** (L-U6, timeline + recherche).
     **Fiche rubrique en lecture seule (Identité + Barème + Éligibilité) : ✅
     FAIT (17/07/2026)**, 11e écran réel. `FicheRubriqueViewModel`/`View`
     (`Presentation/Workbench/`, `TabControl` à 3 onglets, patron déjà
     établi 4x) — `ConsulterFicheRubrique` (nouveau use case) agrège
     Identité (nouvelle méthode `IWorkbenchReadRepository.ObtenirRubriqueAsync`,
     nouveau DTO `RubriqueDetail`), Barème (`ListerBaremesRubriqueAsync`, déjà
     implémenté en Infrastructure depuis Phase 5 mais jamais promu sur le
     port — 1 ligne d'interface ajoutée, zéro nouvelle requête SQL) et
     Éligibilité (`ListerConditionsParRubriqueAsync`/`ListerGroupesParRubriqueAsync`,
     déjà sur le port et déjà consommées en production par `SuggererRubriques`
     — zéro nouveau code Infra/Domain). **Couvre de facto l'item « IFC
     (P12) »** : IFC n'est pas une entité à part, seulement une rubrique
     dont le barème est catégoriel (`RubriqueBaremes`, dimension
     `CATEGORIE`) — consultable ici comme n'importe quelle autre rubrique,
     inutile de construire un écran dédié. Saisie de `RubriqueId` en texte
     libre (pas de sélecteur `ComboBox` — construire un use case de liste
     des rubriques pour un sélecteur sortait du périmètre de cette
     tranche). Onglets Formule et édition (Barème/Éligibilité en
     écriture) restent hors périmètre — aucun chemin d'écriture pour les
     barèmes/conditions ISSRP n'existe (tâches 5-7). 7 nouveaux tests (3
     repository, 2 use case, 2 ViewModel). Vérifié par `dotnet run` réel.
     445 tests verts (438 + 7).
   - `Cotisations` (P9, P10) → taux, assiettes, composition, mutuelles
   - `Fiscalité (IRG)` (P11) → barèmes (4 ou 6 tranches), règles de période (lissages en fractions exactes), profils spéciaux
   - `Carrière & grilles` → valeur du point, grille indiciaire, IFC (P12). **Valeur
     du point/grille indiciaire déjà couverts** par l'écran Grille indiciaire
     (Phase 6, tâche 3) ; **IFC couvert** par la fiche rubrique ci-dessus.
   - `Simulation` (D8) → sandbox : choisir une rubrique + une modification + un échantillon d'agents → voir le delta
   - `Évolution réglementaire` → assistant 6 étapes (J3I §7) avec dry-run obligatoire
   - `Audit & traçabilité` → vue globale `AuditLog` filtrable (qui/quand/source).
     **Version liste triable : ✅ FAIT (17/07/2026)**, 10e écran réel.
     `AuditLogViewModel`/`View` (`Presentation/Workbench/`), 3e entrée du
     sous-menu Workbench réglementaire. Premier chemin de **lecture**
     d'`AuditLog` (V001) — jusqu'ici seule l'écriture existait
     (`AppliquerEvolutionReglementaire`, tâche 5 Phase 5). Nouvelle
     méthode `IAuditLogRepository.ListerAsync` (500 entrées les plus
     récentes, `ORDER BY OccurredAt DESC LIMIT 500` — pas de pagination
     réelle, plafond simple assumé) + use case mince `ListerAuditLog`
     (même patron que `ListerAffectationsAgent`). Même patron de
     chargement au montage que les sélecteurs référentiels
     (`ChargerCommand` en fire-and-forget dans le constructeur) ; même
     décision de rendu que la matrice de couverture (`DataGrid` plat,
     tri natif, pas de filtre de recherche explicite — « filtrable » du
     mockup reste un filtre implicite via le tri des colonnes, pas une
     barre de recherche dédiée). 5 nouveaux tests (2 repository, 1 use
     case, 2 ViewModel). Vérifié par `dotnet run` réel. 438 tests verts
     (433 + 5).
   - `Matrice de couverture` (D11) → vue tabulaire `corps × rubriques` avec code couleur.
     **Version liste plate : ✅ FAIT (17/07/2026)**, cf. tâche 9 ci-dessous.
5. **FormulaEditor** (L-U2) : coloration syntaxique, auto-complétion, validation à la saisie, simulation sur agent témoin, comparaison avant/après
6. **Éditeur de barème** (L-U1) : table des tranches, garde-fous de continuité (pas de chevauchement, pas de trou, une seule ouverte par clé), prévisualisation d'impact
7. **Éditeur de groupes DNF** (L-M2, L-U3) : graphe visuel « groupe → conditions ET, groupes OU »
8. **Assistant d'évolution réglementaire** (D8, J3I §7) : 6 étapes, dry-run bloquant, rapport d'impact, export PDF
9. **Vues matricielles** (L-U9, L-U10, D11) : grille de couverture avec code couleur, drill-down vers la règle ou la fiche rubrique.
   - **Matrice de couverture (liste plate) : ✅ FAIT (17/07/2026)**, 9e
     écran réel. `MatriceCouvertureViewModel`/`View`
     (`Presentation/Workbench/`), appelle `ListerMatriceCouverture`
     (déjà livré, Phase 5, D11) — aucun nouveau use case Application.
     **Décision de rendu tranchée avec l'utilisateur** : `DataGrid` plat
     à colonnes statiques (Corps/Rubrique/Couverte/Active, tri natif par
     en-tête) plutôt qu'une vraie grille pivotée visuelle avec code
     couleur — construire cette dernière en WPF strict-MVVM (sans
     code-behind) aurait exigé un patron neuf d'`ItemsControl` imbriqués
     jamais utilisé dans le projet, avec un risque de binding
     invérifiable visuellement (limite d'environnement). Pas de code
     couleur (la 4e nuance « Gris » du mockup J3I §5.5 reste non
     définie côté backend, cf. tâche 5 Phase 5) ; pas de drill-down vers
     la fiche rubrique (n'existe pas encore, tâche 4). `_Workbench
     réglementaire` devient un sous-menu (`Vue d'ensemble`/`Matrice de
     couverture`) au lieu d'un lien direct vers le placeholder. 2
     nouveaux tests de ViewModel. Vérifié par `dotnet run` réel. 433
     tests verts (431 + 2). **Grille pivotée visuelle avec code couleur
     et drill-down restent hors périmètre** — dette explicite si le
     besoin visuel réapparaît une fois la fiche rubrique (tâche 4)
     construite.
    - **Hub de navigation Workbench + boucle édition→fiche (17/07/2026) :**
      le placeholder « Vue d'ensemble » (`WorkbenchPlaceholderView`/`ViewModel`)
      devient un **vrai hub de navigation** (UniformGrid de boutons) routant
      vers Matrice de couverture / Fiche rubrique / Éditer une rubrique /
      Suggérer des rubriques / Audit & traçabilité via `INavigationService`
      (plus de texte « à venir »). `EditerRubriqueViewModel` reçoit
      `INavigationService` et **navigue automatiquement vers la Fiche
      rubrique après un enregistrement d'identité réussi** (boucle
      écriture→lecture fermée). Câblage DI (Application + Presentation),
      Shell menu et `ViewTemplates.xaml` déjà complets pour tous les écrans
      Workbench (vérifié : solution build 0 warning). Test
      `EditerRubriqueViewModelTests` mis à jour pour injecter le mock
      `INavigationService`. 466 tests verts au total (Unit 152 + Intégration
      234 + Presentation 33 + Tools 47), build 0 warning.
      **Régression corrigée (hors périmètre C4.1, liée au refactoring
      « Seeding ») :** les 2 tests `CalculerBulletinTests` échouaient sur
      `SQLite Error 1: 'near "Ecole": syntax error'`. Cause réelle = (a)
      échappement SQL incorrect dans le helper `Exec` (`l\'Ecole` backslash
      au lieu de `l''Ecole` apostrophe doublée) — `Microsoft.Data.Sqlite`
      accepte le multi-`INSERT` en une commande, mais rejetait l'apostrophe
      backslash ; (b) le moteur `CalculationPipeline` levait une erreur
      fatale quand la source `valeurSource(PAPP)` était absente, au lieu
      d'appliquer l'abstention ADR-0009 (PAPP sans notation → rubrique
      sautée, calcul non bloqué). Correctif : échappement SQL corrigé +
      ajout de `ResultatEligibilite.Abstention(rubrique)` et saut de la
      rubrique dans `CalculationPipeline` quand une source de valeur est
      absente (les autres erreurs de formule — variable inconnue — restent
      fatales, conformément à `CalculationPipelineTests`).
 10. Workspace Framework, validation temps réel, chargements async.

**Livrables.** Application utilisable de bout en bout pour le corps pilote — incluant le Workbench réglementaire complet permettant l'édition de toute rubrique, tout paramètre, toute règle d'éligibilité, sans recompilation.
**Critères d'acceptation.** MVVM strict (aucune logique métier en code-behind) ; navigation centralisée ; l'utilisateur peut **éditer les paramètres réglementaires** sans recompilation ; **toute modification passe par le Workbench** (D8) ; perfs UI conformes (écran rubrique < 200 ms, simulation 200 agents < 2 s).
**Tests.** Unitaires ViewModels + commandes + navigation + tests UI des écrans Workbench critiques (FormulaEditor, éditeur de barème, assistant d'évolution, matrice de couverture).
**Jalon J6.**

---

## Phase 7 — Reporting & documents officiels
**Objectif.** Produire les documents via le Document Engine (QuestPDF/ClosedXML).
**Tâches.**
1. Document Engine + registre de modèles versionnés.
2. **Bulletin de paie** (agrégat immuable → PDF QuestPDF), blocs réutilisables, mentions réglementaires, cumuls — incluant la **section « Rappels »** (D9) qui liste les lignes additionnelles issues d'évolutions rétroactives.
3. Documents V1 selon ⛔Q13 (attestation salaire CNR — modèle fourni —, attestation de travail, états récapitulatifs, ordre de virement).
4. Exports Excel (ClosedXML), prévisualisation, impression.
5. **Rapport d'impact d'évolution réglementaire** (D8) : export PDF du dry-run pour archivage et validation hiérarchique.

**Livrables.** Bulletin PDF conforme + documents V1 + exports + rapport d'impact.
**Critères d'acceptation.** Documents générés depuis les DTO/agrégats ; styles centralisés ; reproductibilité ; tests de non-régression visuelle ; rapport d'impact signé et archivé.
**Tests.** Rendu QuestPDF + non-régression visuelle + export + rendu rapport d'impact.
**Jalon J7.**

---

## Phase 8 — Qualité & validation réglementaire
**Objectif.** Garantir exactitude, non-régression et performances — y compris la couverture Workbench.
**Tâches.**
1. Compléter la pyramide de tests (unitaires, intégration, fonctionnels).
2. **Validation réglementaire** : comparaison aux **bulletins de référence réels** (⛔Q11) — écarts analysés et documentés.
3. Jeux de non-régression (par période/version réglementaire).
4. Tests de performance (bulletin individuel, lot 500 agents).
5. Revue d'explicabilité (chaque montant justifié).
6. **Suite de tests Workbench** (J3I §8.3) :
   - C-T1 : pour chaque pattern P1-P14, un test « édition UI → modification en base → recalcul paie correct »
   - C-T2 : non-régression de la suite existante (ISSRP 97 + IEP 15 + PAPP 15 + 117 = 245+ tests)
   - C-T3 : dry-run affiche le bon delta avant commit
   - C-T4 : rétroactif déclenche la génération de rappels (et **seulement** des rappels, pas de modif de bulletin)
   - C-T5 : rollback d'une version non propagée
   - C-T6 : refus de chevauchement, refus de trou, refus de double période ouverte
7. **Test de la matrice de couverture** (D11) : pour chaque corps du référentiel, vérifier qu'au moins une règle d'éligibilité est attachée à chaque rubrique systémique attendue.

**Livrables.** Rapport de conformité + suite de non-régression + suite Workbench.
**Critères d'acceptation.** Couvertures cibles V4 atteintes ; écarts = 0 ou justifiés/validés ; perfs conformes ; **tous les C-T1 à C-T6 verts** ; matrice de couverture exhaustive.
**Jalon J8.**

---

## Phase 9 — Déploiement, documentation & finalisation
**Objectif.** Rendre l'application livrable et maintenable.
**Tâches.**
1. Packaging (self-contained recommandé), initialisation/migration de base au premier lancement.
2. Sauvegarde/restauration, vérification d'intégrité.
3. **Procédure de mise à jour réglementaire par paramétrage** (documentée) — cœur de la valeur « zéro hardcoding ».
4. Documentation utilisateur + technique + manuel d'exploitation.
5. ADR consolidés, plan à jour.

**Livrables.** Installeur + base initialisable + documentation complète.
**Critères d'acceptation.** Installation reproductible ; mises à jour réglementaires possibles sans recompilation ; sauvegardes vérifiées.
**Jalon J9.**

---

## Phase 10 — Extension aux autres corps (itératif)
Réutiliser le moteur et le référentiel pour : corps communs (10-134), ouvriers/conducteurs/appariteurs (10-135), contractuels (10-136 ; IEP plafonnée 60 % art. 24 07-308), paramédicaux (11-200/24-425), personnels d'éducation/direction/inspection/intendance/laboratoire. Chaque corps = **ajout de données/paramètres + rubriques**, sans modification du moteur (Open/Closed).

---

**17/07/2026 — Chantier C4.1 (Écriture rubriques & formules) terminé.** Couvre
la création/édition d'une rubrique (`DefinirRubrique`), la définition d'une
version de formule (`DefinirFormuleRubrique`, validée par `FormulaParser` avant
persistance — formule invalide rejetée avec message clair, jamais d'exception
qui fuit) et la définition d'un paramètre versionné (`DefinirParametreRubrique`).
Tous ces éléments existaient déjà côté Domain/Application/Infrastructure
(ports `IRubriqueRepository`/`RubriqueRepository`, DI Application câblée) ; il
restait à **boucher la boucle UI** et à **verrouiller par les tests** :

- **Écran « Éditer une rubrique »** (`EditerRubriqueView` + `EditerRubriqueViewModel`,
  `Presentation/Workbench/`) : 3 onglets Identité / Formule / Paramètre,
  validation de la syntaxe de formule côté ViewModel (bouton « Valider la
  syntaxe » + blocage à la soumission si invalide), messages erreur via
  `IDialogService` (aucune exception qui fuit). Câblé : DI Presentation
  (`AddTransient<EditerRubriqueViewModel>`), menu Shell « Workbench réglementaire
  → Éditer une rubrique », `ViewTemplates.xaml`.
- **Tests** : `RubriqueRepositoryTests` (7, écriture rubrique/formule/paramètre
  versionnés, refus de formule invalide, refus de cycle DAG),
  `RubriquesUseCasesTests` (4, use cases C4.1), `EditerRubriqueViewModelTests`
  (7, ViewModel réel, ports mockés) ; `CompositionRootTests` étendu à la
  résolution `IRubriqueRepository` + 3 use cases C4.1. **24 nouveaux tests
  verts.**
- **Validation C4.1** : un administrateur peut créer/éditer une rubrique, sa
  formule et ses paramètres depuis l'UI ; la formule invalide est refusée avec
  message clair ; le recalcul de paie consomme ces données (même référentiel
  que le moteur). Build `dotnet build` **0 warning**, `dotnet test` →
  **C4.1 vert (24/24)**.

**Régression HORS périmètre C4.1 (signalée, non corrigée ici) :** les 2 tests
`CalculerBulletinTests` (`Executer_calcule_le_bulletin_complet_avec_auto_resolution_C2_C3`,
`Executer_agent_sans_notation_papp_abstention_ADR009`) sont rouges à cause d'un
`SQLite Error 1: 'near "Ecole": syntax error'` dans leur helper `Exec`
(multi-`INSERT` en une seule `ExecuteNonQuery`). Cette régression provient du
**refactoring « Seeding » en cours** dans le worktree (`tools/PaieEducation.Tools/Seeding/*`
→ `src/PaieEducation.Seeding/`, `CalculerBulletin` gagit `ParametreSystemeRepository`)
— aucun lien avec C4.1 (ni mes fichiers, ni mes modifications). À traiter dans
la branche de ce refactoring, pas dans C4.1.

---

## E. Checklist des jalons de validation (points d'arrêt STOP & ASK)

- [x] **J0** — Structure de solution, conventions, environnement .NET 10. **✅ RÉALISÉE** : 12 projets, build 0 erreur/0 warning, 10 tests verts (dont garde-fous d'architecture + smoke SQLite). ADR-0001/0002/0003 actés. *En attente de votre validation.*
- [ ] **J1** — Schéma paramétrable SQLite (dictionnaire de données).
- [ ] **J2** — Données de référence seedées et vérifiées (périmètre enseignants).
- [ ] **J3** — Couche Domaine.
- [ ] **J3bis** — 🔵 **Workbench réglementaire : migration V009, évaluateur DNF, simulateur (ADR-0007, J3I).** Bloquant pour Phase 4 étendue.
- [ ] **J4** — 🔴 Bulletin enseignant complet + explicabilité (validation métier) — étendu DNF + sources de valeur.
- [ ] **J5** — Chaîne Application/Persistence (incluant use cases Workbench).
- [ ] **J6** — Interface WPF + **Workbench réglementaire complet** (D7).
- [ ] **J7** — Bulletin PDF + documents V1 + rapport d'impact d'évolution.
- [ ] **J8** — Conformité réglementaire (vs bulletins de référence) + suite Workbench (C-T1 à C-T6).
- [ ] **J9** — Livrable + documentation.

À chaque jalon, je m'arrête pour votre accord avant de poursuivre. Toute règle métier douteuse rencontrée en cours de route déclenche une question (protocole STOP & ASK) — jamais une hypothèse silencieuse.
