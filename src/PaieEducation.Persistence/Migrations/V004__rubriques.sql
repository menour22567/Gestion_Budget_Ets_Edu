-- V004 — Rubriques : éléments de paie et leurs formules / paramètres / dépendances
--
-- Modèle :
--   * Rubriques            : table racine, statique (l'identité d'une rubrique
--                            est stable : IEP reste IEP, IRG reste IRG).
--                            Ce qui change dans le temps (taux, seuils, formules)
--                            vit dans les tables filles versionnées.
--   * RubriqueFormules     : expression de calcul (texte) à une date d'effet.
--                            Évaluée par le FormulaEngine en Phase 4 — aucune
--                            formule n'est codée en dur.
--   * RubriqueParametres   : couples (clé, valeur) versionnés. Permettent
--                            taux, seuils, forfaits pilotés en base.
--   * RubriqueDependances  : arêtes du graphe DAG (la rubrique A dépend de B
--                            pour son calcul). Détection de cycles au runtime.

CREATE TABLE Rubriques (
    Id           TEXT NOT NULL PRIMARY KEY,        -- ex. "IEP", "PAPP", "IRG", "ISSRP_45"
    Libelle      TEXT NOT NULL,
    Nature       TEXT NOT NULL CHECK (Nature IN ('GAIN', 'RETENUE', 'COTISATION', 'IMPOT')),
    BaseCalcul   TEXT NOT NULL CHECK (BaseCalcul IN
                    ('TRAITEMENT', 'TBASE', 'TBASE_ECHELON', 'FORFAIT', 'ASSIETTE_COTISABLE', 'ASSIETTE_IMPOSABLE')),
    Periodicite  TEXT NOT NULL CHECK (Periodicite IN
                    ('MENSUELLE', 'TRIMESTRIELLE', 'ANNUELLE', 'PONCTUELLE')),
    OrdreCalcul  INTEGER NOT NULL CHECK (OrdreCalcul >= 0),
    EstImposable INTEGER NOT NULL DEFAULT 0 CHECK (EstImposable IN (0, 1)),
    EstCotisable INTEGER NOT NULL DEFAULT 0 CHECK (EstCotisable IN (0, 1)),
    Description  TEXT,                              -- explication humaine de la rubrique
    Actif        INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt    TEXT NOT NULL,
    UpdatedAt    TEXT,
    Source       TEXT,
    Hash         TEXT NOT NULL
);

CREATE TABLE RubriqueFormules (
    Id         TEXT NOT NULL PRIMARY KEY,          -- ex. "RF-IEP-2007-01-01"
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Expression TEXT NOT NULL,                      -- ex. "round(TBASE * TAUX_IEP * ECH, 2)"
    Ordre      INTEGER NOT NULL DEFAULT 0,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_RubriqueFormules_Rubrique_DateEffet
    ON RubriqueFormules (RubriqueId, DateEffet);

CREATE TABLE RubriqueParametres (
    Id         TEXT NOT NULL PRIMARY KEY,          -- ex. "RP-IEP-TAUX-2007-01-01"
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    Cle        TEXT NOT NULL,                      -- ex. "TAUX", "SEUIL_MIN", "FORFAIT_CAT_1"
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Valeur     TEXT NOT NULL,                      -- texte pour porter n'importe quel type
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_RubriqueParametres_Rubrique_Cle_DateEffet
    ON RubriqueParametres (RubriqueId, Cle, DateEffet);

CREATE TABLE RubriqueDependances (
    Id         TEXT NOT NULL PRIMARY KEY,
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    DependDeId TEXT NOT NULL REFERENCES Rubriques(Id),
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL,
    CHECK (RubriqueId <> DependDeId)              -- pas d'auto-dépendance
);
CREATE UNIQUE INDEX IX_RubriqueDependances_Rubrique_DependDe_DateEffet
    ON RubriqueDependances (RubriqueId, DependDeId, DateEffet);
CREATE INDEX IX_RubriqueDependances_DependDeId
    ON RubriqueDependances (DependDeId);
