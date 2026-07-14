-- V005 — Éligibilité des rubriques (par corps/grade/catégorie/fonction) + Cotisations paramétrables
--
-- ReglesEligibilite : la même rubrique peut avoir des règles différentes selon
--   le profil de l'agent (corps, grade, échelon, ancienneté, ...). C'est ce qui
--   permet, par exemple, les 3 taux d'ISSRP (45 % / 30 % / 15 %) en modélisant
--   3 rubriques distinctes (ISSRP_45, ISSRP_30, ISSRP_15) — chacune avec sa règle
--   d'éligibilité pointant sur son groupe de corps.
--
-- Cotisations : paramétrage fin des retenues (taux + assiette), conformément
--   à Q3. La cotisation SS part ouvrière (9 %) est un cas particulier stocké
--   ici comme n'importe quelle autre cotisation. Les retenues facultatives
--   (mutuelle, œuvres sociales) sont en TypeCotisation = 'FACULTATIVE' et
--   AssietteRef = 'MONTANT_FIXE' (Q3b).

CREATE TABLE ReglesEligibilite (
    Id         TEXT NOT NULL PRIMARY KEY,
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    Critere    TEXT NOT NULL CHECK (Critere IN
                  ('FILIERE', 'CORPS', 'GRADE', 'CATEGORIE', 'FONCTION',
                   'TYPE_CONTRAT', 'ECHELON', 'ANCIENNETE')),
    Operateur  TEXT NOT NULL CHECK (Operateur IN
                  ('=', 'IN', 'NOT_IN', '>=', '<=', '>', '<')),
    Valeur     TEXT NOT NULL,                       -- ex. "PEM" (=) ou "PEM,PES,INSPECTION" (IN)
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE INDEX IX_ReglesEligibilite_RubriqueId ON ReglesEligibilite (RubriqueId);
CREATE INDEX IX_ReglesEligibilite_Critere_Valeur ON ReglesEligibilite (Critere, Valeur);

CREATE TABLE Cotisations (
    Id             TEXT NOT NULL PRIMARY KEY,       -- ex. "SS", "RETRAITE", "MUTUELLE", "OEUVRES_SOCIALES"
    Code           TEXT NOT NULL,
    Libelle        TEXT NOT NULL,
    TypeCotisation TEXT NOT NULL CHECK (TypeCotisation IN
                    ('OBLIGATOIRE_SALARIALE', 'OBLIGATOIRE_PATRONALE', 'FACULTATIVE')),
    Taux           REAL,                            -- nullable (montant fixe pour FACULTATIVE)
    AssietteRef    TEXT NOT NULL CHECK (AssietteRef IN
                    ('ASSIETTE_COTISABLE', 'ASSIETTE_IMPOSABLE',
                     'TRAITEMENT_BASE', 'TRAITEMENT_BRUT', 'MONTANT_FIXE')),
    EstRetenue     INTEGER NOT NULL DEFAULT 1 CHECK (EstRetenue IN (0, 1)),
    DateEffet      TEXT NOT NULL,
    DateFin        TEXT,
    Source         TEXT,
    Hash           TEXT NOT NULL,
    CreatedAt      TEXT NOT NULL,
    CHECK (Taux IS NULL OR (Taux >= 0 AND Taux <= 1))
);
CREATE UNIQUE INDEX IX_Cotisations_Code_DateEffet
    ON Cotisations (Code, DateEffet);

CREATE TABLE CotisationAssietteRubriques (
    Id           TEXT NOT NULL PRIMARY KEY,
    CotisationId TEXT NOT NULL REFERENCES Cotisations(Id),
    RubriqueId   TEXT NOT NULL REFERENCES Rubriques(Id),
    Incluse      INTEGER NOT NULL DEFAULT 1 CHECK (Incluse IN (0, 1)),
    DateEffet    TEXT NOT NULL,
    DateFin      TEXT,
    Source       TEXT,
    Hash         TEXT NOT NULL,
    CreatedAt    TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_CotisationAssietteRubriques_Cotisation_Rubrique_DateEffet
    ON CotisationAssietteRubriques (CotisationId, RubriqueId, DateEffet);
