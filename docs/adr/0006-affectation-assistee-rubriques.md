# ADR-0006 — Affectation assistée des rubriques : extension du socle d'éligibilité, un seul évaluateur

**Statut :** Accepté — J3 (14/07/2026). Promu de « Proposé » à « Accepté » le
15/07/2026, simultanément à l'ADR-0007 (cf. son « Action immédiate ») : J3H
(lot 1 du prompt `docs/prompts/PROMPT_MOTEUR_AFFECTATION_RUBRIQUES.md`) et les
questions Q-J3H-1…4 ont été validés par l'utilisateur les 14-15/07/2026.

## Contexte

Le besoin « affectation intelligente des rubriques » (suggestions à la création d'agent,
liberté utilisateur, avertissements non bloquants historisés, explicabilité) pouvait être
implémenté comme un moteur de règles séparé (tables `REGLE_AFFECTATION`/`CONDITION_REGLE`/
`ACTION_REGLE` du prompt initial). Le projet possède déjà `ReglesEligibilite`,
`RubriqueBaremes`, `GradeAttributs` (V008) et un `EligibilityEngine` planifié (Phase 4) :
deux systèmes de règles parallèles divergeraient inévitablement — inacceptable pour
l'audit réglementaire. Par ailleurs le modèle V008 combine les conditions en ET plat
(RM-040), insuffisant pour l'ISSRP 45 % (« grades directs OU grades conditionnels ET
origine enseignante », RM-044/J3G).

## Décision

1. **Pas de second moteur.** L'affectation assistée est une **extension du socle
   d'éligibilité** : dictionnaire `CriteresEligibilite` (remplace le CHECK en dur),
   en-têtes `GroupesEligibilite` (sévérité D2, message, priorité), `MessagesRegles`,
   flags `EstAffectableManuellement` (D1) et `OccurrencesMultiples` (D4) sur `Rubriques`.
2. **Sémantique en forme normale disjonctive** : conditions d'un groupe ETées, groupes
   OUés, conditions sans groupe = communes ; les règles V008 existantes restent valides
   sans réécriture.
3. **Un seul évaluateur** de règles (pur et synchrone, ADR-0005) sert le calcul
   (`EligibilityEngine`) et les suggestions/avertissements (`SuggestionEngine`), avec
   explicabilité native (conditions satisfaites + source réglementaire).
4. **L'affectation est de la gestion** (ADR-0004, PK GUID, Phase 5) : `AgentAttributs`
   (critères D3), `AgentRubriques` (statuts SUGGEREE/ACCEPTEE/SUPPRIMEE/SUSPENDUE,
   origine traçant la version de règle), `AvertissementsHistorique` (append-only).
5. **Précédence** : rubriques systémiques → l'éligibilité décide (pipeline exclusif) ;
   rubriques affectables → `AgentRubriques` décide, l'éligibilité ne produit que
   suggestions et avertissements. Une inéligibilité constatée avertit, ne retire jamais.

## Justification

- Une seule source de vérité « qui a droit à quoi » : le calcul et l'écran ne peuvent pas
  se contredire (aucune rubrique à la fois supprimée à l'écran et payée).
- Zéro migration pour étendre : nouveau critère = ligne de dictionnaire + attributs ;
  nouvelle règle/message/sévérité = données (principe cardinal du PLAN_ACTION).
- Les groupes DNF sont le minimum qui couvre tous les cas réglementaires recensés
  (J3B/J3G) — l'imbrication booléenne générale est explicitement hors périmètre V1.
- La liberté utilisateur (le logiciel conseille, l'utilisateur décide) est bornée par la
  seule sécurité indispensable : COTISATION/IMPOT/assiettes/net restent du ressort
  exclusif du pipeline (D1).

## Conséquences

- La migration V009 (référentiel) n'est écrite qu'après validation de J3H ; les tables de
  gestion arrivent avec `Agents` en Phase 5.
- Le seed J3G (matrice ISSRP) attend V009 : les 7 lignes conditionnelles `ORIGINE_CORPS`
  nécessitent les groupes.
- L'évaluateur (lot 2) doit traiter « critère non résolu » comme condition non satisfaite
  plus avertissement INFO — jamais une exception.
- Tout nouvel opérateur ou type d'action reste un ajout de **code** (Open/Closed) — la
  documentation ne prétend pas le contraire.
