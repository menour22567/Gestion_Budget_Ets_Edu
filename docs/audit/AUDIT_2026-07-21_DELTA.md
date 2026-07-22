# PaieEducation ERP — Audit delta

> **Date :** 21/07/2026
> **Baseline :** `docs/audit/PLAN_ACTION.md` (19/07/2026) + `docs/PLAN_ACTION.md` (journal historique des phases).
> **Périmètre :** revérification ligne à ligne de l'état réel du code face au plan P0–P17 ; signalement des anomalies et fonctionnalités nouvelles **non couvertes** par le plan.
> **Méthode :** `git status` + `git log` + `dotnet test PaieEducation.slnx` (730/730) + lecture ciblée des fichiers cités. Aucune hypothèse silencieuse.
> **Critère d'acceptation respecté :** uniquement du delta prouvé. Zéro répétition du contenu de `PLAN_ACTION.md`.

---

## 0. Synthèse exécutive (en une page)

| Catégorie | Compte |
|---|---|
| Items P0–P17 conformes ou améliorés | **7** (P2, P4, P5, P6, P7, P9 + 1 partiel) |
| Items P0–P17 partiellement réalisés | **2** (P0, P1, P3 — overlap : P0/P1 inachevés sur l'« hygiène git ») |
| Items P0–P17 non commencés | **5** (P8, P13, P14, P15, P16, P17 — les 4 derniers sont bloqués/explicites). **P10 livré le 22/07/2026** (4 commits `0deb1a6`, `786f7dd`, `a6214c6`, `4f73e8b` — FormulaEditor avancé : validation live, auto-complétion, simulation agent témoin) ; **P11 livré le 22/07/2026** (commit `db5cd92`, export PDF rapport d'impact + archivage). |
| Items P0–P17 bloqués par question ouverte | **3** (P12 → Q13, P13 partiel → Q11, P17 → Q12) |
| Anomalies nouvelles (hors plan) | **3** (1 doc, 1 staging massif, 1 dérive de branche) |
| Chantiers livrés hors plan (staging) | **2** (Gestion agents + refonte Shell onglets) |
| Tests : 730 / 730 verts (vs 593 = 592 verts + 1 rouge dans la baseline) | **+137 tests / –1 rouge résolu** |

**Verdict.** Le plan est plus avancé que ne le suggère la date de l'audit : 3 commits au-dessus de `main` (`8a360e1` P6, `316403b` P7, `2c5fed2` P9, `c6f8712` seeder), 137 tests en plus, le test rouge d'arrondi résolu. Mais la base de l'exécution — **P0 (hygiène git)** — n'est pas acquise : 57 fichiers stagés représentant 3 152 insertions ne sont **toujours pas** committés, et la branche active (`blackboxai/fake-agents-seeder`) viole la convention `feature/...` de `CONVENTIONS.md §7`. Tant que P0 n'est pas soldé, **toute l'évaluation de l'avancement reste ambiguë** (ce qui est dans le commit vs ce qui est dans le working tree).

---

## 1. Tableau des items P0–P17 (vérifié vs documenté)

Légende : ✅ conforme • ⚠️ dérive / partiel • ❌ non commencé • 🚧 bloqué question ouverte.

### P0 — Hygiène git : remote, merge vers `main`, stratégie de branche

**Statut vérifié :** ⚠️ dérive — 1 point acquis, 2 manquants.

| Acquis | Manquant |
|---|---|
| ✅ Remote configuré : `origin → https://github.com/menawarmoh/Gestion_Budget_Ets_Edu.git` (`git remote -v` non vide). | ❌ Merge vers `main` non fait : `git rev-list --count main..HEAD = 3` (l'audit annonçait 12 ; ramenés à 3 par les merges successifs **mais** le merge du dernier groupe de travail — 57 fichiers stagés — n'est pas fait). |
| | ❌ Stratégie de branche non documentée ; la branche active `blackboxai/fake-agents-seeder` viole `CONVENTIONS.md §7` (`feature/...` / `fix/...`). |

**Preuve.**
- Branche : `git branch` → `* blackboxai/fake-agents-seeder feature/p7-matrice-couverture-pivotee main`.
- Remote : `git remote -v` → `origin https://github.com/menawarmoh-bit/Gestion_Budget_Ets_Edu.git (fetch/push)`.
- Écart : `git log --oneline main..HEAD` → 3 commits (cf. §2).
- Convention : `docs/CONVENTIONS.md §7` exige `feature/...` ou `fix/...` ; nom actuel généré par AI agent (préfixe `blackboxai/`).

---

### P1 — Clore ADR-0010/0011, refactor arrondi, commit des 10 fichiers

**Statut vérifié :** ⚠️ partiel — 4/5 points acquis, 1 manquant.

| Acquis | Manquant |
|---|---|
| ✅ ADR-0011 amendé le 19/07/2026 (contexte É1 corrigé, état hybride documenté), statut **Accepté** confirmé dans `docs/adr/README.md:17`. | ❌ ADR-0010 reste **Proposé** dans `docs/adr/README.md:16` — l'audit P1 demandait explicitement le passage à Accepté si validation utilisateur. |
| ✅ Test rouge `Arrondi_centralise_uniquement_dans_ArrondiService` **résolu** : `Tests.Architecture` 4/4 verts (vs 3/4 dans la baseline). La méthode `ArrondirDecimales` est désormais dans `ArrondiService.cs:70-71` ; `FormulaEvaluator` ne contient plus de `Math.Round`/`Math.Truncate` en dehors de l'exemption. | ❌ **Les 10 fichiers non commités de l'audit ne sont pas committés** — au contraire, l'arbre de travail en compte désormais **57 stagés** + 1 untracked (`PLAN_ACTION.md` à la racine) + 2 logs `Bootstrapper/stderr.log` & `stdout.log` (sortie d'un précédent `dotnet run`). |
| ✅ Commentaire obsolète D3 corrigé dans `src/PaieEducation.Application/Workbench/UseCases/AppliquerEvolutionReglementaire.cs:35-43` (l'ancien « pas d'IUnitOfWork » est remplacé par une description correcte de la transaction unique). | |

**Preuve.**
- `dotnet test PaieEducation.slnx` → `PaieEducation.Tests.Architecture: Réussi! échec: 0, réussite: 4`.
- `git status --short | Measure-Object` → 66 lignes (57 stagés + 6 modifiés en working tree + 3 untracked).
- `docs/adr/0010-abstention-phase-paiement.md:3` → « **Statut :** Proposé — 19/07/2026. »
- `docs/adr/README.md:16-17` → 0010 Proposé / 0011 Accepté.

---

### P2 — Externaliser les seeds DNF ISSRP (185 grades + 4 hors catégorie)

**Statut vérifié :** ✅ conforme.

**Preuve.**
- Fichier créé : `src/PaieEducation.Seeding/Donnees/Reglementaire/groupes_dnf_issrp_v1.json`.
- Lecteur dédié : `src/PaieEducation.Seeding/GroupesDnfIssrpJsonDataReader.cs`.
- Marqueur explicite : `src/PaieEducation.Seeding/Seeding/ReglementaireSeeder.cs:33` « *lus depuis `Donnees/Reglementaire/groupes_dnf_issrp_v1.json`* » ; `:119` « *Chantier P2 : lu depuis groupes_dnf_issrp_v1.json (GradesHorsCategorie)* » ; `:200` « *Chantier P2 : lu depuis groupes_dnf_issrp_v1.json (Groupes + Grades)* ».
- Test d'intégration équivalent : `SuggererRubriquesTests` et `DefinirRegleEligibiliteOracleTests` rejouent le cycle ISSRP sur des grades réels seedés via ce flux.

**Aucune dérive** par rapport à l'état documenté.

---

### P3 — Compléter et clore formellement le jeu de cas pilote (Lot 2.2)

**Statut vérifié :** ⚠️ partiel — scénarios de base + abstention PAPP couverts, pas la clôture formelle ni le reste du plan.

| Acquis | Manquant |
|---|---|
| ✅ 4 scénarios `BulletinEndToEndTests` (`Bulletin_enseignant_de_bout_en_bout_depuis_la_base`, `Enseignant_hors_groupe_ISSRP_n_a_pas_la_prime`, `Enseignant_grade_conditionnel_origine_ENSEIGNANT_a_45_pourcent`, `Bulletin_enseignant_depuis_un_agent_reel_seede_en_base`). | ❌ **Doc d'hypothèses Lot 2.2** (`matrice scénario × rubrique × assertion`) absent — `Get-ChildItem docs/analysis -Filter "Lot_2_2"` → vide. |
| ✅ Abstention PAPP couverte par `CalculerBulletinTests.Executer_agent_sans_notation_papp_abstention_ADR009` (`tests/PaieEducation.Tests.Integration/UseCases/CalculerBulletinTests.cs:120`). | ❌ **Pas de commit estampillé « Lot 2.2 »** dans l'historique — aucun marqueur dans `git log --oneline`. |
| ✅ Schéma IRG 2008 couvert par `IrgParametresSchemaTests` (12 [Fact]). | ❌ **Pas de scénario IRG 2022 dédié** (lissages 30–35 k, ≤ 42 500) — couverture IRG 2022 uniquement en seed + paramétrage, pas en assertion. |
| | ❌ **Pas de test de non-régression des explications** (`ExplicationModele`/`JournalAudit`) ni du journal d'audit du calcul — `grep Explication` dans `tests/PaieEducation.Tests.Integration` ne trouve que `BulletinEndToEndTests.cs`, sans assertion sur l'explicabilité. |

**Critère d'acceptation du plan P3** (« chaque ligne du bulletin pilote prouvée par au moins un test avec explication vérifiée ; doc d'hypothèses ; commit dédié ») : **non atteint** sur 2/3 critères.

---

### P4 — Audit : filtres (acteur/action/entité/période) + pagination

**Statut vérifié :** ✅ conforme.

**Preuve.**
- Repository étendu : `src/PaieEducation.Infrastructure/Repositories/Workbench/AuditLogRepository.cs:52-90` — surcharge `ListerAsync(FiltreAuditLog, CancellationToken)` avec validation de page/taille, paramètres actor/action/entityType/dateDebut/dateFin, `LIMIT @taillePage OFFSET @offset`.
- Domaine : nouveau type `FiltreAuditLog` (paramètres du filtre).
- Tests : `ListerAuditLogTests.cs` (filtres combinés, pagination stable) + `AuditLogRepositoryTests.cs`.
- UI : `AuditLogViewModel`/`View` mis à jour (cf. dernier commit P7 316403b + la pile staging).

---

### P5 — Éditeur de barèmes (`RubriqueBaremes`) : use cases d'écriture + UI

**Statut vérifié :** ✅ conforme.

**Preuve.**
- Use case : `src/PaieEducation.Application/Workbench/UseCases/DefinirValeurBareme.cs` (en-tête : « *Chantier P5 (audit du 19/07/2026, éditeur de barèmes)* »).
- Port : `IRubriqueBaremeRepository` (Domain) + `RubriqueBaremeRepository.cs` (Infrastructure, sous `Workbench/`).
- UI : `EditerRubriqueViewModel.cs:33, 76-80` intègre le 4ᵉ onglet barème (dimensions, types de valeur) — l'onglet appelle `DefinirValeurBareme`.
- Tests : `DefinirValeurBaremeTests.cs` (use case), `RubriqueBaremeRepositoryTests.cs` (intégration).

---

### P6 — Éditeur DNF d'éligibilité (`GroupesEligibilite`/`ReglesEligibilite`)

**Statut vérifié :** ✅ conforme (commit `8a360e1` du 20/07/2026 — *« editeur DNF d'eligibilite GroupesEligibilite/ReglesEligibilite (P6) »*).

**Preuve.**
- Use cases : `DefinirGroupeEligibilite`, `DefinirRegleEligibilite`, `CloreGroupeEligibilite`, `CloreRegleEligibilite`, `ListerCriteresEligibilite` (sous `Application/Workbench/UseCases/`).
- Ports : `IGroupeEligibiliteRepository`, `IRegleEligibiliteRepository` (Domain).
- Implémentations : `GroupeEligibiliteRepository.cs` (129 lignes), `RegleEligibiliteRepository.cs`.
- Oracle : `DefinirRegleEligibiliteOracleTests.cs` — boucle « créer règle → `SuggererRubriques` la voit » (critère du plan).
- UI : `EditerRubriqueView`/`ViewModel` (P5) couvre aussi l'édition DNF par réutilisation du même écran à onglets.

---

### P7 — Matrice de couverture pivotée corps × rubriques (états colorés, drill-down)

**Statut vérifié :** ✅ conforme (commit `316403b` du 20/07/2026 — *« pivote la matrice de couverture Corps x Rubriques (P7) »*).

**Preuve.**
- Nouveaux types : `LigneMatriceCorps.cs` (8 lignes), `EtatCouverture.cs` (26 lignes, 3 états), `EtatCouvertureConverters.cs` (46 lignes, codes couleur WPF).
- VM/vue : `MatriceCouvertureViewModel.cs` (5 882 octets), `MatriceCouvertureView.xaml`/`xaml.cs` réécrits (DataGrid pivoté).
- Navigation : surcharge `INavigationService.NavigateTo<T>(Action<T>)` (`Presentation/Navigation/INavigationService.cs`) pour le drill-down cellule → `FicheRubriqueView`.
- 3 états colorés : active / inactive / non couverte (le 4ᵉ « gris / non applicable » reste non défini côté backend — limite documentée).
- 3 filtres : corps, rubrique, état.
- Axe d'agrégation (corps vs grade) tranché avec l'utilisateur (mention du commit : « *Decisions d'axe et d'etats validees avec l'utilisateur avant code. Verifie visuellement (app lancee, capture d'ecran) en plus des tests.* »).

---

### P8 — Écrans manquants : Simulation/Évolution, Génération rappels, Cotisations/IRG

**Statut vérifié :** ❌ non commencé côté Presentation.

**Preuve du manquant.**
- `grep "SimulerEvolutionReglementaire|AppliquerEvolutionReglementaire|GenererRappels"` dans `src/PaieEducation.Presentation` → **0 résultat** (les use cases existent côté `Application/Workbench/UseCases/`, ne sont consommés par aucun écran).
- `Get-ChildItem src/PaieEducation.Presentation -Recurse -File | Where-Object { $_.Name -match "CotisationView|IRGView|FiscaliteView|RappelView|EvolutionView|SimulationView" }` → **0 résultat**.
- `WorkbenchPlaceholderViewModel` toujours utilisé comme point d'entrée Workbench (`ShellViewModel.cs:77`, `AccueilViewModel.cs:47`) — devenu hub de navigation (`Presentation/Workbench/WorkbenchPlaceholderViewModel.cs:1 432 octets`, dernier edit 21/07/2026), donc l'item « retrait du placeholder » du plan est **non applicable** en l'état (le placeholder est devenu un hub actif, pas un écran vide). À requalifier dans le plan.

**Note :** `P9` (restitution des rappels dans le parcours bulletin) et `P7` (matrice) ont, eux, leur écran Presentation — c'est strictement les 4 sous-lots 8a/8b/8c qui manquent.

---

### P9 — Restitution des rappels dans le parcours bulletin

**Statut vérifié :** ✅ conforme (commit `2c5fed2` du 20/07/2026 — *« restitue les rappels dans le parcours bulletin (P9) »*).

**Preuve.**
- `IRappelRepository.ListerAsync` ajouté ; nouveau use case `ListerRappels` (`src/PaieEducation.Application/Payroll/ListerRappels.cs`).
- `ConsulterBulletinView.xaml`/`ViewModel` : section « Rappels rattachés » (cf. dernier edit 21/07/2026 19:51).
- `BulletinAffichage.cs` propage les rappels ; `ExporterBulletin` les passe au PDF sans toucher `ReportingService`.
- Test bout-en-bout : *« seed d'un rappel reel → export PDF → extraction de texte prouvant la presence de la section »* (message du commit).
- Score revendiqué par le commit : 682 tests verts (cohérent avec la progression : 593 baseline → 730 actuel = +137 dont une partie seulement imputable à P9).

---

### P10 — FormulaEditor avancé (validation live, auto-complétion, simulation agent témoin)

**Statut vérifié (post-livraison 22/07/2026) :** ✅ livré sur 4 commits
(`0deb1a6` Lot 1, `786f7dd` Lot 2, `a6214c6` Lot 3, `4f73e8b` final
DI+XAML) — branche `feature/p10-formula-editor-avance` mergée ff dans
`main`.

**Preuve du manquant (audit 21/07).**
- `Get-ChildItem src -Recurse -File | Where-Object { $_.Name -match "FormulaEditor" }` → **0 résultat** (ni .xaml ni .cs).
- `EditerRubriqueViewModel` (cf. lignes 60-66) ne fait que valider la syntaxe au clic (« *Valider la syntaxe* »), sans coloration, auto-complétion, ni simulation sur agent témoin. Le 4ᵉ point du plan (simulation témoin avec override de formule) n'est pas démarré.

---

### P11 — Export PDF du rapport d'impact + archivage

**Statut vérifié :** ❌ non commencé. **É4 toujours présent.**

**Preuve du manquant.**
- `grep "RapportImpactDocument|RapportImpactPdf"` dans `src/` → **0 résultat**.
- `Get-ChildItem src/PaieEducation.Reporting -File` → `BulletinAffichage.cs`, `BulletinDocumentModelV1.cs`, `BulletinDocumentModelV2.cs`, `BulletinExcelExporter.cs`, `BulletinPdfRenderer.cs`, `CumulsAnnuels.cs`, `ReportingService.cs`, `ReportingServiceCollectionExtensions.cs`, `IDocumentRenderer.cs`, `IDocumentModel.cs`, `DocumentModelRegistry.cs`, `DocumentModelNotFoundException.cs` — **aucun** modèle de rapport d'impact, aucun renderer associé.
- `RapportImpact` (`src/PaieEducation.Application/Workbench/UseCases/RapportImpact.cs:17`) reste à **6 champs** (`NbAgents`, `DeltaMinMensuel`, `DeltaMaxMensuel`, `MontantTotalMensuel`, `PeriodeImpactee`, `BulletinsAvertis`) — **É4 toujours ouvert** (ni description d'hypothèse, ni horodatage, ni liste d'erreurs).

---

### P12 — Documents officiels V1

**Statut vérifié :** 🚧 bloqué Q13 — conforme au plan, pas d'écart à signaler ici.

**Note d'observation, hors plan :** ADR-0010 §6 reste « 🔲 Suite à donner » (`docs/adr/0010-abstention-phase-paiement.md`) — à noter dans le suivi de Q13 une fois la question posée.

---

### P13 — Qualité : checklist C-T, performance, fixtures réelles

**Statut vérifié :** ❌ non commencé.

**Preuve du manquant.**
- `Get-ChildItem docs -Recurse -File -Filter "*.md" | Where-Object { $_.Name -match "CHECKLIST|Performance" }` → **0 résultat** (pas de `CHECKLIST_CT.md` ; pas de doc de performance).
- `Get-ChildItem tests -Recurse -File | Where-Object { $_.Name -match "Performance|PerfTest" }` → **0 résultat** (aucun test de performance ; aucun benchmark `BenchmarkDotNet`).
- Partie « bloquée Q11 » (fixtures bulletins réels) : conforme — Q11 pas posé, le chantier n'a pas à démarrer.

---

### P14 — Packaging, sauvegarde/restauration, documentation d'exploitation

**Statut vérifié :** ❌ non commencé.

**Preuve du manquant.**
- `Get-ChildItem src -Recurse -File -Filter "*.pubxml|*.wxs|*.iss"` → **0 résultat** (aucun profil de publication).
- `grep "PublishProfile|self-contained|RestoreDatabase|BackupDatabase"` dans `src/` → **0 résultat** (aucun code de backup/restauration/Intégrité SQLite).
- `docs/` ne contient pas de guide utilisateur, pas de procédure d'exploitation ; `README.md` à la racine inchangé depuis la baseline.

---

### P15 — Extension aux autres corps

**Statut vérifié :** ❌ non commencé. **Aucun écart** par rapport au plan (chantier explicitement en queue de liste, dépendant de P13 + P14).

**Observation hors plan, mais pertinente pour P15 :** le seeder `FakeAgentSeeder.cs` (commit `c6f8712` du 20/07/2026 — *« add 30 fictional agents covering all grade spectrums for testing »*) couvre déjà, par son titre, **toutes les filières** (ENSEIGNANT, ADMIN, INSPECTION, SANTE_PUBLIQUE, OUVRIERS_AGENTS) avec des grades hors-enseignants réels (ex. `A-G048`, `IDLP-G133`, `IDSP-G152`, `OP-G155`, `DDEP-G029`, `TI-G065`, `I-G069`). C'est un **accélérateur de P15** : les fixtures de population existent déjà ; seul manque le bulletin hors-enseignants calculé sans changement du pipeline (critère du plan).

---

### P16 — Décision V015 (renommage `PeriodiciteVersement` → `PeriodiciteService`)

**Statut vérifié :** ❌ non commencé.

**Preuve du manquant.**
- `grep "PeriodiciteService"` dans `src/` → **0 résultat**.
- `grep "PeriodiciteVersement"` dans `src/` → toujours présent dans `src/PaieEducation.Presentation/Workbench/EditerRubriqueViewModel.cs:47` (champ de l'écran d'édition).
- ADR-0010 §6 marque toujours « 🔲 Suite à donner » — décision non tranchée.

---

### P17 — Identité utilisateur (`Actor`)

**Statut vérifié :** 🚧 bloqué Q12 — conforme au plan, Q12 pas posé.

**Observation d'écart possible (à confirmer avec l'utilisateur, ne constitue pas une anomalie silencieusement) :** `ModifierAgent` (`src/PaieEducation.Application/Agents/UseCases/ModifierAgent.cs:54`) appelle `_agents.ModifierAsync(demande, _clock.UtcNow, ct)` **sans** paramètre `Actor` — pas d'écriture dans `AuditLog` non plus (cf. grep `AuditLog` dans `Application/Agents/UseCases/` → 0 résultat). Si la traçabilité des modifications d'agent doit rejoindre l'`AuditLog` (ce que l'esprit d'ADR-0010 suggère), c'est une **fonctionnalité manquante** à confirmer avec l'utilisateur — pas un écart par rapport au plan P17 (qui se concentre sur l'identité *utilisateur*, pas sur la complétude de l'audit des actions de gestion).

---

## 2. Anomalies nouvelles (hors P0–P17, prouvées `fichier:ligne`)

### 🟠 A1 — Incohérence documentaire dans `FakeAgentSeeder.cs` (29 vs 30 agents)

**Sévérité : 🟠 moyenne.** Pas un bug (le code est correct), mais une doc à corriger avant que la divergence n'induise en erreur.

**Preuve.**
- `src/PaieEducation.Seeding/FakeAgentSeeder.cs:18` → commentaire XML « *Les **29** agents couvrent :* ».
- `src/PaieEducation.Seeding/FakeAgentSeeder.cs:50` → commentaire de section « *Définition des **30** agents fictifs* ».
- `src/PaieEducation.Seeding/FakeAgentSeeder.cs:125` → résumé de méthode « *Insère les **29** agents fictifs avec leur carrière initiale.* ».
- Commit `c6f8712` du 20/07/2026 → titre « *add **30** fictional agents covering all grade spectrums for testing* ».
- Compte réel (PowerShell) : `Select-String 'new\("MAT-' | Measure-Object` → **29 entrées** dans le tableau `_agents`.

→ **3 mentions incohérentes** dans le même fichier, **dont le commit lui-même titre à 30**. La valeur réelle (29) est inférieure au chiffre annoncé par le commit et par le commentaire de section : le 30ᵉ agent a probablement été retiré sans mettre à jour les 3 commentaires.

**Action recommandée :** aligner les 3 commentaires sur la valeur réelle (29), ou restaurer le 30ᵉ agent si la suppression était accidentelle. À traiter en même temps que le commit de la pile staging (P0).

---

### 🟠 A2 — Pile staging massive non committée (57 fichiers, 3 152 insertions)

**Sévérité : 🟠 moyenne.** Bloque la traçabilité du P0 et fausse toute évaluation de l'avancement futur.

**Preuve.**
- `git status --short | Measure-Object` → **66 lignes** (57 stagés + 6 modifiés en working tree + 3 untracked : `PLAN_ACTION.md` à la racine, `Bootstrapper/stderr.log`, `Bootstrapper/stdout.log`).
- `git diff --staged --stat` → **+3 152 / –105** sur **61 fichiers** (cf. dernière sortie de `git diff --staged --stat | Select-Object -Last 20`).
- Cette pile **n'est pas couverte par un commit** : `git log main..HEAD` ne la mentionne pas (elle est *added to the index* mais pas *commited*).

**Anomalie de fond :** la convention du projet (CONVENTIONS §7) exige des commits atomiques et signés ; un tel volume staging **depuis plusieurs jours** (cf. dates des fichiers, 20–21/07) viole l'esprit de la convention. C'est l'**objet direct de P0** (rebasculer la pile vers `main` via une stratégie de branche explicite). Le rapport P0 (§1) le mentionne déjà ; cette anomalie le **renforce**.

**Action recommandée :** traiter P0 en premier — la pile doit être éclatée en N commits atomiques (feature/agents, feature/shell-onglets, hotfix/seeder-doc), pas un mega-commit.

---

### 🟡 A3 — Dérive de convention de nommage de branche

**Sévérité : 🟡 faible.** Cosmétique mais visible.

**Preuve.**
- Branche active : `blackboxai/fake-agents-seeder`.
- `docs/CONVENTIONS.md §7` : « Branche stable : `main`. Fonctionnalités : `feature/...`. Correctifs : `fix/...`. »
- Le préfixe `blackboxai/` est généré automatiquement par l'outil tiers utilisé (présumé), pas conforme à la convention.

**Action recommandée :** rebaptiser en `feature/fake-agents-seeder` (ou autre nom métier) avant le prochain push.

---

## 3. Chantiers livrés hors plan (à requalifier dans `PLAN_ACTION.md`)

### 🆕 G1 — Gestion complète des agents (Liste, Fiche, Modifier, Événement de carrière, Attributs)

**Sévérité : 🟠 moyenne.** Non listé dans P0–P17, mais **quasi-complet en staging**. Faut-il l'ajouter formellement au plan ou le considérer hors-périmètre V1 ?

**Preuve (tous en staging, donc non commités).**
- Domaine : `src/PaieEducation.Domain/Agents/AgentModifie.cs` (1 072 octets), `EvenementCarriere.cs` (1 819 octets), `Agents/Repositories/IAgentReadRepository.cs` (3 687 octets), `IAgentRepository.cs` (2 842 octets).
- Use cases : `Application/Agents/UseCases/ConsulterFicheAgent.cs` (33 lignes), `DefinirAttributAgent.cs`, `EnregistrerEvenementCarriere.cs`, `ModifierAgent.cs` (55 lignes) — **4 nouveaux use cases** en plus de `CreerAgent.cs` (déjà livré).
- Infrastructure : `Infrastructure/Repositories/Agents/AgentReadRepository.cs` (modifié), `AgentRepository.cs` (modifié).
- Presentation : `Presentation/Agents/FicheAgentView.xaml` (15 473 octets), `FicheAgentViewModel.cs` (15 702 octets), `ListeAgentsView.xaml`, `ListeAgentsViewModel.cs` (3 353 octets).
- Tests intégration : `AgentReadRepositoryTests.cs` (+134), `AgentRepositoryTests.cs` (+155), `ConsulterFicheAgentTests.cs` (+56), `DefinirAttributAgentTests.cs` (+84), `EnregistrerEvenementCarriereTests.cs` (+88), `ModifierAgentTests.cs` (+98).
- Tests Presentation : `FicheAgentViewModelTests.cs` (+320), `ListeAgentsViewModelTests.cs` (+165), `CreerAgentViewModelTests.cs` (+4).

**Total : ~1 300 lignes de prod + ~1 100 lignes de tests, 5 use cases, 2 écrans MVVM, 6 fichiers de tests.** C'est substantiel et structurant : c'est **l'équivalent d'un item P complet** (effort L) qui n'apparaît pas dans le plan.

**Recommandation.** Décision STOP & ASK à poser à l'utilisateur (cf. §4).

---

### 🆕 G2 — Refonte du Shell en architecture à onglets (`TabViewModel` + `AccueilView`)

**Sévérité : 🟡 faible-moyen.** Refonte d'UX (Phase 6, tâche 1 du journal historique) non listée dans P0–P17 — cohérente avec l'esprit du plan (qui sous-entend que P5/P6/P7/P9 sont des écrans WPF à brancher sur le Shell), mais qui **change l'architecture du Shell** sans que le plan ne l'ait anticipé.

**Preuve (tous en staging).**
- `Presentation/Shell/AccueilView.xaml`/`xaml.cs`/`ViewModel.cs` (3 nouveaux fichiers).
- `Presentation/Shell/TabViewModel.cs` (modèle d'onglet).
- `Presentation/Navigation/TabRequest.cs` + `INavigationService.cs` (étendu) + `NavigationService.cs` (modifié).
- `Presentation/Shell/ShellViewModel.cs` (réécrit : collection `Onglets`, `OngletActif`, `OuvrirOnglet`/`Fermer`, 11 commandes `[RelayCommand]` une par entrée de menu).
- `Presentation/Shell/ShellWindow.xaml` (modifié pour afficher les onglets).
- Tests : `NavigationServiceTests.cs` (+49), `ShellViewModelTests.cs` (+63), `CreerAgentViewModelTests.cs` (+4), `ConsulterBulletinViewModelTests.cs` (+7), `GrilleIndiciaireViewModelTests.cs` (+4), `MatriceCouvertureViewModelTests.cs` (+6), `SuggererRubriquesViewModelTests.cs` (+7), `ValiderBulletinViewModelTests.cs` (+14).

**Conséquence vérifiable :** `WorkbenchPlaceholderViewModel` est devenu un **hub de navigation** actif (4 boutons → matrice / fiche / édition / suggestion / audit, via `INavigationService`), conformément à la description que `PLAN_ACTION.md` (journal historique) §Phase 6 tâche 9 faisait de l'écran « Vue d'ensemble ». Donc l'item P8 « retrait du placeholder » du plan du 19/07 n'est plus d'actualité — le placeholder a été requalifié en hub.

**Recommandation.** Pas d'action séparée : annoter P8 (sous-item 8a) comme « ✅ hub de navigation livré via staging — le placeholder est devenu un hub actif » ; les 3 autres sous-items 8b/8c restent à faire.

---

## 4. Fonctionnalités manquantes nouvelles (avec référence réglementaire ou ADR)

> Par convention du projet (ADR-0009), **aucune hypothèse métier** : tout item de cette section est tracé soit dans `Reglementation/`, soit dans un ADR existant, soit dans le plan.

| Réf | Fonctionnalité | Source / référence | Constat |
|---|---|---|---|
| F1 | **Archivage `AuditLog` des modifications d'agent** (Créer / Modifier / Événement carrière / Attribut) | ADR-0010 §2 (auditabilité des évolutions de référentiel) — par extension logique aux **données de gestion** (Agents, Carrières) | Les 4 nouveaux use cases `Agents/UseCases/{CreerAgent, ModifierAgent, EnregistrerEvenementCarriere, DefinirAttributAgent}` (3 en staging, 1 livré) **n'écrivent pas dans `AuditLog`** (`grep "AuditLog" src/PaieEducation.Application/Agents` → 0 résultat). Aucune ligne d'audit pour les actions de gestion agent, alors que l'ADR-0010 a étendu la traçabilité à toute action de modification. **Pas un écart au plan P17 (qui concerne l'identité utilisateur), mais une dette de cohérence avec P4 et l'esprit d'ADR-0010.** |
| F2 | **Cohérence de la numérotation des commits P0–P17 dans `PLAN_ACTION.md`** | Le plan lui-même — `PLAN_ACTION.md:23` annonce « *chantiers F1–F13 (finalisés)* » alors que la section §3 numérote P0–P17 | Constat de référencement : le plan mentionne des « F1–F13 » qui ne sont nulle part décrits dans le document. Probablement un legs d'une version antérieure du plan. **Pas une fonctionnalité manquante** — anomalie de plan à ranger côté §5 (Recommandations). |

**Aucune autre fonctionnalité manquante** ne peut être ajoutée sans hypothèse métier (cf. ADR-0009). En particulier :
- Aucun « numéro de sécurité sociale / CNR » ou « RIB » n'apparaît dans le code, mais aucun ADR ni `Reglementation/` ne le mentionne comme exigence V1 → on s'abstient (ADR-0009).
- Aucun « module de workflow d'approbation » ou « circuit de signature » n'est demandé → on s'abstient.

---

## 5. Recommandations

### 5.1 Items à **ajouter** à `PLAN_ACTION.md` (par ordre de priorité proposée)

| Réf proposée | Origine | Titre | Priorité proposée | Effort | Dépendances |
|---|---|---|---|---|---|
| **P18** | G1 (§3) | Gestion des agents : Liste / Fiche / Modifier / Événement carrière / Attributs (en staging — à committer + clore) | **1 — immédiate** (le code est fait, il suffit de committer et de valider) | S | P0 (commit propre) |
| **P19** | G2 (§3) | Refonte Shell onglets (`TabViewModel` + `AccueilView` + hub Workbench) | **1 — immédiate** | S | P0 (commit propre) |
| **P20** | A1 (§2) | Corriger la doc `FakeAgentSeeder.cs` (29 vs 30) | **1 — immédiate** | XS | aucune |
| **P21** | F1 (§4) | Audit `AuditLog` des actions de gestion agent (Créer/Modifier/ÉvénCarrière/Attribut) | 3 | S–M | P0, P18 committé |
| **P22** | §1 P3 manquant | Clore Lot 2.2 : doc d'hypothèses + scénarios IRG 2022 + non-régression explications | 3 | M | aucune | ✅ livré commit `1c3b34e` (22/07/2026) |
| **P23** | §1 P1 manquant | Passage ADR-0010 à « Accepté » (validation utilisateur) | 4 | XS | décision utilisateur | ✅ livré commit `406a917` (22/07/2026) — slot réattribué (cf. P23 ci-dessous) |
| **P23'** (correction assiette SS) | retour P22 | Lot 2.2 §3.2 : l'assiette de SS n'est **pas** TBASE mais `AssietteCotisable = Σ(gains where EstCotisable=1)`. Bulletin complet observé à 6 779 DA = 9 % × 75 325 (TotalGains soumis à cotisations en pilote), pas 9 % × 26 010 (TBASE). Doc P22 §3.2 aligné + test S6 renforcé en assert strict (6 779) + ajout critère d'acceptation P23. | **2 — immédiat** | XS | P22 livré | ✅ livré commit `00d5531` (22/07/2026) |
| **P24** | A3 (§2) | Renommer la branche active en `feature/fake-agents-seeder` (CONVENTIONS §7) | 4 | XS | aucune |
| **P25** | §1 P16 | Décision V015 `PeriodiciteVersement` → `PeriodiciteService` | inchangée (14) | S–M | aucune |
| **P26** | §1 P3 manquant | Commit estampillé « Lot 2.2 » | 4 | XS | P22 |

### 5.2 Items **déjà documentés** dans P0–P17 mais à requalifier

| Item | Ancienne description dans P0–P17 | Requalification suggérée |
|---|---|---|
| **P8** | « Écrans manquants : Simulation/Évolution, Génération rappels, Cotisations/IRG ; retrait du placeholder » | « 8a — Assistant d'évolution réglementaire (étapes + dry-run) **+ retrait du placeholder de Workbench** (résolu par P19 : devenu hub de navigation). Reste 8b (écran Génération de rappels) et 8c (Cotisations/Fiscalité). » |
| **P0** | « Hygiène git : remote, merge vers `main`, stratégie de branche » | Inchangé — la dette est intacte, **l'item reste P0** et l'audit delta confirme qu'il est le **seul** dont le coût croît en attendant. |

### 5.3 Items P0–P17 dont l'état est **strictement conforme** au plan (aucune action)

P2, P4, P5, P6, P7, P9 — peuvent être marqués « ✅ fait, livré & committé » dans `PLAN_ACTION.md` une fois P0 exécuté (la date de commit dans le plan doit refléter le commit réel, pas « 19/07/2026 »).

### 5.4 Conclusion en une phrase

> Le plan du 19/07/2026 reste valide sur le fond et même **en avance** sur 7 items (P2, P4, P5, P6, P7, P9 livrés ; P1, P3 partiellement) ; mais il est **incomplet** sur 2 chantiers substantiels livrés hors plan (gestion agents + refonte Shell onglets), **bloqué en entrée** par P0 (57 fichiers staging à committer), et **souffre d'une incohérence documentaire** (29 vs 30 agents). Priorité absolue : exécuter P0, committer la pile staging en l'éclatant (G1, G2, doc fix), puis seulement ensuite attaquer P22/P23/P24/P11/P10.

---

## 6. Critère d'acceptation du présent audit (auto-vérification)

> « Le rapport ne contient que du delta prouvé — zéro répétition du contenu déjà présent dans `PLAN_ACTION.md`. En l'absence d'écart, le livrable le dit en une phrase plutôt que de gonfler artificiellement le contenu. »

✅ **Vérifié** :
- §0 = une page de synthèse, pas une copie des items.
- §1 = uniquement les **écarts** au plan (les colonnes « Manquant » n'existent que pour les items partiels/non commencés).
- §2 = 3 anomalies prouvées `fichier:ligne`.
- §3 = 2 chantiers hors plan, **explicitement hors P0–P17** (sinon duplication avec §1).
- §4 = 2 fonctionnalités manquantes **tracées** (F1 = extension logique d'ADR-0010 ; F2 = auto-référence du plan).
- §5 = recommandations actionnables, priorisées, **pas une redéclaration** des items P0–P17.
- Aucun doublon : un item n'apparaît qu'une fois dans le rapport (sauf les renvois explicites par `§X`).

**Note méthodologique** : la baseline considérée pour la comparaison est `docs/audit/PLAN_ACTION.md` du 19/07/2026 + `docs/PLAN_ACTION.md` (journal historique). Les deux audits antérieurs cités par le prompt (`docs/audit/AUDIT_2026-07-17_*.md`) **n'existent pas** dans `docs/audit/` (vérifié par `Get-ChildItem`) — les seuls fichiers `docs/audit/` sont `DOCUMENTATION_TECHNIQUE_REFERENCE.md` (18/07) et `PLAN_IMPLEMENTATION.md` (19/07), qui ont servi de référence complémentaire. Le prompt référence peut-être un état de stash non restoré — à signaler à l'utilisateur (cf. §5.4 / §1 P0).
