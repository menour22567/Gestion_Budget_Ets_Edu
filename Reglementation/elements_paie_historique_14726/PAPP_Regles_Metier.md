# PAPP — Prime d'amélioration des performances pédagogiques

## Référence métier

- **Base** : Traitement (Traitement Principal — token `[TP]`).
- **Périodicité** : servie trimestriellement.
- **Taux** : 0 % à 40 % selon la notation.
- **Plafond** : 40 % (régime PEDAGOGIQUE / INSPECTION), appliqué par `DALNotation`.

Périodes réglementaires d'éligibilité (inchangées par ce pilote, gérées par
`T_Regles_Eligibilite_Indemnite`) :

| Période | Population |
|---|---|
| 01/01/2008 → 28/05/2012 | enseignants, éducation, orientation/guidance, alimentation scolaire |
| 29/05/2012 → 31/12/2024 | + personnels de direction et d'inspection |
| 01/01/2025 → en vigueur | enseignants, éducation, orientation/guidance, alimentation scolaire, direction, inspection |

Le calculateur reproduit l'existant ; il **ne réécrit pas** l'éligibilité réglementaire.

## Périmètre du pilote

Ce pilote couvre **uniquement la rubrique PAPP**.

Sont **hors périmètre** (chemin `EvaluerExpression` historique, taux statiques) :
`PAPG`, `PRP`, `RND`, `PAP_PM`, `IAPG` et les paramédicaux.

## Calcul

```
Montant = TraitementPrincipal × TauxRendement
```

- `TraitementPrincipal` = `ContextePaieDTO.TraitementPrincipal` (token `[TP]`).
- `TauxRendement` = `ResultatTauxRendement.TauxApplique`, une **fraction décimale**
  (40 % = `0.40`, 30 % = `0.30`, 0 % = `0`), déjà plafonnée à 40 % par `DALNotation`.

Aucun arrondi local : l'appelant `CalculerBulletinComplet` applique
`Math.Round(..., 2, MidpointRounding.AwayFromZero)`, comme pour toute rubrique
évaluée par `EvaluerExpression`. Parité stricte avec la formule SQL historique
`[TP]*[TAUX_RENDEMENT]`.

## Source du taux

Le taux est résolu par `DALNotation.CalculerTauxRendementAvecDiagnostic` —
exactement la même source que celle utilisée par le moteur pour substituer le
token `[TAUX_RENDEMENT]`. Le calculateur stocke le diagnostic dans
`contexte.DiagnosticRendement` (exposé ensuite par le bulletin et les alertes).

## Résolution du taux de rendement

Dans le bulletin complet, le taux PAPP est pré-résolu par le moteur via
`DALNotation`. `PAPPCalculator` réutilise ce diagnostic lorsqu'il est disponible
(et correspond bien à la rubrique PAPP, contrôle sur `DiagnosticRendement.Rubrique`)
afin d'éviter une double requête et de garantir la stabilité du calcul.

En appel isolé (tests unitaires, appel hors moteur), un fallback contrôlé vers
`DALNotation.CalculerTauxRendementAvecDiagnostic` reste disponible pour préserver
la testabilité et la compatibilité. Les deux chemins partagent la même source de
vérité : à entrées égales, le taux est identique (parité garantie).

## Statuts du taux

Le calculateur s'appuie sur la sémantique introduite par le ticket de
sécurisation de `DALNotation` (voir [Rendement_Notation_Diagnostic.md](Rendement_Notation_Diagnostic.md)) :

| Statut | Montant PAPP | Bloquant | Comportement |
|---|---|---|---|
| `TauxApplique` | `TP × taux` | non | calcul nominal |
| `ZeroLegitime` | `0` | non | note clôturée à 0 — pas de prime, normal |
| `NoteAbsente` | `0` | (selon amont) | diagnostic explicite, comportement actuel préservé |
| `NoteNonCloturee` | `0` | (selon amont) | diagnostic explicite, comportement actuel préservé |
| `ErreurTechnique` | `0` | True | diagnostic explicite, `EstBloquant`/alerte conservés |
| `IndetermineTechnique` | `0` | True | diagnostic explicite, `EstBloquant`/alerte conservés |

## Compatibilité

Ce ticket **ne modifie pas la politique métier actuelle** : une notation absente
ou non clôturée produit encore `PAPP = 0`, mais le diagnostic devient explicite
et observable via `contexte.DiagnosticRendement` et `b.AlertesCalcul`.

Sont **strictement préservés** :
- la formule SQL `[TP]*[TAUX_RENDEMENT]` dans `T_Formules_Calcul` ;
- les règles d'éligibilité (`T_Regles_Eligibilite_Indemnite`) ;
- les taux réglementaires et la périodicité trimestrielle ;
- `DALNotation` (consommé en lecture, non modifié par ce pilote) ;
- les autres rubriques rendement (taux statiques, hors pilote).

Aucune migration n'est ajoutée ; aucune donnée réglementaire n'est touchée.

## Décision sur les cas bloquants : reportée

Ce pilote rend le calcul PAPP **typé et observable**. Il ne décide **pas** d'une
politique stricte de blocage paie (fail-fast sur `ErreurTechnique` /
`IndetermineTechnique`). Cette décision relèvera d'un ticket métier dédié,
qui pourra s'appuyer sur les statuts désormais disponibles.

## Hors périmètre

PAPG, PRP, RND, PAP_PM, IAPG et les paramédicaux ne sont pas traités dans ce
pilote : leurs taux réglementaires et leurs populations diffèrent, et un
chantier « rendement » global n'est pas l'objet de ce ticket.

## Tests

Voir [`PaieEducation/Tests/PAPPCalculatorTests.vb`](../PaieEducation/Tests/PAPPCalculatorTests.vb) — 15 tests :
registry (PAPP01–02), calcul nominal et politique des 6 statuts (PAPP03–08),
plafond 40 % (PAPP09), parité au centime sur 5 jeux (PAPP10), non-régression
formule et hors-périmètre (PAPP11–12), routage et compatibilité (PAPP13–15).
