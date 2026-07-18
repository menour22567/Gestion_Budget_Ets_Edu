# PaieEducation ERP — Documentation Technique de Référence (Audit)

> **Nature :** Référence technique officielle du projet, issue d'un audit exhaustif du code source, de l'architecture, des migrations, des données de seed et des tests.
> **Date d'audit :** 17/07/2026
> **Périmètre audité :** intégralité de `src/`, `tests/`, `tools/`, `docs/`, `PaieEducation.slnx`, migrations SQLite V001→V013.
> **Méthode :** lecture du code, exécution réelle `dotnet test` (résultat : **445 tests, 0 échec**), recoupement code ↔ `docs/PLAN_ACTION.md` ↔ documentation de référence V3/V4.
> **Statut de conformité build :** `dotnet build` **0 erreur / 0 warning** (`TreatWarningsAsErrors=true`), tests **445/445 verts**.

---

## 1. Vue d'ensemble

### 1.1 Nature de l'application

Application **monolithique desktop 100 % hors-ligne** de gestion de la paie des établissements publics de l'Éducation nationale algérienne (et corps assimilés). Mono-utilisateur, mode autonome sans authentification (décision Q12). Hors production, en cours de développement.

**Principe cardinal (respecté dans le socle, partiellement dans les points d'entrée) :** *zéro hardcoding* des règles et valeurs réglementaires. Grilles, indices, point indiciaire, barèmes IRG, taux de cotisation, rubriques, formules, éligibilité, arrondi vivent en base SQLite, versionnés par date d'effet.

### 1.2 Pile technologique

| Domaine | Technologie | Version / source |
|---|---|---|
| Runtime | .NET | 10.0.301 (LTS), épinglé `global.json` (`rollForward: latestFeature`) |
| Langage | C# | `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable` |
| UI | WPF + MVVM | `CommunityToolkit.Mvvm` (source generators `[ObservableProperty]`/`[RelayCommand]`) |
| Persistance | SQLite | `Microsoft.Data.Sqlite` + `Dapper` (micro-ORM, SQL explicite) |
| Reporting | QuestPDF, ClosedXML | **référencés mais non utilisés** (projet Reporting vide) |
| Tests | xUnit, Moq, NetArchTest | 6 projets de test |
| Build | MSBuild + Central Package Management | `Directory.Packages.props`, `Directory.Build.props`, format solution `.slnx` |

Réglages MSBuild communs (`Directory.Build.props`) : `TreatWarningsAsErrors=true`, `Deterministic=true`, `EnforceCodeStyleInBuild=true`, `NuGetAuditMode=direct` (ADR-0003), `NeutralLanguage=fr`.

### 1.3 Volumétrie du code (hors `bin`/`obj`)

| Projet | LOC C# | Rôle |
|---|---:|---|
| `PaieEducation.Domain` | ~3 260 | Cœur métier pur (moteur de calcul + Workbench) |
| `PaieEducation.Infrastructure` | ~1 750 | Repositories Dapper, sérialisation snapshot, horloge |
| `PaieEducation.Application` | ~1 290 | 20 use cases (CQRS léger) |
| `PaieEducation.Presentation` | ~1 110 | Shell WPF, ViewModels, vues, navigation |
| `PaieEducation.Persistence` | ~330 (+ ~1 040 SQL) | Migrateur SQLite + 13 migrations |
| `PaieEducation.Shared` | ~140 | `Result<T>`, `Error`, `Guard`, `IClock` |
| `PaieEducation.Bootstrapper` | ~86 | Composition Root WPF (`App.xaml.cs`) |
| `PaieEducation.Reporting` | **0** | **Vide** (aucune classe) |
| `tools/PaieEducation.Tools` | ~— | CLI seed/migrate/validate + 4 seeders |

---

## 2. Architecture

### 2.1 Style

**Clean Architecture + DDD** (ADR-0001), déclinée en couches concentriques. La règle de dépendance (Domain au centre, sans dépendance sortante) est **vérifiée automatiquement** par `tests/PaieEducation.Tests.Architecture/DependencyRulesTests.cs` (NetArchTest, 3 tests) :

- `Domain` ne dépend d'aucun projet ni d'aucune techno (Sqlite/Dapper/QuestPDF/ClosedXML/WPF/Mvvm interdits).
- `Shared` ne dépend d'aucun projet métier.
- `Application` ne dépend ni d'`Infrastructure`, ni de `Persistence`, ni de `Presentation`, ni de `Reporting`.

### 2.2 Matrice de dépendances effective

```
                 Shared   Domain   Application   Persistence   Infrastructure   Presentation   Reporting   Bootstrapper
Shared              —
Domain          (aucune dépendance — cœur pur)
Application       ✓        ✓
Persistence       ✓        (migrations SQL embarquées)
Infrastructure    ✓        ✓         ✓             ✓
Presentation      ✓        ✓(*)      ✓                                                          
Reporting         ✓                  ✓
Bootstrapper                         ✓             ✓             ✓               ✓
```

(*) `Presentation` référence `Domain` pour des types de valeur consommés par les ViewModels (`ProfilFiscal`, DTO de projection). La frontière **Presentation → Application → Domain** est respectée : aucun écran n'appelle un port `Domain` directement, toujours via un use case.

Les **ports (interfaces repository)** sont définis dans `Domain` (3 bounded contexts de ports : `Domain/Agents/Repositories`, `Domain/Calcul/Repositories`, `Domain/Workbench/Repositories`) et implémentés dans `Infrastructure`. `Application` ne connaît que les ports — l'inversion de dépendance est réelle et testée (c'est `DependencyRulesTests` qui a imposé la création de ces ports en Phase 5).

### 2.3 Bounded contexts

| Contexte | Emplacement Domain | Contenu |
|---|---|---|
| **Calcul** (paie) | `Domain/Calcul/` | Pipeline, formules, IRG, cotisations, arrondi, dépendances (DAG), validation, explicabilité, audit, snapshot, rappels, VO `Fraction`/`ReferentielItem` |
| **Workbench réglementaire** | `Domain/Workbench/` | Éligibilité DNF, barèmes, sources de valeur, continuité temporelle, VO (`GroupeEligibilite`, `ConditionEligibilite`, `CritereEligibilite`, `BaremeValue`, `SourceValeur`, `PeriodeReglementaire`, `MessageRegle`, `AffectationRubrique`, `EntreeAuditLog`, `RubriqueDetail`…) |
| **Agents** | `Domain/Agents/` | `NouvelAgent` (DTO), port `IAgentRepository` |

> ⚠️ **Note de conception :** le Workbench possède ses **propres** `Result`/`Error`/`Guard` internes (`Domain/Workbench/Internal/`) distincts de ceux de `Shared` et de `Domain/Common/`. Il existe donc **trois implémentations parallèles** de `Result<T>`/`Error` dans la solution (`Shared/Results`, `Domain/Common`, `Domain/Workbench/Internal`). Voir §8 (dette).

### 2.4 Flux de calcul d'un bulletin (chemin nominal)

```
UI (CalculerBulletinViewModel)
  └─> CalculerBulletin (use case, Application)
        ├─ IAgentCarriereRepository.ResoudreAsync(agentId, datePaie)  → AgentContext (point-in-time)
        ├─ IVariableRepository.ResoudreAsync(agent, datePaie)         → INDICE_MIN, INDICE_ECH, VPI, TBASE, TRT, ECH, CAT
        ├─ IPayrollReadRepository.ChargerAsync(...)                   → PayrollInput (rubriques, formules, barèmes, conditions, cotisations, règle IRG)
        └─> CalculationPipeline.Calculer(input)  [Domain pur, déterministe]
              1. DependencyResolver.Ordonner (DAG, détection de cycle)
              2. Pour chaque gain (ordre): éligibilité DNF → FormulaEvaluator → ArrondiService
              3. Assiettes (cotisable / imposable)
              4. ContributionCalculator (taux × assiette, depuis la base)
              5. Assiette imposable = Σ gains imposables − cotisations salariales
              6. IrgCalculator (barème + abattement 40% borné + exonération ≤30k + lissages)
              7. Totaux + net
              8. ValidationEngine.Valider (RM-081, cohérence)
        └─ (ValiderBulletin) SnapshotEngine → BulletinRepository (SnapshotJson figé, ADR-0008)
```

**Points d'attention du flux (voir §8) :** `Demande.SourcesValeur` (ex. `NOTATION_AGENT` pour PAPP) et `Demande.ClesBareme` sont **fournis par l'appelant**, non résolus depuis la base ; le `ModeArrondi` est **codé en dur** (`DinarPlusProche`) dans le use case au lieu d'être lu dans `Parametres`.

---

## 3. Inventaire des modules et composants

### 3.1 Domain — Calcul (moteur)

| Composant | Fichier | Rôle | Qualité |
|---|---|---|---|
| `CalculationPipeline` | `Calcul/Pipeline/CalculationPipeline.cs` | Orchestrateur pur du bulletin | ✅ Solide, déterministe |
| `FormulaParser` / `FormulaEvaluator` / `FormulaNode` | `Calcul/Formules/` | Parseur + évaluateur d'expressions lues en base ; fonctions `round/abs/min/max/bareme/valeurSource` | ✅ Robuste (erreurs en `Result`) |
| `IrgCalculator` | `Calcul/Irg/IrgCalculator.cs` | IRG mensuel progressif + abattement + exonération + lissages (fractions exactes) | ✅ Fidèle au pseudo-code de référence |
| `ContributionCalculator` | `Calcul/Cotisations/` | Cotisations (taux × assiette, référence d'assiette paramétrée) | ✅ |
| `DependencyResolver` | `Calcul/Services/DependencyResolver.cs` | Tri topologique DAG, détection de cycle | ✅ |
| `ArrondiService` | `Calcul/Services/ArrondiService.cs` | Arrondi centralisé (dinar / dizaine) | ✅ (mais mode non lu depuis la base côté use case) |
| `ValidationEngine` | `Calcul/Validation/` | Contrôles finaux du bulletin | ✅ |
| `RappelCalculator` | `Calcul/Rappels/` | Lignes de rappel rétroactives | ✅ |
| Moteurs `Explicabilite`/`Audit`/`Snapshot` | `Calcul/Explicabilite/`, `Audit/`, `Snapshot/` | Modèles d'explication, journal d'audit, snapshot figé | ✅ |
| `Fraction` | `Calcul/ValueObjects/Fraction.cs` | Arithmétique exacte (lissages IRG) | ✅ |

### 3.2 Domain — Workbench

| Composant | Fichier | Rôle |
|---|---|---|
| `RegleEligibiliteEvaluator` | `Workbench/Services/RegleEligibiliteEvaluator.cs` | Évaluation **DNF** (ET dans groupe, OU entre groupes) + conditions communes ; explicabilité complète (abstention ADR-0009) |
| `BaremeResolver` | `Workbench/Services/BaremeResolver.cs` | Résolution barème par dimension/borne/date |
| `SourceValeurResolver` + `ISourceValeurCalculator` (7 impl.) | `Workbench/Services/`, `Workbench/Calculators/` | Catalogue extensible Open/Closed des sources (`NOTATION_AGENT`, `ANCIENNETE_*`, `INDICE_ECHELON`, `POINT_INDICIAIRE`, `BASE_ASSIETTE`, `CONSTANTE_REGLEMENTAIRE`) — **⚠️ non câblé au pipeline** (voir §8) |
| `ContinuiteTemporelle` | `Application/Workbench/Services/` | Validation chevauchement/trou/période unique ouverte |
| `CritereEligibiliteResolver` | `Workbench/Services/` | Résolution de la valeur d'un critère depuis l'`AgentContext` |

### 3.3 Application — Use cases (20, tous enregistrés en DI)

| Domaine | Use cases | Type |
|---|---|---|
| Agents | `CreerAgent` | Écriture |
| Paie | `CalculerBulletin`, `ValiderBulletin`, `ConsulterBulletin` | Lecture / écriture |
| Référentiels | `DefinirValeurPoint`, `DefinirIndiceMinGrille`, `DefinirIndiceEchelon`, `ListerReferentiels` | Écriture / lecture |
| Workbench | `SuggererRubriques`, `AccepterSuggestion`, `SupprimerAffectation`, `SuspendreAffectation`, `ListerAffectationsAgent`, `SimulerEvolutionReglementaire`, `AppliquerEvolutionReglementaire`, `DupliquerVersion`, `GenererRappels`, `ListerMatriceCouverture`, `ListerAuditLog`, `ConsulterFicheRubrique` | Lecture / écriture |

### 3.4 Infrastructure — Repositories (12, enregistrés Scoped)

`AgentCarriereRepository`, `VariableRepository`, `PayrollReadRepository`, `AgentRepository`, `BulletinRepository`, `BulletinReadRepository`, `RappelRepository`, `GrilleIndiciaireRepository`, `ReferentielReadRepository`, `WorkbenchReadRepository` (+ `WorkbenchReadCache`), `AgentRubriqueRepository`, `AuditLogRepository`.

Transverses : `SystemClock` (`IClock`, Singleton), `BulletinSnapshotJsonConverters` (5 `JsonConverter<T>` sur mesure pour re-désérialiser les VO à constructeur privé — exigence dure d'ADR-0008 : rejouer un snapshot reproduit le bulletin à l'identique).

Connexion : `SqliteConnection` Scoped, `PRAGMA foreign_keys=ON` à l'ouverture.

### 3.5 Presentation — écrans (11 réels + 1 placeholder)

| # | Écran | ViewModel | Use case consommé | Menu |
|---|---|---|---|---|
| 1 | Calculer un bulletin | `CalculerBulletinViewModel` | `CalculerBulletin` | Paie |
| 2 | Valider un bulletin | `ValiderBulletinViewModel` | `ValiderBulletin` | Paie |
| 3 | Consulter un bulletin | `ConsulterBulletinViewModel` | `ConsulterBulletin` | Paie |
| 4 | Créer un agent | `CreerAgentViewModel` | `CreerAgent` + `ListerReferentiels` | Agents |
| 5 | Suggérer des rubriques | `SuggererRubriquesViewModel` | `SuggererRubriques` + transitions + `ListerAffectationsAgent` | Agents |
| 6 | Grille indiciaire (4 onglets) | `GrilleIndiciaireViewModel` | `DefinirValeurPoint`/`…MinGrille`/`…Echelon`/`DupliquerVersion` | Référentiels |
| 7 | Fiche rubrique (lecture, 3 onglets) | `FicheRubriqueViewModel` | `ConsulterFicheRubrique` | Workbench |
| 8 | Matrice de couverture (liste plate) | `MatriceCouvertureViewModel` | `ListerMatriceCouverture` | Workbench |
| 9 | Audit & traçabilité | `AuditLogViewModel` | `ListerAuditLog` | Workbench |
| — | Workbench (vue d'ensemble) | `WorkbenchPlaceholderViewModel` | *(placeholder)* | Workbench |

Navigation **ViewModel-first** : `DataTemplate` implicites (`Resources/ViewTemplates.xaml`), `INavigationService`/`NavigationService`, `IDialogService`/`DialogService`. Aucune logique métier en code-behind (MVVM strict respecté).

### 3.6 Tools — CLI de seed

`PaieEducation.Tools` (console) : `migrate`, `seed nomenclature|reglementaire|irg|all`, `validate` (`PRAGMA integrity_check` + `foreign_key_check` + counts). 4 seeders : `NomenclatureSeeder` (CSV cascade 185 grades), `ReglementaireSeeder` (rubriques pilote, groupes DNF ISSRP, barèmes, cotisations, paramètres), `IrgSeeder` (barèmes 2008/2022 + 4 règles de période), `FormulesSeeder`.

---

## 4. Cartographie des phases (plan d'action ↔ implémentation réelle)

Légende : ✅ Terminée · 🟡 Partielle · ❌ Non implémentée · ⏳ Itératif/reporté

| Phase | Intitulé | État réel | Preuve / réserve |
|---|---|:---:|---|
| **0** | Cadrage & fondations | ✅ | 8 projets src + 6 tests + 1 tools ; DI ; `DependencyRulesTests` ; ADR-0001→0009 ; `CONVENTIONS.md`, `GLOSSAIRE.md` |
| **1** | Référentiel paramétrable SQLite | ✅ | 13 migrations V001→V013, schéma versionné complet, `DICTIONNAIRE_DONNEES.md`, migrateur rejouable + tests d'upgrade |
| **2** | Ingestion & seed | ✅ (pilote) | CLI + 4 seeders ; cascade 185 grades ; IRG 2008/2022 ; cotisations ; paramètres. **Réserve :** table `IndemniteHistorique` (régimes indemnitaires TXT) non matérialisée ; extraction PDF manuelle |
| **3** | Couche Domaine (DDD) | 🟡 | Domaine pur et testé, mais : **pas de VO `Money`** (decimal partout — écart `CONVENTIONS.md §5`) ; **pas de Domain Events** ; agrégats « anémiques » (`Agent` = identité pure, `Bulletin` = record) |
| **3bis** | Workbench V009 + services | ✅ | V009 appliqué, évaluateur DNF, `BaremeResolver`, `SourceValeurResolver`, `ContinuiteTemporelle`, `SimulerEvolutionReglementaire` |
| **4** | Moteur de calcul (pilote enseignants) | ✅ | Pipeline bout-en-bout, IRG, cotisations, formules, DNF, barèmes, validation, explicabilité, audit, snapshot, rappels, arrondi. **Réserves :** `SourceValeurResolver` non intégré au pipeline ; `ModeArrondi` codé en dur dans le use case ; validation vs bulletins réels reportée (Q11) |
| **5** | Application & Persistence | 🟡 | 20 use cases, 12 repos, DI complet. **Manques :** `GérerRéférentiels` ne couvre **que la grille indiciaire** (pas d'écriture rubriques/cotisations/barèmes/conditions) ; **pas d'`IUnitOfWork`** (audit non-atomique) ; pas de repos V009 dédiés (`BaremeRepository`/`SourceValeurRepository`/…) ; pas de backup/restore |
| **6** | Présentation WPF/MVVM | 🟡 | Shell + 11 écrans + navigation. **Manques majeurs :** Design System (tâche 2) quasi absent (pas de `MoneyTextBox`/`ERPDataGrid`/`ExplainabilityPanel`) ; arborescence Workbench (tâche 4) réduite à une fiche rubrique **lecture seule** ; **FormulaEditor, éditeur de barème, éditeur DNF, assistant d'évolution** non faits ; écrans Cotisations/Fiscalité/Simulation/Évolution absents ; matrice pivotée visuelle absente ; notifications absentes ; saisie manuelle `SourcesValeur`/`ClesBareme` sur l'écran de calcul |
| **7** | Reporting & documents officiels | ❌ | Projet `Reporting` **vide** (0 code). Aucun bulletin PDF, aucun export Excel, aucune attestation |
| **8** | Qualité & validation réglementaire | 🟡 | Pyramide de tests réelle (445). **Manques :** suite Workbench C-T1→C-T6 non formalisée ; tests de perf (lot 500 agents) absents ; **validation vs bulletins de référence réels (Q11) non faite** (données attendues) |
| **9** | Déploiement & finalisation | ❌ | Pas de packaging ; **le Bootstrapper migre mais ne seed pas** (installation neuve = référentiel vide) ; pas de backup/restore ; pas de doc utilisateur/exploitation |
| **10** | Extension autres corps | ⏳ | Seul le corps pilote (enseignants + inspection IDLS) est seedé. Architecture Open/Closed prête, données non étendues |

**Synthèse :** le projet a une **base logicielle mature et saine** (Phases 0-4 essentiellement complètes, moteur de qualité production, 445 tests verts). Les manques se concentrent sur **l'UI de paramétrage complète (Phase 6)**, **le reporting (Phase 7, néant)**, **le déploiement/seed au premier lancement (Phase 9)** et **la validation réglementaire terrain (Phase 8)**.

---

## 5. Fonctionnalités implémentées (par capacité métier)

- **Calcul d'un bulletin de paie** (corps pilote enseignant) : traitement de base → IEP → indemnités → PAPP → ISSRP (soutien scolaire 45/30/15 en DNF) → assiettes → cotisations → IRG → net, avec **journal d'explicabilité** (chaque montant justifié) et **audit d'étapes**.
- **Cycle de vie du bulletin** : Calculer → Valider (snapshot figé immuable, ADR-0008) → Consulter (relecture depuis snapshot, sans recalcul).
- **Gestion d'agents** : création (Agents + Carrières en transaction), résolution point-in-time de la carrière et des variables de base.
- **Affectation assistée des rubriques** : suggestion via DNF, machine à états `SUGGEREE ⇄ ACCEPTEE ⇄ SUSPENDUE → SUPPRIMEE`.
- **Paramétrage grille indiciaire** : valeur du point, indice min. catégorie, indice d'échelon, duplication de version — versionnés par date d'effet.
- **Workbench réglementaire (lecture)** : fiche rubrique (identité/barème/éligibilité), matrice de couverture `corps × rubriques`, journal d'audit.
- **Évolution réglementaire** : simulation d'impact (dry-run, sans écriture) puis application (clôture+nouvelle version ou duplication) avec trace `AuditLog` et garde-fou dry-run/bypass.
- **Rappels rétroactifs** : recalcul point-in-time à la date d'effet, génération de lignes de rappel (idempotent), sans réécriture des bulletins validés.

---

## 6. Inventaire des règles métier identifiées (dans le code / la doc)

| Réf | Règle | Où |
|---|---|---|
| Q1 | `traitement = (indice_min + indice_échelon) × valeur_point` ; point = 45 DA (versionné) | `VariableRepository`, seed `ValeurPoint` |
| Q2-rev | Deux indemnités d'expérience distinctes : **IEP** (`IE × VPI` fonctionnaires ; `TBASE × min(ANC_PUB×1,4% + ANC_PRIV×0,7% ; 60%)` contractuels) et **EXP_PEDAG** (`TBASE × 4% × ECH`) | Formules en base, seed rubriques |
| Q4/Q-01 | Barème IRG : 2008 (4 tranches) + **2022+ (6 tranches)** ; lissage général `137/51 − 27925/8` (30–35k), spécial `93/61 − 81213/41` (≤42,5k) | `IrgCalculator`, `IrgSeeder`, V006/V007 |
| Q5 | Abattement 40 % sur IRG brut borné [1000 ; 1500] ; exonération si imposable ≤ 30 000 | `IrgCalculator` |
| Q3/Q3b-rev | Cotisations paramétrées (sécu part ouvrière 9 %) ; retenues optionnelles à montant fixe (œuvres sociales) ou taux×assiette (mutuelle MUNATEC 1 %) | `Cotisations`, `RubriqueParametres` |
| Q6/ISSRP | Matrice corps→rubriques ; ISSRP 45 % = groupe pédagogique élargi (DNF, résolution GRADE→CORPS), 30 %, 15 % | `ReglementaireSeeder`, groupes DNF, `RegleEligibiliteEvaluator` |
| Q7/D9 | Rappels rétroactifs dès V1, règle = date d'effet réglementaire ; lignes additionnelles, jamais réécriture | `RappelCalculator`, `GenererRappels`, V013 |
| Q8 | Ancienneté = années de service effectif (dispo/suspension non déduites) | `AnciennetePubliqueCalculator` |
| Q9b | Arrondi centralisé uniforme, défaut dinar le plus proche (paramétrable) | `ArrondiService`, seed `Parametres` |
| RM-040 | Éligibilité vide ⊨ vraie (aucune condition = dû à tous) | `RegleEligibiliteEvaluator` |
| RM-081 | Contrôles de cohérence finaux du bulletin | `ValidationEngine` |
| RM-105 | Snapshot figé du bulletin validé | `SnapshotEngine`, `BulletinRepository` |
| ADR-0008 | Immutabilité des périodes clôturées / bulletins validés | unicité `(AgentId, DatePaie)`, snapshot |
| ADR-0009 | Abstention réglementaire : critère non résolu ⇒ non éligible + explication, jamais de droit déduit | `RegleEligibiliteEvaluator` |

Sources métier de référence : `Reglementation/` (décrets, IRG, ISSRP, IFC, grilles indiciaires) et `docs/analysis/J3*`/`J4*`.

---

## 7. Modèle de données (schéma effectif après V001→V013)

Base SQLite unique, migrations idempotentes auto-découvertes (`Migrations/*.sql` embarquées), table méta `SchemaVersions`. `PRAGMA foreign_keys=ON`. Convention : `DateEffet`/`DateFin`, `Version`, `Source`, `Hash` sur les tables réglementaires ; résolution **point-in-time** (`WHERE DateEffet <= @date ORDER BY DateEffet DESC LIMIT 1`).

> ⚠️ Le schéma évolue par **re-création** : `Rubriques` créée en V004 puis **DROP+CREATE en V008** ; `ReglesEligibilite` créée en V005, recréée V008, recréée **V009** (passage `Critere` TEXT → `CritereId` FK). L'ordre des migrations fait foi ; le schéma effectif est celui d'après V013.

| Groupe | Tables (schéma final) | Migration |
|---|---|---|
| Technique / audit | `SchemaVersions`, `AuditLog` | bootstrap, V001 |
| Nomenclature | `Filieres`, `TypesContrat`, `TypesPersonnel`, `Fonctions`, `Echelons`, `Categories`, `Corps`, `Grades`, `Etablissements` | V002 |
| Grille indiciaire | `ValeurPoint`, `GrilleIndiciaire`, `IndicesEchelon` | V003 |
| Rubriques & formules | `Rubriques` (v2), `RubriqueFormules`, `RubriqueParametres`, `RubriqueDependances`, `RubriqueBaremes` | V004, V008 |
| Éligibilité | `ReglesEligibilite` (→ `CritereId`/`GroupeId` FK), `GroupesEligibilite`, `CriteresEligibilite`, `SourcesValeur`, `MessagesRegles`, `GradeAttributs` | V005, V008, V009 |
| Cotisations | `Cotisations`, `CotisationAssietteRubriques` | V005 |
| IRG | `BaremeIRG`, `BaremeIRGTranches`, `IRGReglesPeriode` (fractions) | V006, V007 |
| Paramètres | `Parametres` (dont arrondi) | V006 |
| Affectation (flags) | `Rubriques.EstAffectableManuellement`, `.OccurrencesMultiples`, `.SourceValeurId` | V009, V010 |
| Agents & carrière | `Agents`, `Carrieres`, `Periodes`, `AgentAttributs`, `AgentRubriques`, `AgentRubriqueParametres`, `AvertissementsHistorique` | V011 |
| Bulletins | `Bulletins` (`SnapshotJson`, unicité `AgentId,DatePaie`) | V012 |
| Rappels | `Rappels` | V013 |

Clés : **référentiels = code métier** (ex. `"GI-13-2024-01-01"`, `Grades.Id="IDLS-G148"`) ; **tables de gestion = GUID** (`Agents`, `Bulletins`) — ADR-0004. Détail colonne par colonne : `docs/DICTIONNAIRE_DONNEES.md`.

---

## 8. Anomalies, incohérences, dette technique et risques

Classées par gravité pour l'exploitation. Chaque item est **factuel** (constaté dans le code).

### 8.1 Bloquants pour une mise en service réelle

| # | Constat | Impact | Emplacement |
|---|---|---|---|
| A1 | **Aucun seed au premier lancement.** `App.xaml.cs` applique les migrations mais ne seed **rien**. Une installation neuve ouvre une base vide : aucun corps, aucune rubrique, aucun barème IRG, aucune grille. L'appli est inutilisable sans exécuter la CLI `Tools seed all` manuellement. | Installation non fonctionnelle « out of the box » | `Bootstrapper/App.xaml.cs`, `tools/PaieEducation.Tools` |
| A2 | **Reporting inexistant.** Projet `Reporting` vide. Aucun bulletin PDF ni export : la finalité première (produire des bulletins) n'a pas de sortie documentaire. | Pas de livrable métier imprimable | `src/PaieEducation.Reporting/` |
| A3 | **`SourcesValeur`/`ClesBareme` saisis à la main.** L'écran « Calculer » exige que l'utilisateur fournisse la notation (PAPP `NOTATION_AGENT`) et les clés de barème. Non résolus depuis le dossier agent → le calcul « bout-en-bout » réel depuis l'UI n'est pas automatique. | PAPP/barèmes non calculables sans saisie experte | `Application/Payroll/UseCases/CalculerBulletin.cs:19-21,57-59`, ViewModels |

### 8.2 Écarts d'architecture / conformité

| # | Constat | Impact |
|---|---|---|
| B1 | **`ModeArrondi` codé en dur** (`new ArrondiService(ModeArrondi.DinarPlusProche)`) dans `CalculerBulletin` alors que `Parametres` stocke la règle d'arrondi. Fuite du principe « zéro hardcoding ». | `CalculerBulletin.cs:63` |
| B2 | **`SourceValeurResolver` + 7 `ISourceValeurCalculator` non intégrés** : construits et unit-testés dans `Domain/Workbench` mais **jamais enregistrés en DI ni appelés par le pipeline** (qui consomme un dictionnaire `SourcesValeur` pré-résolu). Le pattern Open/Closed D6 n'est pas actif à l'exécution. | Code partiellement mort ; D6 non tenu en runtime |
| B3 | **Pas de VO `Money`.** `decimal` nu partout, alors que `CONVENTIONS.md §5` prescrit un objet valeur monétaire porteur de l'arrondi/devise. | Risque d'incohérence d'arrondi diffus ; écart doc |
| B4 | **Trois `Result<T>`/`Error` parallèles** (`Shared/Results`, `Domain/Common`, `Domain/Workbench/Internal`). Duplication conceptuelle, conversions implicites entre sous-arbres. | Dette de cohérence |
| B5 | **Pas d'`IUnitOfWork`.** L'écriture réglementaire et sa ligne `AuditLog` **ne sont pas atomiques** (`AppliquerEvolutionReglementaire` : si l'audit échoue après le commit, la base reste modifiée, le use case renvoie un échec). | Risque d'incohérence audit/donnée |
| B6 | **`GérerRéférentiels` incomplet** : seule la grille indiciaire a des chemins d'écriture. **Aucun chemin d'écriture** pour rubriques, cotisations, barèmes, conditions d'éligibilité DNF → le Workbench d'édition (cœur de la valeur « paramétrage sans recompilation ») n'existe qu'en lecture. |

### 8.3 Fonctionnalités transverses absentes

| # | Constat |
|---|---|
| C1 | **Aucune identité utilisateur** (mode autonome Q12 assumé) : `Actor`/`Utilisateur` sont des paramètres libres non reliés à une session. `AuditLog.Actor` n'est pas fiable. |
| C2 | **Pas de logging/observabilité** câblé (`Microsoft.Extensions.Logging` disponible, non utilisé). |
| C3 | **Pas de notifications** (spécifié Phase 6 tâche 1, non fait). |
| C4 | **Pas de Domain Events** (spécifié Phase 3). |
| C5 | **Pas de backup/restore ni de vérification d'intégrité applicative** au runtime (seulement la CLI `validate`). |

### 8.4 Risques

| Risque | Probabilité | Gravité | Mitigation |
|---|---|---|---|
| **Conformité réglementaire non prouvée** : aucun bulletin réel de référence n'a validé les montants (Q11 en attente). Le moteur est correct « par construction/test » mais pas confronté au terrain. | Moyenne | **Élevée** | Prioriser la collecte de bulletins réels et la suite de non-régression Phase 8 dès que disponibles |
| **Seed manuel oublié** en installation → base vide → perception « logiciel cassé ». | Élevée | Élevée | Intégrer le seed au premier lancement (chantier prioritaire) |
| **Périmètre limité au corps pilote** : extension aux autres corps non validée sur données réelles. | Certaine (par design) | Moyenne | Phase 10 itérative, matrice de couverture comme garde-fou |
| **Erreur d'arrondi** si le mode diverge entre pipeline (codé en dur) et paramétrage attendu. | Faible | Moyenne | Lire `Parametres` (B1) |

---

## 9. Recommandations (synthèse — détail dans le Plan d'implémentation)

1. **Rendre l'installation fonctionnelle** : seed au premier lancement (détection base vide → migrate + seed embarqué), sinon rien n'est utilisable (A1). *Priorité critique.*
2. **Livrer le Reporting** : bulletin PDF QuestPDF depuis le snapshot immuable, exports Excel (A2). *Priorité haute.*
3. **Auto-résoudre `SourcesValeur`/`ClesBareme`** depuis le dossier agent (notation, carrière) et **lire l'arrondi depuis `Parametres`** (A3, B1). *Priorité haute.*
4. **Compléter le Workbench d'édition** : use cases + écrans d'écriture pour rubriques/formules/barèmes/cotisations/conditions DNF/IRG — la promesse « paramétrage sans recompilation » n'est tenue qu'en lecture (B6). *Priorité haute.*
5. **Intégrer réellement `SourceValeurResolver`** dans le pipeline (câblage DI + résolution) ou acter sa suppression (B2). *Priorité moyenne.*
6. **Introduire `IUnitOfWork`** pour l'atomicité écriture+audit (B5), et **un `IUserContext`** même minimal pour fiabiliser `AuditLog.Actor` (C1). *Priorité moyenne.*
7. **Validation réglementaire terrain** (Phase 8) + suite Workbench C-T1→C-T6 + tests de perf lot 500 agents. *Priorité haute dès réception des bulletins réels.*
8. **Convergence technique** : unifier `Result`/`Error` (B4), décider du VO `Money` (B3), câbler le logging (C2). *Priorité faible/moyenne (hygiène).*
9. **Packaging & doc** : self-contained, backup/restore, procédure de mise à jour réglementaire, manuel d'exploitation (Phase 9). *Priorité moyenne.*

---

## 10. Conclusion de l'audit

Le projet est dans un état **sain et cohérent** pour un logiciel en développement : architecture Clean/DDD réellement respectée (garde-fous automatisés), moteur de calcul de qualité production (pur, déterministe, explicable, 445 tests verts, build sans warning), schéma de données versionné complet, et une couche Application/Infrastructure solide.

Les écarts ne sont **pas** des défauts de conception mais des **zones non encore construites**, majoritairement concentrées sur : la **sortie documentaire** (Reporting, néant), l'**expérience de premier lancement** (pas de seed), l'**UI de paramétrage complète** (Workbench d'édition), l'**auto-résolution des entrées de calcul** (sources/barèmes/arrondi), et la **validation réglementaire terrain**. Ces manques sont pour la plupart **documentés et assumés** dans `PLAN_ACTION.md` — l'audit confirme que le code correspond fidèlement à ce que le plan déclare, sans écart caché ni fonctionnalité fantôme.

Le plan d'implémentation associé (`AUDIT_2026-07-17_PLAN_IMPLEMENTATION.md`) structure la finalisation en chantiers priorisés.
