# Prompt révisé — Module d'affectation assistée des rubriques de paie

> **Statut :** v1.0 — 14/07/2026. Révision du prompt initial « moteur d'affectation intelligente »,
> ancrée dans l'existant du projet (V008, ADR-0004/0005, plan de phases).
> **Usage :** ne pas exécuter d'un bloc. Chaque lot (§ E) est un prompt autonome, à lancer au
> jalon indiqué, après validation des décisions préalables (§ D).

---

## A. Contexte — ce qui existe déjà et que tu dois réutiliser

Tu interviens sur **PaieEducation**, ERP de paie de l'Éducation nationale algérienne :
.NET 10, WPF/MVVM, SQLite + Dapper, Clean Architecture/DDD (ADR-0001). Le principe cardinal
du projet est le **zéro hardcoding** : toute règle ou valeur réglementaire vit en base,
versionnée par `DateEffet`/`DateFin`, avec `Source` (référence du texte réglementaire) et
`Hash` d'audit sur chaque ligne.

Le socle de règles existe déjà — **il est interdit d'en créer un second en parallèle** :

| Existant | Rôle | Où |
| --- | --- | --- |
| `Rubriques` | Catalogue (Nature GAIN/RETENUE/COTISATION/IMPOT, flags, périodicités) | V008 |
| `ReglesEligibilite` | Critère + opérateur + valeur, versionnées — « qui a droit à quoi » | V008 |
| `RubriqueBaremes` | Valeurs par tranche de critère (taux/montants conditionnels) | V008 |
| `GradeAttributs` | Attributs versionnés de grade (ex. `ORIGINE_ENSEIGNANTE_POSSIBLE`) | V008 |
| `EligibilityEngine` | Évaluateur des règles au moment du calcul | Phase 4 (planifié) |
| `ExplainabilityEngine` | Justification de chaque montant calculé | Phase 4 (planifié) |

Conventions non négociables :

- **ADR-0004** : tables de *référentiel* → PK = code métier TEXT ; tables de *gestion*
  (agents, affectations, historiques) → PK = GUID.
- **ADR-0005** : le cœur d'évaluation est **synchrone et pur** — il reçoit un contexte
  immuable complet et retourne un résultat déterministe ; toute I/O vit en couche Application.
- **Q10** : périmètre V1 = corps pilote enseignants. **Q12** : mode autonome sans
  authentification en V1 (le champ « utilisateur » de l'historisation reste prévu mais peut
  être un libellé de poste).
- Méthode **STOP & ASK** : toute règle métier douteuse déclenche une question, jamais une
  hypothèse silencieuse.

## B. Objectif

Étendre le système d'éligibilité existant en un **module d'affectation assistée** des
rubriques par agent :

1. lors de la création/modification d'un agent, le moteur évalue les règles et **propose**
   les rubriques applicables (pré-affectées en statut « suggérée ») ;
2. l'utilisateur garde la **liberté totale** sur les rubriques librement affectables
   (voir § C.2) : accepter, supprimer, ajouter, suspendre, réactiver, dupliquer quand
   c'est autorisé, borner dans le temps ;
3. un moteur d'**avertissements non bloquants** signale les écarts aux règles paramétrées
   (« une rubrique habituellement obligatoire pour ce grade semble absente ») ;
4. chaque suggestion et chaque avertissement est **explicable** (« Pourquoi cette
   rubrique ? » → conditions satisfaites + texte réglementaire source) et **historisé**
   avec la décision de l'utilisateur, à des fins d'audit.

Un seul évaluateur de règles sert à la fois le calcul de paie (`EligibilityEngine`) et les
suggestions d'affectation — jamais deux implémentations.

## C. Principes

### C.1 Le logiciel conseille, l'utilisateur décide

Aucune suggestion n'est imposée ; aucun avertissement ne bloque. Le système conserve
uniquement la trace des avertissements affichés et des décisions prises.

### C.2 Périmètre de la libre affectation

La liberté totale porte sur les rubriques de nature **GAIN** et sur les **retenues
optionnelles** (Q3b révisée le 14/07/2026 — Q3b-rev), qui existent sous deux formes :
**montant fixe** au choix de l'agent (œuvres sociales) ou **taux versionné appliqué à une
assiette** — ex. mutuelle **MUNATEC** du secteur de l'éducation nationale : 1 % du salaire
soumis aux cotisations (BaseCalcul `ASSIETTE_COTISABLE`, taux dans `RubriqueParametres`
versionné `DateEffet`/`DateFin`, modifiable dans le temps sans code).
Les rubriques **systémiques** — cotisations obligatoires, IRG, assiettes, net — sont
calculées exclusivement par le pipeline de paie et ne sont **ni affectables ni supprimables
manuellement**. La liste exacte des natures/rubriques exclues est une donnée de
paramétrage (flag `EstAffectableManuellement` sur `Rubriques`), pas une constante du code.

### C.3 Sévérités informatives, jamais bloquantes

Trois niveaux, portés par la règle : `INFO`, `RECOMMANDEE`, `OBLIGATOIRE_REGLEMENTAIRE`.
La sévérité ne change que la présentation (icône, couleur, message) et le poids dans
l'historisation — jamais le comportement (même `OBLIGATOIRE_REGLEMENTAIRE` reste
supprimable, avec avertissement historisé).

### C.4 Zéro hardcoding — formulation honnête

Interdits absolus dans le code : `if (wilaya == "Tamanrasset")`, `if (corps == ENSEIGNANT)`,
tout nom de rubrique, taux, seuil ou critère réglementaire. Peuvent être ajoutés **sans
toucher au code** : règles, conditions, rubriques, messages, valeurs de critères, nouveaux
critères (via le dictionnaire § E.1). Nécessitent du **code** (Open/Closed — nouvelle
implémentation enregistrée, sans modifier l'existant) : un nouvel **opérateur** ou un
nouveau **type d'action**, car ils portent une sémantique d'exécution. La documentation
ne doit pas prétendre le contraire.

### C.5 Sémantique des règles V1 — délibérément simple

Une règle = conjonction (**ET**) de conditions plates ; le **OU** s'exprime par plusieurs
règles produisant la même action. Opérateurs V1 : `=`, `<>`, `IN`, `NOT_IN`, `>`, `>=`,
`<`, `<=`, `BETWEEN`. **Pas** de groupes imbriqués, de `LIKE`, ni d'`EXISTS` en V1 : aucun
cas réglementaire recensé (J3B/J3G) n'en a besoin. L'architecture doit permettre de les
ajouter plus tard (Open/Closed), pas de les livrer maintenant.

### C.6 Temporalité

Toute règle, condition et affectation est bornée par `DateEffet`/`DateFin`. Une rubrique
temporaire (ex. prime exceptionnelle 01/01/2027 → 31/12/2027) n'est suggérée que pendant
sa validité ; une affectation dont la règle source a expiré passe à l'écran en état
« à vérifier ». L'historique conserve la **version** de la règle déclencheuse, pour que
les rappels rétroactifs (Q7) et les audits rejouent le raisonnement d'époque.

## D. Décisions préalables — **actées le 14/07/2026** (validation utilisateur)

- **D1 — Périmètre d'exclusion. ✅ Validé.** Non affectables manuellement : Nature ∈
  {COTISATION, IMPOT} + rubriques d'assiette et de net (pipeline exclusif). Les RETENUE
  optionnelles Q3b (œuvres sociales, mutuelles) restent librement affectables. Porté par
  le flag `EstAffectableManuellement` sur `Rubriques`.
- **D2 — Sévérités. ✅ Validé.** Trois niveaux § C.3 : `INFO`, `RECOMMANDEE`,
  `OBLIGATOIRE_REGLEMENTAIRE` — présentation et historisation seulement, jamais de blocage.
- **D3 — Critères agent V1. ✅ Tranché** (délégué à l'analyse, 14/07/2026, sur la base du
  dossier `Reglementation/`) :
  - *Dérivés de la carrière, aucun stockage nouveau* : FILIERE, CORPS, GRADE, CATEGORIE,
    ECHELON, ANCIENNETE (publique, Q8), TYPE_CONTRAT, FONCTION.
  - *Résolu via l'affectation* : TYPE_ETABLISSEMENT (attribut de l'établissement
    d'affectation — primaire/collège/lycée ; ind. de direction 15-271, soutien scolaire
    25-55).
  - *Nouveaux `AgentAttributs` V1* (versionnés `DateEffet`/`DateFin`) :
    1. `ORIGINE_STATUTAIRE` — ISSRP 45 % (Q-03, déjà actée) ;
    2. `EXERCICE_EFFECTIF` — condition des ind. de direction et de gestion financière
       (art. 9 bis 1/9 bis 2 D.ex. 15-271 : « en exercice effectif ») ;
    3. `ANCIENNETE_PRIVEE_ANNEES` — ANC_PRIV de la formule IEP_CONT (non dérivable de la
       carrière interne) ;
    4. `PROFIL_IRG` (`STANDARD` | `HANDICAPE` | `RETRAITE_RG`) — lissage spécial IRG
       2020+ (RM-065) et abattement handicapés 2010–2020 (RM-066, Q-11).
  - *Écartés V1* : zone géographique, wilaya, commune, région, secteur d'activité, régime
    de retraite, type de financement, situation familiale, diplôme — **aucune rubrique du
    périmètre réglementaire fourni n'en dépend** (vérifié sur l'ensemble du dossier
    `Reglementation/`). Ajoutables plus tard par une ligne de dictionnaire + attributs,
    sans migration (c'est le test d'extensibilité du modèle).
- **D4 — Occurrences multiples. ✅ Tranché** (analyse du dossier `Reglementation/`,
  14/07/2026) : **aucune rubrique réglementaire du périmètre** (décrets 10-78, 11-373,
  12-403, 15-271, 25-55, 10-134/135/136, 13-188/189/190) **n'admet d'occurrences
  multiples** — chaque indemnité est définie comme un taux/montant unique par agent
  (confirmé par `IEP_Regles_Metier.md` : « une seule ligne, aucun doublon » ; cohérent
  RM-062). `OccurrencesMultiples = 1` uniquement pour :
  1. les **retenues optionnelles à montant fixe** (famille Q3b-rev : œuvres sociales) —
     plusieurs retenues simultanées possibles, chaque occurrence portant libellé, montant
     et bornes de dates propres. Les retenues optionnelles **à taux** (ex. MUNATEC 1 % de
     l'assiette cotisable) restent à occurrence **unique** — une adhésion par organisme,
     chaque organisme étant sa propre rubrique ;
  2. les **rappels/régularisations** (Q7) — une occurrence par (rubrique rappelée ×
     période de référence), générées par le moteur, jamais affectées manuellement.
  Conséquence pour le lot 2 : RM-062 se raffine en « un couple (rubrique, occurrence) ne
  peut être calculé deux fois pour un même bulletin ».

## E. Découpage en lots — un prompt par jalon

### Lot 1 (immédiat, fin de J3) — Conception du modèle, sans code

Livrables : `docs/analysis/J3H_MODELE_AFFECTATION.md` + un ADR. Aucune migration tant que
le document n'est pas validé.

Contenu à concevoir :

1. **Dictionnaire de critères** : table `CriteresEligibilite` (référentiel, PK code métier)
   remplaçant le CHECK en dur de `ReglesEligibilite.Critere` — un nouveau critère devient
   une ligne, pas une migration. Chaque critère déclare sa source de résolution
   (attribut d'agent, attribut de grade, donnée de carrière, donnée calculée type ancienneté).
2. **`AgentAttributs`** (gestion, GUID, versionnée `DateEffet`/`DateFin`) : symétrique de
   `GradeAttributs`, porte les quatre attributs actés en D3 (`ORIGINE_STATUTAIRE`,
   `EXERCICE_EFFECTIF`, `ANCIENNETE_PRIVEE_ANNEES`, `PROFIL_IRG`) sans colonnes en dur.
3. **Extension de `ReglesEligibilite`** : `Severite` (§ C.3), `MessageCode` (message
   paramétrable, table `MessagesRegles`), `Priorite` (ordre d'affichage des suggestions,
   pas ordre d'application — les règles ne s'excluent pas en V1).
4. **`AgentRubriques`** (gestion, GUID) : affectation par agent — rubrique, statut
   (`SUGGEREE`, `ACCEPTEE`, `SUPPRIMEE`, `SUSPENDUE`), origine (`REGLE:<id-version>`,
   `MANUELLE`), `DateEffet`/`DateFin` propres, occurrence, paramètres surchargés
   (montant/coefficient prérempli, modifiable).
5. **`AvertissementsHistorique`** (gestion, GUID, append-only) : date, agent, rubrique,
   règle + version, sévérité, message affiché, décision (`ACCEPTE`, `IGNORE`, `SUPPRIME`).
6. **Règle de précédence calcul ↔ affectation**, à documenter explicitement : pour les
   rubriques affectables, le moteur de calcul consomme `AgentRubriques` (statut
   ACCEPTEE/SUGGEREE non supprimée, dans sa période) ; pour les rubriques systémiques,
   il applique `ReglesEligibilite` directement. Aucune rubrique ne doit pouvoir être à la
   fois « supprimée à l'écran » et « payée par le moteur ».

Critères d'acceptation : chaque table classée référentiel/gestion (ADR-0004) ; tout est
versionné ; le document montre sur **trois cas réels** le cycle complet règle →
suggestion → décision → historique → calcul : (a) ISSRP 45 % conditionnée par
`ORIGINE_STATUTAIRE` ; (b) retenue optionnelle MUNATEC à taux versionné (1 % de
l'assiette cotisable, Q3b-rev) affectée puis supprimée par l'utilisateur ; (c) prime
temporaire bornée `DateEffet`/`DateFin`.

### Lot 2 (Phase 4) — Évaluateur de règles unifié

1. Évaluateur **pur et synchrone** (ADR-0005) sur un contexte agent immuable ; la couche
   Application charge règles et attributs résolus à la date demandée.
2. Le même évaluateur sert `EligibilityEngine` (calcul) et `SuggestionEngine`
   (affectation + avertissements). Interdiction de dupliquer la logique.
3. **Explicabilité native** : le résultat d'évaluation porte, par règle déclenchée, les
   conditions satisfaites (critère, opérateur, valeur attendue, valeur de l'agent) et la
   `Source` réglementaire — consommé tel quel par « Pourquoi cette rubrique ? » et par
   l'`ExplainabilityEngine` du bulletin.
4. Performance : cache en mémoire des règles actives résolues par période (invalidé à
   toute écriture de paramétrage). Pas de compilation d'expressions ni d'évaluation
   incrémentale en V1 — quelques centaines de règles plates s'évaluent en millisecondes ;
   mesurer avant d'optimiser.

Critères d'acceptation : déterminisme (mêmes entrées → mêmes sorties) ; chaque opérateur
V1 testé (nominal + bornes) ; bornes temporelles testées (veille/jour/lendemain de
`DateEffet` et `DateFin`) ; test prouvant que calcul et suggestion donnent le même verdict
d'éligibilité sur le même contexte.

### Lot 3 (Phase 5) — Use cases et historisation

1. Use cases : `SuggererRubriques` (à la création d'agent et sur demande),
   `AffecterRubrique`, `SupprimerRubrique`, `SuspendreRubrique`, `ReactiverRubrique`,
   `AccepterSuggestion` — chacun écrivant `AvertissementsHistorique` quand une règle de
   sévérité ≥ RECOMMANDEE est contredite.
2. Réévaluation des suggestions quand un attribut d'agent change (mutation, changement de
   grade) : nouvelles suggestions proposées, affectations orphelines marquées
   « à vérifier » — jamais supprimées d'office.
3. Transactions atomiques ; `AvertissementsHistorique` strictement append-only.

### Lot 4 (Phase 6) — Interface WPF/MVVM

1. Écran d'affectation : liste des rubriques de l'agent avec badges — `Suggérée`,
   `Réglementaire` (sévérité OBLIGATOIRE_REGLEMENTAIRE), `Personnalisée` (origine
   MANUELLE), `Temporaire` (DateFin renseignée), `À vérifier` (règle expirée/orpheline).
2. Panneau « Pourquoi cette rubrique ? » alimenté par l'explicabilité du lot 2
   (conditions et décret/texte source), sans requête supplémentaire.
3. Écrans d'administration : CRUD des règles, conditions, sévérités et messages —
   activation/désactivation, dates d'effet — sans recompilation. MVVM strict, aucune
   logique métier en code-behind.

## F. Hors périmètre V1 (explicitement)

- Groupes de conditions imbriqués, `LIKE`/`EXISTS`, règles s'excluant mutuellement.
- Compilation d'expressions, évaluation incrémentale.
- Généralisation à d'autres domaines (carrières, absences, promotions) : c'est une
  **contrainte de nommage et de conception** (l'évaluateur et le dictionnaire de critères
  ne mentionnent jamais « paie »), pas un livrable.
- Critères non retenus en D3 (ils s'ajouteront par une ligne de dictionnaire + attributs).

## G. Stratégie de tests transverse

- Unitaires : évaluateur (opérateurs, bornes temporelles, contexte incomplet → critère
  non résolu = condition non satisfaite + avertissement, jamais d'exception bloquante).
- Intégration : migrations rejouables ; cycle complet suggestion → décision → historique →
  calcul sur les deux cas réels du lot 1 ; évolution réglementaire simulée (nouvelle
  version de règle à date d'effet future) sans modification de code.
- Non-régression : la suite existante (117 tests) reste verte à chaque lot.
