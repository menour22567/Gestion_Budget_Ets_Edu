# Suppression des doublons PRP / IAPG

## Décision métier

PRP est supprimée car elle duplique PAPP.
IAPG est supprimée car elle duplique PAPG.

Décision actée (non révisable dans ce ticket).

## Prime pédagogique conservée

**PAPP** — Prime d'Amélioration des Performances Pédagogiques (taux dynamique
0–40 % selon notation, typée — voir [PAPP_Regles_Metier.md](PAPP_Regles_Metier.md)).

PRP était son prédécesseur historique (la formule PAPP avait été créée en copiant
celle de PRP, Migration_007) : c'est bien un doublon fonctionnel.

## Prime de gestion conservée

**PAPG** — Prime d'Amélioration des Performances de Gestion (`[TP]*0.40`, personnels
d'intendance).

IAPG n'était matérialisée nulle part (ni formule, ni règle, ni donnée) : un code
« zombie » référencé uniquement dans des listes/familles. Doublon de PAPG.

## Principe projet

Le projet n'est pas encore en production.
Aucune conservation historique n'est requise.
Les anciennes lignes PRP/IAPG sont supprimées définitivement (référentiel,
données dérivées, lignes de bulletins, dépendances, activations GEP).

## Effet sur le calcul futur

PRP et IAPG ne sont plus :
- proposées (retirées du combo `frmReglesPrimeRendement`) ;
- calculées (plus de formule dans `T_Formules_Calcul` ; retirées des familles
  RENDEMENT/IRG_10 et des listes fallback du moteur) ;
- affichées (plus de ligne possible ; retirées du détecteur IRG-10 de
  `BulletinAffichageBLL`) ;
- incluses dans les familles actives.

## Mise en œuvre

- **Migration_118_Suppression_Doublons_Rendement_PRP_IAPG** : migration idempotente
  qui supprime PRP et IAPG (filtre exact, insensible casse/espaces) de toutes les
  tables où ils apparaissent :
  `T_Formules_Calcul`, `T_Regles_Eligibilite_Indemnite`,
  `T_Historique_Regles_Eligibilite`, `T_Formules_Famille`, `T_Regles_Prime_Rendement`,
  `T_Baremes_Indemnitaires`, `T_Dependances_Elements` (source + dépendance),
  `T_Grade_ElementsPaie`, `T_Lignes_Bulletin`, `T_Primes_Variables`,
  `T_Rappels_Regularisations`.
  Garde-fou intégré : vérifie que PAPP et PAPG subsistent après nettoyage.
  S'exécute après M111 (qui re-seede les familles), donc le nettoyage est
  réappliqué à chaque bootstrap — idempotent et durable.
- **Code VB nettoyé** : `FamillesRubriquesService` (listes attendues),
  `ModCalculPaie` (FALLBACK_RUBRIQUES_RENDEMENT / IRG_10), `BulletinAffichageBLL`
  (détecteur IRG-10), `frmReglesPrimeRendement` (combo), commentaires
  `RubriqueCalculatorRegistry`.
- **Tests d'invariant** : suite `DoublonsRendementTests` (DUBL01–DUBL10) +
  mise à jour des tests existants qui présupposaient PRP/IAPG actifs.

## Hors périmètre (volontairement non traité ici)

- correction PAPG notation (PAPG reste `[TP]*0.40` statique) ;
- correction PAP_PM 0.30 / 0.35 (bascule 2024/2025) ;
- reclassement R005 (conservée comme retenue IRG 10 %) ;
- typage RND ;
- modification de PAPP (déjà typée et scellée).

Ces points relèvent de tickets séparés.
