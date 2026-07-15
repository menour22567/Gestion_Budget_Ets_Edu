# ADR-0007 — Workbench réglementaire : toute rubrique, tout paramètre, toute règle d'éligibilité est saisissable et historisable par l'utilisateur

**Statut :** Accepté — J3 (14/07/2026). Validé par l'utilisateur sur la base de
`docs/analysis/J3I_WORKBENCH_REGLEMENTAIRE.md` (décisions D5–D11).

## Contexte

Le projet a posé dès l'origine le principe « zéro hardcoding » (ADR-0001, PLAN_ACTION §A) :
toute valeur réglementaire vit en base, versionnée par date d'effet, et l'utilisateur peut
faire évoluer le système sans recompilation. Le socle V001→V008 + extensions J3E/J3H
couvre déjà 13 des 14 patterns de calcul identifiés dans le dossier `Reglementation/`.

**Mais** une directive utilisateur explicite, posée le 14/07/2026, élargit la portée :

> *« Toutes les rubriques doivent être paramétrables par l'utilisateur à travers des
> interfaces utilisateurs pour qu'en cas où le régime indemnitaire change, l'utilisateur
> peut appliquer les modifications qu'il désire avec son historisation. Donc tu dois adapter
> l'application à toutes les possibilités de calcul des rubriques de paie en se basant sur
> la réglementation qui se trouve dans le dossier du projet sous `Règlementation`. »*

Le constat est sans appel : sans surface utilisateur dédiée, le « zéro hardcoding » reste
un principe de couche données sans traduction concrète pour l'administrateur fonctionnel,
qui doit encore passer par SQL ou par le seed pour adapter la base à un nouveau décret.
Le Workbench réglementaire est la **surface utilisateur** qui rend le principe
opérationnel de bout en bout — règle, paramètre, condition, barème, profil, périodicité.

Le dossier `Reglementation/` (secteur EN, corps communs, ouvriers, contractuels,
paramédicaux, IRG, IFC) impose par ailleurs la couverture de patterns non triviaux :
forfaits indexés par catégorie/grade/type d'établissement (P4, P5, P6), 3 taux par groupe
de corps (P7), condition « en exercice effectif » (P8), taux indexé sur une source
externe comme la notation (P3), IRG avec lissages par période (P11). Le modèle V008 en
ET plat (RM-040) est insuffisant pour P6 et P7, et il n'existe pas de mécanisme générique
pour P3.

## Décision

1. **Workbench réglementaire = surface UI + workflow**, pas un second modèle de données.
   Il s'appuie sur V001→V008 et lui ajoute uniquement les quelques tables strictement
   nécessaires pour boucher les trois trous avérés (V009 — voir §3). Tout le reste de la
   donnée est déjà exprimable.
2. **Adoption de `GroupesEligibilite` (DNF) — abandon de la limite V1 « pas de conditions
   composées »** (D5). Les conditions d'un groupe sont ETées, les groupes sont OUés ; les
   conditions sans groupe restent communes (comportement V008 inchangé). Reprend la
   proposition J3H §2.
3. **Introduction de `SourcesValeur` (D6)** — catalogue extensible des sources
   (`NOTATION_AGENT`, `ANCIENNETE_PUBLIQUE`, `ANCIENNETE_PRIVEE`, `INDICE_ECHELON`,
   `POINT_INDICIAIRE`, `BASE_ASSIETTE`, `CONSTANTE_REGLEMENTAIRE`, …) qui remplace
   les calculateurs typés par rubrique pour le pattern P3. Une nouvelle source = une
   ligne du catalogue + un calculateur enregistré via DI (Open/Closed).
4. **Architecture d'écrans spécialisés par pattern** (D7) — une arborescence
   (`Rubriques / Cotisations / Fiscalité / Carrière & grilles / Simulation / Évolution /
   Audit / Matrice`), un écran-type par pattern, plutôt qu'un écran fourre-tout. Le
   pattern est détecté par lecture de `Rubriques.SourceValeurCode` + présence de
   `RubriqueBaremes` + `ReglesEligibilite`.
5. **Dry-run obligatoire avant commit** (D8) — toute modification réglementaire passe
   par un assistant (§7 de J3I) qui produit un rapport d'impact (agents × montant ×
   période) avant que la modification soit commitée. Aucun commit sans dry-run, sauf
   bypass admin documenté dans `AuditLog`.
6. **Rétroactif = nouvelle version + génération de rappels** (D9) — les bulletins
   validés ne sont jamais modifiés. Une version dont la `DateEffet` est antérieure à
   aujourd'hui déclenche la création de lignes de **rappels** (lignes additionnelles)
   pour la période rétroactive, via le moteur de rappels (Phase 4, RM-103). La traçabilité
   comptable est préservée.
7. **Migration unique V009** (D10) — regroupe L-M1 (`SourcesValeur` + colonne
   `Rubriques.SourceValeurCode`), L-M2 (`GroupesEligibilite` + colonne
   `ReglesEligibilite.GroupeId`), L-M3 (colonnes d'audit `CreatedBy`/`UpdatedBy`/
   `UpdatedAt` sur `RubriqueBaremes`), et le socle J3H (`CriteresEligibilite`,
   `MessagesRegles`, et les préparatifs de `AgentAttributs`/`AgentRubriques`/
   `AvertissementsHistorique`). Une seule migration, un seul test d'upgrade V008→V009.
8. **Matrice de couverture en V1** (D11) — vue tabulaire `corps × rubriques` avec
   code couleur (vert = règle active, orange = inactive, rouge = pas de règle, gris =
   non applicable). Sert d'outil de validation admin et de support d'audit.

## Justification

- **Un seul modèle, deux usages** : le calcul de paie et le Workbench lisent la même
  donnée. Aucune divergence possible entre ce que l'admin saisit et ce que le moteur
  calcule. Le calcul n'est pas un consommateur spécial, c'est un client comme un autre.
- **Zéro migration pour les évolutions courantes** : un nouveau décret = duplication
  d'une version existante + modification des paramètres + clôture de l'ancienne — pas
  de touche au code, pas de migration, pas de recompilation. C'est la traduction
  opérationnelle stricte du principe cardinal.
- **DNF plutôt qu'imbrication booléenne générale** : le OU suffit pour tous les cas
  recensés (P6, P7) ; l'imbrication complète ajouterait de la complexité d'UI et de
  test sans bénéfice réglementaire recensé. Si un cas non couvert émerge plus tard,
  l'extension est locale (J3E-RM-040 borne le périmètre).
- **`SourcesValeur` plutôt que calculateur typé par rubrique** : la notation n'est
  qu'une source parmi d'autres (l'ancienneté publique, l'indice d'échelon, etc.). Un
  catalogue extensible permet d'ajouter de nouvelles sources sans toucher au moteur de
  calcul — un nouveau taux indexé sur une donnée d'agent = une ligne de catalogue, pas
  une nouvelle classe VB.
- **Dry-run = sécurité fonctionnelle, pas technique** : un administrateur qui modifie
  un taux impactant 1 240 agents doit voir l'ampleur du changement avant de valider.
  C'est un garde-fou métier, pas un confort. Le moteur de calcul reste déterministe
  (ADR-0005) ; le dry-run est une lecture, pas une mutation.
- **Rétroactif en rappels, pas en ré-écriture** : un bulletin validé est immuable
  (Tome F vol. 22 §2, RM-103). Toute évolution réglementaire rétroactive s'exprime
  par une ligne additionnelle, pas par une ré-écriture. La comptabilité est préservée,
  l'audit est linéaire.
- **V009 unifiée** : la migration est un point de bascule technique, pas un catalogue
  de petites migrations. Un seul test d'upgrade, une seule fenêtre de validation, un
  seul commit. C'est aussi une garantie de cohérence : les colonnes ajoutées ensemble
  sont cohérentes entre elles.
- **Matrice de couverture = assurance qualité** : un projet de paie qui se trompe
  sur la couverture d'un corps (ex. un labo oublié dans l'ISSRP) paie des gens à tort
  ou à tort. La matrice est un outil de validation continue, pas un rapport annuel.

## Alternatives considérées

- **Moteur de règles séparé (`REGLE_AFFECTATION` / `CONDITION_REGLE` / `ACTION_REGLE`)** :
  rejeté — diverge de l'ADR-0006 et de l'éligibilité existante, double la complexité
  d'audit. Voir ADR-0006 §Contexte.
- **Imbrication booléenne complète** : rejeté — non requis par les cas réglementaires
  recensés (J3B), complexité d'UI disproportionnée. La DNF suffit.
- **Calculateur typé par rubrique pour P3** : rejeté — c'est l'existant (PAPP a son
  `PAPPCalculator`). Ne passe pas à l'échelle : pour ajouter un taux indexé sur
  l'ancienneté, il faudrait un nouveau calculateur. Le catalogue `SourcesValeur` est
  plus extensible.
- **Migrations V009a, V009b, V009c** : rejeté — risque de désynchronisation entre
  colonnes d'une même migration, fenêtres de validation multiples, plus de tests
  d'upgrade à maintenir.
- **Workbench = écran unique « tout en un »** : rejeté — UI illisible, impossible à
  tester exhaustivement, performances dégradées. L'arborescence spécialisée est
  lisible, testable, et chaque écran reste petit.
- **Pas de dry-run** : rejeté — risque d'erreur d'impact non détectable, pas de
  retour en arrière possible une fois les bulletins validés impactés. Le dry-run est
  une lecture, pas un coût.
- **Rétroactif = ré-écriture des bulletins** : rejeté — viole RM-103, brise la
  traçabilité comptable, complique l'audit.
- **Matrice de couverture post-V1** : rejeté — c'est précisément dans les premières
  phases qu'on en a besoin pour valider le seed. La sortir de V1 reporterait le
  problème et réduirait la qualité du livrable.

## Conséquences

- **V009** devient la prochaine migration de référentiel. Elle est bloquante pour la
  Phase 4 (moteur étendu). Elle est conçue comme un **upgrade** depuis V008, pas une
  ré-installation — toutes les bases V008 existantes migrent.
- **Phase 3bis** est insérée dans le plan (entre Phase 3 — Domaine — et Phase 4 —
  Moteur) pour porter la migration V009 + les tests d'upgrade.
- **Phases 4, 5, 6, 8** sont étendues (cf. `PLAN_ACTION.md` mis à jour) :
  - Phase 4 : `valeurSource()`, évaluation DNF, dry-run rapide
  - Phase 5 : use cases `AppliquerEvolution`, `Simuler`, `DupliquerVersion`,
    `CloreVersion`, `GenererRappels`
  - Phase 6 : Workbench UI complet (arborescence + FormulaEditor + éditeur de barème
    + éditeur de groupes + matrice de couverture + assistant d'évolution)
  - Phase 8 : suite de tests Workbench (C-T1 à C-T6 de J3I §8.3)
- **L'ADR-0006** (Affectation assistée) reste valide et alimente V009 — J3I ne le
  contredit pas, il l'étend avec la notion de « Workbench » et les 3 sources de
  valeur. L'ADR-0006 doit être promu de « Proposé » à « Accepté » simultanément
  (cf. action suivante).
- **`J3F_QUESTIONS_OUVERTES.md`** est mis à jour : Q-04 (DNF), Q-08 (P3 par
  SourcesValeur), Q-09 (périodicité de versement) sont closes par les décisions D5,
  D6, D7 + la table `Rubriques.PeriodiciteVersement` déjà en V008.
- **`J3B_CATALOGUE_REGLES_METIER.md`** reçoit un index croisé vers le pattern et
  l'écran Workbench associé (cf. J3I §13.2).
- **Aucun lot de l'ADR-0006 n'est supprimé** ; le Workbench est une extension, pas
  une réécriture.

## Action immédiate

Promouvoir l'ADR-0006 au statut « Accepté » simultanément à la présente décision, et
lancer la migration V009 en Phase 3bis après validation finale de J3I.
