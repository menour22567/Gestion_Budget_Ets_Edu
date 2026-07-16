# J4.e — Dossier de conception : module d'affectation assistée (post Q1–Q7)

> **Statut :** v1.1 — 15/07/2026 — **jalon A validé** par l'utilisateur (dossier accepté
> dans son principe ; lot 2-restes autorisé).
> **Décisions intégrées :** Q1–Q7 validées le 15/07/2026 (Q1a, Q2a, Q3a, Q4a + `MotifRefus`,
> Q5a + principe général, Q6 différée sans blocage, Q7a + test d'extensibilité ZONE).
> **Arbitrages du jalon A (15/07/2026)** : **Q-C1** — recommandation (a) REJETÉE : quand
> `ORIGINE_STATUTAIRE = INCONNU`, le système n'affecte **aucun** taux dépendant de cette
> information (ni 45 % ni 30 %) et émet un avertissement explicable non bloquant — le
> groupe 30 % conditionne donc sur `ORIGINE_STATUTAIRE = AUTRE`, jamais sur `<>` ;
> **Q-C2** — grain **GRADE** validé, le CSV réglementaire devient la source de vérité, le
> corps n'est utilisé que si un texte l'impose explicitement ; **V-2** — tableau des flags
> validé, y compris TRAITEMENT/IEP_FONC/IEP_CONT systémiques : l'affectabilité découle de
> la **fonction métier**, jamais de la seule nature comptable ; **Q-08** — différée jusqu'à
> vérification de la matrice contre les textes et le CSV, sans hypothèse.
> **Principe d'abstention réglementaire (règle d'architecture, 15/07/2026 — → ADR-0009)** :
> en cas d'information réglementaire incomplète, absente ou ambiguë, le système n'invente,
> n'extrapole ni ne déduit jamais un droit ; il applique une règle explicitement démontrable
> ou s'abstient en produisant un avertissement explicable, toujours non bloquant.
> **Références :** `docs/prompts/PROMPT_MOTEUR_AFFECTATION_RUBRIQUES.md` (v2.0), J3G
> (matrice ISSRP), J3H (modèle d'affectation), J3K (schéma V009 réel), ADR-0004/0005/0006/0007.
> **Aucun code applicatif n'est écrit tant que les jalons § 12 ne sont pas validés.**

---

## 1. Synthèse

Ce dossier traduit les décisions Q1–Q7 en spécifications implémentables. Il couvre :
la migration **V010** (flags D1/D4 sur `Rubriques`, rien d'autre — Q3), le **seed ISSRP
remodelé en DNF au grain GRADE** (Q1, § 6), le **contrat d'explicabilité** de l'évaluateur
(§ 7.1), les moteurs **`SuggestionEngine`** / **`AvertissementEngine`** (§ 7.2-7.3), le
design preview **Phase 5** des tables de gestion amendé de `MotifRefus` (Q4, § 4.3), le
principe d'**immutabilité des périodes clôturées** (Q5, ADR-0008 proposé, § 8), le **test
d'extensibilité ZONE** (Q7, § 9), et le plan d'implémentation re-séquencé (§ 10).

Deux découvertes de conception nécessitent votre arbitrage avant seed (§ 2) : le sort des
**7 grades conditionnels quand `ORIGINE_STATUTAIRE = INCONNU`** (Q-C1), et le fait que le
seed actuel utilise des **codes CORPS placeholders** alors que la matrice J3G — seule
version validable ligne à ligne — est au grain **GRADE** (Q-C2).

## 2. Points ⛔ EN ATTENTE DE DÉCISION

| Réf | Question | Bloque | Recommandation |
|---|---|---|---|
| **⛔ V-1** | Validation **ligne à ligne** de la matrice J3G (185 grades) — c'est le document `J3G_MATRICE_ISSRP_PROPOSITION.md` lui-même, complété des points d'attention § 6.4 | Seed ISSRP (lot 2a § 3) | Valider J3G tel quel + arbitrer Q-08 (n° 130-132) et inspecteurs génériques (n° 133/135/136/148) |
| ~~⛔~~ **V-2 — VALIDÉE (15/07/2026)** | Tableau de flags § 5 validé tel quel, y compris `TRAITEMENT`/`IEP_FONC`/`IEP_CONT` systémiques. **Principe acté** : l'affectabilité découle de la fonction métier, jamais de la seule nature comptable. | — | § 5 ci-dessous, prêt pour la migration V010 |
| ~~⛔~~ **Q-C1 — TRANCHÉE (15/07/2026)** | `ORIGINE = INCONNU` sur les 7 grades conditionnels : option (b) retenue, **renforcée** — le système n'affecte **aucun** taux dépendant d'une information inconnue (ni 45 % ni 30 %) et émet un avertissement explicable non bloquant. Application du principe d'abstention (ADR-0009). | — | Groupe 30 % : `ORIGINE_STATUTAIRE = AUTRE` (§ 6.2) |
| ~~⛔~~ **Q-C2 — TRANCHÉE (15/07/2026)** | Grain **GRADE** validé ; `Cascade_Corps_Grades_30526.csv` = source de vérité ; le corps n'est utilisé que si un texte réglementaire l'impose explicitement. Tableaux CORPS placeholders supprimés au seed. | — | Correspondance grades ↔ codes soumise avec V-1 (J4F) |
| **⛔ V-3** | MUNATEC (Q6) : seed différé en attente de la réglementation. | Uniquement son seed | Poursuivre sans (acté). |
| **⛔ Q-08** | Enseignants contractuels (J3G n° 130-132) : 45 % proposé par alignement. | 3 lignes de la matrice | Hérité de J3G — à trancher avec V-1. |

Tout le reste du dossier est exécutable indépendamment de ces points (lot « 2-restes », § 10).

## 3. Vue d'ensemble des livrables

| Livrable | Quand | Contenu |
|---|---|---|
| **Lot 2-restes** | Immédiat (aucun ⛔) | Contrat d'explicabilité § 7.1, cache par période § 7.4, tests |
| **Migration V010** | Après V-2 | 2 flags sur `Rubriques` + seed § 5 (référentiel seul — Q3) |
| **Seed ISSRP DNF** | Après V-1/Q-C1/Q-C2 | § 6 — groupes + conditions GRADE, remplace la matrice à plat |
| **Phase 5 (lot 3)** | Avec `Agents` | Tables de gestion § 4.3 (dont `MotifRefus`), moteurs § 7.2-7.3, use cases |
| **Phase 6 (lot 4)** | UI | Écrans § 11 |
| **ADR-0008** | Avec ce dossier | Immutabilité des périodes clôturées § 8 |
| **ADR-0009** | Avec ce dossier | **Principe d'abstention réglementaire** (règle d'architecture, 15/07/2026) : jamais de droit inventé, extrapolé ou déduit — règle démontrable ou abstention + avertissement explicable non bloquant. Conséquences : Q-C1 (§ 6.2), résolution des critères (« non résolu = non satisfait + avertissement », déjà implémentée), défauts d'attributs J3H § 4 à réexaminer sous ce prisme au lot 3 |

## 4. Schéma cible

### 4.1 Migration V010 — delta exact vs V009 (référentiel seulement, Q3)

```sql
-- V010__affectation_flags.sql
ALTER TABLE Rubriques ADD COLUMN EstAffectableManuellement INTEGER NOT NULL DEFAULT 0
    CHECK (EstAffectableManuellement IN (0, 1));   -- D1 : sûr par défaut
ALTER TABLE Rubriques ADD COLUMN OccurrencesMultiples INTEGER NOT NULL DEFAULT 0
    CHECK (OccurrencesMultiples IN (0, 1));        -- D4

-- Seed : UNIQUEMENT les lignes validées au tableau § 5 (V-2) — aucun UPDATE implicite
-- par Nature. Idempotent (UPDATE ciblé par Id), pattern V001-V009.
```

Rien d'autre en V010 : pas de table de gestion (Q3), pas de modification des tables V009.
`docs/DICTIONNAIRE_DONNEES.md` complété au même commit.

### 4.2 Sémantique transitoire des flags (avant Phase 5)

Tant que `AgentRubriques` n'existe pas, le pipeline (J4.c) continue de calculer une
rubrique GAIN ssi formule active + éligibilité. Les flags sont posés en V010 mais la
précédence D0 ne s'active qu'à la création des tables de gestion (lot 3). Un test
d'intégration fige cette règle de transition pour éviter tout comportement implicite.

### 4.3 Tables de gestion — design preview Phase 5 (créées avec `Agents`)

DDL de référence : J3H §§ 4, 7, 8, adapté aux conventions J3K, avec **un seul amendement**
issu de Q4 :

```sql
CREATE TABLE AvertissementsHistorique (
    Id              TEXT NOT NULL PRIMARY KEY,   -- GUID (gestion, ADR-0004)
    EmisLe          TEXT NOT NULL,
    Utilisateur     TEXT,                        -- Q12 : libellé libre en V1
    AgentId         TEXT NOT NULL REFERENCES Agents(Id),
    RubriqueId      TEXT NOT NULL REFERENCES Rubriques(Id),
    GroupeId        TEXT,                        -- règle déclencheuse (NULL si manuel)
    GroupeDateEffet TEXT,                        -- version de la règle à l'émission
    Severite        TEXT NOT NULL,
    MessageAffiche  TEXT NOT NULL,               -- SNAPSHOT du texte résolu
    Decision        TEXT NOT NULL CHECK (Decision IN ('ACCEPTE', 'IGNORE', 'SUPPRIME')),
    MotifRefus      TEXT,                        -- Q4 : optionnel, JAMAIS exigé ;
                                                 -- historisé et exposé à l'audit si présent
    CreatedAt       TEXT NOT NULL
);
```

`AgentAttributs`, `AgentRubriques`, `AgentRubriqueParametres` : inchangés vs J3H.
Invariants (Application + tests, jamais de DELETE physique) : inchangés vs J3H § 7,
plus : **la saisie de `MotifRefus` est toujours facultative** — aucun écran ni use case ne
la rend obligatoire (test de non-blocage dédié).

## 5. Tableau de seed des flags — ⛔ V-2, validation explicite requise

Catalogue réel au 15/07/2026 (9 rubriques : `ReglementaireSeeder` + `FormulesSeeder`) :

| Rubrique | Nature | `EstAffectableManuellement` proposé | `OccurrencesMultiples` proposé | Justification |
|---|---|:---:|:---:|---|
| `TRAITEMENT` | GAIN | **0** ⚠️ | 0 | Base structurelle du bulletin (TBASE dérivé de la grille) — la supprimer priverait l'agent de tout salaire ; c'est le pipeline qui la sert, pas une décision d'affectation |
| `IEP_FONC` | GAIN | **0** ⚠️ | 0 | Composante échelon du traitement (TRT = TBASE + IEP_FONC, Art. 5 D.p. 07-304) — indissociable du traitement |
| `IEP_CONT` | GAIN | **0** ⚠️ | 0 | Symétrique contractuel de IEP_FONC (ancienneté composite, Art. 16 D.p. 07-304) — dérivée du contrat, pas d'un choix |
| `EXP_PEDAG` | GAIN | **1** | 0 | Indemnité conditionnée (corps EN hors intendance/laboratoire) — cas d'usage type de la libre affectation |
| `PAPP` | GAIN | **1** | 0 | Prime 0-40 % selon notation — affectable, valeur pilotée par la source NOTATION_AGENT |
| `ISSRP_45` | GAIN | **1** | 0 | Suggérée par les groupes DNF § 6 |
| `ISSRP_30` | GAIN | **1** | 0 | idem |
| `ISSRP_15` | GAIN | **1** | 0 | idem |
| `IRG` | IMPOT | **0** | 0 | D1 — pipeline exclusif |

⚠️ = **déviation assumée** de la règle implicite « GAIN ⇒ affectable » (D1) : ces trois
rubriques sont des GAIN *structurels*. C'est exactement le cas que la validation explicite
(votre précision Q2) doit trancher. Si vous préférez la lecture stricte de D1
(tout GAIN = 1), le moteur reste correct — les suggestions pré-affecteront ces rubriques
systématiquement — mais l'utilisateur pourra supprimer le traitement d'un agent.

Rubriques **futures** (pattern acté, hors seed V010) : `RET_MUNATEC` (RETENUE, affectable=1,
occurrences=0 — V-3) ; retenues à montant fixe type œuvres sociales (affectable=1,
**occurrences=1**) ; rubriques de rappel Q7 (affectable=0, occurrences=1, générées par le
moteur — § 8). Point de convergence à traiter en lot 3 : `MUTUELLE`/`OEUVRES_SOCIALES`
vivent aujourd'hui dans `Cotisations` (FACULTATIVE) — la cible Q3b-rev les modélise en
`Rubriques` RETENUE ; proposition de migration au moment de la création d'`Agents`
(jalon D, § 12).

## 6. Seed ISSRP en DNF — structure des groupes (⛔ V-1 / Q-C1 / Q-C2)

### 6.1 Ce qui est remplacé

Les tableaux `Issrp45CorpsCodes`/`Issrp30CorpsCodes`/`Issrp15CorpsCodes` (grain CORPS,
placeholders) et les règles à plat `RE-ISSRP-{taux}-{corps}` (`GroupeId NULL`,
opérateur `=`) — structurellement fausses sous l'évaluateur : « CORPS = X **ET**
CORPS = Y » n'est jamais vrai. Les 3 tests Tools qui interrogent ce seed en SQL brut sont
réécrits contre la nouvelle structure. **Aucun contournement dans le moteur de calcul**
(Q1) : le test e2e J4.c abandonne son `IN` local et lit le seed réel.

### 6.2 Structure cible (2025+, D.ex. 25-55 Art. 10)

```text
GroupesEligibilite (DateEffet 2025-01-01, Source « Art. 10 D.ex. 25-55 ») :
  GE-ISSRP45-DIRECT   ISSRP_45  OBLIGATOIRE_REGLEMENTAIRE  MSG-ISSRP45
  GE-ISSRP45-ORIGINE  ISSRP_45  OBLIGATOIRE_REGLEMENTAIRE  MSG-ISSRP45-ORIGINE
  GE-ISSRP30-DIRECT   ISSRP_30  OBLIGATOIRE_REGLEMENTAIRE  MSG-ISSRP30
  GE-ISSRP30-ORIGINE  ISSRP_30  OBLIGATOIRE_REGLEMENTAIRE  MSG-ISSRP30-ORIGINE
  GE-ISSRP15-DIRECT   ISSRP_15  OBLIGATOIRE_REGLEMENTAIRE  MSG-ISSRP15

ReglesEligibilite (conditions, grain GRADE — Q-C2 reco (a)) :
  (GE-ISSRP45-DIRECT)   GRADE IN (47 grades « 45 % » J3G [+3 si Q-08 validée])
  (GE-ISSRP45-ORIGINE)  GRADE IN (7 grades conditionnels J3G)
  (GE-ISSRP45-ORIGINE)  ORIGINE_STATUTAIRE = ENSEIGNANT
  (GE-ISSRP30-DIRECT)   GRADE IN (20 grades « 30 % » J3G)
  (GE-ISSRP30-ORIGINE)  GRADE IN (7 grades conditionnels J3G)
  (GE-ISSRP30-ORIGINE)  ORIGINE_STATUTAIRE = AUTRE   ← Q-C1 tranchée : jamais de droit
                                                       déduit d'une information inconnue ;
                                                       INCONNU ⇒ ni 45 % ni 30 %, avertis-
                                                       sement explicable non bloquant (ADR-0009)
  (GE-ISSRP15-DIRECT)   GRADE IN (15 grades « 15 % » J3G)
```

### 6.3 Période historique 2008–2024 (Art. 9 bis D.ex. 11-373)

```text
GE-ISSRP15-HIST  ISSRP_15  (DateEffet 2008-01-01, DateFin 2024-12-31)
  GRADE IN (tous les grades EN classés 45/30/15 dans J3G — taux unique 15 %)
```

### 6.4 Protocole de validation V-1

1. Vous validez `J3G_MATRICE_ISSRP_PROPOSITION.md` ligne à ligne (185 grades), en
   tranchant Q-08 (n° 130-132) et les inspecteurs génériques (n° 133/135/136/148).
2. Je génère depuis `Cascade_Corps_Grades_30526.csv` la table de correspondance
   grade → code GRADE utilisé dans les conditions `IN`, et je vous la soumets en
   **tableau Markdown** (un écart CSV/matrice = une question, jamais une hypothèse).
3. Seed écrit seulement après vos deux validations ; critères d'acceptation § 10, lot 2a.

Note dictionnaire : le critère utilisé est `GRADE` (CARRIERE, seedé V009). Le critère
`ORIGINE_STATUTAIRE` (ATTRIBUT_AGENT) est seedé V009 ; sa résolution effective attend
`AgentAttributs` (Phase 5) — d'ici là, l'évaluateur le traite en « critère non résolu »
(condition non satisfaite + avertissement INFO), comportement déjà acté.

## 7. Conception des moteurs

### 7.1 Contrat d'explicabilité (lot 2-restes — immédiat)

`ResultatEligibilite` actuel : `(bool EstEligible, IReadOnlyList<DiagnosticCondition>)` —
les diagnostics ne portent que les échecs. Extension (rupture assumée, jamais en prod) :

```csharp
// Domain/Workbench — pur, sans I/O (ADR-0005)
public sealed record ExplicationCondition(
    string CritereId, string Operateur, string ValeurAttendue,
    string? ValeurAgent,           // null = critère non résolu
    bool Satisfaite, string? Detail);

public sealed record ExplicationGroupe(
    string? GroupeId,              // null = conditions communes
    bool Satisfait,
    IReadOnlyList<ExplicationCondition> Conditions);

public sealed record ResultatEligibilite(
    bool EstEligible,
    IReadOnlyList<ExplicationGroupe> Groupes);   // satisfaites ET non satisfaites
```

L'évaluateur reste pur et n'exploite toujours pas les en-têtes `GroupesEligibilite` pour
le verdict ; la `Source`, la sévérité et le message sont joints par les moteurs § 7.2-7.3
(couche au-dessus), qui reçoivent les en-têtes chargés par l'Application. « Pourquoi cette
rubrique ? » et l'`ExplainabilityEngine` consomment ce résultat **sans retraitement**.

### 7.2 `SuggestionEngine` (Domain, pur — lot 3)

```csharp
public sealed record Suggestion(
    string RubriqueId,
    string GroupeId, string GroupeDateEffet,     // version de la règle (audit, Origine)
    string Severite, string? MessageId, int Priorite,
    ResultatEligibilite Explication);

public interface ISuggestionEngine
{
    /// Rubriques affectables (flag = 1) éligibles à la date et non déjà affectées.
    /// N'écrit rien : la couche Application matérialise en AgentRubriques(SUGGEREE).
    IReadOnlyList<Suggestion> Suggerer(
        AgentContext agent, string datePaie,
        IReadOnlyList<Rubrique> rubriques,
        IReadOnlyList<ConditionEligibilite> conditions,
        IReadOnlyDictionary<string, CritereEligibilite> criteres,
        IReadOnlyList<GroupeEligibilite> groupes,
        IReadOnlyList<AffectationRubrique> affectationsExistantes);
}
```

### 7.3 `AvertissementEngine` (Domain, pur — lot 3)

Types de diagnostic (sémantique d'exécution = code, Open/Closed ; déclencheurs, messages
et sévérités = données) :

| Type | Déclencheur |
|---|---|
| `RUBRIQUE_RECOMMANDEE_ABSENTE` | Éligible + affectable + aucune ligne active |
| `AFFECTATION_INELIGIBLE` | Ligne active mais plus éligible (mutation, changement d'attribut) |
| `REGLE_EXPIREE` | Ligne dont la règle d'origine a atteint sa `DateFin` (« à vérifier ») |
| `ECHEANCE_PROCHE` | `DateFin` d'affectation dans les N jours (N = paramètre en base) |
| `DONNEE_MANQUANTE` | Critère non résolu lors d'une évaluation |

```csharp
public sealed record Avertissement(
    string Type, string RubriqueId, string Severite,
    string? GroupeId, string? GroupeDateEffet,
    string? MessageId, ResultatEligibilite? Explication);

public interface IAvertissementEngine
{
    IReadOnlyList<Avertissement> Diagnostiquer(
        AgentContext agent, string datePaie,
        IReadOnlyList<Rubrique> rubriques,
        IReadOnlyList<AffectationRubrique> affectations,
        IReadOnlyList<ConditionEligibilite> conditions,
        IReadOnlyDictionary<string, CritereEligibilite> criteres,
        IReadOnlyList<GroupeEligibilite> groupes);
}
```

Les deux moteurs appellent **l'unique** `RegleEligibiliteEvaluator` — aucun verdict
d'éligibilité n'est calculé ailleurs. Aucune sortie de ces moteurs ne porte de notion de
blocage : ce sont des listes en lecture, la couche Application les historise
(`AvertissementsHistorique`) et l'UI les affiche.

### 7.4 Cache par période (lot 2-restes)

Cache mémoire des (conditions actives, critères, groupes) résolus par date de paie,
invalidé à toute écriture de paramétrage (hook des repositories d'écriture Workbench).
Pas de compilation d'expressions (D). Mesure avant/après sur le critère existant
BaremeResolver (1000 résolutions < 50 ms) étendu à l'évaluateur.

## 8. Principe d'immutabilité des périodes clôturées (Q5) → ADR-0008 proposé

**Décision.** Une période de paie clôturée n'est **jamais recalculée**. Toute correction à
effet rétroactif (changement de règle, d'affectation, d'attribut, de barème) est servie
par une **rubrique de rappel** sur une période ultérieure ouverte : occurrence par
(rubrique rappelée × période de référence) — D4 —, générée par le moteur (jamais affectée
manuellement), montant = différence entre le bulletin d'époque (snapshot) et le
recalcul à droit constant de la période de référence.

**Conséquences :**
- le générateur de rappels (J4.d) devient le **seul** mécanisme de correction rétroactive ;
- le bulletin calculé est snapshoté (SnapshotEngine J4.d) — le rappel se calcule contre le
  snapshot, pas contre une réévaluation du passé ;
- l'audit peut rejouer le raisonnement d'époque : `Origine = GROUPE:<Id>@<DateEffet>`
  (J3H) fige la version de règle, le snapshot fige les montants ;
- statut de « période clôturée » : à définir avec le modèle de périodes (Phase 5) —
  jalon D.

ADR-0008 rédigé à la validation de ce dossier ; il élève la règle au rang de **principe
général du moteur** (votre précision Q5).

## 9. Test d'extensibilité ZONE (Q7) — démonstration sans code

Scénario automatisé (test d'intégration, lot 3), **données uniquement** :

1. `CriteresEligibilite` ← ligne `ZONE` (ATTRIBUT_AGENT, ENUM) — une ligne, pas de migration ;
2. `Rubriques` ← `IND_ZONE` (GAIN, affectable = 1) ; `RubriqueParametres` ← taux/montant
   versionné (les décrets 95-300, 13-210/211/212 sont dans `Reglementation/Indemn Zone/`
   pour le seed réel futur — hors V1) ;
3. `MessagesRegles` ← `MSG-ZONE-SUD` ; `GroupesEligibilite` ← `GE-INDZONE-SUD`
   (RECOMMANDEE) ; `ReglesEligibilite` ← `ZONE IN ('SUD', …)` ;
4. `AgentAttributs` ← `(agent, ZONE, 'SUD', DateEffet)` ;
5. **Assertions :** `SuggestionEngine` propose `IND_ZONE` avec explication complète ;
   l'agent hors zone ne la reçoit pas ; l'utilisateur peut l'écarter (avertissement
   historisé, `MotifRefus` optionnel) ; **aucune recompilation** — le test n'introduit
   aucun type ni aucune condition en dur.

Ce test (`ExtensibiliteZoneTests`) devient le critère d'acceptation permanent de
l'exigence D — il échoue si quelqu'un code un critère en dur.

## 10. Plan d'implémentation re-séquencé

### Lot 2-restes — immédiat, aucun ⛔

| # | Tâche | Critère d'acceptation |
|---|---|---|
| 1 | `ResultatEligibilite` enrichi (§ 7.1) + adaptation évaluateur et tests | Conditions satisfaites ET non satisfaites exposées ; suite verte |
| 2 | Cache par période (§ 7.4) | Invalidation à l'écriture testée ; perf mesurée |
| 3 | Test transition flags (§ 4.2) | Comportement pipeline inchangé tant que `AgentRubriques` absent |

### Lot 2a — ✅ LIVRÉ (16/07/2026), sauf item 4

| # | Tâche | Critère d'acceptation | Statut |
|---|---|---|---|
| 1 | Migration V010 (§ 4.1) + seed flags validé (§ 5, QUALIF/DOC_PEDAG ajoutées avec le même principe) | Idempotente, rejouable ; flags conformes au tableau validé | ✅ |
| 2 | Table de correspondance grades ↔ CSV soumise puis seed ISSRP DNF (§ 6, J4F) | L'évaluateur déclare éligibles exactement les agents attendus par J3G ; e2e sans contournement ; tests Tools réécrits | ✅ — 6 groupes DNF, 8 conditions, 0 contournement CORPS résiduel, 287/287 tests verts |
| 3 | `DICTIONNAIRE_DONNEES.md` à jour | Même commit | ✅ (§6.1, §7.1) — `PLAN_ACTION.md` inchangé (ne référençait pas cette dette) |
| 4 | ADR-0008 (§ 8, immutabilité périodes clôturées) + ADR-0009 (§ 3, abstention réglementaire) | Acceptés | ✅ Rédigés et acceptés le 16/07/2026 — `docs/adr/0008-immutabilite-periodes-cloturees.md`, `docs/adr/0009-abstention-reglementaire.md`, index `docs/adr/README.md` à jour. |

### Lot 3 — Phase 5 (avec `Agents`) — jalon D préalable

Tables de gestion § 4.3 ; `SuggestionEngine`/`AvertissementEngine` ; use cases
(`SuggererRubriques`, `AffecterRubrique`, `SupprimerRubrique`, `SuspendreRubrique`,
`ReactiverRubrique`, `AccepterSuggestion`) écrivant l'historique (sévérité ≥ RECOMMANDEE) ;
réévaluation sur changement d'attribut ; invariants J3H § 7 + `MotifRefus` facultatif ;
convergence `Cotisations` FACULTATIVE → rubriques RETENUE ; `ExtensibiliteZoneTests` (§ 9) ;
tests de non-blocage (toute transition possible quelle que soit la sévérité).

### Lot 4 — Phase 6 (UI)

Écran d'affectation (badges), panneau « Pourquoi cette rubrique ? » (consomme § 7.1 sans
requête supplémentaire), écrans d'administration Workbench (rubriques, critères,
conditions, groupes, messages), saisie `MotifRefus` optionnelle. Recette : scénario
exigence D de bout en bout via l'UI seule.

## 11. Écrans (conception résumée — détail au lancement du lot 4)

1. **Affectation agent** : liste `AgentRubriques` + suggestions, badges `Suggérée` /
   `Réglementaire` / `Personnalisée` / `Temporaire` / `À vérifier` ; actions toujours
   actives (aucun bouton désactivé par un avertissement — preuve par test UI).
2. **Pourquoi cette rubrique ?** : rendu direct d'`ExplicationGroupe` (conditions,
   valeurs de l'agent, décret source).
3. **Administration Workbench** : CRUD des 5 référentiels V009/V010, dates d'effet,
   simulation d'impact (`SimulerEvolutionReglementaire`, D8) avant activation.

## 12. Jalons de validation (STOP & ASK)

| Jalon | Objet | Bloque |
|---|---|---|
| **A** | Ce dossier + tableau flags (V-2) + structure DNF (§ 6.2, Q-C1/Q-C2) | Lots 2a |
| **B** | Matrice J3G ligne à ligne + correspondance grades/CSV (V-1, Q-08) | Seed ISSRP |
| **C** | Revue de la migration V010 avant commit — implémentée et testée (16/07/2026), **rien n'est commité** (aucune demande explicite reçue) | Commit V010 |
| **D** | Design `Agents` + modèle de périodes + convergence Cotisations (Phase 5) | Lot 3 |
| **E** | Maquettes écrans + démo recette D | Lot 4 |

Le lot 2-restes démarre sans jalon (aucune dépendance métier).
