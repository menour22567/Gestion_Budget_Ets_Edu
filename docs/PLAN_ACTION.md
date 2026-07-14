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
| **Q2** | **IEP** = `4 % × (n° échelon détenu) × traitement de base`. Cumul par échelon, **sans plafond**, assis sur le **n° d'échelon** (pas l'ancienneté) pour les fonctionnaires. |
| **Q3** | Cotisations (sécurité sociale/retraite part ouvrière, œuvres sociales éventuelles), **taux + assiette + rubriques entrantes/sortantes** = **paramétrés en base, éditables par l'utilisateur**. Aucun taux codé en dur. La BD et les écrans doivent supporter ce paramétrage. |
| **Q4** | Barème IRG fourni (`CALCUL IRG ALGERIE.txt`). Barème 2008 : `≤10 000 → 0 %` ; `10 001–30 000 → 20 %` ; `30 001–120 000 → 30 %` ; `>120 000 → 35 %`. Périodes/lissages 2020, 2021, 2022+ avec coefficients. **Tout stocké en base (tranches + règles de période).** |
| **Q5** | Abattement **40 % appliqué sur l'IRG brut** (borné [1000 ; 1500 DA]). Exonération totale si imposable ≤ 30 000. Caractère imposable/cotisable = **flag paramétrable par rubrique**. |
| **Q6** | Matrice éligibilité rubrique×corps **construite en base** à partir des décrets. Le client fournira/validera un tableau récapitulatif corps→rubriques (⏳ attendu). |
| **Q7** | Rappels/régularisations rétroactifs **gérés dès la V1**, règle = **date d'effet réglementaire**. |
| **Q8** | Ancienneté = **années de service effectif, sans interruptions déductibles** (disponibilité, suspension non déduites). |
| **Q9** | Arrondi **centralisé unique**. Au dinar le plus proche / à la dizaine → **à préciser (voir ⛔Q9b)**. Paramétrable. |
| **Q10** | Périmètre V1 = **corps pilote « enseignants » d'abord** pour valider le moteur, puis extension aux autres corps. |
| **Q3b** | Seed cotisation **sécurité sociale part ouvrière = 9 %** (éditable). **Œuvres sociales** = retenues **temporaires/facultatives en montants fixes**, au choix de l'employé (pas un % obligatoire). **Cotisations mutuelles** = **facultatives**. → modélisées comme *retenues optionnelles à montant fixe pilotées par l'agent*, distinctes des cotisations obligatoires. |
| **Q4b** | **Confirmé** : barème 2022+ modélisé par **coefficients de lissage** (pas de table de tranches distincte). On stocke : tranches 2008 + règles de période (exonération 30 000, abattement 40 % [1000;1500], `coefGeneral/constGeneral`, `coefSpecial/constSpecial`, `plafondSpecial`) par plage de dates (2020, 2021, 2022+). Profil `HANDICAPE_OU_RETRAITE_RG` géré. |
| **Q9b** | Arrondi **centralisé, uniforme partout** (rubriques + net). Défaut retenu : **au dinar le plus proche**, paramétrable (dinar/dizaine). |
| **Q11** | Bulletins réels de référence fournis **après mise en marche** → validation réglementaire (Phase 8) itérative post-livraison. |
| **Q12** | **Mode autonome** (sans authentification) en V1. Modèle de rôles prévu mais désactivé. |
| **Q13** | Documents V1 (au-delà du bulletin) précisés **après mise en marche**. Architecture Reporting extensible dès la Phase 7. |
| **Q6** | Matrice corps→rubriques construite depuis : `Cascade_Corps_Grades_30526.csv` (**source faisant foi**, remplace 11526), `elements_paie_historique_corrige`, `ISSRP_Corrige`, `Prime-Rendement_historique`, `IFC 2008+2015`, `Grille_indiciaire_2007_2024`. **ISSRP** : 45 % = groupe pédagogique **élargi** (enseignants + directeurs + inspecteurs + censeurs + conseillers issus du corps enseignant + grades de promotion d'origine enseignante) ; 30 % ; 15 %. |

---

## B. Questions ouvertes restantes

✅ **Toutes les questions P1/P2/P3 sont résolues** (voir section A). Éléments reportés **après mise en marche** du logiciel, sans impact sur le démarrage :
- Fourniture des **bulletins réels de référence** (Q11) → validation réglementaire itérative (Phase 8).
- Précision des **documents officiels V1** additionnels (Q13) → l'architecture Reporting reste extensible (Phase 7).
- **Matrice corps→rubriques** : bases fournies (Q6) ; affinage au fil de l'extension aux autres corps.

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
| 4 | Moteur de calcul de paie (pilote enseignants) | XL | 2,3 | **J4** |
| 5 | Application & Persistence | L | 3,4 | **J5** |
| 6 | Présentation WPF/MVVM | XL | 5 | **J6** |
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

## Phase 4 — Moteur de calcul de paie (pilote : enseignants)
**Objectif.** Implémenter le pipeline orienté-moteur et les calculateurs du corps pilote, **alimentés par la base** (V4 Tome C, vol. 9–10).
**Périmètre pilote (rubriques enseignants) :** Traitement de base → IEP (4 %/échelon) → Indemnité de qualification (40/45 %) → Documentation pédagogique (forfait 2000/2500/3000) → PAPP (0–40 %) → Soutien scolaire (45 %) → Assiette cotisable/imposable → Cotisations (paramétrables) → IRG → Net.

**Tâches.**
1. `PayrollContext` (immuable) + `ContextBuilder`.
2. `EligibilityEngine` (lit `ReglesEligibilite`).
3. `VariableEngine` (indice, traitement de base, ancienneté, n° échelon…).
4. `FormulaEngine` + `FormulaParser`/`ExpressionEvaluator` (formules **lues en base**, aucune formule codée en dur).
5. `DependencyResolver` (graphe DAG, détection de cycles).
6. `CalculationPipeline` + calculateurs `IPayrollCalculator` (un par rubrique/famille), priorisés.
7. `IrgCalculator` : tranches + règles de période **depuis la base** (barème 2008 + lissages 2020/2021/2022+), abattement sur IRG brut, exonération ≤ 30 000.
8. `ContributionCalculator` : taux/assiette **depuis la base**.
9. `TotalsEngine`, `ValidationEngine`, `ExplainabilityEngine` (justification de chaque montant), `AuditEngine`, `SnapshotEngine`.
10. **Rappels rétroactifs** (Q7) : recalcul à la réglementation en vigueur à la **date d'effet**.
11. Service d'**arrondi centralisé** (Q9).

**Livrables.** Moteur exécutant un bulletin enseignant de bout en bout + journal d'explication.
**Critères d'acceptation.** Déterminisme (mêmes entrées → mêmes sorties) ; chaque montant explicable ; dépendances résolues sans cycle ; **0 règle réglementaire dans le code** ; perfs cibles V4 (bulletin < 300 ms).
**Tests.** Unitaires par calculateur (cas nominal, limites, erreurs) + tests de reproductibilité + tests IRG par tranche/période. Comparaison aux montants attendus dès réception des bulletins de référence (⛔Q11).
**Risques.** Ambiguïté barème 2022+ (⛔Q4b) → *paramétrable, ajustable sans recompilation*.
**Jalon J4 — CRITIQUE.** Validation d'un bulletin enseignant complet et de son explicabilité.

---

## Phase 5 — Application & Persistence
**Objectif.** Orchestrer les cas d'usage et brancher la persistance réelle.
**Tâches.**
1. **Application** : Use Cases (CQRS léger), Commands/Queries, DTO, mapping, validation applicative, `IUnitOfWork`, notifications, gestion d'erreurs normalisée.
2. **Persistence** : `Persistence Models` distincts des entités (ADR-066), mappers, **repositories spécialisés** par agrégat (Dapper), transactions, migrations, backup/restore.
3. Câblage DI complet dans `Bootstrapper` (Composition Root).
4. Use cases pilotes : CréerAgent, CalculerBulletin, ValiderBulletin, ConsulterBulletin, GérerRéférentiels (paramétrage).

**Livrables.** Chaîne complète Application→Domain→Persistence→SQLite fonctionnelle (hors UI).
**Critères d'acceptation.** Domaine sans dépendance Dapper/SQLite ; transactions atomiques ; calcul + persistance d'un bulletin via use case ; perfs d'accès conformes V4.
**Tests.** Intégration repositories (SQLite temporaire) + tests de use cases (mocks).
**Jalon J5.**

---

## Phase 6 — Présentation WPF/MVVM
**Objectif.** Interface professionnelle MVVM strict (V4 Tome E).
**Tâches.**
1. **Shell** (fenêtre principale, navigation centralisée `INavigationService`, `IDialogService`, notifications).
2. Design System + Business Controls (MoneyTextBox, sélecteurs corps/grade/échelon/période, `ExplainabilityPanel`, `ERPDataGrid`).
3. Écrans : **paramétrage des référentiels** (rubriques, taux cotisations, barèmes, grilles — édition par l'utilisateur, Q3) ; gestion agents/carrière ; lancement/contrôle des calculs ; **consultation bulletin + panneau d'explication**.
4. Workspace Framework, validation temps réel, chargements async.

**Livrables.** Application utilisable de bout en bout pour le corps pilote.
**Critères d'acceptation.** MVVM strict (aucune logique métier en code-behind) ; navigation centralisée ; l'utilisateur peut **éditer les paramètres réglementaires** sans recompilation ; perfs UI conformes.
**Tests.** Unitaires ViewModels + commandes + navigation.
**Jalon J6.**

---

## Phase 7 — Reporting & documents officiels
**Objectif.** Produire les documents via le Document Engine (QuestPDF/ClosedXML).
**Tâches.**
1. Document Engine + registre de modèles versionnés.
2. **Bulletin de paie** (agrégat immuable → PDF QuestPDF), blocs réutilisables, mentions réglementaires, cumuls.
3. Documents V1 selon ⛔Q13 (attestation salaire CNR — modèle fourni —, attestation de travail, états récapitulatifs, ordre de virement).
4. Exports Excel (ClosedXML), prévisualisation, impression.

**Livrables.** Bulletin PDF conforme + documents V1 + exports.
**Critères d'acceptation.** Documents générés depuis les DTO/agrégats ; styles centralisés ; reproductibilité ; tests de non-régression visuelle.
**Tests.** Rendu QuestPDF + non-régression visuelle + export.
**Jalon J7.**

---

## Phase 8 — Qualité & validation réglementaire
**Objectif.** Garantir exactitude, non-régression et performances.
**Tâches.**
1. Compléter la pyramide de tests (unitaires, intégration, fonctionnels).
2. **Validation réglementaire** : comparaison aux **bulletins de référence réels** (⛔Q11) — écarts analysés et documentés.
3. Jeux de non-régression (par période/version réglementaire).
4. Tests de performance (bulletin individuel, lot 500 agents).
5. Revue d'explicabilité (chaque montant justifié).

**Livrables.** Rapport de conformité + suite de non-régression.
**Critères d'acceptation.** Couvertures cibles V4 atteintes ; écarts = 0 ou justifiés/validés ; perfs conformes.
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

## E. Checklist des jalons de validation (points d'arrêt STOP & ASK)

- [x] **J0** — Structure de solution, conventions, environnement .NET 10. **✅ RÉALISÉE** : 12 projets, build 0 erreur/0 warning, 10 tests verts (dont garde-fous d'architecture + smoke SQLite). ADR-0001/0002/0003 actés. *En attente de votre validation.*
- [ ] **J1** — Schéma paramétrable SQLite (dictionnaire de données).
- [ ] **J2** — Données de référence seedées et vérifiées (périmètre enseignants).
- [ ] **J3** — Couche Domaine.
- [ ] **J4** — 🔴 Bulletin enseignant complet + explicabilité (validation métier).
- [ ] **J5** — Chaîne Application/Persistence.
- [ ] **J6** — Interface WPF + paramétrage éditable.
- [ ] **J7** — Bulletin PDF + documents V1.
- [ ] **J8** — Conformité réglementaire (vs bulletins de référence).
- [ ] **J9** — Livrable + documentation.

À chaque jalon, je m'arrête pour votre accord avant de poursuivre. Toute règle métier douteuse rencontrée en cours de route déclenche une question (protocole STOP & ASK) — jamais une hypothèse silencieuse.
