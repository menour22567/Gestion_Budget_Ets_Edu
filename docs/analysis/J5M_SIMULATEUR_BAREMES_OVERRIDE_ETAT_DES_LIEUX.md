# J5.m — Simulateur d'impact réel — extension barèmes (D8) : état des lieux et décision de scope

> **Statut :** v1.0 — 18/07/2026 — Audit et décision de scope avant
> implémentation du **Chantier 3 / Lot 3.2** (extension de 3.1 VPI aux
> barèmes forfaitaires).
> **Précédent document lié :** `J5L_SIMULATEUR_IMPACT_REEL_ETAT_DES_LIEUX.md`
> (VPI override, livré commit `3d4a0d0`).
> **Aucun code dans ce document ; c'est un état des lieux.**

---

## 1. Rappel du contexte (3.1 livré)

Le **Lot 3.1** a livré le calcul d'impact réel pour l'évolution de la
**valeur du point indiciaire** (VPI) — scénario « et si on revalorise le
point ? ». Mécanisme clé : `IVariableRepository.ResoudreAvecVPIAsync(agent,
date, vpiOverride)` + `CalculerBulletin.Demande.VpiOverride` (nullable)
+ `IBulletinReadRepository.CompterPourPeriodeAsync(...)` pour
`BulletinsAvertis` (D9).

**Promesse D8** (ADR-0007) partiellement tenue. Reste le scénario « et si
je modifie un forfait ? » — c'est l'objet du Lot 3.2.

---

## 2. Pourquoi les barèmes maintenant (et pas les paramètres ou conditions)

### 2.1 Trois axes d'extension possibles

| Axe | Scénario | Complexité | Valeur métier |
|---|---|---|---|
| **A — Barèmes** | Modifier un forfait (DOC_PEDAG cat. 13 de 2000 → 2500 DA) | Faible | **Forte** — couvre 4 patterns (P4, P5, P6, P12) sur 14 |
| **B — Paramètres (`RubriqueParametres`)** | Modifier un taux (TAUX_QUALIF, IEP_TAUX_PUBLIC_PCT) | Moyenne (clé globale vs clé par rubrique) | Moyenne — limitation V1 sur la clé |
| **C — Conditions d'éligibilité DNF** | Ajouter une éligibilité (ISSRP_45 aussi pour X à partir de 2026) | Forte (édition de `GroupesEligibilite` / `ReglesEligibilite`) | Forte — mais relève de l'édition Workbench, pas du dry-run d'impact |

### 2.2 Décision : 3.2 = Barèmes (A)

**Recommandation :** verrouiller 3.2 sur l'**override des barèmes** (`RubriqueBaremes`)
— cf. J3I §6 ligne « P4 (forfait catégorie) | Fiche rubrique → Barème |
`RubriqueBaremes` (dim CATÉGORIE) | Table des tranches ».

**Pourquoi :**
1. **Le `BaremeResolver` est déjà pur** (`IBaremeResolver.Resoudre(rubriqueId,
   dimension, cle, datePaie, baremes)`) — il n'a pas besoin d'être touché.
   L'override se fait au niveau de l'**alimentation** de la liste `baremes`,
   pas au niveau de la résolution.
2. **Couvre 4 patterns** (P4, P5, P6, P12) sur 14 — soit ~30 % des cas
   d'usage Workbench. Les forfaits DOC_PEDAG, DIR_ETAB, QUALIF, IFC sont
   parmi les plus impactants budgétairement.
3. **Pas de limitation V1 sur la clé** : `RubriqueBaremes` est indexé
   `(RubriqueId, Dimension, BorneInf, DateEffet)`, donc un override par
   (rubrique, dimension, tranche) est sans ambiguïté.
4. **Suit le patron « override »** validé en 3.1 (VPI) : nouvelle méthode
   dédiée sur le port + champ optionnel sur la `Demande` du use case +
   plomberie minimale. Pas de surprise architecturale.
5. **L'axe B (paramètres) est freiné** par la limitation V1 documentée du
   `ConstanteReglementaireCalculator` (« la `Cle` n'est pas unique dans
   `RubriqueParametres`, le contexte de rubrique n'est pas propagé » —
   `ConstanteReglementaireCalculator.cs` lignes 14-19) — ce serait un
   chantier en soi (durcir le port, propager le contexte jusqu'au
   calculateur, etc.), pas un lot rapide.
6. **L'axe C (conditions)** relève plus de l'**édition Workbench** (mode
   d'évolution « clôture + nouvelle version » avec simulation de la
   nouvelle règle d'éligibilité) que d'un dry-run de paramètres purs.
   C'est un chantier à part, plutôt 3.4 ou 3.5.

---

## 3. Conception technique (suite du patron « override »)

### 3.1 Nouveau port / nouvelle méthode

Sur le même modèle que `IVariableRepository.ResoudreAvecVPIAsync` (3.1) :
**pas** de paramètre optionnel sur `ResoudreAsync` → **nouvelle méthode**
intentionnelle. Mais l'override barème est plus naturel comme **collection
de surcharges** (plusieurs barèmes peuvent être overridés en même temps
pour une simulation multi-tranches).

| Élément | 3.1 (VPI) | 3.2 (Barèmes) |
|---|---|---|
| Type d'override | Scalaire (1 valeur) | Collection (N barèmes) |
| Port étendu | `IVariableRepository` | `IPayrollReadRepository` |
| Nouvelle méthode | `ResoudreAvecVPIAsync` | `ChargerAvecBaremesOverrideAsync` |
| Champ `Demande` | `VpiOverride: decimal?` | `BaremesOverride: IReadOnlyList<BaremeValue>?` |
| Effet sur `PayrollInput.Baremes` | n/a | DB-loaded + overrides (après) |

### 3.2 Plomberie détaillée

```
Simulateur.Demande
  └─ BaremesOverride: List<BaremeValue>   (NOUVEAU)
      │
      ▼  transmis tel quel à
CalculerBulletin.Demande
  └─ BaremesOverride: List<BaremeValue>   (NOUVEAU)
      │
      ▼  si != null, utilisé dans
IPayrollReadRepository.ChargerAsync(agent, date, variables, sources, cles, profil, baremesOverride, ct)
  │
  ▼  agrège DB-loaded baremes + overrides → PayrollInput.Baremes
      │
      ▼  BaremeResolver (inchangé) résout comme d'habitude
```

L'override est un `BaremeValue` complet (avec `Periode` couvrant la date de
paie). Si plusieurs overrides couvrent la même clé à la même date, le
**premier** de la liste gagne (insertion en tête via `Insert(0, override)`) —
logique « override bat DB ».

### 3.3 Critères d'acceptation

| Réf | Critère | Mesure |
|---|---|---|
| **C-B1** | `BaremeOverride` peut être null (backward compat) | Tests existants (526) doivent rester verts |
| **C-B2** | `BaremeOverride` non-null = l'override bat la DB | Test intégration : 1 agent, barème DB = 2000, override = 3000 → bulletin avec forfait 3000 |
| **C-B3** | L'override est « étendu » (override d'une seule tranche sur 3 laisse les 2 autres intactes) | Test : barème 3 tranches, override tranche 2, simulation : calcul utilise override pour tranche 2, DB pour les autres |
| **C-B4** | `DeltaMinMensuel`, `DeltaMaxMensuel`, `MontantTotalMensuel` reflètent la différence réelle | Test : forfait 2000→3000 = delta +1000 DA par agent éligible |
| **C-B5** | `BulletinsAvertis` fonctionne aussi avec override barème (pas de régression) | Test : override barème + DateEffet < today → bulletins comptés |
| **C-B6** | Validation de continuité temporelle (L-U8) reste appliquée AVANT le calcul (sécurité) | Test : barème override qui chevauche une autre période → refus |

### 3.4 Plan d'exécution (8 étapes)

1. ✅ Audit + état des lieux (ce document)
2. ✅ Décision de scope
3. Ajouter `BaremesOverride` à `CalculerBulletin.Demande` (nullable, default `null`)
4. Ajouter surcharge `ChargerAvecBaremesOverrideAsync` à `IPayrollReadRepository`
5. Implémenter dans `PayrollReadRepository` (charger DB, agréger avec overrides)
6. Plomber dans `CalculerBulletin.ResoudreAsync` (utiliser la surcharge si override)
7. Refactoriser `SimulerEvolutionReglementaire` :
   - Ajouter `BaremesOverride` à `Demande`
   - Transmettre à `CalculerBulletin.Demande` dans le chemin full
8. Tests Unit (lite + full) + Tests Intégration
9. Build + commit

---

## 4. Risques et mitigations

| Risque | Impact | Mitigation |
|---|---|---|
| `BaremeValue` n'est pas sérialisable / n'a pas de fabrique publique | Impossible à construire depuis le test | Vérifier l'API publique avant ; sinon créer une fabrique de test |
| `PayrollReadRepository.ChargerAsync` est mocké 5 fois (ViewModels, tests) | Régression sur les mocks | Nouvelle méthode dédiée, l'ancienne `ChargerAsync` reste inchangée → 0 mock à toucher |
| Override d'un barème hors période de paie | Calcul incohérent | Test : `Periode` override doit couvrir `DatePaie`, sinon `NotFound` clair |
| Plusieurs overrides pour la même clé | Ambiguïté | Règle « premier gagne » (insertion en tête), testée explicitement |
| Performance : 2 passes `CalculerBulletin` × N agents × override barème | UI Workbench trop lente | Identique à 3.1 — accepté pour ce lot, optimisation en Phase 8 |

---

## 5. Décisions à valider (STOP & ASK)

| Réf | Question | Option recommandée | Bloque |
|-----|----------|--------------------|--------|
| **D-B1** | Verrouiller 3.2 sur l'override barème (axe A), pas paramètres (B) ni conditions (C) ? | ✅ Oui — patron validé, valeur forte, complexité maîtrisée | Code |
| **D-B2** | Nouvelle méthode `ChargerAvecBaremesOverrideAsync` sur `IPayrollReadRepository`, pas de paramètre optionnel sur `ChargerAsync` (même choix qu'en 3.1) ? | ✅ Oui — pas de breaking change, intention explicite | Code |
| **D-B3** | Override = `IReadOnlyList<BaremeValue>?` (collection, pas scalaire) | ✅ Oui — naturel pour les barèmes (multi-tranches) | Code |
| **D-B4** | Règle « premier override gagne » si doublon | ✅ Oui — pragmatique, testable | Tests |
| **D-B5** | Reporter paramètres (axe B) après barème — dans 3.3 si l'utilisateur le demande | ✅ Oui — limitation V1 documentée, chantier à part | Lots suivants |
| **D-B6** | Reporter conditions DNF (axe C) à un chantier d'édition Workbench dédié | ✅ Oui — relève plus de l'édition que du dry-run | Lots suivants |

---

## 6. Annexe — chemins du code à modifier

| Chemin | Type de modification |
|---|---|
| `src/PaieEducation.Domain/Calcul/Repositories/IPayrollReadRepository.cs` | + `ChargerAvecBaremesOverrideAsync` (nouvelle méthode) |
| `src/PaieEducation.Infrastructure/Repositories/Payroll/PayrollReadRepository.cs` | + implémentation de `ChargerAvecBaremesOverrideAsync` (agrégation DB + overrides) |
| `src/PaieEducation.Application/Payroll/UseCases/CalculerBulletin.cs` | + `BaremesOverride` à `Demande` + plomberie |
| `src/PaieEducation.Application/Workbench/UseCases/SimulerEvolutionReglementaire.cs` | + `BaremesOverride` à `Demande` + transmission |
| `tests/PaieEducation.Tests.Unit/Workbench/Application/SimulerImpactReelTests.cs` | + 2-3 tests unitaires (validation du nouveau champ) |
| `tests/PaieEducation.Tests.Integration/UseCases/SimulerImpactReelIntegrationTests.cs` | + 2-3 tests intégration (bout-en-bout avec barème) |
| `docs/PLAN_ACTION.md` | + entrée au journal Phase 5 (3.2 barèmes) |
| `docs/analysis/J5M_SIMULATEUR_BAREMES_OVERRIDE_ETAT_DES_LIEUX.md` | ce document |

**Total :** 4 fichiers modifiés (ajouts seulement) + 2 fichiers de test
étendus + 1 doc.

---

*Dernière mise à jour : 18/07/2026 — v1.0 — soumis à validation implicite
(D-B1 à D-B6 adoptés sauf objection).*
