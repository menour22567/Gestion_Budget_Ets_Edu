# J3.h — Modèle d'affectation assistée des rubriques (lot 1)

> **Statut :** proposition v1.0 — 14/07/2026 — soumise à validation (STOP & ASK).
> **Aucune migration n'est écrite tant que ce document n'est pas validé.**
> **Références :** `docs/prompts/PROMPT_MOTEUR_AFFECTATION_RUBRIQUES.md` (lot 1, décisions
> D1–D4 et Q3b-rev), ADR-0004 (PK), ADR-0005 (évaluateur pur), V008, J3E, J3G (en attente),
> RM-040/044/062/068/100.

---

## 1. Positionnement : une extension du socle, pas un second moteur

Le modèle sépare deux notions que le prompt initial confondait :

- **Éligibilité réglementaire** (existant, V005/V008) : « qui a droit à quoi », évaluée par
  le moteur à chaque calcul, jamais stockée par agent.
- **Affectation par agent** (nouveau) : la liste des rubriques d'un agent telle que
  l'utilisateur la voit et la décide — suggestions, acceptations, suppressions,
  suspensions, occurrences — avec historisation d'audit.

Un **seul évaluateur** de règles (lot 2, pur et synchrone — ADR-0005) sert les deux usages.

### Tables nouvelles ou étendues

| Table | Type (ADR-0004) | Nouveau / étendu | Créée par |
| --- | --- | --- | --- |
| `CriteresEligibilite` | Référentiel (PK code) | Nouveau | V009 (après validation) |
| `GroupesEligibilite` | Référentiel (PK code) | Nouveau | V009 |
| `MessagesRegles` | Référentiel (PK code) | Nouveau | V009 |
| `ReglesEligibilite` | Référentiel | Étendu (`GroupeId`, FK critère) | V009 |
| `Rubriques` | Référentiel | Étendu (2 flags D1/D4) | V009 |
| `AgentAttributs` | Gestion (PK GUID) | Nouveau | Phase 5 (avec `Agents`) |
| `AgentRubriques` (+ `AgentRubriqueParametres`) | Gestion (PK GUID) | Nouveau | Phase 5 |
| `AvertissementsHistorique` | Gestion (PK GUID) | Nouveau | Phase 5 |

Les tables de gestion référencent `Agents`, qui n'existe qu'en Phase 5 : elles sont
**conçues maintenant, créées avec `Agents`**. Toutes les tables de référentiel portent
`DateEffet`/`DateFin`, `Source`, `Hash`, `CreatedAt` (conventions V001+).

## 2. Groupes de conditions — correction d'une limite latente du modèle

**Constat.** `ReglesEligibilite` combine aujourd'hui toutes les conditions d'une rubrique
en **ET plat** (RM-040). Or l'ISSRP 45 % (RM-043/044, matrice J3G en attente) exige :

```text
ISSRP_45 éligible si
      GRADE ∈ {grades pédagogiques directs}                    -- groupe A
   OU (GRADE ∈ {7 grades conditionnels} ET ORIGINE = ENSEIGNANT) -- groupe B
```

Inexprimable en ET plat. La forme retenue (conforme au prompt § C.5 : « le OU s'exprime
par plusieurs règles ») est la **forme normale disjonctive** : une rubrique porte des
*groupes* de conditions ; les conditions d'un groupe sont ETées ; les groupes sont OUés.

**Modèle.** Une table d'en-tête porte l'identité du groupe et ses métadonnées
d'affectation (sévérité, message, priorité) — le prompt lot 1 § 3 plaçait ces colonnes sur
`ReglesEligibilite` ; les remonter dans un en-tête évite de les dupliquer sur chaque
condition (écart de conception justifié, voir Q-J3H-2) :

```sql
CREATE TABLE GroupesEligibilite (
    Id          TEXT NOT NULL PRIMARY KEY,     -- ex. "GE-ISSRP45-ORIGINE"
    RubriqueId  TEXT NOT NULL REFERENCES Rubriques(Id),
    Severite    TEXT NOT NULL DEFAULT 'INFO' CHECK (Severite IN
                  ('INFO', 'RECOMMANDEE', 'OBLIGATOIRE_REGLEMENTAIRE')),   -- D2
    MessageCode TEXT REFERENCES MessagesRegles(Id),
    Priorite    INTEGER NOT NULL DEFAULT 100,  -- ordre d'affichage des suggestions
    DateEffet   TEXT NOT NULL,
    DateFin     TEXT,
    Source      TEXT,
    Hash        TEXT NOT NULL,
    CreatedAt   TEXT NOT NULL
);

-- ReglesEligibilite (recréée en V009, précédent V007/V008) : nouvelle colonne
--   GroupeId TEXT REFERENCES GroupesEligibilite(Id)
--   GroupeId NULL   = condition « commune » à la rubrique (comportement V008 inchangé)
--   GroupeId rempli = condition membre du groupe
```

**Sémantique d'évaluation** (implémentée une seule fois, lot 2) :

1. Conditions communes (`GroupeId NULL`) : toutes exigées (ET) — garde-fous partagés.
2. S'il existe des groupes actifs à la date : **au moins un** groupe entièrement satisfait.
3. S'il n'existe aucun groupe : comportement V008 identique (ET plat) — **la migration ne
   réécrit aucune règle existante**.

## 3. Dictionnaire de critères — `CriteresEligibilite`

Remplace le `CHECK` en dur de `ReglesEligibilite.Critere` : un nouveau critère devient
**une ligne**, pas une migration. Chaque critère déclare *comment* il se résout.

```sql
CREATE TABLE CriteresEligibilite (
    Id               TEXT NOT NULL PRIMARY KEY,   -- ex. "CORPS", "PROFIL_IRG"
    Libelle          TEXT NOT NULL,
    SourceResolution TEXT NOT NULL CHECK (SourceResolution IN
                       ('CARRIERE',        -- donnée du dossier carrière (corps, grade…)
                        'AGENT_ATTRIBUT',  -- AgentAttributs (§4)
                        'GRADE_ATTRIBUT',  -- GradeAttributs (V008)
                        'AFFECTATION',     -- attribut de l'établissement d'affectation
                        'CALCULE')),       -- résolveur dédié (ex. ancienneté Q8)
    CleResolution    TEXT,                 -- clé dans la source (NULL si Id suffit)
    TypeDonnee       TEXT NOT NULL CHECK (TypeDonnee IN
                       ('TEXTE', 'ENTIER', 'DECIMAL', 'BOOLEEN', 'DATE')),
    Description      TEXT,
    DateEffet        TEXT NOT NULL,
    DateFin          TEXT,
    Source           TEXT,
    Hash             TEXT NOT NULL,
    CreatedAt        TEXT NOT NULL
);
```

`ReglesEligibilite.Critere` devient une FK vers cette table. `TypeDonnee` sert à valider
les opérateurs à la saisie (ex. `>=` refusé sur un `BOOLEEN`) — validation applicative,
non bloquante pour l'évaluateur.

**Seed initial (13 critères)** — les 10 de V008 + les 3 nouveaux issus de D3 :

| Id | SourceResolution | CleResolution | TypeDonnee | Justification |
| --- | --- | --- | --- | --- |
| `FILIERE` | CARRIERE | — | TEXTE | V008 |
| `CORPS` | CARRIERE | — | TEXTE | V008 |
| `GRADE` | CARRIERE | — | TEXTE | V008 |
| `CATEGORIE` | CARRIERE | — | TEXTE | V008 |
| `FONCTION` | CARRIERE | — | TEXTE | V008 |
| `TYPE_CONTRAT` | CARRIERE | — | TEXTE | V008 |
| `ECHELON` | CARRIERE | — | ENTIER | V008 |
| `ANCIENNETE` | CALCULE | `ANNEES_SERVICE_EFFECTIF` | ENTIER | V008 ; Q8 (années révolues) |
| `ORIGINE_CORPS` | AGENT_ATTRIBUT | `ORIGINE_STATUTAIRE` | TEXTE | V008 ; Q-03, ISSRP 45 % |
| `TYPE_ETABLISSEMENT` | AFFECTATION | `TypeEtablissement` | TEXTE | V008 ; ind. direction 15-271 |
| `EXERCICE_EFFECTIF` | AGENT_ATTRIBUT | `EXERCICE_EFFECTIF` | BOOLEEN | **D3** ; « en exercice effectif » 15-271 |
| `ANCIENNETE_PRIVEE` | AGENT_ATTRIBUT | `ANCIENNETE_PRIVEE_ANNEES` | ENTIER | **D3** ; ANC_PRIV de IEP_CONT |
| `PROFIL_IRG` | AGENT_ATTRIBUT | `PROFIL_IRG` | TEXTE | **D3** ; lissage spécial 2020+, Q-11 |

**Critère non résolu** (attribut absent du dossier) : la condition est **non satisfaite**
et l'évaluateur émet un avertissement `INFO` (« donnée manquante : … ») — jamais
d'exception, jamais de blocage (prompt § G).

## 4. `AgentAttributs` (gestion — Phase 5)

Symétrique de `GradeAttributs`, versionnée : porte les critères propres à la personne,
sans colonnes en dur.

```sql
CREATE TABLE AgentAttributs (
    Id        TEXT NOT NULL PRIMARY KEY,          -- GUID (table de gestion, ADR-0004)
    AgentId   TEXT NOT NULL REFERENCES Agents(Id),
    Attribut  TEXT NOT NULL,                      -- CleResolution d'un critère AGENT_ATTRIBUT
    Valeur    TEXT NOT NULL,
    DateEffet TEXT NOT NULL,
    DateFin   TEXT,
    Source    TEXT,                               -- pièce justificative / décision admin.
    CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_AgentAttributs_Agent_Attr_Date
    ON AgentAttributs (AgentId, Attribut, DateEffet);
```

La cohérence `Attribut` ↔ `CriteresEligibilite.CleResolution` est validée en couche
Application (pas de FK : le dictionnaire est versionné).

**Attributs V1 (D3)** :

| Attribut | Domaine de valeurs | Défaut si absent |
| --- | --- | --- |
| `ORIGINE_STATUTAIRE` | `ENSEIGNANT` \| `AUTRE` \| `INCONNU` (J3E §4) | `INCONNU` → pas de suggestion 45 %, avertissement INFO |
| `EXERCICE_EFFECTIF` | `0` \| `1` | `0` |
| `ANCIENNETE_PRIVEE_ANNEES` | entier ≥ 0 | `0` |
| `PROFIL_IRG` | `STANDARD` \| `HANDICAPE` \| `RETRAITE_RG` | `STANDARD` |

## 5. `MessagesRegles` (référentiel)

Messages paramétrables des recommandations/avertissements — modifiables sans code.

```sql
CREATE TABLE MessagesRegles (
    Id        TEXT NOT NULL PRIMARY KEY,   -- ex. "MSG-ISSRP45-ORIGINE"
    Texte     TEXT NOT NULL,               -- gabarit : {rubrique}, {agent}, {conditions}, {source}
    DateEffet TEXT NOT NULL,
    DateFin   TEXT,
    Source    TEXT,
    Hash      TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
```

## 6. Flags sur `Rubriques` (D1, D4, Q3b-rev)

```sql
ALTER TABLE Rubriques ADD COLUMN EstAffectableManuellement INTEGER NOT NULL DEFAULT 0
    CHECK (EstAffectableManuellement IN (0, 1));   -- D1 : sûr par défaut
ALTER TABLE Rubriques ADD COLUMN OccurrencesMultiples INTEGER NOT NULL DEFAULT 0
    CHECK (OccurrencesMultiples IN (0, 1));        -- D4
```

Mise à jour du seed : `EstAffectableManuellement = 1` pour les GAIN et les retenues
optionnelles Q3b-rev (dont `RET_MUNATEC`, § 10.b) ; reste à `0` pour COTISATION, IMPOT et
toute rubrique systémique. `OccurrencesMultiples = 1` uniquement pour les retenues
optionnelles **à montant fixe** (œuvres sociales) et les rubriques de rappel (Q7).

## 7. `AgentRubriques` (gestion — Phase 5)

L'affectation vue et décidée par l'utilisateur.

```sql
CREATE TABLE AgentRubriques (
    Id                TEXT NOT NULL PRIMARY KEY,  -- GUID
    AgentId           TEXT NOT NULL REFERENCES Agents(Id),
    RubriqueId        TEXT NOT NULL REFERENCES Rubriques(Id),
    Occurrence        INTEGER NOT NULL DEFAULT 1 CHECK (Occurrence >= 1),
    LibelleOccurrence TEXT,                       -- retenues à montant fixe : "Prêt social"…
    Statut            TEXT NOT NULL CHECK (Statut IN
                        ('SUGGEREE', 'ACCEPTEE', 'SUPPRIMEE', 'SUSPENDUE')),
    Origine           TEXT NOT NULL,              -- 'MANUELLE' ou 'GROUPE:<Id>@<DateEffet>'
    DateEffet         TEXT NOT NULL,
    DateFin           TEXT,
    CreatedAt         TEXT NOT NULL,
    UpdatedAt         TEXT
);
CREATE UNIQUE INDEX IX_AgentRubriques_Agent_Rub_Occ_Date
    ON AgentRubriques (AgentId, RubriqueId, Occurrence, DateEffet);

CREATE TABLE AgentRubriqueParametres (               -- surcharges locales (montant choisi…)
    Id               TEXT NOT NULL PRIMARY KEY,      -- GUID
    AgentRubriqueId  TEXT NOT NULL REFERENCES AgentRubriques(Id),
    Cle              TEXT NOT NULL,
    Valeur           TEXT NOT NULL,
    DateEffet        TEXT NOT NULL,
    DateFin          TEXT,
    CreatedAt        TEXT NOT NULL
);
```

**Machine à états** (aucune transition n'est bloquée par une règle — le logiciel
avertit, l'utilisateur décide) :

```text
SUGGEREE ──accepter──▶ ACCEPTEE ◀──réactiver──┐
   │                      │                    │
   │                  suspendre            SUSPENDUE
   │                      └────────────────────┘
   └──refuser──▶ SUPPRIMEE ◀──supprimer── (ACCEPTEE | SUSPENDUE)
                 (terminal : réaffecter = NOUVELLE ligne, l'ancienne reste en trace)
```

**Invariants** (couche Application + tests, jamais de DELETE physique) :

- `Occurrence > 1` seulement si `Rubriques.OccurrencesMultiples = 1` ;
- affectation `MANUELLE` seulement si `EstAffectableManuellement = 1` ;
- à toute date, au plus une ligne non-`SUPPRIMEE` par (agent, rubrique, occurrence) —
  périodes sans chevauchement ;
- `Origine = 'GROUPE:<Id>@<DateEffet>'` fige la **version** de la règle déclencheuse
  (audit, rappels Q7).

## 8. `AvertissementsHistorique` (gestion — Phase 5, append-only)

```sql
CREATE TABLE AvertissementsHistorique (
    Id              TEXT NOT NULL PRIMARY KEY,   -- GUID
    EmisLe          TEXT NOT NULL,
    Utilisateur     TEXT,                        -- Q12 : libellé libre en V1 (mode autonome)
    AgentId         TEXT NOT NULL REFERENCES Agents(Id),
    RubriqueId      TEXT NOT NULL REFERENCES Rubriques(Id),
    GroupeId        TEXT,                        -- règle déclencheuse (NULL si manuel)
    GroupeDateEffet TEXT,                        -- version de la règle à l'émission
    Severite        TEXT NOT NULL,
    MessageAffiche  TEXT NOT NULL,               -- SNAPSHOT du texte résolu (le gabarit évolue)
    Decision        TEXT NOT NULL CHECK (Decision IN ('ACCEPTE', 'IGNORE', 'SUPPRIME')),
    CreatedAt       TEXT NOT NULL
);
```

Strictement **append-only** : ni UPDATE ni DELETE (convention + test d'invariant).
`MessageAffiche` est un instantané — l'audit doit relire ce que l'utilisateur a vu, pas la
version actuelle du gabarit.

## 9. Règle de précédence calcul ↔ affectation

| Rubrique | Rôle des règles d'éligibilité | Rôle d'`AgentRubriques` | Payée pour une période si |
| --- | --- | --- | --- |
| Systémique (`EstAffectableManuellement = 0`) | **Décident** (comportement actuel) | Aucun (jamais de ligne) | Éligible à la date de la période |
| Affectable (`= 1`) | Alimentent suggestions + avertissements | **Décide** | Ligne `SUGGEREE` ou `ACCEPTEE` couvrant la période |

Corollaires :

- Une rubrique `SUPPRIMEE` ou `SUSPENDUE` n'est **jamais** payée — aucune rubrique ne peut
  être à la fois « supprimée à l'écran » et « payée par le moteur ».
- Une affectation devenue **inéligible** (mutation, règle expirée) reste payée tant que
  l'utilisateur ne la retire pas : le moteur d'avertissements la signale (« à vérifier »),
  il ne la retire jamais d'office. La liberté de l'utilisateur prime (prompt § C.1).
- `SUGGEREE` **est payée** sans revue explicite : les suggestions sont des
  pré-affectations (voir Q-J3H-1) — la différence `SUGGEREE`/`ACCEPTEE` trace si
  l'utilisateur a revu la ligne.
- RM-062 raffinée : « un couple (rubrique, **occurrence**) ne peut être calculé deux fois
  pour un même bulletin ».

## 10. Cinématique des trois cas réels (critère d'acceptation du lot 1)

### (a) ISSRP 45 % conditionnée par l'origine statutaire

Paramétrage (seed J3G, après validation de la matrice) :

```text
GroupesEligibilite :
  GE-ISSRP45-DIRECT  (ISSRP_45, OBLIGATOIRE_REGLEMENTAIRE, MSG-ISSRP45,  01/01/2025)
  GE-ISSRP45-ORIGINE (ISSRP_45, OBLIGATOIRE_REGLEMENTAIRE, MSG-ISSRP45-O, 01/01/2025)
ReglesEligibilite :
  (GE-ISSRP45-DIRECT)  GRADE IN (grades pédagogiques directs)
  (GE-ISSRP45-ORIGINE) GRADE IN (7 grades conditionnels J3G)
  (GE-ISSRP45-ORIGINE) ORIGINE_CORPS = ENSEIGNANT
```

Cycle : création d'un agent « Conseiller de l'éducation » avec
`AgentAttributs(ORIGINE_STATUTAIRE = ENSEIGNANT, Source = décision de promotion)` →
l'évaluateur satisfait le groupe ORIGINE → suggestion `ISSRP_45` badge « Réglementation »,
« Pourquoi ? » affiche : *GRADE ∈ …*, *ORIGINE_CORPS = ENSEIGNANT*, *Art. 10 D.ex. 25-55*
→ l'utilisateur accepte → `AgentRubriques(ACCEPTEE, Origine = GROUPE:GE-ISSRP45-ORIGINE@2025-01-01)`
plus une ligne `AvertissementsHistorique(Decision = ACCEPTE)` → le calcul paie ISSRP_45
(45 % du traitement). Si l'attribut vaut `INCONNU` : aucune suggestion 45 %, avertissement INFO
« origine statutaire non renseignée ».

### (b) Retenue optionnelle MUNATEC à taux versionné (Q3b-rev)

Seed : `Rubriques(RET_MUNATEC, RETENUE, ASSIETTE_COTISABLE, EstAffectableManuellement = 1,
OccurrencesMultiples = 0)` + `RubriqueParametres(MUNATEC_TAUX_PCT = 1, DateEffet à
confirmer — Q-J3H-4)`. Aucune règle d'éligibilité : jamais suggérée, adhésion volontaire.

Cycle : l'utilisateur affecte manuellement → `AgentRubriques(ACCEPTEE, Origine = MANUELLE,
DateEffet = date d'adhésion)` → chaque paie retient 1 % de l'assiette cotisable (calculée
**après** l'assiette, via le DAG de dépendances). Résiliation trois ans plus tard →
statut `SUPPRIMEE` + `DateFin` + trace `AvertissementsHistorique(Decision = SUPPRIME)` →
plus retenue dès la période suivante. Le taux passe à 1,25 % en 2028 → **nouvelle version**
de `MUNATEC_TAUX_PCT` (`DateEffet = 2028-01-01`), zéro modification d'affectation ni de code.

### (c) Prime temporaire bornée

Seed : `Rubriques(PRIME_EXC_2027, GAIN, FORFAIT, EstAffectableManuellement = 1)` +
`GroupesEligibilite(GE-PRIMEEXC-2027, RECOMMANDEE, MSG-PRIME-TEMP, DateEffet = 2027-01-01,
DateFin = 2027-12-31)` + conditions éventuelles (ex. `CORPS IN (…)`).

Cycle : la suggestion n'apparaît que pour les périodes 2027 (résolution temporelle
RM-100) ; acceptation → affectation bornée `DateFin = 2027-12-31` → payée en 2027, plus
payée en 2028 (hors période, sans intervention). Si l'utilisateur prolonge manuellement la
`DateFin` de l'affectation : payée quand même (liberté), badge « À vérifier » (règle
expirée) + avertissement historisé — jamais de blocage.

## 11. Plan de mise en œuvre (après validation de ce document)

1. **Migration V009** (référentiel) : `CriteresEligibilite` (+ seed 13 critères),
   `GroupesEligibilite`, `MessagesRegles` ; recréation de `ReglesEligibilite`
   (FK `Critere`, colonne `GroupeId`) — précédent V007/V008, reseed par la CLI ;
   2 flags sur `Rubriques` + mise à jour du seed (dont `RET_MUNATEC`).
   `docs/DICTIONNAIRE_DONNEES.md` complété au même commit.
2. **Phase 5** (avec `Agents`) : `AgentAttributs`, `AgentRubriques`,
   `AgentRubriqueParametres`, `AvertissementsHistorique` + use cases (lot 3).
3. **Lot 2 (Phase 4)** : évaluateur unifié (groupes, critères du dictionnaire,
   explicabilité, cache par période) — voir prompt § E lot 2.
4. **Tests** : schéma V009 (modèle `RubriquesV2BaremesSchemaTests`), seed des critères,
   invariants d'états (lot 3), non-régression (la suite existante reste verte).

L'interaction avec **J3G** est directe : le seed définitif de la matrice ISSRP (en attente
de validation ligne à ligne) utilisera les groupes du § 2 — les 7 lignes conditionnelles
`ORIGINE_CORPS` sont inexprimables sans eux.

## 12. Questions ouvertes (STOP & ASK)

| Réf | Question | Recommandation |
| --- | --- | --- |
| **Q-J3H-1** | Une ligne `SUGGEREE` non revue est-elle payée ? | **Oui** — les suggestions sont des pré-affectations (« l'utilisateur peut les conserver ») ; sinon un agent créé sans revue explicite n'aurait aucune indemnité. `ACCEPTEE` ne sert qu'à tracer la revue. |
| **Q-J3H-2** | Valider l'en-tête `GroupesEligibilite` (le prompt plaçait `Severite`/`MessageCode`/`Priorite` en colonnes de `ReglesEligibilite`) ? | **Oui** — normalisé, évite la duplication par condition, rend le OU exprimable ; les règles V008 existantes restent valides sans réécriture. |
| **Q-J3H-3** | Valeurs d'`ORIGINE_STATUTAIRE` = `ENSEIGNANT`/`AUTRE`/`INCONNU` (J3E §4), `INCONNU` ⇒ pas de suggestion 45 % + avertissement INFO ? | **Oui** — cohérent avec le STOP & ASK à la saisie prévu par J3E. |
| **Q-J3H-4** | MUNATEC : date d'effet du taux 1 % (et historique éventuel de taux) à fournir pour le seed. | À fournir avant le seed V009 ; le schéma n'en dépend pas. |
