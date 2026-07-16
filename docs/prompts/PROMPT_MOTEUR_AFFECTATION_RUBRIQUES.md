# Prompt — Module d'affectation assistée des rubriques de paie (v2, exécutable)

> **Statut :** v2.0 — 15/07/2026. Fusion de la v1.0 (14/07/2026) et de la reformulation
> « exécutable » du 15/07/2026, re-basée sur l'existant **livré** (V009 committée, J3H/J3K
> validés, évaluateur DNF et moteur de calcul Phase 4 opérationnels).
> **Destinataire :** agent de codage autonome (Claude Code).
> **Usage :** ne pas exécuter d'un bloc. Chaque lot (§ G) est un prompt autonome, à lancer au
> jalon indiqué, après levée des `⛔` dont il dépend (§ F).
> **Règle d'or :** ne rejoue pas ce qui est déjà tranché (§ D). Ne crée pas un second socle
> de règles : le socle existe (§ A) — tu l'étends, tu ne le doubles pas.

---

## A. Contexte — ce qui existe déjà et que tu dois réutiliser

Tu interviens sur **PaieEducation**, ERP de paie de l'Éducation nationale algérienne
(fonction publique, corps assimilés) : .NET 10 LTS, C# 14, WPF + MVVM
(CommunityToolkit.Mvvm), MS.Extensions.DI, SQLite + Dapper, QuestPDF, ClosedXML,
System.Text.Json, xUnit + Moq. **100 % hors ligne.** Clean Architecture + DDD (ADR-0001) ;
`Domain` pur — ni SQLite, ni WPF, ni QuestPDF (test d'archi vert). Principe cardinal :
**zéro hardcoding** — toute règle ou valeur réglementaire vit en base, versionnée
`DateEffet`/`DateFin`, avec `Source` (texte réglementaire) et `Hash` d'audit.

**Socle livré et committé — interdiction d'en créer un second en parallèle :**

| Livré | Rôle | Où |
| --- | --- | --- |
| `Rubriques` | Catalogue (Nature GAIN/RETENUE/COTISATION/IMPOT, flags, périodicités) | V004/V008 |
| `ReglesEligibilite` (+`CritereId` FK, +`GroupeId` FK) | Conditions plates + DNF, versionnées | V008/V009 |
| `GroupesEligibilite` | En-tête DNF : Severite, MessageId, Priorite, période | V009 |
| `CriteresEligibilite` | Dictionnaire de critères (10 seedés) — plus de CHECK en dur | V009 |
| `MessagesRegles` | Messages paramétrables (catalogue vide, à peupler) | V009 |
| `SourcesValeur` | Catalogue technique de résolution de valeurs (7 seedés) | V009 |
| `RubriqueBaremes` / `RubriqueParametres` | Valeurs par tranche / taux versionnés | V008 |
| `GradeAttributs` | Attributs versionnés de grade | V008 |
| `RegleEligibiliteEvaluator` | Évaluateur **pur, synchrone**, ET-plat + DNF via `GroupeId` | Domain (Phase 3bis) |
| `CalculationPipeline`, `FormulaEngine`, `IrgCalculator`, `DependencyResolver` | Moteur de calcul — bulletin e2e vérifié (net 46 624 DA, formules lues en base) | Domain (Phase 4) |

**Conventions verrouillées :**

- **ADR-0004** : référentiel → PK = code métier TEXT (`Id`) ; gestion (agents, affectations,
  historiques) → PK = GUID.
- **ADR-0005** : cœur d'évaluation **synchrone et pur** — contexte immuable complet en
  entrée, résultat déterministe en sortie ; toute I/O en couche Application.
- **ADR-0006** (affectation assistée — **accepté**) et **ADR-0007** (Workbench réglementaire).
- **Q10** : périmètre V1 = corps pilote enseignants. **Q12** : mode autonome sans
  authentification en V1 (champ « utilisateur » = libellé de poste).
- Méthode **STOP & ASK** (§ F) : toute règle métier douteuse déclenche une question, jamais
  une hypothèse silencieuse.

**Docs de référence à lire avant de concevoir :** `docs/analysis/J3H_MODELE_AFFECTATION.md`
(modèle cible d'affectation), `docs/analysis/J3K_V009_FINAL_MODEL.md` (schéma V009 réel),
`docs/PLAN_ACTION.md`, `docs/DICTIONNAIRE_DONNEES.md`, `docs/CONVENTIONS.md`, ADR-0004→0007,
et le dossier `Reglementation/` (source de vérité métier — décrets 10-78, 12-403, 15-271,
25-55, régimes paramédicaux 2011/2024, IRG 2008–2026, IFC, ISSRP, prime de rendement,
indemnités de zone Sud).

## B. Mission

Donner à l'utilisateur une **liberté totale d'affectation des rubriques par agent**,
assistée de **suggestions réglementaires par défaut** et d'**avertissements strictement non
bloquants**, **entièrement paramétrés en base** (zéro hardcoding). Quatre exigences
indissociables :

- **A — Liberté.** Pour chaque agent : ajouter, retirer, suspendre, réactiver, dupliquer
  (quand autorisé), borner dans le temps toute rubrique **affectable** (§ D, D1) ;
  l'utilisateur garde la décision finale ; traçabilité de l'origine de chaque affectation
  (suggérée / manuelle / suggérée-puis-écartée).
- **B — Suggestions par défaut.** À la création/modification d'un agent, le moteur interroge
  les règles en base et **pré-affecte** les rubriques applicables (statut `SUGGEREE`),
  construites dynamiquement à partir des caractéristiques de l'agent — jamais d'une liste
  codée en dur.
- **C — Avertissements non bloquants.** Diagnostics contextuels (rubrique habituellement due
  mais absente, échéance proche, affectation devenue inéligible…) portant une **sévérité**
  (§ D, D2) — **jamais** de blocage, ni direct, ni indirect (pas de validation impossible,
  pas de bouton désactivé, pas de contournement obligatoire).
- **D — Zéro hardcoding.** Ajouter une rubrique + sa condition d'éligibilité + sa règle de
  suggestion + son message doit être faisable **par un administrateur fonctionnel via l'UI,
  sans recompilation** — critère de recette central (§ G, lot 4).

Chaque suggestion et chaque avertissement est **explicable** (« Pourquoi cette rubrique ? »
→ conditions satisfaites + texte réglementaire source) et **historisé** avec la décision de
l'utilisateur, à des fins d'audit.

## C. Principes permanents

- **C.1 — Le logiciel conseille, l'utilisateur décide.** Aucune suggestion imposée, aucun
  avertissement bloquant. Le système conserve uniquement la trace des avertissements
  affichés et des décisions prises. **Garde-fou :** si un blocage te paraît indispensable
  (contrainte d'intégrité technique), **ne l'implémente pas — interroge-moi.**
- **C.2 — Zéro hardcoding, formulation honnête.** Interdits absolus dans le code :
  `if (wilaya == "Tamanrasset")`, tout nom de rubrique, taux, seuil ou critère
  réglementaire. Sans toucher au code : règles, conditions, rubriques, messages, valeurs et
  nouveaux critères (ligne de dictionnaire). Avec du code (Open/Closed — nouvelle
  implémentation enregistrée, sans modifier l'existant) : nouvel **opérateur** ou nouveau
  **type d'action**. La documentation ne prétend jamais le contraire.
- **C.3 — Temporalité.** Toute règle, condition et affectation est bornée
  `DateEffet`/`DateFin`. Une rubrique temporaire n'est suggérée que pendant sa validité ;
  une affectation dont la règle source a expiré passe « à vérifier ». L'historique conserve
  la **version** de la règle déclencheuse (rappels rétroactifs Q7, audit).

## D. Décisions DÉJÀ actées — à appliquer, pas à re-questionner

> Ces points ferment les « questions ouvertes » des versions antérieures. Ne les rouvre
> pas ; si tu crois devoir en dévier, passe par STOP & ASK (§ F) avec l'impact chiffré.

- **D0 — Précédence calcul ↔ affectation** (J3H § 9) : rubrique **systémique**
  (`EstAffectableManuellement = 0`) → l'éligibilité **décide**, aucune ligne d'affectation.
  Rubrique **affectable** (`= 1`) → `AgentRubriques` **décide** ; les règles n'alimentent
  que suggestions + avertissements. Une rubrique `SUPPRIMEE`/`SUSPENDUE` n'est **jamais**
  payée ; une affectation devenue inéligible reste payée jusqu'à retrait manuel (badge
  « à vérifier » + avertissement, jamais de retrait d'office). `SUGGEREE` **est payée**
  (Q-J3H-1) ; `ACCEPTEE` ne fait que tracer la revue.
- **D1 — Périmètre d'exclusion. ✅** Non affectables : Nature ∈ {COTISATION, IMPOT} +
  rubriques d'assiette et de net (pipeline exclusif). RETENUE optionnelles Q3b-rev
  (œuvres sociales à montant fixe ; mutuelles à taux versionné sur assiette — ex. MUNATEC
  1 % de l'assiette cotisable) = librement affectables. Porté par le flag
  `EstAffectableManuellement` sur `Rubriques` (donnée, pas constante).
- **D2 — Sévérités. ✅** `INFO` | `RECOMMANDEE` | `OBLIGATOIRE_REGLEMENTAIRE` — présentation
  (icône, couleur, message) et poids d'historisation seulement, **jamais** le comportement
  (même `OBLIGATOIRE_REGLEMENTAIRE` reste supprimable, avec trace).
- **D3 — Critères V1. ✅** Dérivés carrière : FILIERE, CORPS, GRADE, CATEGORIE, ECHELON,
  ANCIENNETE (Q8), TYPE_CONTRAT, FONCTION, TYPE_ETABLISSEMENT (via l'affectation).
  `AgentAttributs` versionnés : `ORIGINE_STATUTAIRE` (ISSRP 45 %, Q-03),
  `EXERCICE_EFFECTIF` (art. 9 bis D.ex. 15-271), `ANCIENNETE_PRIVEE_ANNEES` (IEP_CONT),
  `PROFIL_IRG` (`STANDARD`|`HANDICAPE`|`RETRAITE_RG`, RM-065/066). Écartés V1 (aucune
  rubrique du périmètre n'en dépend — vérifié sur `Reglementation/`) : zone, wilaya,
  situation familiale, diplôme… — ajoutables plus tard par **une ligne de dictionnaire**,
  sans migration (c'est le test d'extensibilité du modèle).
- **D4 — Occurrences. ✅** Aucune rubrique réglementaire du périmètre n'admet d'occurrences
  multiples. `OccurrencesMultiples = 1` seulement pour : retenues optionnelles à **montant
  fixe** (chaque occurrence avec libellé, montant, bornes propres) et rappels Q7 (générés
  par le moteur, jamais affectés manuellement). Les retenues à taux (MUNATEC) restent à
  occurrence unique — une adhésion par organisme, chaque organisme = sa rubrique.
  RM-062 raffinée : « un couple (rubrique, occurrence) ne peut être calculé deux fois pour
  un même bulletin ».
- **Formalisme des règles. ✅** Critères structurés (critère/opérateur/valeur) combinés en
  **ET plat** + **groupes DNF** (`GroupeId` ; conditions d'un groupe ETées, groupes OUés,
  conditions communes `GroupeId NULL` toujours exigées) — déjà en base (V009) et implémenté
  (`RegleEligibiliteEvaluator`). **Pas** de JSON de règles, **pas** de DSL, **pas** de
  compilation d'expressions en V1. Opérateurs V1 : `=`, `<>`, `IN`, `NOT_IN`, `>`, `>=`,
  `<`, `<=`, `BETWEEN`. Pas de groupes imbriqués, de `LIKE` ni d'`EXISTS` : aucun cas
  réglementaire recensé (J3B/J3G) n'en a besoin — l'architecture doit permettre de les
  ajouter plus tard (Open/Closed), pas de les livrer.
- **Un seul évaluateur** sert calcul (`EligibilityEngine`) et suggestions/avertissements —
  interdiction de dupliquer la logique.
- **Explicabilité native.** L'évaluation retourne, par groupe déclenché, les conditions
  satisfaites (critère, opérateur, valeur attendue, valeur agent) + la `Source`
  réglementaire — consommées telles quelles par « Pourquoi cette rubrique ? » et
  l'`ExplainabilityEngine` du bulletin.
- **Critère non résolu** (attribut absent du dossier) = condition **non satisfaite** +
  avertissement `INFO` (« donnée manquante : … ») — jamais d'exception, jamais de blocage.

## E. Principe de refonte de schéma (le projet n'est jamais parti en prod)

Base **en cours de conception** : aucune donnée définitive, **aucune migration de données,
aucune rétrocompatibilité, aucune conservation exigée**. Tu es autorisé à restructurer /
renommer / fusionner / scinder / supprimer tables et colonnes si cela améliore cohérence,
maintenabilité, performances ou évolutivité. **L'objectif est la meilleure architecture,
pas la préservation de l'existant.** Contrepartie impérative : toute refonte importante =
**argumentée** (problème constaté / options / choix / impacts / coût), **soumise à
validation avant implémentation**, puis tracée en **ADR**. (V009 est déjà committée : une
modification de son schéma est une refonte à argumenter, pas un acquis.)

## F. Protocole STOP & ASK — questions restant réellement ouvertes

**Règles permanentes** (conception et implémentation) : regroupe et numérote tes questions ;
pour chacune : **contexte · enjeu/impact · 2–4 options · ta recommandation argumentée**.
Marque tout point en suspens `⛔ EN ATTENTE DE DÉCISION` — une hypothèse n'est jamais
acquise tant que je ne l'ai pas confirmée. **Ne devine jamais** une règle métier ni une
valeur réglementaire. N'avance pas sur une tâche dont une dépendance métier est `⛔`, sauf
progression strictement indépendante. Je fournis les données manquantes au format que tu
demandes (PDF, Word, TXT, Markdown, Excel, CSV) : précise format et niveau de détail.

**Points ouverts connus au 15/07/2026 :**

1. **Timing des tables de gestion.** `AgentAttributs` / `AgentRubriques` /
   `AvertissementsHistorique` dépendent de `Agents` (absente, Phase 5). Migration V010
   dédiée à l'affectation, ou fusion avec la création de `Agents` ? (reco : V010 conjointe
   à `Agents`).
2. **Flags D1/D4 sur `Rubriques`.** `EstAffectableManuellement` / `OccurrencesMultiples`
   ne sont **pas** encore en base (J3K a livré `SourceValeurId` seul). Quelle migration,
   quel seed par rubrique existante ? (reco : V010, seed sûr = 0 sauf GAIN et retenues
   optionnelles).
3. **Seed matrice ISSRP en DNF.** La matrice du `ReglementaireSeeder` (conditions `=` à
   plat, `GroupeId NULL`) est **incompatible** avec l'évaluateur DNF → jamais vraie. À
   remodéliser en groupes DNF (J3G / J3H § 10.a) : validation ligne à ligne requise avant
   seed définitif.
4. **Rétroactivité (Q5/Q7).** Un changement de règle ou une affectation à effet passé
   déclenche-t-il un recalcul des bulletins déjà calculés ? Politique vis-à-vis des
   périodes validées/clôturées ? Articulation avec les rappels rétroactifs (J4.d, non fait).
5. **MUNATEC (Q-J3H-4).** Date d'effet (et historique éventuel) du taux 1 % à fournir
   avant tout seed. Le schéma n'en dépend pas.
6. **Traçabilité des écarts.** Écarter une rubrique suggérée réglementairement :
   historiser le refus, en demander le motif, le remonter dans l'audit /
   `ExplainabilityEngine` ? (reco : oui, motif optionnel).

## G. Découpage en lots — état re-basé au 15/07/2026

### Lot 1 — Conception du modèle ✅ LIVRÉ (ne pas refaire)

`docs/analysis/J3H_MODELE_AFFECTATION.md` + ADR-0006 (accepté) : dictionnaire
`CriteresEligibilite`, `AgentAttributs`, groupes DNF, `AgentRubriques`
(+ `AgentRubriqueParametres`), `AvertissementsHistorique` (append-only), machine à états
`SUGGEREE → ACCEPTEE / SUPPRIMEE / SUSPENDUE`, précédence D0, cinématique des trois cas
réels (ISSRP 45 % / MUNATEC / prime temporaire). V009 en a implémenté la partie
référentiel (avec deltas J3K : `MessageCode` → `MessageId`, audit différencié
catalogues/réglementaire) ; **restes → lots 2a/3**.

### Lot 2 — Évaluateur de règles unifié 🟡 PARTIELLEMENT LIVRÉ

**Livré (Phase 3bis + fixes)** : `RegleEligibiliteEvaluator` pur et synchrone (ADR-0005),
ET-plat + DNF déduite du `GroupeId` des conditions (les en-têtes `GroupesEligibilite` ne
servent qu'au diagnostic UI) ; critère non résolu = non satisfait, sans exception.

**Reste à livrer :**

1. **Contrat d'explicabilité complet** : le résultat d'évaluation porte, par groupe
   déclenché, les conditions satisfaites (critère, opérateur, valeur attendue, valeur
   agent) et la `Source` — consommé sans retraitement par l'UI et l'`ExplainabilityEngine`.
2. Exploitation des métadonnées d'en-tête (Severite, MessageId, Priorite, période) par les
   moteurs du lot 3 — l'évaluateur lui-même reste pur.
3. **Performance** : cache mémoire des règles actives résolues par période, invalidé à
   toute écriture de paramétrage. Pas de compilation d'expressions ni d'évaluation
   incrémentale en V1 — mesurer avant d'optimiser.

**Critères d'acceptation** : déterminisme (mêmes entrées → mêmes sorties) ; chaque
opérateur V1 testé (nominal + bornes) ; bornes temporelles testées (veille/jour/lendemain
de `DateEffet` et `DateFin`) ; test prouvant que calcul et suggestion donnent le **même
verdict** d'éligibilité sur le même contexte (dépend du lot 3).

### Lot 2a — Migration V010 + seed ISSRP (NOUVEAU — après levée des ⛔ F.1/F.2/F.3)

1. Flags `EstAffectableManuellement` / `OccurrencesMultiples` sur `Rubriques` + reseed
   (D1/D4 ; `RET_MUNATEC` inclus dès que F.5 est levée).
2. Tables de gestion selon la décision F.1 : `AgentAttributs`, `AgentRubriques`
   (+ `AgentRubriqueParametres`), `AvertissementsHistorique` — DDL de référence : J3H
   §§ 4, 7, 8 (adapté aux conventions J3K).
3. **Seed ISSRP remodelé en groupes DNF** (matrice validée ligne à ligne, F.3) :
   `GroupesEligibilite` GE-ISSRP45-DIRECT / GE-ISSRP45-ORIGINE + conditions `IN` et
   `ORIGINE_STATUTAIRE`, remplaçant la matrice à plat ; les 3 tests Tools qui
   l'interrogent en SQL brut sont mis à jour.
4. `docs/DICTIONNAIRE_DONNEES.md` complété **au même commit**.

**Critères d'acceptation** : migration idempotente et rejouable (pattern V001–V009) ;
l'évaluateur DNF déclare désormais éligibles les agents ISSRP attendus (cas e2e sans
contournement `IN` local) ; suite existante verte.

### Lot 3 — Use cases et historisation (Phase 5, avec `Agents`)

1. **`SuggestionEngine`** (pré-affecte `SUGGEREE`) et **`AvertissementEngine`**
   (diagnostics non bloquants), tous deux **au-dessus de l'unique
   `RegleEligibiliteEvaluator`** — jamais deux implémentations.
2. Use cases : `SuggererRubriques` (à la création d'agent et sur demande),
   `AffecterRubrique`, `SupprimerRubrique`, `SuspendreRubrique`, `ReactiverRubrique`,
   `AccepterSuggestion` — chacun écrivant `AvertissementsHistorique` quand une règle de
   sévérité ≥ `RECOMMANDEE` est contredite.
3. Réévaluation des suggestions quand un attribut d'agent change (mutation, changement de
   grade) : nouvelles suggestions proposées, affectations orphelines marquées
   « à vérifier » — jamais supprimées d'office.
4. Transactions atomiques ; `AvertissementsHistorique` strictement **append-only**
   (ni UPDATE ni DELETE — convention + test d'invariant) ; `MessageAffiche` = snapshot du
   texte résolu (l'audit relit ce que l'utilisateur a vu, pas le gabarit actuel).
5. Invariants (couche Application + tests, jamais de DELETE physique) : `Occurrence > 1`
   seulement si `OccurrencesMultiples = 1` ; affectation `MANUELLE` seulement si
   `EstAffectableManuellement = 1` ; à toute date, au plus une ligne non-`SUPPRIMEE` par
   (agent, rubrique, occurrence) ; `Origine = 'GROUPE:<Id>@<DateEffet>'` fige la version
   de la règle déclencheuse.

### Lot 4 — Interface WPF/MVVM (Phase 6)

1. Écran d'affectation : liste des rubriques de l'agent avec badges — `Suggérée`,
   `Réglementaire` (OBLIGATOIRE_REGLEMENTAIRE), `Personnalisée` (origine MANUELLE),
   `Temporaire` (DateFin renseignée), `À vérifier` (règle expirée/orpheline).
2. Panneau « Pourquoi cette rubrique ? » alimenté par l'explicabilité du lot 2
   (conditions et décret/texte source), **sans requête supplémentaire**.
3. Écrans d'administration : CRUD rubriques, critères, conditions, groupes, sévérités et
   messages — activation/désactivation, dates d'effet — **sans recompilation**. MVVM
   strict, aucune logique métier en code-behind.

**Critère de recette central (exigence D, démo bout en bout attendue)** : un administrateur
fonctionnel ajoute, **via l'UI seule**, une nouvelle rubrique réglementaire + sa condition
d'éligibilité + sa règle de suggestion + son message d'avertissement ; elle est proposée,
explicable et payée — **sans recompilation ni intervention d'un développeur**.

## H. Hors périmètre V1 (explicitement)

- Groupes de conditions imbriqués, `LIKE`/`EXISTS`, règles s'excluant mutuellement.
- Compilation d'expressions, évaluation incrémentale.
- Généralisation à d'autres domaines (carrières, absences, promotions) : contrainte de
  **nommage et de conception** (l'évaluateur et le dictionnaire ne mentionnent jamais
  « paie »), pas un livrable.
- Critères non retenus en D3 (ajoutables par une ligne de dictionnaire + attributs).

## I. Stratégie de tests transverse

- **Unitaires** : `SuggestionEngine` / `AvertissementEngine` ; chaque opérateur V1
  (nominal + bornes) ; bornes temporelles ; contexte incomplet → critère non résolu =
  condition non satisfaite + avertissement, jamais d'exception bloquante.
- **Non-blocage (obligatoire, prouvé par test automatisé)** : aucun avertissement, quelle
  que soit la sévérité, ne bloque jamais une action utilisateur — toute transition de la
  machine à états reste possible.
- **Paramétrabilité** : ajout d'une rubrique de bout en bout **sans code** (scénario § G
  lot 4) ; évolution réglementaire simulée (nouvelle version de règle à date d'effet
  future) sans modification de code.
- **Cohérence moteur** : calcul et suggestion donnent le même verdict d'éligibilité sur le
  même contexte.
- **Intégration** : migrations rejouables ; cycle complet suggestion → décision →
  historique → calcul sur les trois cas réels (ISSRP 45 % / MUNATEC / prime temporaire).
- **Non-régression** : la suite existante (**271 tests** au 15/07/2026) reste verte à
  chaque lot ; Domain reste pur (test d'archi).

## J. Livrable attendu par lot de conception (Markdown, AUCUN code applicatif avant validation)

1. **Synthèse de compréhension** (≤ 1 page) — articulation avec l'existant livré.
2. **Questions ouvertes priorisées** (`⛔`, § F + les tiennes) en tête, au format STOP & ASK.
3. **Dossier de conception** : modèle de domaine (entités/VO/agrégats/invariants/services/
   événements) ; **schéma cible V010** (conventions Tome D / ADR-0004), justification de
   chaque table et **delta explicite vs V009** ; conception `SuggestionEngine` +
   `AvertissementEngine` (interfaces C#, E/S, insertion dans le pipeline, articulation
   `Eligibility`/`Explainability`) ; conception des écrans WPF (affectation, suggestions,
   avertissements non bloquants, administration).
4. **Analyse d'impact / refonte** (§ E) : ce que tu proposes de restructurer, argumenté,
   à valider.
5. **Plan d'implémentation** par lots séquencés (§ G) : objectif, tâches granulaires (une
   tâche = un incrément vérifiable), livrables, dépendances, critères d'acceptation,
   risques + parades.
6. **Stratégie de tests** (§ I) et **checklist des jalons de validation** (les points où tu
   t'arrêtes pour mon accord).
7. **ADR** proposés pour les décisions structurantes.

## K. Ta première réponse

Produis **uniquement** : (1) ta synthèse de compréhension ; (2) tes questions ouvertes
priorisées (§ F + les tiennes) au format STOP & ASK ; (3) la confirmation de ton accès aux
docs (V3/V4, J3H, J3K, ADR, `Reglementation/`) en signalant précisément ce qui te manque et
sous quel format tu veux le recevoir. Puis **attends mes réponses** avant de produire le
dossier de conception et le plan.
