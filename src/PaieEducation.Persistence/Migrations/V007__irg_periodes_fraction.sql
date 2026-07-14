-- V007 — IRG : passage aux fractions exactes + ajout de la période « avant 2020-06-01 »
--
-- Motivations (Tome H §10, Tome C moteur de calcul, fichiers
-- `Reglementation/IRG_Algerie_2008_2026/CALCUL IRG ALGERIE.txt` et
-- `EXPLICATION BAREME IRG 2020 APPL (2).docx`) :
--
--   1. Les coefficients et constantes des lissages 2020+, 2021 et 2022+ sont
--      des **fractions** réglementaires exactes :
--        - 2020-06..2020-12 :  CoefGeneral = 8/3    ; ConstGeneral = 20000/3
--                              CoefSpecial = 5/3    ; ConstSpecial = 12500/3
--                              PlafondSpecial = 40 000
--        - 2021              :  CoefGeneral = 8/3    ; ConstGeneral = 20000/3
--                              CoefSpecial = 5/3    ; ConstSpecial = 12500/3
--                              PlafondSpecial = 42 500
--        - 2022+             :  CoefGeneral = 137/51 ; ConstGeneral = 27925/8
--                              CoefSpecial = 93/61  ; ConstSpecial = 81213/41
--                              PlafondSpecial = 42 500
--      Stocker en REAL `2.6666666666666665` perd la valeur réglementaire
--      (« 8/3 » ne se représente pas exactement en double). On stocke
--      désormais en TEXT la fraction canonique ("8/3", "20000/3", …).
--
--   2. V006 n'avait que 3 règles de période (2020, 2021, 2022+). Or le
--      pseudo-code IRG distingue **4 plages** :
--        - avant 2020-06-01                  : barème 2008 seul (pas
--                                              d'exonération, pas de lissage)
--        - 2020-06-01 → 2020-12-31           : lissage 8/3, plafond spé 40 000
--        - 2021-01-01 → 2021-12-31           : lissage 8/3, plafond spé 42 500
--        - 2022-01-01 → …                    : lissage 137/51, plafond spé 42 500
--      La 4e période est désormais possible (DateDebut sentinelle
--      "1000-01-01" pour « depuis toujours »).
--
--   3. La table est recréée (DROP + CREATE) plutôt qu'ALTER COLUMN ALTER TYPE
--      (non supporté en SQLite). En V1 (développement, pas d'UI de saisie) la
--      base est nécessairement vide de données IRG, donc DROP est sans perte.
--
--   4. Le **seed** des 4 règles de période + du barème 2008 n'est PAS dans
--      cette migration (volontairement) : on veut pouvoir tester chaque
--      composante isolément. Le seed canonique sera appliqué par la CLI
--      `tools/PaieEducation.Tools` en J2.d.

DROP TABLE IRGReglesPeriode;

CREATE TABLE IRGReglesPeriode (
    Id               TEXT    NOT NULL PRIMARY KEY,
    Code             TEXT    NOT NULL UNIQUE,
    Libelle          TEXT    NOT NULL,
    DateDebut        TEXT    NOT NULL,
    DateFin          TEXT,
    BaremeId         TEXT    NOT NULL REFERENCES BaremeIRG(Id),
    ExonerationSeuil INTEGER NOT NULL DEFAULT 0    CHECK (ExonerationSeuil >= 0),
    AbattementTaux   REAL    NOT NULL DEFAULT 0    CHECK (AbattementTaux >= 0 AND AbattementTaux <= 1),
    AbattementMin    INTEGER NOT NULL DEFAULT 0    CHECK (AbattementMin >= 0),
    AbattementMax    INTEGER NOT NULL DEFAULT 0    CHECK (AbattementMax >= AbattementMin),
    -- Coefficients et constantes stockés en TEXT sous forme de fraction
    -- canonique : "1" (= pas de transformation), "8/3", "20000/3", "137/51",
    -- "27925/8", "93/61", "81213/41". Le moteur parse et applique (Phase 4).
    -- Le CHECK interdit la valeur spéciale "0" pour les Coef (division par 0).
    CoefGeneral      TEXT    NOT NULL DEFAULT '1'  CHECK (CoefGeneral <> '0'),
    ConstGeneral     TEXT    NOT NULL DEFAULT '0',
    CoefSpecial      TEXT    NOT NULL DEFAULT '1'  CHECK (CoefSpecial <> '0'),
    ConstSpecial     TEXT    NOT NULL DEFAULT '0',
    PlafondSpecial   INTEGER NOT NULL DEFAULT 0    CHECK (PlafondSpecial >= 0),
    Source           TEXT,
    Hash             TEXT    NOT NULL,
    CreatedAt        TEXT    NOT NULL
);
CREATE INDEX IX_IRGReglesPeriode_DateDebut ON IRGReglesPeriode (DateDebut);
