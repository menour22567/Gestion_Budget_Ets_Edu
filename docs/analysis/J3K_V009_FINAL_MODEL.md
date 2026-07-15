# J3.k — Modèle V009 final — Spécification de référence pour l'implémentation

> **Statut :** v1.0 — 15/07/2026 — Spécification de référence pour l'implémentation V009
> (Phase 3bis du PLAN_ACTION).
> **Rôle :** ce document est **la** spec à suivre pour coder la migration V009, les
> Value Objects, les services de domaine, et les tests d'upgrade. Pure référence
> technique — pas de justification ici (cf. J3J pour le « pourquoi », J3I pour le
> « dans quel cadre »).
> **Documents source :** `docs/analysis/J3I_WORKBENCH_REGLEMENTAIRE.md` (V009 § 9
> refactoré), `docs/analysis/J3J_REFACTORING_AVANT_V009.md` (R1-R5), ADR-0007,
> `docs/DICTIONNAIRE_DONNEES.md` § 8ter refactoré, `docs/PLAN_ACTION.md` Phase 3bis.
> **Validé par :** utilisateur le 15/07/2026 (validation R1-R5 + nuances R4 et D-complémentaire).

---

## 0. Synthèse exécutive (1 écran)

| Métrique | V009 |
|---|---|
| **Tables nouvelles** | 4 (`SourcesValeur`, `CriteresEligibilite`, `MessagesRegles`, `GroupesEligibilite`) |
| **Tables amendées** | 3 (`Rubriques`, `ReglesEligibilite`, `RubriqueBaremes`) |
| **Tables supprimées** | 0 |
| **Tables de gestion reportées à Phase 5** | 3 (`AgentAttributs`, `AgentRubriques`, `AvertissementsHistorique` — design preview J3J § 8.3-8.5) |
| **Colonnes ajoutées** | 7 (1 sur `Rubriques`, 2 sur `ReglesEligibilite`, 2 sur `RubriqueBaremes`, + `RubriquesBaremes.CreatedBy` qui était peut-être déjà là) |
| **Colonnes supprimées** | 1 (`ReglesEligibilite.Critere` — TEXT avec CHECK) |
| **CHECK redondants supprimés** | 1 (`ReglesEligibilite.Critere IN (...)`) |
| **Seed initial** | 7 lignes `SourcesValeur`, 10 lignes `CriteresEligibilite`, 0 `MessagesRegles` (catalogue vide), 0 `GroupesEligibilite` |
| **Migration de données** | `ReglesEligibilite.Critere` → `CritereId` par jointure sur le seed `CriteresEligibilite` |
| **Convention PK** | `Id` partout (TEXT code métier) — harmonisé ADR-0004 |
| **Audit** | Distinction catalogues techniques (minimal) / données réglementaires (complet) |
| **Une seule migration** | Oui, V009 (D10) |

---

## 1. Nouvelles tables (DDL complet)

### 1.1 `SourcesValeur` — catalogue technique (R2 + R4 révisé)

```sql
CREATE TABLE SourcesValeur (
    Id          TEXT NOT NULL PRIMARY KEY,    -- code métier, ex. 'NOTATION_AGENT'
    Libelle     TEXT NOT NULL,
    Description TEXT,
    Actif       INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt   TEXT NOT NULL,                -- ISO 8601 UTC
    CreatedBy   TEXT NOT NULL
);
-- Pas de DateEffet/DateFin/Source/Hash : catalogue technique (R4 révisé)

-- Seed V1 (7 codes)
INSERT INTO SourcesValeur (Id, Libelle, Description, Actif, CreatedAt, CreatedBy) VALUES
    ('NOTATION_AGENT',        'Note de l''agent',          'Résolu via DALNotation (PAPP, PAPG, REND)',              1, '2026-07-15T00:00:00Z', 'system'),
    ('ANCIENNETE_PUBLIQUE',   'Ancienneté publique (années)', 'Service public antérieur',                            1, '2026-07-15T00:00:00Z', 'system'),
    ('ANCIENNETE_PRIVEE',     'Ancienneté privée (années)', 'Service privé antérieur (D3)',                          1, '2026-07-15T00:00:00Z', 'system'),
    ('INDICE_ECHELON',        'Indice d''échelon',          'Depuis T_Indices_Echelons à la date de paie',           1, '2026-07-15T00:00:00Z', 'system'),
    ('POINT_INDICIAIRE',      'Valeur du point indiciaire', 'Depuis T_ValeurPoint à la date de paie',                1, '2026-07-15T00:00:00Z', 'system'),
    ('BASE_ASSIETTE',         'Base d''assiette',           'Snapshot de l''assiette courante (cotisable/imposable)', 1, '2026-07-15T00:00:00Z', 'system'),
    ('CONSTANTE_REGLEMENTAIRE', 'Constante réglementaire',  'Taux/plafond/borne lu en base (RubriqueParametres)',    1, '2026-07-15T00:00:00Z', 'system');
```

### 1.2 `CriteresEligibilite` — catalogue technique (R2 + R3 + R4 révisé)

```sql
CREATE TABLE CriteresEligibilite (
    Id              TEXT NOT NULL PRIMARY KEY,    -- code métier, ex. 'CORPS'
    Libelle         TEXT NOT NULL,
    TypeValeur      TEXT NOT NULL CHECK (TypeValeur IN ('TEXT', 'INT', 'DATE', 'ENUM')),
    SourceResolution TEXT NOT NULL CHECK (SourceResolution IN
        ('ATTRIBUT_AGENT', 'ATTRIBUT_GRADE', 'CARRIERE', 'CALCULE')),
    Actif           INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt       TEXT NOT NULL,
    CreatedBy       TEXT NOT NULL
);
-- Pas de DateEffet/DateFin/Source/Hash : catalogue technique (R4 révisé)

-- Seed V1 (10 codes — D3 + J3E)
INSERT INTO CriteresEligibilite (Id, Libelle, TypeValeur, SourceResolution, Actif, CreatedAt, CreatedBy) VALUES
    ('FILIERE',              'Filière',                       'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('CORPS',                'Corps',                         'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('GRADE',                'Grade',                         'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('CATEGORIE',            'Catégorie',                     'INT',  'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('FONCTION',             'Fonction',                      'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('TYPE_CONTRAT',         'Type de contrat',               'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('ECHELON',              'Échelon',                       'INT',  'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('ANCIENNETE',           'Ancienneté (années)',           'INT',  'CALCULE',        1, '2026-07-15T00:00:00Z', 'system'),
    ('TYPE_ETABLISSEMENT',   "Type d'établissement",          'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('ORIGINE_STATUTAIRE',   'Origine statutaire',            'ENUM', 'ATTRIBUT_AGENT', 1, '2026-07-15T00:00:00Z', 'system');
```

### 1.3 `MessagesRegles` — texte réglementaire (R2 + R4 révisé — audit complet)

```sql
CREATE TABLE MessagesRegles (
    Id          TEXT NOT NULL PRIMARY KEY,    -- ex. 'MSG-ISSRP-45-INCONNU-ORIGINE'
    Categorie   TEXT NOT NULL CHECK (Categorie IN ('ELIGIBILITE', 'AVERTISSEMENT', 'SUGGESTION')),
    TexteFr     TEXT NOT NULL,
    TexteAr     TEXT,                          -- nullable, post-V1
    Source      TEXT NOT NULL,                 -- référence réglementaire (décret/arrêté) — obligatoire
    DateEffet   TEXT NOT NULL,                 -- versioning (R4 révisé)
    DateFin     TEXT,
    Actif       INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt   TEXT NOT NULL,
    CreatedBy   TEXT NOT NULL
);

-- Seed V1 : catalogue vide — les messages seront créés par les Workbench (D7) au fil des besoins
-- Exemple de ce qu'un seed pourra contenir (non créé en V009, juste pour montrer la forme) :
-- INSERT INTO MessagesRegles VALUES
--   ('MSG-ISSRP-45-INCONNU', 'AVERTISSEMENT',
--    'Origine statutaire INCONNU : éligibilité ISSRP_45 non garantie. Vérifier le corps et l''origine.',
--    NULL, 'Décret 25-55 art. 10', '2025-01-01', NULL, 1, '2026-07-15T00:00:00Z', 'system');
```

### 1.4 `GroupesEligibilite` — règle réglementaire (D5, J3H §2)

```sql
CREATE TABLE GroupesEligibilite (
    Id          TEXT NOT NULL PRIMARY KEY,    -- ex. 'GE-ISSRP45-ORIGINE'
    RubriqueId  TEXT NOT NULL REFERENCES Rubriques(Id),
    Severite    TEXT NOT NULL DEFAULT 'INFO' CHECK (Severite IN
        ('INFO', 'RECOMMANDEE', 'OBLIGATOIRE_REGLEMENTAIRE')),   -- D2
    MessageId   TEXT REFERENCES MessagesRegles(Id),    -- nullable : peut être vide si message en dur
    Priorite    INTEGER NOT NULL DEFAULT 100,
    DateEffet   TEXT NOT NULL,
    DateFin     TEXT,
    Source      TEXT,                          -- référence réglementaire
    Hash        TEXT NOT NULL,
    CreatedAt   TEXT NOT NULL,
    CreatedBy   TEXT NOT NULL
);

CREATE INDEX IX_GroupesEligibilite_RubriqueId ON GroupesEligibilite(RubriqueId);
CREATE INDEX IX_GroupesEligibilite_MessageId ON GroupesEligibilite(MessageId);

-- Seed V1 : vide — les groupes seront créés par les Workbench (D7) au fil des besoins
```

---

## 2. Amendements de tables existantes

### 2.1 `Rubriques` — ajout `SourceValeurId`

```sql
ALTER TABLE Rubriques ADD COLUMN SourceValeurId TEXT REFERENCES SourcesValeur(Id);
-- NULL par défaut (P1, P2, P4-P7 : formule self-contained)
-- Non NULL uniquement pour P3 (% indexé sur une source externe) — exemple : PAPP avec NOTATION_AGENT
```

### 2.2 `ReglesEligibilite` — amendements (R3 + L-M2)

```sql
-- 2.2.1 Ajout de CritereId (FK vers CriteresEligibilite.Id)
ALTER TABLE ReglesEligibilite ADD COLUMN CritereId TEXT REFERENCES CriteresEligibilite(Id);

-- 2.2.2 Ajout de GroupeId (FK vers GroupesEligibilite.Id, NULL = condition commune)
ALTER TABLE ReglesEligibilite ADD COLUMN GroupeId TEXT REFERENCES GroupesEligibilite(Id);

-- 2.2.3 Migration de données : Critere (TEXT) → CritereId (FK)
-- Pour chaque ligne existante, on résout le code métier et on remplit CritereId.
UPDATE ReglesEligibilite
SET CritereId = (SELECT Id FROM CriteresEligibilite
                 WHERE CriteresEligibilite.Id = ReglesEligibilite.Critere)
WHERE Critere IS NOT NULL;

-- 2.2.4 Vérification : aucune règle ne doit rester avec CritereId NULL
-- (le seed CriteresEligibilite doit contenir tous les codes utilisés historiquement)
-- Si une valeur inattendue existe, la migration échoue explicitement avec un message clair.

-- 2.2.5 Suppression de la colonne Critere (TEXT) et de son CHECK
-- ATTENTION : SQLite ne supporte pas DROP COLUMN avant 3.35 ; vérifier la version du SDK.
-- Si < 3.35 : reconstruire la table (cf. § 5 — Notes d'implémentation).
ALTER TABLE ReglesEligibilite DROP COLUMN Critere;
-- Le CHECK associé (Critere IN ('FILIERE', ...)) est implicitement supprimé avec la colonne.

CREATE INDEX IX_ReglesEligibilite_CritereId ON ReglesEligibilite(CritereId);
CREATE INDEX IX_ReglesEligibilite_GroupeId ON ReglesEligibilite(GroupeId);
```

### 2.3 `RubriqueBaremes` — ajout audit (L-M3)

```sql
-- 2.3.1 Vérification : CreatedBy existe déjà en V008 (oui, cf. DICTIONNAIRE § 8bis.1)
-- Pas de modification pour CreatedBy.

-- 2.3.2 Ajout UpdatedAt
ALTER TABLE RubriqueBaremes ADD COLUMN UpdatedAt TEXT;

-- 2.3.3 Ajout UpdatedBy
ALTER TABLE RubriqueBaremes ADD COLUMN UpdatedBy TEXT;

-- Trigger applicatif (côté Domain, pas SQL) : toute modification d'une ligne écrit dans AuditLog
```

---

## 3. Diagramme relationnel (vue d'ensemble V009)

```
┌──────────────────────┐         ┌──────────────────────┐
│   SourcesValeur      │         │ CriteresEligibilite  │
│   (PK: Id)           │         │   (PK: Id)           │
└──────────┬───────────┘         └──────────┬───────────┘
           │                                │
           │ 1                            1 │
           │                                │
           │ N                            N │
           │                                │
┌──────────┴───────────┐         ┌──────────┴───────────┐
│      Rubriques       │         │  ReglesEligibilite   │
│   (PK: Id)           │◄────────┤   (PK: Id)           │
│   +SourceValeurId   │  1    N │   +CritereId (FK)    │
└──────────┬───────────┘         │   +GroupeId (FK)     │
           │                     │   -Critere (TEXT)    │
           │ 1                   └──────────┬───────────┘
           │                                │ N
           │                                │
           │                                │ 0..1
           │                                ▼ 1
           │                     ┌──────────────────────┐
           │                     │ GroupesEligibilite   │
           │                     │   (PK: Id)           │
           │                     │   +RubriqueId (FK)   │
           │                     │   +Severite, ...     │
           │                     └──────────┬───────────┘
           │                                │ 0..1
           │                                ▼ 1
           │                     ┌──────────────────────┐
           │                     │   MessagesRegles     │
           │                     │   (PK: Id)           │
           │                     │   +Source (audit)    │
           │                     │   +DateEffet/Fin     │
           │                     └──────────────────────┘
           │
           │ N
           ▼ 1
┌──────────────────────┐
│   RubriqueBaremes    │
│   (PK: Id)           │
│   +UpdatedAt         │
│   +UpdatedBy         │
└──────────────────────┘

Tables NON créées en V009 (R1, reportées Phase 5) :
  - AgentAttributs
  - AgentRubriques
  - AvertissementsHistorique
  (design preview J3J § 8.3-8.5)
```

---

## 4. Plan d'implémentation (ordonné)

### 4.1 Étape 1 — Migration SQL

Créer `src/PaieEducation.Persistence.Migrations/V009__workbench_reglementaire.sql` :

1. CREATE TABLE `SourcesValeur` (§ 1.1)
2. CREATE TABLE `CriteresEligibilite` (§ 1.2)
3. CREATE TABLE `MessagesRegles` (§ 1.3)
4. CREATE TABLE `GroupesEligibilite` (§ 1.4) + index
5. ALTER TABLE `Rubriques` ADD COLUMN `SourceValeurId` (§ 2.1)
6. ALTER TABLE `ReglesEligibilite` ADD COLUMN `CritereId` (§ 2.2.1)
7. ALTER TABLE `ReglesEligibilite` ADD COLUMN `GroupeId` (§ 2.2.2)
8. UPDATE `ReglesEligibilite` SET `CritereId = ...` (§ 2.2.3) — jointure sur le seed
9. ALTER TABLE `ReglesEligibilite` DROP COLUMN `Critere` (§ 2.2.5) — *voir § 5 si SQLite < 3.35*
10. ALTER TABLE `RubriqueBaremes` ADD COLUMN `UpdatedAt` (§ 2.3.2)
11. ALTER TABLE `RubriqueBaremes` ADD COLUMN `UpdatedBy` (§ 2.3.3)
12. CREATE INDEX sur `ReglesEligibilite.CritereId` et `GroupeId` (§ 2.2 fin)
13. INSERT seed `SourcesValeur` (7 lignes, § 1.1)
14. INSERT seed `CriteresEligibilite` (10 lignes, § 1.2)

**Idempotence** : chaque CREATE/ALTER est gardé par un test d'existence (pattern V001-V008). Les seeds utilisent `INSERT OR IGNORE` (clé = `Id`).

### 4.2 Étape 2 — Value Objects domaine (Domain)

```
Domain/
├── ValueObjects/
│   ├── Bareme/
│   │   ├── BaremeValue.cs           # (rubrique, dimension, clé) avec résolution par date
│   │   ├── BaremeDimension.cs       # enum : CATEGORIE | ECHELON | ANCIENNETE | ...
│   │   └── BaremeTypeValeur.cs      # enum : TAUX | MONTANT
│   ├── SourceValeur.cs              # record(Id, Libelle, Description, Actif) + évaluation typée
│   ├── CritereEligibilite.cs        # record(Id, Libelle, TypeValeur, SourceResolution)
│   ├── MessageRegle.cs              # record(Id, Categorie, TexteFr, TexteAr, Source, DateEffet, DateFin)
│   ├── GroupeEligibilite.cs         # record(Id, RubriqueId, Severite, MessageId, Priorite, DateEffet, DateFin, Source, Hash)
│   ├── ConditionEligibilite.cs      # record(RubriqueId, CritereId, Operateur, Valeur, GroupeId?) avec DNF
│   └── PeriodeReglementaire.cs      # (DateEffet, DateFin) + validation chevauchement/trou
├── Services/
│   ├── BaremeResolver.cs
│   ├── SourceValeurResolver.cs
│   ├── RegleEligibiliteEvaluator.cs   # ET plat + DNF
│   └── ContinuiteTemporelle.cs        # validation périodes
└── Calculators/                       # Open/Closed
    ├── ISourceValeurCalculator.cs
    ├── NotationAgentCalculator.cs     # PAPP, PAPG, REND
    ├── AnciennetePubliqueCalculator.cs
    ├── AnciennetePriveeCalculator.cs
    ├── IndiceEchelonCalculator.cs
    ├── PointIndiciaireCalculator.cs
    ├── BaseAssietteCalculator.cs
    └── ConstanteReglementaireCalculator.cs
```

### 4.3 Étape 3 — Use cases de simulation (Application)

```
Application/
├── UseCases/
│   ├── SimulerEvolutionReglementaire.cs     # D8 — produit un RapportImpact (lecture seule)
│   └── DetecterContinuiteTemporelle.cs       # utilisé par Simulation + UI Phase 6
└── DTOs/
    └── RapportImpact.cs                     # (nbAgents, deltaMin, deltaMax, montantTotal, periode)
```

### 4.4 Étape 4 — Tests (Tests.Unit + Tests.Integration)

```
tests/
├── PaieEducation.Tests.Unit/
│   ├── Domain/
│   │   ├── RegleEligibiliteEvaluatorTests.cs    # ET plat + DNF (cas nominaux + bornes)
│   │   ├── BaremeResolverTests.cs
│   │   ├── SourceValeurResolverTests.cs
│   │   ├── ContinuiteTemporelleTests.cs
│   │   └── Calculators/
│   │       └── NotationAgentCalculatorTests.cs
│   └── Application/
│       └── SimulerEvolutionReglementaireTests.cs
└── PaieEducation.Tests.Integration/
    ├── V009UpgradeTests.cs                       # V008 → V009 (test d'upgrade)
    ├── V009SchemaTests.cs                        # tables, colonnes, FK, index
    └── V009SeedTests.cs                          # SourcesValeur (7), CriteresEligibilite (10)
```

**Critères d'acceptation des tests :**
- CA-R1 — Suite 117 existante reste verte
- CA-R2 — `SourcesValeur` requêtable par `Id` (pas `Code`)
- CA-R3 — `ReglesEligibilite` n'a plus de `CHECK IN (...)`
- CA-R4 — Aucun fichier ne référence `SourcesValeur.Code`, `CriteresEligibilite.Code`, `MessagesRegles.Code`, `ReglesEligibilite.Critere`
- CA-R5 — DICTIONNAIRE/J3I/J3J à jour
- CA-R6 — `MessagesRegles` a l'audit complet ; `SourcesValeur` et `CriteresEligibilite` ont l'audit minimal
- CA-R7 — `AgentAttributs`/`AgentRubriques`/`AvertissementsHistorique` non créées
- CA-R8 — Convention `Id` partout vérifiée par grep

---

## 5. Notes d'implémentation (gotchas)

### 5.1 DROP COLUMN en SQLite

SQLite a supporté `ALTER TABLE DROP COLUMN` à partir de la **version 3.35.0** (mars
2021). Si le SDK SQLite utilisé (via `Microsoft.Data.Sqlite`) est < 3.35, il faut
**reconstruire la table** :

```sql
-- Pattern de reconstruction (12 étapes — voir https://www.sqlite.org/lang_altertable.html#otheralter)
BEGIN TRANSACTION;
CREATE TABLE ReglesEligibilite_new (
    -- nouvelle structure (sans Critere, avec CritereId et GroupeId)
);
INSERT INTO ReglesEligibilite_new
    SELECT Id, CritereId, GroupeId, Operateur, Valeur, DateEffet, DateFin,
           Source, Hash, CreatedAt, CreatedBy
    FROM ReglesEligibilite;
DROP TABLE ReglesEligibilite;
ALTER TABLE ReglesEligibilite_new RENAME TO ReglesEligibilite;
-- recréer les index
COMMIT;
```

Le test d'upgrade doit explicitement vérifier la version de SQLite et adapter la
stratégie. C'est un point de friction acceptable.

### 5.2 Ordre des ALTER TABLE / UPDATE

La séquence critique est :
1. CREATE TABLE pour les nouvelles tables (1-4)
2. ALTER TABLE pour ajouter les nouvelles colonnes (5-7, 10-11)
3. UPDATE pour migrer les données (8) — **nécessite que les nouvelles tables existent**
4. ALTER TABLE DROP COLUMN (9) — **nécessite que la migration de données soit faite**

Cette séquence ne peut pas être réorganisée sans casser la migration.

### 5.3 Index sur colonnes ajoutées

Les index `IX_ReglesEligibilite_CritereId` et `IX_ReglesEligibilite_GroupeId` sont
créés **après** l'ALTER TABLE (et après l'UPDATE qui peuple `CritereId`) pour
bénéficier des données déjà migrées. Ordre : CREATE INDEX, pas avant l'UPDATE.

### 5.4 Convention ADR-0004 — `Id` partout

Vérification automatisée par grep dans le code review :
- `SourcesValeur\.Code` → 0 occurrence
- `CriteresEligibilite\.Code` → 0 occurrence
- `MessagesRegles\.Code` → 0 occurrence
- `ReglesEligibilite\.Critere[^I]` (pour ne pas matcher `CritereId`) → 0 occurrence
- `SourceValeurCode` → 0 occurrence (renommé en `SourceValeurId`)
- `MessageCode` (sauf dans DICOM) → 0 occurrence (renommé en `MessageId`)

### 5.5 Points à arbitrer en J3L (post-V009)

- `Cotisations.Code` (V005) : redondance possible avec `Cotisations.Id` — à vérifier
- Conventions de nommage des Ids de référentiels : tout en kebab-case (ex. `MSG-...`,
  `GE-...`) ou tout en PascalCase (ex. `MsgIssrp45Inconnu`) — actuellement hétérogène
- Format des messages bilingues : colonne `TexteAr` (V1) ou extension future
  (table `MessagesReglesTraductions`) — décision post-V1

---

## 6. Critères « Prêt à coder » (STOP & ASK final)

| Critère | Statut |
|---|:---:|
| ADR-0007 (Workbench réglementaire) accepté | ✅ |
| J3I v1.0 (concept Workbench) | ✅ |
| J3J v1.0 (refactor R1-R5) | ✅ |
| J3K v1.0 (cette spec — modèle final) | ⏳ **dernière validation utilisateur** |
| DICTIONNAIRE § 8ter refactoré | ✅ |
| PLAN_ACTION Phase 3bis § 1 refactoré | ✅ |
| Cohérence J3I/J3J/DICTIONNAIRE/PLAN_ACTION | ✅ (vérifié) |
| Décision V009-bis (renommage ou V009 + rev. 1) | 🟡 à arbitrer (recommandation : V009 + `SchemaVersions.Name = "workbench_reglementaire_rev1"`) |
| Convention `Id` vérifiée sur V001-V008 | 🟡 à vérifier (grep automatisé) — point V005 `Cotisations.Code` séparé |
| Décision portée sur le travail : **lancer l'implémentation V009** | ⏳ attente dernière validation J3K |

---

## 7. Index des références

| Document | Section | Utilité |
|---|---|---|
| `J3I_WORKBENCH_REGLEMENTAIRE.md` | § 5, § 7, § 8 | Concept Workbench, écrans, workflow d'évolution |
| `J3I_WORKBENCH_REGLEMENTAIRE.md` | § 9 (refactoré) | Vue d'ensemble V009 alignée avec ce doc |
| `J3J_REFACTORING_AVANT_V009.md` | § 3 (R1-R5) | Justification des refactors |
| `J3J_REFACTORING_AVANT_V009.md` | § 4 | Schéma V009 final (aligné avec ce doc) |
| `J3J_REFACTORING_AVANT_V009.md` | § 6 | Décisions validées par l'utilisateur |
| `J3J_REFACTORING_AVANT_V009.md` | § 8.3-8.5 | Design preview des tables reportées (Phase 5) |
| `DICTIONNAIRE_DONNEES.md` | § 8bis (V008) + § 8ter (V009) | Dictionnaire de données complet |
| `DICTIONNAIRE_DONNEES.md` | § 9.8-9.10 | Requêtes de résolution (barème, DNF, source) |
| `PLAN_ACTION.md` | Phase 3bis | Phasage et dépendances |
| `adr/0007-workbench-reglementaire.md` | Décisions 1-8 | Contrat fonctionnel V009 |
| `adr/0004-cles-metier-referentiels.md` | — | Convention `Id` (PK = code métier TEXT) |
| `adr/0005-moteur-calcul-synchrone.md` | — | Évaluateur pur et synchrone (signature) |
| `CONVENTIONS.md` | § 4, § 5 | Nommage et règles de code |
| `Reglementation/elements_paie_historique_14726/*` | — | Source de vérité des cas à modéliser |

---

*Dernière mise à jour : 15/07/2026 — v1.0 — en attente de dernière validation
utilisateur (STOP & ASK, § 6).*
