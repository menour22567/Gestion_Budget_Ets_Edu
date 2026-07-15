-- V008 — Extensions J3 approuvées le 14/07/2026 (docs/analysis/J3E, PLAN_ACTION « J3-plan »)
--
--   1. Rubriques : ajout de PeriodiciteVersement (INC-04 : PAPP/PAPG/rendement
--      sont « calculées mensuellement, servies trimestriellement ») et de la
--      valeur INDICE_ECHELON dans BaseCalcul (Q2-rev : IEP_FONC = IE × VPI,
--      l'assiette est l'indice d'échelon lui-même, pas un traitement).
--      SQLite ne sait pas modifier un CHECK → DROP + CREATE, même précédent
--      que V007 (en V1 la base est vide de données rubriques au moment de la
--      migration ; le seed canonique arrive après, via la CLI).
--
--   2. ReglesEligibilite : critères étendus ORIGINE_CORPS (ISSRP 45 % —
--      « grades de promotion dont l'origine statutaire est le corps
--      enseignant », non décidable par le seul corps, Q-03) et
--      TYPE_ETABLISSEMENT (ind. de direction : primaire/collège/lycée).
--
--   3. RubriqueBaremes : barèmes par tranche de critère (J3E §3) — IFC par
--      catégorie, documentation pédagogique par catégorie, direction par type
--      d'établissement, qualification 40/45 %, soutien paramédical… Valeur en
--      TEXT canonique (fraction ou décimal) parsée par le moteur, comme V007.
--
--   4. GradeAttributs : attributs versionnés de grade (J3E §4) — ex.
--      ORIGINE_ENSEIGNANTE_POSSIBLE=1 pour pré-paramétrer la matrice ISSRP.
--
-- Les FK des tables filles (RubriqueFormules, RubriqueParametres,
-- RubriqueDependances, CotisationAssietteRubriques) référencent Rubriques par
-- nom : la recréation sous le même nom les laisse valides.

DROP TABLE ReglesEligibilite;
DROP TABLE Rubriques;

CREATE TABLE Rubriques (
    Id                   TEXT NOT NULL PRIMARY KEY,  -- ex. "IEP_FONC", "PAPP", "IRG", "ISSRP_45"
    Libelle              TEXT NOT NULL,
    Nature               TEXT NOT NULL CHECK (Nature IN ('GAIN', 'RETENUE', 'COTISATION', 'IMPOT')),
    BaseCalcul           TEXT NOT NULL CHECK (BaseCalcul IN
                           ('TRAITEMENT', 'TBASE', 'TBASE_ECHELON', 'INDICE_ECHELON',
                            'FORFAIT', 'ASSIETTE_COTISABLE', 'ASSIETTE_IMPOSABLE')),
    Periodicite          TEXT NOT NULL CHECK (Periodicite IN
                           ('MENSUELLE', 'TRIMESTRIELLE', 'ANNUELLE', 'PONCTUELLE')),
    -- Périodicité de service (versement). NULL = identique à Periodicite (calcul).
    -- Ex. PAPP : Periodicite=MENSUELLE (calcul), PeriodiciteVersement=TRIMESTRIELLE.
    PeriodiciteVersement TEXT CHECK (PeriodiciteVersement IS NULL OR PeriodiciteVersement IN
                           ('MENSUELLE', 'TRIMESTRIELLE', 'ANNUELLE', 'PONCTUELLE')),
    OrdreCalcul          INTEGER NOT NULL CHECK (OrdreCalcul >= 0),
    EstImposable         INTEGER NOT NULL DEFAULT 0 CHECK (EstImposable IN (0, 1)),
    EstCotisable         INTEGER NOT NULL DEFAULT 0 CHECK (EstCotisable IN (0, 1)),
    Description          TEXT,
    Actif                INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0, 1)),
    CreatedAt            TEXT NOT NULL,
    UpdatedAt            TEXT,
    Source               TEXT,
    Hash                 TEXT NOT NULL
);

CREATE TABLE ReglesEligibilite (
    Id         TEXT NOT NULL PRIMARY KEY,
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    Critere    TEXT NOT NULL CHECK (Critere IN
                  ('FILIERE', 'CORPS', 'GRADE', 'CATEGORIE', 'FONCTION',
                   'TYPE_CONTRAT', 'ECHELON', 'ANCIENNETE',
                   'ORIGINE_CORPS', 'TYPE_ETABLISSEMENT')),
    Operateur  TEXT NOT NULL CHECK (Operateur IN
                  ('=', 'IN', 'NOT_IN', '>=', '<=', '>', '<')),
    Valeur     TEXT NOT NULL,
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE INDEX IX_ReglesEligibilite_RubriqueId ON ReglesEligibilite (RubriqueId);
CREATE INDEX IX_ReglesEligibilite_Critere_Valeur ON ReglesEligibilite (Critere, Valeur);

CREATE TABLE RubriqueBaremes (
    Id         TEXT NOT NULL PRIMARY KEY,           -- ex. "RB-IFC-2015-CAT-7"
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    Dimension  TEXT NOT NULL CHECK (Dimension IN
                  ('CATEGORIE', 'ECHELON', 'ANCIENNETE', 'TYPE_ETABLISSEMENT', 'CORPS')),
    BorneInf   TEXT NOT NULL,                       -- "7" ou "PRIMAIRE" (discret : BorneInf = BorneSup)
    BorneSup   TEXT,                                -- NULL = +infini
    TypeValeur TEXT NOT NULL CHECK (TypeValeur IN ('TAUX', 'MONTANT')),
    Valeur     TEXT NOT NULL,                       -- décimal/fraction canonique, parsé par le moteur
    DateEffet  TEXT NOT NULL,
    DateFin    TEXT,
    Source     TEXT,
    Hash       TEXT NOT NULL,
    CreatedAt  TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_RubriqueBaremes_Rub_Dim_Borne_Date
    ON RubriqueBaremes (RubriqueId, Dimension, BorneInf, DateEffet);
CREATE INDEX IX_RubriqueBaremes_RubriqueId ON RubriqueBaremes (RubriqueId);

CREATE TABLE GradeAttributs (
    Id        TEXT NOT NULL PRIMARY KEY,            -- ex. "GA-CDE-G011-ORIGINE"
    GradeId   TEXT NOT NULL REFERENCES Grades(Id),
    Attribut  TEXT NOT NULL,                        -- ex. "ORIGINE_ENSEIGNANTE_POSSIBLE"
    Valeur    TEXT NOT NULL,
    DateEffet TEXT NOT NULL,
    DateFin   TEXT,
    Source    TEXT,
    Hash      TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_GradeAttributs_Grade_Attribut_DateEffet
    ON GradeAttributs (GradeId, Attribut, DateEffet);
