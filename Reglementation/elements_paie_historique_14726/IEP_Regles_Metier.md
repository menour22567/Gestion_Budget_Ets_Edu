# IEP — Indemnité d'Expérience Professionnelle

## Objet

L'IEP (Indemnité d'Expérience Professionnelle) rétribue l'ancienneté
professionnelle de l'agent. Elle se décline en **deux rubriques distinctes**
selon le statut contractuel :

| Code rubrique | Public visé   | Base de calcul                            |
|---------------|---------------|-------------------------------------------|
| `IEP_FONC`    | Fonctionnaires (TITULAIRE / STAGIAIRE) | Indice d'échelon × Valeur du point |
| `IEP_CONT`    | Contractuels                            | Traitement de Base × taux composite d'ancienneté plafonné |

Les deux rubriques sont mutuellement exclusives au niveau métier : un
fonctionnaire perçoit `IEP_FONC` (et `IEP_CONT` court-circuite à 0), un
contractuel perçoit `IEP_CONT` (et `IEP_FONC` est exclu par la règle
d'éligibilité `T_Regles_Eligibilite_Indemnite`).

## Différence entre IEP_FONC et IEP_CONT

- `IEP_FONC` reflète directement la grille indiciaire : à chaque
  avancement d'échelon, l'IEP augmente automatiquement via `[IE]*[VPI]`.
  C'est une rétribution **liée à la progression de carrière**.
- `IEP_CONT` est une **prime d'ancienneté composite** : on cumule
  l'ancienneté de service public (taux 1,4 % / an) et l'ancienneté de
  service privé (taux 0,7 % / an), plafonnée à 60 %, puis appliquée
  au Traitement de Base.

## Formule fonctionnaire (IEP_FONC)

```
IEP_FONC = IE × VPI
```

- `IE`  : indice d'échelon (entier ≥ 0)
- `VPI` : valeur du point indiciaire (décimale, historisée par date)

La formule est stockée à l'identique dans `T_Formules_Calcul`
(Migration_010_TypeCalcul, expression `[IE]*[VPI]`).

L'arrondi final est appliqué par le moteur (`Math.Round(..., 2, AwayFromZero)`)
exactement comme pour toutes les autres rubriques évaluées par
`EvaluerExpression`.

## Formule contractuel (IEP_CONT)

Logique d'origine (Migration_035_IEP_Parametrique) :

```
SI EST_FONCTIONNAIRE = 1
    => IEP_CONT = 0
SINON
    tauxAnciennete = ANC_PUB × IEP_TAUX_PUBLIC_PCT
                   + ANC_PRIV × IEP_TAUX_PRIVE_PCT
    SI tauxAnciennete > IEP_PLAFOND_PCT
        => tauxAnciennete = IEP_PLAFOND_PCT
    => IEP_CONT = TB × tauxAnciennete / 100
```

Valeurs par défaut des paramètres réglementaires (clés
`T_Parametres_Globaux`) :

| Clé                       | Valeur par défaut | Signification                                  |
|---------------------------|-------------------|------------------------------------------------|
| `IEP_TAUX_PUBLIC_PCT`     | `1,4`             | % d'IEP par année d'ancienneté publique         |
| `IEP_TAUX_PRIVE_PCT`      | `0,7`             | % d'IEP par année d'ancienneté privée           |
| `IEP_PLAFOND_PCT`         | `60`              | Plafond cumulé du taux composite (% du TB)      |

## Explication du plafond 60 %

Le plafond `IEP_PLAFOND_PCT = 60` est un **plafond appliqué au taux
composite**, pas un plafond d'années de service. Lorsque la somme
`ANC_PUB × 1,4 + ANC_PRIV × 0,7` dépasse 60 %, le taux retenu est
exactement 60 % et le montant devient `TB × 0,6`.

Exemples :

| ANC_PUB | ANC_PRIV | Taux brut       | Taux retenu | TB = 30 000 → IEP_CONT |
|---------|----------|-----------------|-------------|--------------------------|
| 10      | 0        | 14 %            | 14 %        | 4 200,00                 |
| 0       | 10       | 7 %             | 7 %         | 2 100,00                 |
| 30      | 30       | 42 % + 21 % = 63 % | 60 %     | 18 000,00                |
| 0       | 0        | 0 %             | 0 %         | 0,00                     |

Le plafond n'est **jamais** interprété comme une limite d'années.

## Formules en base : NON MODIFIÉES

Le pilote IEP introduit deux calculateurs typés
(`IEPFonctionnaireCalculator`, `IEPContractuelCalculator`) qui
**reproduisent à l'identique** la logique des formules SQL stockées dans
`T_Formules_Calcul`.

Sont **strictement préservées** :

- L'expression `[IE]*[VPI]` pour `IEP_FONC` (Migration_010_TypeCalcul).
- L'expression paramétrique `IIF([EST_FONCTIONNAIRE]=1, 0, IIF((...) < [PARAM:IEP_PLAFOND_PCT], ..., [PARAM:IEP_PLAFOND_PCT]) * [TB] / 100)`
  pour `IEP_CONT` (Migration_035_IEP_Parametrique).
- Les clés `IEP_TAUX_PUBLIC_PCT`, `IEP_TAUX_PRIVE_PCT`, `IEP_PLAFOND_PCT`
  dans `T_Parametres_Globaux` (mêmes valeurs, mêmes lectures).
- Les règles d'éligibilité dans `T_Regles_Eligibilite_Indemnite`
  (incluant l'exclusion de `IEP_FONC` pour les contractuels).

Aucune migration n'est ajoutée par ce pilote ; aucune donnée
réglementaire n'est touchée.

## Parité numérique calculateur typé ↔ formule en base

Les calculateurs typés et la formule en base partagent **la même source de
vérité** pour les taux et le plafond :

- Le calculateur lit `snap.IepTauxPublicPct`, `snap.IepTauxPrivePct`,
  `snap.IepPlafondPct` depuis `ParametresPaieSnapshot`.
- `EvaluerExpression` résout les tokens `[PARAM:IEP_TAUX_PUBLIC_PCT]`,
  `[PARAM:IEP_TAUX_PRIVE_PCT]`, `[PARAM:IEP_PLAFOND_PCT]` via
  `T_Parametres_Globaux` (mêmes clés).

Les deux chemins convergent donc automatiquement à toute modification
admin. La suite `IEPCalculatorTests` (15 tests) vérifie cette parité au
centime sur plusieurs jeux de données ainsi que l'invariance des
formules en base.

## Périmètre du pilote

- **Couvert** : `IEP_FONC`, `IEP_CONT` uniquement.
- **Non couvert (fallback `EvaluerExpression` inchangé)** : toutes les
  autres rubriques (IRG, SS, Allocations familiales, IQualif, PAPP, PAPG,
  INUIS, IDP, IGFM, DIR, IST, ISTC, ISSRP_15/30/45 ↑ couvertes par un
  pilote dédié, etc.).

## Éligibilité ID-only et anomalie `ID_TypeContrat` (chantier IEP_FONC)

L'éligibilité de `IEP_FONC` est portée par `T_Regles_Eligibilite_Indemnite` :

| ID | Type_Contrat | ID_TypeContrat | Type_Regle |
|----|--------------|----------------|------------|
| 122 | Fonctionnaire | 2 | INCLUSION |
| 123 | TITULAIRE | 4 | INCLUSION |
| 124 | STAGIAIRE | 3 | INCLUSION |
| 125 | CONTRACTUEL | 1 | EXCLUSION |
| 126 | VACATAIRE | 5 | EXCLUSION |

Depuis **ARCH-7D**, le matching `CritereTypeContratCorrespond`
(`DALEligibiliteIndemnites`) est **ID-only strict** : une règle dont
`ID_TypeContrat` est renseigné n'est satisfaite que si l'employé porte le
**même** `ID_TypeContrat`. Le fallback sur le texte `Type_Contrat` a été
supprimé.

**Conséquence (anomalie diagnostiquée)** : un fonctionnaire dont
`T_Employes.ID_TypeContrat` est `NULL` (le formulaire Employés écrit le
texte mais pas l'ID — cf. Migration_121) se voit refuser toutes les
rubriques pilotées par règle, dont `IEP_FONC`, alors que la formule, la
cible `FONCTIONNAIRE` et la règle textuelle sont correctes. Le diagnostic
moteur est alors `REGLE_HORS_DATE` (« règles présentes mais aucune ne
correspond au contexte »).

**Correction** : [Migration_124](../PaieEducation/Migrations/Migration_124_RattrapageIdTypeContrat_PostImport.vb)
rejoue, de façon idempotente et non destructive, le backfill de
`ID_TypeContrat` depuis `Type_Contrat` (jointure texte exacte sur
`T_Types_Contrat`). Aucune autre rubrique, table ou code n'est modifié.

## Test réel validé (employé EMP-001 / ID 12562)

Test réel exécuté via `MoteurCalculPaie.CalculerBulletinComplet` sur la
base réelle du projet, employé **12562** (MEN MED, TITULAIRE, grade 601
Intendant, échelon 8), période **04/2026** :

| Élément | Valeur |
|---------|--------|
| Éligibilité | Oui — règle 123 (TITULAIRE, INCLUSION) |
| Source de vérité | `T_Formules_Calcul` (`[IE]*[VPI]`) + `IEPFonctionnaireCalculator` |
| IE (indice échelon) | 311 (`T_Indices_Echelons` grade 601 / éch. 8, barème ≥ 2025) |
| VPI (valeur du point) | 45,00 (`T_Parametres_Globaux.VALEUR_POINT`) |
| Montant calculé | **13 995,00 DA** (311 × 45,00) |
| Code rubrique final | `IEP_FONC` |
| Ligne bulletin générée | Oui — une seule (aucun doublon) |
| Cotisable | Oui (intégrée à l'assiette SS/IRG) |

Avant correction de `ID_TypeContrat`, IEP_FONC était **absente** du
bulletin ; après correction, elle est présente et conforme. Les tests
d'intégration `IEP17` (fonctionnaire → présente, montant IE×VPI, 1 ligne)
et `IEP18` (contractuel → exclue) verrouillent ce comportement.

## Garde-fou de cohérence paramètres / formule historique

Le calculateur typé IEP_CONT lit les taux depuis les paramètres globaux :
- taux ancienneté publique ;
- taux ancienneté privée ;
- plafond.

À la date du pilote IEP, la formule SQL historique conservée en base contient encore les constantes réglementaires 1.4 / 0.7 / 60.

Un test de garde-fou vérifie donc que les paramètres globaux restent cohérents avec ces constantes.

Si l'administration décide de rendre ces paramètres réellement modifiables, il faudra ouvrir un ticket dédié pour aligner la formule historique ou supprimer la dépendance de parité avec cette formule.

### Implémentation du garde-fou

Le test [`IEP16_ParametresGlobaux_CoherentsAvecFormuleHistorique`](../PaieEducation/Tests/IEPCalculatorTests.vb) :

1. Charge le snapshot via `CacheParametres.Instance.Charger(datePaie)` — exactement le chemin utilisé par `IEPContractuelCalculator`.
2. Vérifie que `snap.IepTauxPublicPct = 1.4D`, `snap.IepTauxPrivePct = 0.7D`, `snap.IepPlafondPct = 60D`.
3. Échoue avec un message explicite listant les clés divergentes et rappelant l'action corrective requise (migration paramétrique sur le modèle de Migration_035, ou suppression de la dépendance de parité).

### Action attendue en cas d'échec du garde-fou

Si `IEP16` casse, **ne pas modifier les valeurs des paramètres globaux pour faire passer le test**. Cela masquerait la divergence sans la corriger. La marche à suivre est :

1. Ouvrir un ticket dédié « Paramétrisation effective IEP_CONT ».
2. Aligner la formule SQL `IEP_CONT` dans `T_Formules_Calcul` (migration dédiée, sur le modèle de [Migration_035_IEP_Parametrique](../PaieEducation/Migrations/Migration_035_IEP_Parametrique.vb), avec résolution stable des trois tokens `[PARAM:IEP_*]`).
3. Ou alternativement : retirer du test de parité (IEP12) la dépendance à la formule SQL et marquer la formule historique comme purement documentaire.
4. Mettre à jour ce document avec la décision prise.
