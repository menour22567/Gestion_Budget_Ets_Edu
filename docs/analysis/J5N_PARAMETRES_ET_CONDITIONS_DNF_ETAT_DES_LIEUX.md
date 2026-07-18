# J5.n — Simulateur d'impact réel : paramètres (3.3) + conditions DNF (3.4) — état des lieux et décision de scope

> **Statut :** v1.0 — 19/07/2026 — Audit et décision de scope avant
> implémentation des **Chantiers 3.3 et 3.4** (extensions de 3.1 VPI et
> 3.2 barèmes, déjà livrés).
> **Précédents documents liés :** `J5L_SIMULATEUR_IMPACT_REEL_ETAT_DES_LIEUX.md`
> (VPI, livré `3d4a0d0`) + `J5M_SIMULATEUR_BAREMES_OVERRIDE_ETAT_DES_LIEUX.md`
> (barèmes, livré `7d2831a`).
> **Aucun code dans ce document ; c'est un état des lieux.**

---

## 1. Rappel du contexte

Le simulateur d'impact réel (D8 / ADR-0007) est en place depuis deux lots :
- **3.1** — VPI (`NouvelleValeurPoint`) — commit `3d4a0d0` — 15 tests
- **3.2** — Barèmes forfaitaires (`BaremesOverride`) — commit `7d2831a` — 5 tests

531 tests verts au moment où ce document est écrit.

Les **deux axes restants** identifiés dans J5L §2.1 sont :
- **B — Paramètres** (`RubriqueParametres`) — axe B
- **C — Conditions d'éligibilité DNF** (`ReglesEligibilite` / `GroupesEligibilite`) — axe C

L'utilisateur demande d'enchaîner sur 3.3 (= axe B) et 3.4 (= axe C). Cet état
des lieux tranche le scope des deux lots pour éviter de sous-estimer le
second (axe B) ou de sur-investir sur du déjà-couvert (axe C).

---

## 2. Axe B — Paramètres (`RubriqueParametres`) : 3.3

### 2.1 État de l'art codebase

Le port `IRubriqueParametreLookup` (Domain, `src/PaieEducation.Domain/Calcul/Repositories/IRubriqueParametreLookup.cs`)
expose une seule méthode : `LireParametreAsync(cle, dateEffet, ct)` qui lit
`RubriqueParametres` par `Cle` (sans `RubriqueId`). Le `ConstanteReglementaireCalculator`
(Infrastructure) consomme ce port pour la source `CONSTANTE_REGLEMENTAIRE`.

**Limitations V1 documentées** (cf. `IRubriqueParametreLookup.cs` lignes 9-18
et `ConstanteReglementaireCalculator.cs` lignes 14-19) :

> *« La `Cle` n'est pas unique dans `RubriqueParametres` (un même code,
> ex. `TAUX_45`, peut servir plusieurs rubriques) et le contexte de
> rubrique n'est pas encore propagé jusqu'au calculateur. Le lookup
> prend donc la version la plus récente toutes rubriques confondues. »*

C'est une **lacune architecturale connue** depuis la Lot 1.2 (V1), pas un
oubli de 3.3.

### 2.2 Conséquence sur l'extension simulateur

Pour étendre le simulateur aux paramètres, il faudrait :
1. **Durcir le port** : `LireParametreAsync(rubriqueId, cle, dateEffet, ct)` —
   signature change, impact sur 5+ mocks (Calculator, ViewModel, tests).
2. **Propager le contexte de rubrique** jusqu'au `ConstanteReglementaireCalculator`
   — actuellement, le calculateur reçoit seulement `agent, datePaie` ; il
   faudrait ajouter `rubriqueId` à `ISourceValeurCalculator.Calculer(agent,
   datePaie)` — signature change, impact sur 7 calculateurs.
3. **Brancher la source CONSTANTE_REGLEMENTAIRE** dans `CalculEntreeResolver`
   pour qu'elle soit effectivement résolue au moment du calcul — actuellement
   le calculator est enregistré en DI mais **jamais appelé** par le resolver
   (seul `NOTATION_AGENT` l'est).
4. **Adapter au moins une formule du seed** pour consommer `valeurSource(PAPP_TAUX)`
   au lieu d'un taux hardcodé, pour qu'un changement de paramètre ait un
   impact observable.
5. **Ajouter l'override** (Lot 3.3 lui-même).

C'est **5 chantiers en cascade**, pas un lot rapide. Et la valeur métier est
**nulle en l'état du seed** : aucune formule ne consomme un paramètre
versionné, donc « et si je change `IEP_TAUX_PUBLIC_PCT` ? » n'a pas de
réponse possible sans l'étape 4 (refonte d'une formule).

### 2.3 Décision de scope (D-P1 à D-P4)

**Recommandation :** **3.3 = override au niveau du lookup uniquement**, sans
intégration simulateur dans ce lot.

- Étend `IRubriqueParametreLookup` avec une surcharge `LireParametreAvecOverridesAsync`
  (nouvelle méthode, pas de breaking change sur l'existant).
- Implémente dans `RubriqueParametreLookup` (override > DB).
- Tests : unitaire + intégration (preuve que l'override bat la DB).
- **PAS d'intégration simulateur** dans ce lot — ça viendrait dans un
  chantier séparé qui combinerait D-P1 (durcir le port) + D-P2 (propager
  le contexte) + D-P3 (refonte d'une formule consommatrice).

C'est une **extension de plate-forme** sans use case utilisateur immédiat,
mais qui pose les fondations pour les chantiers à venir. Le scope est petit,
testable, et documenté.

### 2.4 Critères d'acceptation 3.3

| Réf | Critère | Mesure |
|---|---|---|
| **C-P1** | `LireParametreAsync` reste inchangé (backward compat) | Tests existants (531) restent verts |
| **C-P2** | `LireParametreAvecOverridesAsync(cle, date, overrides, ct)` retourne l'override s'il existe pour `cle` | Test unitaire : `overrides = { "TAUX_X": 0.5 }` → résultat 0.5 sans toucher DB |
| **C-P3** | Si pas d'override, comportement identique à `LireParametreAsync` | Test unitaire : `overrides = { "AUTRE_CLE": 0.5 }` → lecture DB normale |
| **C-P4** | `overrides = null` ≡ `overrides = {}` ≡ pas d'override | Test unitaire |
| **C-P5** | Lecture DB réelle valide (pas de régression) | Test d'intégration avec DB seedée |

---

## 3. Axe C — Conditions d'éligibilité DNF (`ReglesEligibilite`) : 3.4

### 3.1 État de l'art codebase

Le simulateur `SimulerEvolutionReglementaire` accepte déjà
`ConditionsApres` + `Criteres` dans sa `Demande`, et le
`RegleEligibiliteEvaluator` (Lot 1.2) évalue la DNF (groupes ETés, groupes
OUés) pour chaque agent.

**C'est déjà câblé en lite path** (chemin validation pure, sans I/O) :
les 7 tests originaux (`SimulerEvolutionReglementaireTests.cs`) couvrent
le décompte d'agents éligibles via DNF.

**Mais c'est moins clair en full path** (chemin impact réel, Lot 3.1/3.2) :
le simulateur utilise `ConditionsApres` pour évaluer l'éligibilité dans
`ExecuterCheminFull`, mais aucun test d'intégration ne vérifie **explicitement**
que les conditions DNF (groupes) sont correctement propagées et évaluées
dans le chemin full.

### 3.2 Gap à boucher

Le risque : si le chemin full évalue l'éligibilité différemment du chemin
lite (par exemple, à cause d'un état partagé entre les 2 passes `CalculerBulletin`),
le `RapportImpact.BulletinsAvertis` ou le delta net serait calculé sur
un sous-ensemble d'agents incorrect.

**Tests d'intégration manquants** :
- Condition DNF avec 2 groupes (A et B), 1 agent dans chaque groupe, 1 agent
  hors groupe → vérifier `NbAgents=2`, delta calculé pour les 2.
- Condition DNF avec 3 groupes (A, B, C) et un agent qui appartient à
  plusieurs groupes → vérifier qu'il n'est compté qu'une fois.
- Rétroactivité + DNF : `DateEffet < DateCalcul` + 2 agents éligibles +
  2 bulletins validés → `BulletinsAvertis=2`.

### 3.3 Décision de scope (D-D1 à D-D3)

**Recommandation :** **3.4 = tests d'intégration qui prouvent que le chemin
full respecte la DNF** des conditions overridées.

- Aucune modification de code de production nécessaire (le simulateur est
  déjà correct).
- 3-4 tests d'intégration sur le chemin full avec des conditions DNF
  complexes (groupes, multi-groupes, rétroactivité + DNF).
- Preuve de cohérence lite ↔ full : même `NbAgents` pour les mêmes inputs.

### 3.4 Critères d'acceptation 3.4

| Réf | Critère | Mesure |
|---|---|---|
| **C-D1** | Le chemin full respecte une condition DNF à 2 groupes (A ou B) | Test : 2 agents dans A, 1 dans B, 1 hors → NbAgents=3 |
| **C-D2** | Un agent éligible via plusieurs groupes n'est compté qu'une fois | Test : 1 agent satisfait A et B → NbAgents=1 (pas 2) |
| **C-D3** | Cohérence lite ↔ full pour le `NbAgents` | Test : même Demande, lite puis full → même NbAgents |
| **C-D4** | DNF + rétroactivité : `BulletinsAvertis` est correct | Test : 3 agents éligibles, 2 bulletins validés dans la période rétroactive → BulletinsAvertis=2 |
| **C-D5** | DNF + override barème (Lot 3.2) : le delta est calculé sur les agents éligibles uniquement | Test : barème override + 1 agent éligible + 1 inéligible → delta = 1 * delta, pas 2 |

---

## 4. Plan d'exécution

### 4.1 Lot 3.3 (Paramètres — scope réduit)

1. Audit + état des lieux (ce document) — **FAIT**
2. Décision de scope — **FAIT** (§2.3)
3. Ajouter `LireParametreAvecOverridesAsync(cle, date, overrides, ct)` à `IRubriqueParametreLookup`
4. Implémenter dans `RubriqueParametreLookup` (overrides > DB)
5. Tests unitaires (mocks SQLite in-memory ou stub)
6. Tests d'intégration (DB seedée + override > DB)
7. Build + commit `Lot 3.3`

### 4.2 Lot 3.4 (Conditions DNF — scope test only)

1. Ajouter 3-4 tests d'intégration au fichier `SimulerImpactReelIntegrationTests.cs`
   (les `ConditionsApres` sont déjà supportées par le simulateur, c'est juste
   un renforcement de la couverture).
2. Build + commit `Lot 3.4`

### 4.3 Commits séparés

- **Commit 3.3** : "Lot 3.3: ajoute override au lookup des paramètres (J5N, sans intégration simulateur)"
- **Commit 3.4** : "Lot 3.4: verrouille le chemin full sur conditions DNF (J5N, tests only)"

Deux commits pour respecter le pattern "un commit = un scenario" (3.1, 3.2
séparés ; 3.3, 3.4 séparés).

---

## 5. Décisions à valider (STOP & ASK)

| Réf | Question | Option recommandée | Bloque |
|-----|----------|--------------------|--------|
| **D-P1** | 3.3 = override du lookup uniquement, sans intégration simulateur ? | ✅ Oui — intégration = 5 chantiers, hors scope d'un lot | Code |
| **D-P2** | Nouvelle méthode `LireParametreAvecOverridesAsync`, pas de paramètre optionnel sur `LireParametreAsync` ? | ✅ Oui — patron validé en 3.1/3.2 | Code |
| **D-P3** | Refonte d'une formule du seed pour consommer un paramètre = chantier séparé (post-3.4) ? | ✅ Oui — pas de cas d'usage tant que les formules hardcodent les taux | Lots suivants |
| **D-D1** | 3.4 = tests d'intégration uniquement (pas de code de production) ? | ✅ Oui — le simulateur est déjà correct, c'est de la couverture | Tests |
| **D-D2** | 3.3 et 3.4 = 2 commits séparés ? | ✅ Oui — un scenario = un commit | Commits |

---

## 6. Annexe — chemins du code à modifier

### 6.1 Lot 3.3

| Chemin | Type de modification |
|---|---|
| `src/PaieEducation.Domain/Calcul/Repositories/IRubriqueParametreLookup.cs` | + `LireParametreAvecOverridesAsync` (nouvelle méthode) |
| `src/PaieEducation.Infrastructure/Repositories/Payroll/RubriqueParametreLookup.cs` | + implémentation (overrides > DB) |
| `tests/PaieEducation.Tests.Unit/Workbench/Application/LookupParametreOverrideTests.cs` | **NOUVEAU** — tests unitaires |
| `tests/PaieEducation.Tests.Integration/Repositories/RubriqueParametreLookupOverrideTests.cs` | **NOUVEAU** — tests d'intégration bout-en-bout DB |
| `docs/PLAN_ACTION.md` | + entrée au journal Phase 5 (3.3 paramètres) |
| `docs/analysis/J5N_PARAMETRES_ET_CONDITIONS_DNF_ETAT_DES_LIEUX.md` | ce document |

### 6.2 Lot 3.4

| Chemin | Type de modification |
|---|---|
| `tests/PaieEducation.Tests.Integration/UseCases/SimulerImpactReelIntegrationTests.cs` | + 3-4 tests DNF en chemin full (aucune modif de prod) |
| `docs/PLAN_ACTION.md` | + entrée au journal Phase 5 (3.4 conditions DNF) |

---

*Dernière mise à jour : 19/07/2026 — v1.0 — soumis à validation implicite
(D-P1 à D-D2 adoptés sauf objection).*
