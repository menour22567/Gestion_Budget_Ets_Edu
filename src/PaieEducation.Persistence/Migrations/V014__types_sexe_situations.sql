-- V014 — TypesSexe, SituationsFamiliales
--
-- Lot 1.3 : externaliser les validations énumérées (SexesValides,
-- SituationsValides) dans la base de données. Suit le même schéma que
-- TypesContrat (V002) : Id = code métier, Libelle = libellé humain.
-- Les contraintes CHECK sur Agents (V011) restent en place comme filet de
-- sécurité ; la validation applicative s'appuie désormais sur ces tables.

CREATE TABLE TypesSexe (
    Id         TEXT NOT NULL PRIMARY KEY,         -- "M" | "F"
    Libelle    TEXT NOT NULL,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);

CREATE TABLE SituationsFamiliales (
    Id         TEXT NOT NULL PRIMARY KEY,         -- "CELIBATAIRE" | "MARIE" | "DIVORCE" | "VEUF"
    Libelle    TEXT NOT NULL,
    Actif      INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL
);
