-- V006 — Barème IRG (paramétrable) + règles de période + paramètres système
--
-- Modélisation conforme à Q4 et Q4b :
--   * Le barème 2008 est stocké UNE fois dans BaremeIRG / BaremeIRGTranches.
--   * Les règles de période (2020, 2021, 2022+) sont stockées dans
--     IRGReglesPeriode, chacune avec :
--         - ExonerationSeuil (IRG=0 si imposable <= seuil, ex. 30 000 DA)
--         - Abattement (40 % sur IRG brut, borné [AbattementMin ; AbattementMax])
--         - CoefGeneral / ConstGeneral : lissage "général" (formule Phase 4)
--         - CoefSpecial / ConstSpecial / PlafondSpecial : profil handicapé/retraité RG
--   * Pas de table de tranches séparée par période : c'est le moteur qui applique
--     les coefficients sur l'IRG brut calculé via le barème 2008.
--
-- Parametres : clé/valeur typés, versionnés, pour tous les réglages
-- transverses (arrondi, défauts, seuils globaux, etc.).

CREATE TABLE BaremeIRG (
    Id         TEXT NOT NULL PRIMARY KEY,           -- ex. "IRG-2008"
    Code       TEXT NOT NULL UNIQUE,
    Libelle    TEXT NOT NULL,
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);

CREATE TABLE BaremeIRGTranches (
    Id         TEXT NOT NULL PRIMARY KEY,
    BaremeId   TEXT NOT NULL REFERENCES BaremeIRG(Id),
    BorneInf   INTEGER NOT NULL CHECK (BorneInf >= 0),
    BorneSup   INTEGER,                            -- NULL = +infini
    Taux       REAL NOT NULL CHECK (Taux >= 0 AND Taux <= 1),
    Ordre      INTEGER NOT NULL CHECK (Ordre >= 1),
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_BaremeIRGTranches_Bareme_BorneInf
    ON BaremeIRGTranches (BaremeId, BorneInf);
CREATE UNIQUE INDEX IX_BaremeIRGTranches_Bareme_Ordre
    ON BaremeIRGTranches (BaremeId, Ordre);

CREATE TABLE IRGReglesPeriode (
    Id               TEXT NOT NULL PRIMARY KEY,
    Code             TEXT NOT NULL UNIQUE,         -- ex. "PERIODE-2020", "PERIODE-2021", "PERIODE-2022+"
    Libelle          TEXT NOT NULL,
    DateDebut        TEXT NOT NULL,
    DateFin          TEXT,
    BaremeId         TEXT NOT NULL REFERENCES BaremeIRG(Id),
    ExonerationSeuil INTEGER NOT NULL DEFAULT 0 CHECK (ExonerationSeuil >= 0),
    AbattementTaux   REAL    NOT NULL DEFAULT 0    CHECK (AbattementTaux >= 0 AND AbattementTaux <= 1),
    AbattementMin    INTEGER NOT NULL DEFAULT 0    CHECK (AbattementMin >= 0),
    AbattementMax    INTEGER NOT NULL DEFAULT 0    CHECK (AbattementMax >= AbattementMin),
    CoefGeneral      REAL    NOT NULL DEFAULT 1    CHECK (CoefGeneral > 0),
    ConstGeneral     INTEGER NOT NULL DEFAULT 0,
    CoefSpecial      REAL    NOT NULL DEFAULT 1    CHECK (CoefSpecial > 0),
    ConstSpecial     INTEGER NOT NULL DEFAULT 0,
    PlafondSpecial   INTEGER NOT NULL DEFAULT 0    CHECK (PlafondSpecial >= 0),
    Source           TEXT,
    Hash             TEXT NOT NULL,
    CreatedAt        TEXT NOT NULL
);
CREATE INDEX IX_IRGReglesPeriode_DateDebut ON IRGReglesPeriode (DateDebut);

CREATE TABLE Parametres (
    Id          TEXT NOT NULL PRIMARY KEY,
    Cle         TEXT NOT NULL UNIQUE,              -- ex. "ARRONDI_MODE", "VALEUR_POINT_DEFAUT"
    Valeur      TEXT NOT NULL,
    Type        TEXT NOT NULL CHECK (Type IN ('INT', 'REAL', 'BOOL', 'TEXT', 'DATE')),
    Description TEXT,
    DateEffet   TEXT NOT NULL,
    DateFin     TEXT,
    Source      TEXT,
    Hash        TEXT NOT NULL,
    CreatedAt   TEXT NOT NULL
);
