-- V003 — Grille indiciaire (données réglementaires versionnées)
--
-- Toutes les valeurs monétaires/indices sont en DZD. Chaque ligne est
-- valide sur la plage [DateEffet, DateFin[ ; DateFin NULL = toujours en vigueur.
-- Le couple (XxxId, DateEffet) est UNIQUE : on ne peut avoir qu'une version
-- d'une valeur à une date d'effet donnée.
--
-- Résolution par date (utilisée par le moteur de paie) :
--   SELECT * FROM TableVersionnee
--   WHERE Id = $id AND DateEffet <= $date
--     AND (DateFin IS NULL OR DateFin >= $date)
--   ORDER BY DateEffet DESC LIMIT 1;
--
-- Les lignes sont immuables : si la valeur change, on insère une nouvelle
-- ligne avec une nouvelle DateEffet et on renseigne DateFin sur la précédente.

CREATE TABLE ValeurPoint (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. "VP-2007-01-01"
    DateEffet  TEXT NOT NULL,                     -- ISO 8601 YYYY-MM-DD
    DateFin    TEXT,                              -- ISO 8601 ; NULL = en vigueur
    Valeur     REAL NOT NULL CHECK (Valeur > 0),  -- DZD ; 45 DA par défaut (paramétrable)
    Version    TEXT NOT NULL,                     -- libellé version (ex. "2007", "2022-03")
    Source     TEXT,                              -- ex. "Décret 07-308"
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_ValeurPoint_DateEffet ON ValeurPoint (DateEffet);

CREATE TABLE GrilleIndiciaire (
    Id          TEXT NOT NULL PRIMARY KEY,        -- ex. "GI-7-2007-01-01"
    CategorieId TEXT NOT NULL REFERENCES Categories(Id),
    DateEffet   TEXT NOT NULL,
    DateFin     TEXT,
    IndiceMin   INTEGER NOT NULL CHECK (IndiceMin > 0),
    Version     TEXT NOT NULL,
    Source      TEXT,
    Hash        TEXT NOT NULL,
    CreatedAt   TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_GrilleIndiciaire_Categorie_DateEffet
    ON GrilleIndiciaire (CategorieId, DateEffet);

CREATE TABLE IndicesEchelon (
    Id         TEXT NOT NULL PRIMARY KEY,         -- ex. "IE-1-2007-01-01"
    EchelonId  TEXT NOT NULL REFERENCES Echelons(Id),
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Indice     INTEGER NOT NULL CHECK (Indice >= 0),
    Version    TEXT NOT NULL,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_IndicesEchelon_Echelon_DateEffet
    ON IndicesEchelon (EchelonId, DateEffet);
