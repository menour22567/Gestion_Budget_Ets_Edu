# PaieEducation ERP — Plan d'Implémentation (issu de l'audit)

> **Origine :** dérivé de `AUDIT_2026-07-17_DOCUMENTATION_TECHNIQUE.md`.
> **Objet :** conduire le projet de son état actuel (moteur + Application/Infrastructure matures, UI partielle, Reporting néant, déploiement absent) jusqu'à l'achèvement V1, de façon **incrémentale, testable et sans régression**.
> **Contraintes rappelées :** monolithe C#/.NET 10 ; mono-utilisateur ; paramétrage en base (zéro hardcoding) ; hors production ; pas d'obligation de conserver l'historique de données ; respect strict des règles de paie de l'Éducation nationale algérienne.
> **Invariants de travail :** chaque lot laisse `dotnet build` **0 warning** et `dotnet test` **vert** ; toute règle métier douteuse déclenche un STOP & ASK ; toute valeur réglementaire va en base, jamais dans le code.

---

## Vue d'ensemble des chantiers

| # | Chantier | Objectif | Priorité | Complexité | Dépend de |
|---|---|---|:---:|:---:|---|
| **C1** | Installation & données au 1er lancement | Rendre l'appli utilisable « out of the box » | 🔴 Critique | Moyenne | — |
| **C2** | Auto-résolution des entrées de calcul | Calcul réel depuis le dossier agent (sources, barèmes, arrondi) | 🔴 Critique | Moyenne | C1 |
| **C3** | Reporting & documents officiels | Bulletin PDF + exports + attestations | 🟠 Haute | Élevée | C2 |
| **C4** | Workbench d'édition réglementaire | Paramétrage complet sans recompilation (écriture) | 🟠 Haute | Élevée | C1 |
| **C5** | Design System & ergonomie UI | Contrôles métier réutilisables, sélecteurs, explicabilité | 🟡 Moyenne | Moyenne | C4 (partiel) |
| **C6** | Robustesse transverse | UnitOfWork, identité, logging, backup/restore | 🟡 Moyenne | Moyenne | — |
| **C7** | Validation réglementaire & qualité | Conformité vs bulletins réels, perf, suite Workbench | 🟠 Haute | Moyenne | C2, C3 |
| **C8** | Convergence & dette technique | Money, Result unifié, intégration SourceValeurResolver | 🟢 Faible | Faible/Moyenne | — |
| **C9** | Packaging, doc & exploitation | Livrable installable + documentation | 🟡 Moyenne | Moyenne | C1, C3, C6 |
| **C10** | Extension aux autres corps | Généralisation par la donnée (Open/Closed) | 🟢 Faible | Moyenne | C4, C7 |

**Ordonnancement recommandé :** `C1 → C2 → (C3 ∥ C4) → C5 → C6 → C7 → C8 → C9 → C10`.
Les chantiers `C6` et `C8` peuvent s'intercaler à tout moment (hygiène, sans blocage fonctionnel).

---

## C1 — Installation & données au premier lancement 🔴

**Objectif.** Une base neuve doit s'auto-initialiser (migration + seed) au premier démarrage : l'utilisateur voit une application fonctionnelle, pas une base vide.

**Périmètre.** Bootstrapper, Persistence, réutilisation des seeders `Tools` ; aucune modification du moteur.

**Prérequis / dépendances.** Aucun (chantier fondation).

**Composants concernés.** `Bootstrapper/App.xaml.cs`, `Persistence/Migrations`, `tools/PaieEducation.Tools/Seeding/*`, `appsettings.json`.

### Lot C1.1 — Rendre les seeders réutilisables hors CLI
- Extraire la logique de seed des seeders (`NomenclatureSeeder`, `ReglementaireSeeder`, `IrgSeeder`, `FormulesSeeder`) vers un composant invocable par le Bootstrapper **sans** dépendre du projet console `Tools`. Deux options à trancher (STOP & ASK) : (a) déplacer les seeders dans un projet référençable (`Infrastructure` ou nouveau `Seeding`) ; (b) embarquer les jeux de données comme ressources et un `IDataSeeder` dans `Infrastructure`.
- Le CSV cascade (185 grades) devient une **ressource embarquée** (plus de chemin `--csv` externe).
- **Critères d'acceptation.** Le seed complet s'exécute par appel programmatique ; tests `Tests.Tools` toujours verts ; nouveau test « seed complet → counts attendus (corps/grades/rubriques/IRG > 0) ».
- **Risques.** Dépendance de couche (Bootstrapper→Tools interdit par l'esprit Clean) → matérialiser un `IDataSeeder` propre.

### Lot C1.2 — Auto-initialisation au démarrage
- `App.xaml.cs` : après migration, détecter une base « fraîche » (ex. `SchemaVersions` appliqué mais `Corps`/`Rubriques` vides) → exécuter le seed idempotent ; sinon ne rien re-seeder.
- Journaliser le résultat (nombre d'insertions) ; en cas d'échec, message clair et arrêt propre (déjà en place pour la migration).
- **Critères d'acceptation.** Suppression du fichier `paie.db` → relance → application peuplée, écran « Calculer » exploitable ; second lancement ne duplique rien (idempotence vérifiée).
- **Risques.** Idempotence des seeders (déjà `ON CONFLICT DO NOTHING`) à re-vérifier sur toutes les tables.

### Lot C1.3 — Agent de démonstration optionnel
- Seed optionnel (activable via `appsettings.json`) d'un agent pilote (`A-PILOTE`, déjà utilisé par les tests) pour un parcours démo immédiat.
- **Critères d'acceptation.** Flag off par défaut ; on → agent visible et calculable.

**Validation du chantier.** Installation neuve fonctionnelle sans intervention CLI ; parcours « créer agent → suggérer rubriques → calculer bulletin » réalisable de bout en bout.

---

## C2 — Auto-résolution des entrées de calcul 🔴

**Objectif.** Supprimer les saisies manuelles et le hardcoding sur le chemin de calcul : notation (PAPP), clés de barème, mode d'arrondi doivent venir de la base / du dossier agent.

**Périmètre.** Application (use case `CalculerBulletin`), Infrastructure (repos), Domain (câblage `SourceValeurResolver`). Le pipeline pur ne change pas de contrat de calcul.

**Prérequis.** C1 (données présentes).

**Composants concernés.** `CalculerBulletin.cs`, `IPayrollReadRepository`/`PayrollReadRepository`, `VariableRepository`, `SourceValeurResolver` + calculateurs, `AgentAttributs`, `Parametres`.

### Lot C2.1 — Arrondi lu depuis la base
- Lire `Parametres` (clé d'arrondi) et construire `ArrondiService` en conséquence ; injecter la valeur via un port de lecture des paramètres système.
- **Critères d'acceptation.** Changer le paramètre d'arrondi en base modifie le résultat sans recompilation ; test dinar vs dizaine.
- **Risque.** Résultats attendus des tests existants figés sur « dinar le plus proche » → conserver ce défaut seedé.

### Lot C2.2 — Clés de barème résolues depuis la carrière
- `ClesBareme` (CATEGORIE, ECHELON, TYPE_ETABLISSEMENT, GRADE, CORPS…) dérivées automatiquement de l'`AgentContext` déjà résolu, plus fournies par l'appelant.
- **Critères d'acceptation.** L'écran « Calculer » n'exige plus la saisie des clés ; IFC/documentation pédagogique (barèmes catégoriels) résolus automatiquement.

### Lot C2.3 — Sources de valeur résolues (notation & anciennetés)
- Décision d'architecture (STOP & ASK) : **intégrer `SourceValeurResolver`** au pipeline (câblage DI des 7 `ISourceValeurCalculator`) **ou** résoudre les sources dans l'Application avant d'appeler le pipeline. La notation agent (`NOTATION_AGENT`, base PAPP) doit provenir d'`AgentAttributs` ou d'une table de notation dédiée (à définir si absente).
- Anciennetés publique/privée dérivées de la carrière / date de recrutement (RM Q8).
- **Critères d'acceptation.** PAPP calculée sans saisie manuelle de la notation ; test bout-en-bout depuis un agent réel seedé, sans `SourcesValeur` fourni à la main.
- **Risque.** Absence de modèle de notation en base → peut nécessiter une petite migration (table `NotationsAgent` versionnée) — STOP & ASK.

**Validation du chantier.** `CalculerBulletin.Demande` réduite à `(AgentId, DatePaie, Profil)` ; tout le reste est résolu depuis la base ; zéro valeur réglementaire dans le code du chemin de calcul.

---

## C3 — Reporting & documents officiels 🟠

**Objectif.** Produire les sorties documentaires (finalité métier), à partir des agrégats/snapshots immuables.

**Périmètre.** Projet `Reporting` (aujourd'hui vide), consommé par Application/Presentation.

**Prérequis.** C2 (bulletin calculé complet et fiable).

**Composants concernés.** `PaieEducation.Reporting` (QuestPDF/ClosedXML déjà référencés), `BulletinReadRepository`/snapshot, Presentation (bouton d'export).

### Lot C3.1 — Document Engine + registre de modèles versionnés
- `IDocumentRenderer`, registre de modèles, styles centralisés, blocs réutilisables (en-tête établissement, pied réglementaire).
- **Critères d'acceptation.** Rendu déterministe ; styles centralisés ; test de fumée QuestPDF (génération sans exception).

### Lot C3.2 — Bulletin de paie PDF
- Rendu du bulletin depuis le `BulletinSnapshot` (jamais recalcul) : lignes, cumuls, mentions réglementaires, **section Rappels** (D9).
- **Critères d'acceptation.** PDF conforme depuis un bulletin validé ; non-régression visuelle (hash/pixel ou snapshot texte) ; les montants du PDF == snapshot.
- **Risque.** Maquette officielle non fournie → produire une maquette de travail, STOP & ASK pour validation de forme.

### Lot C3.3 — Exports Excel & documents V1
- Export Excel (ClosedXML) d'un bulletin / d'un état récapitulatif ; attestation de salaire CNR (modèle PDF fourni dans `Reglementation/`), attestation de travail.
- **Critères d'acceptation.** Fichiers ouvrables, données correctes ; modèle CNR respecté.

### Lot C3.4 — Rapport d'impact d'évolution (PDF)
- Export PDF du dry-run (`RapportImpact` de `SimulerEvolutionReglementaire`) pour archivage/validation hiérarchique (D8).
- **Critères d'acceptation.** Rapport listant agents × montant × période ; archivable.

**Validation du chantier.** Un bulletin validé s'imprime en PDF conforme ; exports Excel et attestation CNR disponibles.

---

## C4 — Workbench d'édition réglementaire 🟠

**Objectif.** Tenir la promesse cardinale : éditer **toute** rubrique, formule, barème, cotisation, condition d'éligibilité et règle IRG **sans recompilation**. Aujourd'hui : lecture seule uniquement.

**Périmètre.** Application (use cases d'écriture manquants), Infrastructure (repos d'écriture V009), Presentation (arborescence Workbench D7).

**Prérequis.** C1 ; réutilise `ContinuiteTemporelle`, `BaremeResolver`, évaluateur DNF déjà en place.

**Composants concernés.** `Domain/Workbench`, nouveaux repos (`RubriqueRepository`, `BaremeRepository`, `CotisationRepository`, `ConditionEligibiliteRepository`, `IrgRepository`), écrans `Presentation/Workbench`.

### Lot C4.1 — Écriture des rubriques & formules
- Use cases `DefinirRubrique`, `DefinirFormuleRubrique` (versionnée), `DefinirParametreRubrique` ; repos correspondants ; validation applicative (formule parsable via `FormulaParser` avant persistance).
- **Critères d'acceptation.** Créer/éditer une rubrique et sa formule depuis l'UI → recalcul de paie correct ; formule invalide rejetée avec message clair (jamais d'exception qui fuit).
- **Risque.** Dépendances inter-rubriques (DAG) → valider l'absence de cycle à l'enregistrement.

### Lot C4.2 — Éditeur de barèmes (tranches) avec garde-fous
- Use cases + écran d'édition `RubriqueBaremes` avec `ContinuiteTemporelle` (pas de chevauchement/trou, une seule ouverte par clé), prévisualisation d'impact.
- **Critères d'acceptation.** Refus de chevauchement/trou/double période ouverte (tests C-T6) ; barème modifié → calcul mis à jour.

### Lot C4.3 — Éditeur de groupes DNF (éligibilité)
- Use cases d'écriture `ReglesEligibilite`/`GroupesEligibilite` ; UI « groupe → conditions ET, groupes OU ».
- **Critères d'acceptation.** Éditer l'éligibilité ISSRP depuis l'UI → suggestion/calcul cohérents (non-régression du cas ISSRP réel).

### Lot C4.4 — Cotisations & Fiscalité (IRG)
- Écrans + use cases d'écriture pour `Cotisations`/`CotisationAssietteRubriques` et pour `BaremeIRG`/`BaremeIRGTranches`/`IRGReglesPeriode` (barèmes 4/6 tranches, lissages en fractions exactes).
- **Critères d'acceptation.** Modifier un taux de cotisation ou une tranche IRG en base via l'UI → bulletin recalculé sans recompilation.

### Lot C4.5 — Assistant d'évolution réglementaire (6 étapes, dry-run bloquant)
- Câbler `SimulerEvolutionReglementaire` → validation explicite → `AppliquerEvolutionReglementaire` dans un assistant guidé (J3I §7), dry-run obligatoire, rapport d'impact (export via C3.4).
- **Critères d'acceptation.** Aucun commit sans dry-run (sauf bypass tracé `AuditLog`) ; rétroactif → génération de rappels (C-T4), pas de modif de bulletin validé.

**Validation du chantier.** Un administrateur peut modifier n'importe quel paramètre réglementaire depuis l'UI, avec garde-fous, et voir l'effet sur la paie — sans recompilation.

---

## C5 — Design System & ergonomie UI 🟡

**Objectif.** Contrôles métier réutilisables et sélecteurs manquants ; homogénéité visuelle ; explicabilité visible.

**Périmètre.** Presentation (tâche 2 & 10 du plan initial).

**Prérequis.** Partiellement C4 (écrans qui consomment les contrôles).

### Lot C5.1 — Contrôles métier
- `MoneyTextBox` (saisie/format DZD + arrondi), `ERPDataGrid` (tri/format standard), sélecteurs manquants (Fonction, Établissement, Rubrique, Corps/Grade — étendre l'existant Grade/Catégorie/Échelon).
- **Critères d'acceptation.** Contrôles réutilisés sur ≥ 3 écrans ; formats cohérents.

### Lot C5.2 — Panneau d'explicabilité
- `ExplainabilityPanel` consommant le journal d'explication du bulletin (chaque montant justifié : formule, variables lues, éligibilité DNF).
- **Critères d'acceptation.** Sur « Consulter bulletin », chaque ligne montre son explication.

### Lot C5.3 — Matrice de couverture pivotée + drill-down
- Vue matricielle `corps × rubriques` avec code couleur (vert/orange/rouge/gris) et drill-down vers la fiche rubrique (aujourd'hui liste plate uniquement).
- **Critères d'acceptation.** Code couleur conforme J3I §5.5 ; clic → fiche rubrique (C4.1).
- **Risque.** Binding WPF d'une grille pivotée dynamique (limite déjà notée) → prototyper tôt.

### Lot C5.4 — Notifications & chargements async
- Service de notifications (succès/erreur) ; états de chargement homogènes.

**Validation du chantier.** UI professionnelle, cohérente, avec explicabilité visible et sélecteurs complets.

---

## C6 — Robustesse transverse 🟡

**Objectif.** Fiabiliser les écritures, la traçabilité et la sauvegarde.

**Périmètre.** Infrastructure, Application, Shared.

### Lot C6.1 — `IUnitOfWork`
- Introduire une unité de travail (transaction partagée) pour rendre **atomiques** écriture réglementaire + `AuditLog` (corrige B5).
- **Critères d'acceptation.** Échec d'audit → rollback de l'écriture ; test d'atomicité.

### Lot C6.2 — `IUserContext` minimal
- Identité utilisateur (même mono-poste : nom de session Windows / profil configuré) alimentant `Actor`/`Utilisateur` d'`AuditLog` et `AvertissementsHistorique` (corrige C1 de l'audit).
- **Critères d'acceptation.** `AuditLog.Actor` non vide et fiable ; pas d'authentification (Q12 respecté).

### Lot C6.3 — Logging & observabilité
- Câbler `Microsoft.Extensions.Logging` (fichier rotatif local, hors-ligne) sur les use cases et la migration/seed.
- **Critères d'acceptation.** Journal exploitable en cas d'incident ; pas de fuite de données sensibles.

### Lot C6.4 — Backup / restore / vérification d'intégrité
- Sauvegarde/restauration de `paie.db` depuis l'UI (copie + `PRAGMA integrity_check`/`foreign_key_check` réutilisés de la CLI).
- **Critères d'acceptation.** Sauvegarde datée ; restauration testée ; intégrité vérifiée.

**Validation du chantier.** Écritures atomiques, traçabilité fiable, données sauvegardables.

---

## C7 — Validation réglementaire & qualité 🟠

**Objectif.** Prouver l'exactitude réglementaire et les performances (Phase 8).

**Périmètre.** Tests (tous projets), données de référence.

**Prérequis.** C2 (calcul auto), C3 (bulletin imprimable pour comparaison), **réception des bulletins réels (Q11)**.

### Lot C7.1 — Suite Workbench C-T1 → C-T6
- C-T1 (édition UI → base → recalcul correct par pattern) ; C-T2 (non-régression) ; C-T3 (dry-run delta) ; C-T4 (rétroactif → seulement rappels) ; C-T5 (rollback version non propagée) ; C-T6 (refus chevauchement/trou/double ouverte).
- **Critères d'acceptation.** C-T1→C-T6 tous verts.

### Lot C7.2 — Validation vs bulletins de référence réels
- Comparer les montants calculés aux **bulletins réels** fournis ; écarts analysés et documentés ; ajustement par paramétrage (jamais par code).
- **Critères d'acceptation.** Écarts = 0 ou justifiés/validés.
- **Risque / dépendance externe.** Données non encore fournies → jalon conditionné à leur réception (STOP & ASK).

### Lot C7.3 — Tests de performance
- Bulletin individuel < 300 ms ; lot 500 agents ; simulation 200 agents < 2 s (cibles V4).
- **Critères d'acceptation.** Cibles atteintes ou écarts documentés + plan.

**Validation du chantier.** Rapport de conformité + suite de non-régression réglementaire.

---

## C8 — Convergence & dette technique 🟢

**Objectif.** Réduire la dette identifiée sans changer le comportement.

### Lot C8.1 — Décision `Money`
- Trancher (STOP & ASK) : introduire un VO `Money` (conforme `CONVENTIONS.md §5`) **ou** amender la convention pour acter `decimal` + arrondi centralisé. Si adoption : migration progressive, en commençant par les frontières (DTO, reporting).

### Lot C8.2 — Unifier `Result`/`Error`
- Converger les trois implémentations (`Shared`, `Domain/Common`, `Domain/Workbench/Internal`) vers une seule (probablement `Shared`), avec adaptateurs le temps de la migration.
- **Critères d'acceptation.** Une seule source de vérité ; tests verts.

### Lot C8.3 — Intégration ou retrait de `SourceValeurResolver`
- Si non traité en C2.3 : soit l'intégrer réellement (DI + pipeline), soit le retirer pour éviter le code mort (corrige B2).

**Validation du chantier.** Base de code plus cohérente, sans code mort ni duplication conceptuelle.

---

## C9 — Packaging, documentation & exploitation 🟡

**Objectif.** Rendre l'application livrable et maintenable (Phase 9).

**Prérequis.** C1 (seed), C3 (reporting), C6 (backup).

### Lot C9.1 — Packaging self-contained
- Publication self-contained Windows (WPF), initialisation/migration/seed au premier lancement (C1) intégrés au package.
- **Critères d'acceptation.** Installation reproductible sur poste vierge ; base initialisée automatiquement.

### Lot C9.2 — Procédure de mise à jour réglementaire par paramétrage
- Documenter le cœur de la valeur « zéro hardcoding » : comment mettre à jour un barème/taux/formule via le Workbench (C4) sans recompilation.

### Lot C9.3 — Documentation utilisateur, technique & exploitation
- Manuel utilisateur ; manuel d'exploitation (sauvegarde, restauration, mise à jour) ; consolidation ADR ; mise à jour de la présente documentation de référence.
- **Critères d'acceptation.** Un exploitant peut installer, sauvegarder, restaurer et mettre à jour la réglementation en autonomie.

**Validation du chantier.** Installeur + base initialisable + documentation complète.

---

## C10 — Extension aux autres corps 🟢 (itératif)

**Objectif.** Généraliser au-delà du corps pilote **par la donnée uniquement** (Open/Closed, sans modifier le moteur).

**Prérequis.** C4 (édition), C7 (validation).

**Périmètre.** Données/paramètres : corps communs, ouvriers/conducteurs/appariteurs, contractuels (IEP plafonnée 60 %), paramédicaux, personnels d'éducation/direction/inspection/intendance/laboratoire.

### Lots (un par famille de corps)
- Pour chaque corps : seed nomenclature + grille + rubriques + éligibilité + barèmes ; validation via la **matrice de couverture** (C5.3) ; tests de non-régression dédiés.
- **Critères d'acceptation.** Chaque corps ajouté sans modification du moteur ; matrice de couverture exhaustive pour le corps ; bulletins cohérents.
- **Risque.** Spécificités indemnitaires par corps → STOP & ASK au cas par cas.

---

## Feuille de route synthétique (jalons)

| Jalon | Contenu | Chantiers |
|---|---|---|
| **V1-A — « Utilisable »** | Installation auto-seedée + calcul réel sans saisie experte | C1, C2 |
| **V1-B — « Documentaire »** | Bulletin PDF + exports + édition réglementaire de base | C3, C4.1–C4.3 |
| **V1-C — « Administrable »** | IRG/cotisations éditables + assistant d'évolution + Design System + robustesse | C4.4–C4.5, C5, C6 |
| **V1-D — « Conforme »** | Validation vs bulletins réels + perf + suite Workbench | C7 (+ dépendance Q11) |
| **V1-E — « Livrable »** | Packaging + doc + dette maîtrisée | C8, C9 |
| **V2+ — « Généralisé »** | Extension itérative aux autres corps | C10 |

## Principes de mise en œuvre (rappel)

- **Incrémental & sans régression :** un lot = un incrément testable ; la suite des 445 tests (et suivants) reste verte à chaque étape.
- **Zéro hardcoding :** toute nouvelle valeur/règle réglementaire va en base versionnée ; les chantiers C2/C4 renforcent ce principe là où il fuit encore (arrondi, sources).
- **STOP & ASK :** toute ambiguïté réglementaire (maquette bulletin, modèle de notation, spécificités par corps, décision `Money`) est remontée avant implémentation.
- **Traçabilité :** chaque évolution réglementaire passe par le Workbench et l'`AuditLog` (dry-run bloquant, D8).
