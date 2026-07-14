-- V002 — Nomenclature : structure organique & carrière
--
-- Tables statiques (sans date d'effet) : la donnée est vraie depuis sa création
-- et n'a pas de version historique. L'audit se fait via Source/Hash/CreatedAt.
-- Si une valeur change (ex. libellé d'un corps), on ne modifie pas la ligne
-- existante : on en crée une nouvelle et on bascule l'ancienne sur Actif=0.
--
-- Conventions :
--   * Id TEXT PRIMARY KEY = code métier lisible et stable (ex. "PEM", "1", "HC-S1")
--   * Libelle TEXT NOT NULL = libellé humain
--   * Actif INTEGER (0/1) NOT NULL DEFAULT 1
--   * Source TEXT = origine de la donnée (décret, fichier seed)
--   * Hash TEXT NOT NULL = SHA-256 calculé par le seed pour détecter les dérives
--   * CreatedAt / UpdatedAt en ISO 8601 UTC
--
-- Les dates sont stockées en TEXT (ISO 8601 YYYY-MM-DD) : SQLite n'a pas de
-- type DATE natif, et le tri lexicographique sur ISO 8601 coincide avec le
-- tri chronologique, ce qui simplifie les requêtes de résolution par date.

-- ---------------------------------------------------------------------------
-- Tables racines (sans FK)
-- ---------------------------------------------------------------------------

CREATE TABLE Filieres (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. "ENSEIGNANT", "ADMIN"
    Libelle    TEXT NOT NULL,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);

CREATE TABLE TypesContrat (
    Id         TEXT NOT NULL PRIMARY KEY,         -- "STATUTAIRE" | "CONTRACTUEL"
    Libelle    TEXT NOT NULL,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);

CREATE TABLE TypesPersonnel (
    Id         TEXT NOT NULL PRIMARY KEY,
    Libelle    TEXT NOT NULL,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);

CREATE TABLE Fonctions (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. "DIRECTEUR", "CENSEUR"
    Libelle    TEXT NOT NULL,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);

CREATE TABLE Echelons (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. "1", "2", ..., "12"
    Numero     INTEGER NOT NULL UNIQUE CHECK (Numero BETWEEN 1 AND 12),
    Libelle    TEXT NOT NULL,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);

CREATE TABLE Categories (
    Id            TEXT NOT NULL PRIMARY KEY,      -- ex. "1".."17", "HC-S1", "HC-S2"
    Niveau        INTEGER NOT NULL UNIQUE CHECK (Niveau BETWEEN 1 AND 19),
    Libelle       TEXT NOT NULL,
    HorsCategorie INTEGER NOT NULL DEFAULT 0 CHECK (HorsCategorie IN (0, 1)),
    Actif         INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt     TEXT NOT NULL,
    UpdatedAt     TEXT,
    Source        TEXT,
    Hash          TEXT NOT NULL
);

-- ---------------------------------------------------------------------------
-- Tables avec FK (les parents doivent déjà exister)
-- ---------------------------------------------------------------------------

CREATE TABLE Corps (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. "PEM", "PES", "PELP"
    Libelle    TEXT NOT NULL,
    FiliereId  TEXT NOT NULL REFERENCES Filieres(Id),
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);
CREATE INDEX IX_Corps_FiliereId ON Corps (FiliereId);

CREATE TABLE Grades (
    Id         TEXT NOT NULL PRIMARY KEY,
    Libelle    TEXT NOT NULL,
    CorpsId    TEXT NOT NULL REFERENCES Corps(Id),
    Ordre      INTEGER NOT NULL CHECK (Ordre >= 1),
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);
CREATE INDEX IX_Grades_CorpsId ON Grades (CorpsId);
CREATE UNIQUE INDEX IX_Grades_CorpsId_Ordre ON Grades (CorpsId, Ordre);

CREATE TABLE Etablissements (
    Id         TEXT NOT NULL PRIMARY KEY,         -- code interne (ex. "LYC001")
    Nom        TEXT NOT NULL,
    Type       TEXT NOT NULL,                     -- Lycée, CEM, Primaire...
    Adresse    TEXT,
    Telephone  TEXT,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);
