# ADR-0008 — Immutabilité des périodes clôturées : toute correction rétroactive passe par un rappel, jamais par un recalcul

**Statut :** Accepté — 15/07/2026 (Q5, module d'affectation assistée). Validé par
l'utilisateur sur la base de `docs/analysis/J4E_DOSSIER_CONCEPTION_AFFECTATION.md` § 8.

## Contexte

ADR-0007 (Workbench réglementaire, D9) avait déjà tranché que l'évolution d'une **règle
réglementaire** rétroactive ne modifie jamais un bulletin validé : elle déclenche la
création de lignes de rappel sur la période ouverte. Cette décision couvrait un seul
déclencheur — le changement de version d'une règle.

Le module d'affectation assistée (`docs/prompts/PROMPT_MOTEUR_AFFECTATION_RUBRIQUES.md`)
introduit trois **autres** sources de changement rétroactif que RM-102/RM-103 et ADR-0007
D9 ne couvraient pas explicitement : une **affectation manuelle** modifiée à effet passé
(ex. l'utilisateur retire une rubrique et rétrodate le retrait), un **attribut d'agent**
corrigé après coup (ex. `ORIGINE_STATUTAIRE` mise à jour suite à une décision de
promotion tardive), ou un **barème** corrigé (ex. une erreur de saisie dans
`RubriqueBaremes`). Question posée à l'utilisateur (Q5, 15/07/2026) : ces trois cas
suivent-ils la même règle que D9, ou une politique différente ?

**Décision de l'utilisateur (15/07/2026) :** « Je confirme que les périodes clôturées ne
devront jamais être recalculées. Toute correction rétroactive devra être matérialisée par
un rappel sur une période ultérieure. Cette règle devra devenir un **principe général du
moteur de paie**. » — la règle de D9 est donc généralisée à toute source de changement
rétroactif, pas seulement à l'évolution des règles réglementaires, et élevée au rang de
principe d'architecture plutôt que de rester une décision locale au Workbench.

## Décision

1. **Une période de paie clôturée n'est jamais recalculée**, quelle que soit la source du
   changement rétroactif : évolution de règle réglementaire (ADR-0007 D9), modification
   d'affectation (`AgentRubriques`), correction d'attribut d'agent (`AgentAttributs`), ou
   correction de barème (`RubriqueBaremes`).
2. **Le rappel est l'unique mécanisme de correction rétroactive.** Il est **généré par le
   moteur** (générateur de rappels, J4.d) — jamais affecté ou saisi manuellement par
   l'utilisateur, cohérent avec D4 (occurrences multiples réservées aux rappels et aux
   retenues optionnelles à montant fixe).
3. **Le montant du rappel = snapshot d'époque − recalcul à droit constant de la période
   de référence.** Le bulletin d'origine est figé par le SnapshotEngine (J4.d) au moment
   de sa validation ; le rappel se calcule contre ce snapshot, jamais contre une
   réévaluation du passé — cohérent avec RM-105 (journal d'explication + snapshot
   reproductible).
4. **L'audit rejoue le raisonnement d'époque**, pas l'état courant : `Origine =
   GROUPE:<Id>@<DateEffet>` (J3H) fige la version de règle qui a produit chaque
   affectation ; le snapshot fige les montants. Un rappel ne réécrit ni l'un ni l'autre.
5. La notion de « **période clôturée** » est un statut à définir formellement avec le
   modèle de périodes de paie (Phase 5, jalon D de J4E) — cette ADR pose l'invariant,
   pas encore le modèle de données du statut lui-même.

## Justification

- **Cohérence avec l'existant, pas une nouvelle doctrine** : RM-102 (rétroactivité ≠ date
  de publication, génère des rappels) et RM-103 (non-régression : jamais de modification
  rétroactive d'un bulletin validé) posaient déjà ce principe pour les règles
  réglementaires. Cette ADR ne l'invente pas, elle en retire la portée limitée à une seule
  source de changement.
- **Traçabilité comptable** : un bulletin validé qui pourrait être recalculé silencieusement
  romprait la piste d'audit (montant historique différent d'un mois sur l'autre sans trace
  explicite). Le rappel est une ligne additionnelle, visible, datée, expliquée — jamais
  une correction invisible d'un montant déjà versé.
- **Symétrie entre les quatre sources** : distinguer « une évolution de règle donne un
  rappel » de « une correction d'affectation donne un recalcul » créerait deux régimes de
  correction incohérents dans la même application, source de confusion pour l'utilisateur
  et de bugs pour les développeurs futurs qui devraient se souvenir laquelle des quatre
  situations est concernée.
- **Le générateur de rappels reste le point de contrôle unique** : centraliser la
  correction rétroactive dans un seul mécanisme (plutôt que quatre chemins de recalcul
  différents) réduit la surface de test et le risque de divergence entre eux.

## Alternatives considérées

- **Recalcul immédiat des bulletins impactés** : rejeté — viole RM-103 explicitement,
  romprait la traçabilité comptable, et un recalcul en cascade sur N bulletins déjà
  transmis (imprimés, déclarés) n'a pas de sens opérationnel dans un ERP de paie.
- **Régime différencié par source** (rappel pour les règles réglementaires, recalcul
  direct pour les corrections d'affectation/attribut/barème, jugées « moins graves ») :
  rejeté par l'utilisateur — la distinction n'a pas de fondement métier : un montant déjà
  versé à tort reste un montant déjà versé à tort, quelle que soit la cause de l'erreur.
- **Laisser la politique ouverte jusqu'à Phase 5** : rejeté — le principe engage dès
  maintenant la conception des use cases du lot 3 (J4E § 10) qui écrivent
  `AgentAttributs`/`AgentRubriques` ; le trancher tôt évite de concevoir des use cases
  qu'il faudrait ensuite réécrire.

## Conséquences

- Le **générateur de rappels** (J4.d, non fait) devient une dépendance bloquante pour
  toute écriture rétroactive dans `AgentAttributs`/`AgentRubriques`/`RubriqueBaremes` —
  pas seulement pour l'évolution des règles réglementaires comme le prévoyait ADR-0007.
- Le **SnapshotEngine** (J4.d, non fait) doit exister avant que le lot 3 (use cases
  d'affectation, Phase 5) puisse gérer un changement à effet passé — un snapshot absent
  bloque le calcul du rappel, pas le calcul courant.
- Le **modèle de périodes de paie** (jalon D, J4E) doit porter un statut explicite
  « clôturée » — actuellement absent du schéma. À concevoir en Phase 5.
- `docs/analysis/J3B_CATALOGUE_REGLES_METIER.md` RM-102/RM-103 restent valides et sont
  désormais l'expression, pour le domaine réglementaire, du principe général posé ici ;
  aucune contradiction, cette ADR généralise sans modifier leur contenu.
- Le module d'affectation assistée (`AgentRubriques`) ne doit jamais autoriser une
  modification directe d'une ligne dont la période est clôturée — invariant à couvrir par
  un test dédié au lot 3.

## Action immédiate

Aucune action de code immédiate : cette ADR pose l'invariant qui contraint la conception
du lot 3 (Phase 5) et du générateur de rappels (J4.d). Le modèle de périodes de paie
(jalon D de J4E) doit intégrer le statut « clôturée » dès sa conception.
