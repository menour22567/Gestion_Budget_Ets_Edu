-- V009 — Workbench réglementaire (ADR-0007, J3I v1.0, J3J v1.0, J3K v1.0)
--
-- Validé utilisateur 14-15/07/2026. Refactor R1-R5 appliqué :
--   R1 — Tables de gestion agent (AgentAttributs / AgentRubriques /
--        AvertissementsHistorique) NON créées en V009 — créées avec `Agents`
--        en Phase 5. Design preview J3J § 8.3-8.5.
--   R2 — PK = `Id` partout (ADR-0004) ; convention homogène avec V002.
--   R3 — `ReglesEligibilite.Critere` (TEXT + CHECK) SUPPRIMÉ, remplacé par
--        `CritereId` (FK vers CriteresEligibilite.Id) — source unique de vérité.
--   R4 — Audit au juste niveau : catalogues techniques (SourcesValeur,
--        CriteresEligibilite) = audit minimal (Actif + CreatedAt/By) ;
--        texte réglementaire (MessagesRegles) = audit complet préservé
--        (Source + DateEffet/Fin).
--   R5 — YAGNI strict : aucune table/colonne "au cas où".
--
-- Cohérence J3I / J3H : J3H § 1 dit déjà "tables de gestion créées avec
-- Agents en Phase 5". La version refactorée de J3I § 9 / DICTIONNAIRE § 8ter
-- rétablit cette cohérence (CA-R7 J3J).
--
-- Le projet est en développement (pas de prod, pas de données à préserver) :
-- le DROP + CREATE de `ReglesEligibilite` est sans coût (V008 ne seed pas
-- cette table, les seeds canoniques sont gérés par la CLI en Phase 2).
--
-- V008 a déjà fait DROP + CREATE de `ReglesEligibilite` pour étendre le CHECK
-- (ORIGINE_CORPS, TYPE_ETABLISSEMENT). V009 reproduit le même pattern pour
-- R3 (FK + DNF) : c'est l'idiome de modification de CHECK dans ce projet.
--
-- Cette migration est auto-découverte par MigrationLoader.LoadFromAssembly
-- via le pattern <EmbeddedResource Include="Migrations\*.sql" />.

-- ──────────────────────────────────────────────────────────────────────────
-- ÉTAPE 1 — Nouveaux catalogues
-- ──────────────────────────────────────────────────────────────────────────

-- 1.1 SourcesValeur : catalogue technique des sources de valeur (P3 — % indexé
--     sur une source externe). Remplace les calculateurs typés VB (PAPPCalculator)
--     par un mécanisme générique + DI. D6 + R2 + R4 révisé (audit minimal).
CREATE TABLE SourcesValeur (
    Id          TEXT NOT NULL PRIMARY KEY,        -- ex. 'NOTATION_AGENT', 'POINT_INDICIAIRE'
    Libelle     TEXT NOT NULL,
    Description TEXT,
    Actif       INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt   TEXT NOT NULL,                    -- ISO 8601 UTC
    CreatedBy   TEXT NOT NULL
);
-- Pas de DateEffet / DateFin / Source / Hash : catalogue technique, pas une
-- valeur réglementaire. (R4 révisé.)

-- 1.2 CriteresEligibilite : dictionnaire des critères d'éligibilité. Remplace
--     le `CHECK IN (...)` en dur de `ReglesEligibilite.Critere` (R3). Source
--     unique de vérité : un nouveau critère = INSERT dans ce dictionnaire, pas
--     de migration. D3 + R2 + R3 + R4 révisé (audit minimal).
CREATE TABLE CriteresEligibilite (
    Id               TEXT NOT NULL PRIMARY KEY,   -- ex. 'CORPS', 'GRADE', 'ORIGINE_STATUTAIRE'
    Libelle          TEXT NOT NULL,
    TypeValeur       TEXT NOT NULL CHECK (TypeValeur IN ('TEXT', 'INT', 'DATE', 'ENUM')),
    SourceResolution TEXT NOT NULL CHECK (SourceResolution IN
                         ('ATTRIBUT_AGENT', 'ATTRIBUT_GRADE', 'CARRIERE', 'CALCULE')),
    Actif            INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt        TEXT NOT NULL,
    CreatedBy        TEXT NOT NULL
);

-- 1.3 MessagesRegles : messages paramétrables (multilingues) pour les règles
--     d'éligibilité et les avertissements. Nature : texte réglementaire
--     (peut citer verbatim un décret). Audit complet préservé (R4 révisé).
CREATE TABLE MessagesRegles (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. 'MSG-ISSRP-45-INCONNU-ORIGINE'
    Categorie  TEXT NOT NULL CHECK (Categorie IN ('ELIGIBILITE', 'AVERTISSEMENT', 'SUGGESTION')),
    TexteFr    TEXT NOT NULL,
    TexteAr    TEXT,                              -- nullable, post-V1
    Source     TEXT NOT NULL,                     -- référence réglementaire (décret/arrêté)
    DateEffet  TEXT NOT NULL,                     -- versioning du wording
    DateFin    TEXT,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    CreatedBy  TEXT NOT NULL
);

-- 1.4 GroupesEligibilite : en-tête de groupe DNF (D5, J3H §2). Conditions d'un
--     groupe ETées, groupes OUés. Nature : règle réglementaire, audit complet.
CREATE TABLE GroupesEligibilite (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. 'GE-ISSRP45-ORIGINE'
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    Severite   TEXT NOT NULL DEFAULT 'INFO' CHECK (Severite IN
                   ('INFO', 'RECOMMANDEE', 'OBLIGATOIRE_REGLEMENTAIRE')),   -- D2
    MessageId  TEXT REFERENCES MessagesRegles(Id),                         -- nullable
    Priorite   INTEGER NOT NULL DEFAULT 100,
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL,
    CreatedBy  TEXT NOT NULL
);
CREATE INDEX IX_GroupesEligibilite_RubriqueId ON GroupesEligibilite (RubriqueId);
CREATE INDEX IX_GroupesEligibilite_MessageId  ON GroupesEligibilite (MessageId);

-- ──────────────────────────────────────────────────────────────────────────
-- ÉTAPE 2 — DROP + CREATE de ReglesEligibilite (R3)
-- ──────────────────────────────────────────────────────────────────────────
--
-- SQLite ne sait pas modifier un CHECK ni ajouter/supprimer une colonne
-- référencée par un CHECK sans reconstruire la table. V008 a déjà utilisé ce
-- pattern (cf. V008__rubriques_v2_baremes.sql en-tête). En V1 la base est
-- vide de données ReglesEligibilite au moment de la migration (les seeds
-- canoniques sont gérés par la CLI en Phase 2).

DROP TABLE ReglesEligibilite;

CREATE TABLE ReglesEligibilite (
    Id         TEXT NOT NULL PRIMARY KEY,
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    -- R3 : `Critere` (TEXT + CHECK) SUPPRIMÉ. La sémantique est portée par
    --      `CritereId` (FK vers CriteresEligibilite.Id) + le dictionnaire.
    CritereId  TEXT NOT NULL REFERENCES CriteresEligibilite(Id),
    -- L-M2 (J3H §2, D5) : `GroupeId` permet la DNF. NULL = condition commune
    --      (ET plat V008 inchangé) ; non NULL = condition membre d'un groupe
    --      (ET dans le groupe, OU entre groupes).
    GroupeId   TEXT REFERENCES GroupesEligibilite(Id),
    Operateur  TEXT NOT NULL CHECK (Operateur IN
                   ('=', 'IN', 'NOT_IN', '>=', '<=', '>', '<')),
    Valeur     TEXT NOT NULL,                     -- ex. "PEM" (=) ou "PEM,PES,INSPECTION" (IN)
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE INDEX IX_ReglesEligibilite_RubriqueId ON ReglesEligibilite (RubriqueId);
CREATE INDEX IX_ReglesEligibilite_CritereId  ON ReglesEligibilite (CritereId);
CREATE INDEX IX_ReglesEligibilite_GroupeId   ON ReglesEligibilite (GroupeId);

-- ──────────────────────────────────────────────────────────────────────────
-- ÉTAPE 3 — Amendements ALTER TABLE (additif, pas de DROP nécessaire)
-- ──────────────────────────────────────────────────────────────────────────

-- 3.1 Rubriques : ajout SourceValeurId (L-M1, D6, R2).
--     NULL par défaut (P1, P2, P4-P7 : formule self-contained).
--     Non NULL uniquement pour P3 (% indexé sur une source externe).
ALTER TABLE Rubriques ADD COLUMN SourceValeurId TEXT REFERENCES SourcesValeur(Id);
CREATE INDEX IX_Rubriques_SourceValeurId ON Rubriques (SourceValeurId);

-- 3.2 RubriqueBaremes : ajout colonnes d'audit (L-M3, alignement AuditLog V001).
--     CreatedBy existe déjà en V008 — non modifié.
ALTER TABLE RubriqueBaremes ADD COLUMN UpdatedAt TEXT;
ALTER TABLE RubriqueBaremes ADD COLUMN UpdatedBy TEXT;

-- ──────────────────────────────────────────────────────────────────────────
-- ÉTAPE 4 — Seed des catalogues (R4 révisé)
-- ──────────────────────────────────────────────────────────────────────────
--
-- Catalogues techniques (R4) : CreatedAt = '2026-07-15T00:00:00Z', CreatedBy = 'system'.
-- Pas de DateEffet ni Source (audit minimal, catalogue non réglementaire).

-- 4.1 SourcesValeur (7 codes V1)
INSERT OR IGNORE INTO SourcesValeur (Id, Libelle, Description, Actif, CreatedAt, CreatedBy) VALUES
    ('NOTATION_AGENT',         'Note de l''agent',              'Résolu via DALNotation (PAPP, PAPG, REND)',              1, '2026-07-15T00:00:00Z', 'system'),
    ('ANCIENNETE_PUBLIQUE',    'Ancienneté publique (années)',  'Service public antérieur',                                1, '2026-07-15T00:00:00Z', 'system'),
    ('ANCIENNETE_PRIVEE',      'Ancienneté privée (années)',    'Service privé antérieur (D3)',                            1, '2026-07-15T00:00:00Z', 'system'),
    ('INDICE_ECHELON',         'Indice d''échelon',             'Depuis T_Indices_Echelons à la date de paie',            1, '2026-07-15T00:00:00Z', 'system'),
    ('POINT_INDICIAIRE',       'Valeur du point indiciaire',    'Depuis T_ValeurPoint à la date de paie',                 1, '2026-07-15T00:00:00Z', 'system'),
    ('BASE_ASSIETTE',          'Base d''assiette',              'Snapshot de l''assiette courante (cotisable/imposable)',  1, '2026-07-15T00:00:00Z', 'system'),
    ('CONSTANTE_REGLEMENTAIRE', 'Constante réglementaire',       'Taux/plafond/borne lu en base (RubriqueParametres)',     1, '2026-07-15T00:00:00Z', 'system');

-- 4.2 CriteresEligibilite (10 codes V1 — D3 + J3E)
INSERT OR IGNORE INTO CriteresEligibilite (Id, Libelle, TypeValeur, SourceResolution, Actif, CreatedAt, CreatedBy) VALUES
    ('FILIERE',            'Filière',                  'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('CORPS',              'Corps',                    'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('GRADE',              'Grade',                    'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('CATEGORIE',          'Catégorie',                'INT',  'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('FONCTION',           'Fonction',                 'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('TYPE_CONTRAT',       'Type de contrat',          'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('ECHELON',            'Échelon',                  'INT',  'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('ANCIENNETE',         'Ancienneté (années)',      'INT',  'CALCULE',        1, '2026-07-15T00:00:00Z', 'system'),
    ('TYPE_ETABLISSEMENT', 'Type d''établissement',    'ENUM', 'CARRIERE',       1, '2026-07-15T00:00:00Z', 'system'),
    ('ORIGINE_STATUTAIRE', 'Origine statutaire',       'ENUM', 'ATTRIBUT_AGENT', 1, '2026-07-15T00:00:00Z', 'system');

-- 4.3 MessagesRegles : vide (catalogue géré par Workbench D7, seed applicatif
--     au fil des besoins — R4 révisé texte réglementaire, CreatedBy=system
--     + Source obligatoire dès l'insertion).

-- 4.4 GroupesEligibilite : vide (idem, géré par Workbench D7).

-- ──────────────────────────────────────────────────────────────────────────
-- ÉTAPE 5 — R1 : pas d'amorce de tables de gestion
-- ──────────────────────────────────────────────────────────────────────────
--
-- AgentAttributs, AgentRubriques, AvertissementsHistorique sont créées avec
-- `Agents` en Phase 5. Design preview : docs/analysis/J3J § 8.3-8.5.
-- Cohérence J3H § 1 (qui dit déjà "tables de gestion créées avec Agents").

