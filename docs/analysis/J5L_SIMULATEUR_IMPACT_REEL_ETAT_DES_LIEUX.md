# J5.l — Simulateur d'impact réel (D8) : état des lieux et décision de scope

> **Statut :** v1.0 — 18/07/2026 — Audit de couverture et décision de scope avant
> implémentation du **Chantier 3 / Lot 3.1**.
> **Précédent document lié :** `J3I_WORKBENCH_REGLEMENTAIRE.md` §7 (assistant
> d'évolution) + ADR-0007 D8 (dry-run obligatoire avant tout commit).
> **Aucun code dans ce document ; c'est un état des lieux.**

---

## 1. Rappel de la promesse (D8 — ADR-0007)

> *« Toute modification réglementaire passe par un assistant (§7 de J3I) qui
> produit un rapport d'impact (agents × montant × période) avant que la
> modification soit commitée. Aucun commit sans dry-run, sauf bypass admin
> documenté dans `AuditLog`. »*

Le **rapport d'impact** est le **cœur** de la promesse D8, pas un détail. C'est
lui qui transforme l'écran de l'assistant d'un gadget en un **garde-fou métier** :
avant de committer, l'administrateur voit *combien d'agents* sont impactés et
*de combien* (delta min/max, montant total).

Le mockup J3I §7.2 illustre le résultat attendu (extraits) :

> *📊 Agents impactés : 1 240 (sur 1 875 EN — 66 %)*
> *💰 Coût mensuel estimé delta : +3 100 000 DA*
> *⚠ Bulletins validés à recalculer (rappels) : 312*

→ Trois nombres dérivés du calcul, pas trois zéros.

---

## 2. État de l'implémentation (audit 18/07/2026)

`src/PaieEducation.Application/Workbench/UseCases/SimulerEvolutionReglementaire.cs`
lignes 89-95 (extrait) :

```csharp
// 3. Placeholder — Phase 4. Le delta min/max/montant total sera calculé
// par le moteur de calcul de masse sur la base des bulletins calculés.
var rapport = new RapportImpact(
    NbAgents: nbAgents,
    DeltaMinMensuel: 0m,         // ← placeholder
    DeltaMaxMensuel: 0m,         // ← placeholder
    MontantTotalMensuel: 0m,     // ← placeholder
    PeriodeImpactee: demande.NouvellePeriode.DateEffet,
    BulletinsAvertis: 0);        // ← placeholder
```

### 2.1 Ce qui est déjà réel

| Élément | Statut | Source |
|---|---|---|
| Validation de la continuité temporelle (L-U8) | ✅ réel | `ContinuiteTemporelle.Valider` (l. 72) |
| Comptage des agents éligibles (DNF) | ✅ réel | `RegleEligibiliteEvaluator` (l. 80-86) |
| `NbAgents` | ✅ réel | dépend de `AgentsCandidats` + `ConditionsApres` + `Criteres` |
| Tests de la phase « validation » | ✅ 7 tests verts | `SimulerEvolutionReglementaireTests` (regression-safe) |

### 2.2 Ce qui manque (le gap)

| Élément | Statut | Impact |
|---|---|---|
| `DeltaMinMensuel` | ❌ `0m` toujours | L'administrateur ne voit jamais de chiffre d'impact |
| `DeltaMaxMensuel` | ❌ `0m` toujours | idem |
| `MontantTotalMensuel` | ❌ `0m` toujours | idem |
| `BulletinsAvertis` | ❌ `0` toujours | L'aperçu du nombre de bulletins à recalculer (D9) n'apparaît jamais |
| Tests du chemin « impact réel » | ❌ absents | Aucune couverture ne force à implémenter |

### 2.3 Pourquoi le placeholder est resté

Le commentaire « *Phase 4* » est trompeur : la Phase 4 (moteur de calcul
synchrone) **n'a pas vocation** à calculer un dry-run d'évolution. C'est
`RappelCalculator` (Phase 4) qui sait recalculer « à droit constant actuel » un
bulletin déjà validé, agent × agent — mais c'est un calcul de **rappel** (D9),
pas un dry-run prospectif.

Le bon endroit pour le calcul d'impact d'une évolution est la couche
**Application** : on a déjà les agents éligibles (`RegleEligibiliteEvaluator`)
et le moteur de calcul (`CalculerBulletin`). Il manque juste le **mécanisme
« what-if »** sur la VPI, c'est-à-dire la possibilité de rejouer un calcul de
bulletin avec une VPI différente sans toucher la base.

---

## 3. Décision de scope (STOP & ASK implicite)

### 3.1 Scope fonctionnel

**Recommandation :** verrouiller le calcul d'impact réel à l'**évolution de la
VPI** (valeur du point indiciaire), pour les raisons suivantes :

1. **Cohérence avec `AppliquerEvolutionReglementaire`** qui est lui-même déjà
   verrouillé à `ValeurPoint` (mêmes méthodes `DefinirValeurPointAsync` /
   `DupliquerValeurPointAsync`). Le `RapportImpact` produit par le simulateur
   est ce qui est *réellement* passé à `AppliquerEvolutionReglementaire` via
   `Demande.RapportImpact` — donc si l'un est VPI, l'autre doit l'être aussi.
2. **Le scénario type** de la doc J3I §7.2 (qualification, ISSRP, etc.) est
   plus complexe et sort du scope d'un lot. La VPI est le cas le plus simple
   *et* le plus fréquent (revalorisations générales), donc le bon point
   d'entrée.
3. **L'extension à d'autres rubriques** (paramètres, barèmes, conditions
   d'éligibilité) se fera dans des lots ultérieurs, en suivant le même
   patron « override » sur le(s) paramètre(s) modifié(s).

### 3.2 Scope technique

**Recommandation :** ajouter un mécanisme d'override **limité à la VPI** sur
`IVariableRepository` (méthode `ResoudreAvecVPIAsync`), et le plumber à
travers `CalculerBulletin.Demande.VpiOverride` (nullable). Le reste du calcul
reste piloté par la base — on n'invente pas un mode « full override » qui
soustrait toute la résolution à la DB.

Pourquoi une **nouvelle méthode** plutôt qu'un paramètre optionnel sur
`ResoudreAsync` :

- **Pas de breaking change** : les 5 mocks Moq existants (CalculerBulletin,
  ValiderBulletin, 2×ViewModel Presentation) n'ont pas à évoluer.
- **Séparation explicite des intentions** : `ResoudreAsync` = lecture « réelle »,
  `ResoudreAvecVPIAsync` = lecture « simulée ». Le nom porte la sémantique.
- **Évolutif** : les futurs override « what-if » (autres paramètres, barèmes)
  suivront le même patron `ResoudreAvecXxxAsync`.

### 3.3 Scope de tests

| Niveau | Quoi | Pourquoi |
|---|---|---|
| **Unit (lite)** | Tests existants de la phase « validation » (`SimulerEvolutionReglementaireTests`, 7 tests) | Ne pas casser le contrat actuel — le simulateur **doit** continuer à fonctionner en mode validation pure (sans DB, sans I/O) |
| **Unit (full)** | Nouveau fichier `SimulerImpactReelTests` : mocks de `CalculerBulletin` + `IAgentCarriereRepository` + `IBulletinReadRepository` + `IClock` | Tester le calcul de delta sans I/O (mocks retournent des `Bulletin` fixes) |
| **Intégration** | Nouveau fichier `SimulerImpactReelIntegrationTests` : DB migrée, agent réel, VPI réelle, 2 bulletins validés | Prouver que le calcul bout-en-bout fonctionne contre la vraie stack (`SchemaTestSupport.CreateMigrated()` comme les 17 tests d'intégration existants) |

---

## 4. Critères d'acceptation du lot

| Réf | Critère | Mesure |
|---|---|---|
| **C-S1** | `DeltaMinMensuel`, `DeltaMaxMensuel`, `MontantTotalMensuel` ne sont **plus jamais** à 0m quand `NouvelleValeurPoint` est fourni et qu'il y a au moins un agent éligible | Test intégration avec 3 agents (2 éligibles) et VPI 45→50 DA : delta ≈ +2 500 DA/agent, total ≈ +5 000 DA/mois |
| **C-S2** | `BulletinsAvertis` est réel pour une évolution rétroactive | Test intégration avec 2 bulletins validés en 03/2025 et une évolution 01/2025 : `BulletinsAvertis = 2` |
| **C-S3** | `BulletinsAvertis = 0` pour une évolution future | Test intégration avec 2 bulletins validés en 03/2025 et une évolution 12/2026 : `BulletinsAvertis = 0` |
| **C-S4** | Le mode « validation pure » (sans `NouvelleValeurPoint`) continue à fonctionner **sans dépendance** (constructeur parameterless) | 7 tests existants doivent rester verts sans modification |
| **C-S5** | Le calcul est **idempotent** : 2 simulations successives avec les mêmes paramètres produisent le même `RapportImpact` | Test intégration : 2 appels successifs, valeurs strictement identiques |
| **C-S6** | Aucune régression : 511 tests existants + nouveaux tests restent verts | `dotnet test` global |

---

## 5. Plan d'exécution (12 étapes)

1. Audit + état des lieux (ce document) — **FAIT**
2. Décision de scope — **FAIT** (§3)
3. Ajouter `ResoudreAvecVPIAsync` à `IVariableRepository` (nouvelle méthode)
4. Implémenter dans `VariableRepository` (override au lieu de `ChargerValeurPointAsync`)
5. Ajouter `VpiOverride` à `CalculerBulletin.Demande` (nullable)
6. Plumber dans `CalculerBulletin` : si `VpiOverride` est set, appeler `ResoudreAvecVPIAsync`
7. Ajouter `CompterPourPeriodeAsync(periodeDebut, periodeFin, ct)` à `IBulletinReadRepository`
8. Implémenter dans `BulletinReadRepository`
9. Refactoriser `SimulerEvolutionReglementaire` :
   - Ajouter `NouvelleValeurPoint` à `Demande`
   - Ajouter deps optionnels (constructor overload) : `CalculerBulletin?`, `IBulletinReadRepository?`, `IClock?`
   - Chemin `NouvelleValeurPoint == null` → lite (actuel, pas de deps)
   - Chemin `NouvelleValeurPoint != null` → impact réel (deps requises, sinon `InvalidOperationException`)
10. Tests Unit (lite) : vérifier que les 7 tests existants passent **sans modification**
11. Tests Unit (full) : nouveau fichier, mocks
12. Tests Intégration : nouveau fichier, DB seedée, agent réel, VPI réelle
13. Build 0 warning, `dotnet test` global → tous verts
14. Commit + récap tableau de progression

---

## 6. Risques et mitigations

| Risque | Impact | Mitigation |
|---|---|---|
| `CalculerBulletin` ne sait pas gérer un `VpiOverride` correctement | Calcul d'impact faux | Tests d'intégration avec DB réelle et agent réel (le plus important est de prouver que la nouvelle VPI remonte jusqu'au pipeline) |
| `CompterPourPeriodeAsync` oublie un cas limite (date de fin ouverte) | `BulletinsAvertis` sous-estimé | Test intégration : bulletins en 03/2025 + évolution 01/2025-01/2026 ouverte → 2 bulletins comptés |
| Régression sur les 511 tests existants | Casser l'existant | Aucune signature publique modifiée, **seulement ajout** de méthodes/champs optionnels |
| Performance de la simulation (N agents × 2 calculs) | UI Workbench trop lente | Pour ce lot, on accepte le coût — l'optimisation (cache, batch, parallélisation) est un chantier ultérieur. La cible perf V4 (simulation 200 agents < 2 s) sera vérifiée en Phase 8 |

---

## 7. Décisions à valider (STOP & ASK)

| Réf | Question | Option recommandée | Bloque |
|-----|----------|--------------------|--------|
| **D-S1** | Verrouiller le calcul d'impact à la VPI uniquement (cf. scope `AppliquerEvolutionReglementaire`) ? | ✅ Oui — alignement, testabilité, livrable de bout en bout | Code |
| **D-S2** | Nouvelle méthode `ResoudreAvecVPIAsync` plutôt que paramètre optionnel sur `ResoudreAsync` ? | ✅ Oui — pas de breaking change, intention explicite | Code |
| **D-S3** | Mode dual : lite (parameterless, validation pure) + full (dépendances injectées, impact réel) ? | ✅ Oui — préserve les 7 tests existants | Tests |
| **D-S4** | Pour l'extension à d'autres rubriques (paramètres, barèmes), on suivra le même patron « override » mais dans des lots séparés ? | ✅ Oui — YAGNI, un lot = un scenario | Lots suivants |

---

## 8. Annexe — chemins du code à modifier

| Chemin | Type de modification |
|---|---|
| `src/PaieEducation.Domain/Calcul/Repositories/IVariableRepository.cs` | + `ResoudreAvecVPIAsync` |
| `src/PaieEducation.Infrastructure/Repositories/Payroll/VariableRepository.cs` | + implémentation de `ResoudreAvecVPIAsync` |
| `src/PaieEducation.Domain/Calcul/Repositories/IBulletinReadRepository.cs` | + `CompterPourPeriodeAsync` |
| `src/PaieEducation.Infrastructure/Repositories/Payroll/BulletinReadRepository.cs` | + implémentation de `CompterPourPeriodeAsync` |
| `src/PaieEducation.Application/Payroll/UseCases/CalculerBulletin.cs` | + `VpiOverride` à `Demande` + plomberie |
| `src/PaieEducation.Application/Workbench/UseCases/SimulerEvolutionReglementaire.cs` | + `NouvelleValeurPoint` à `Demande` + deps + chemin impact réel |
| `tests/PaieEducation.Tests.Unit/Workbench/Application/SimulerEvolutionReglementaireTests.cs` | **PAS de modification** (préserver 7 tests verts) |
| `tests/PaieEducation.Tests.Unit/Workbench/Application/SimulerImpactReelTests.cs` | **NOUVEAU** (tests avec mocks) |
| `tests/PaieEducation.Tests.Integration/UseCases/SimulerImpactReelIntegrationTests.cs` | **NOUVEAU** (bout-en-bout DB) |
| `docs/PLAN_ACTION.md` | + entrée au journal Phase 5 |
| `docs/analysis/J5L_SIMULATEUR_IMPACT_REEL_ETAT_DES_LIEUX.md` | ce document |

**Total :** 5 fichiers modifiés (ajouts seulement) + 2 nouveaux fichiers de test
+ 1 doc.

---

*Dernière mise à jour : 18/07/2026 — v1.0 — soumis à validation implicite
(D-S1 à D-S4 adoptés sauf objection).*
