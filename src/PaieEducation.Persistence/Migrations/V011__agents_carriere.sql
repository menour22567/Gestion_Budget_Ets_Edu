-- V011 — Agents, Carrière, Période (Phase 5, jalon D)
--
-- Fondation manquante identifiée après J4.d : le moteur de calcul (Phase 4)
-- fonctionne aujourd'hui avec un `AgentContext` construit à la main par les
-- appelants — sa propre doc dit « construit par la couche Application à
-- partir de la base », mais cette base n'existait pas. Cette migration la
-- crée. Conception discutée avec le Tome B — Modèle de Domaine (V4, vol. 8
-- §4-§12) puis validée par STOP&ASK (4 écarts assumés par rapport au
-- diagramme conceptuel, cf. le dossier de conception associé) :
--
--   D-A — Agent = identité pure. Aucune colonne de carrière (Grade/Corps/
--         Échelon...) dupliquée sur Agents : tout est résolu via Carrieres,
--         au même schéma point-in-time que GrilleIndiciaire/IndicesEchelon.
--   D-B — Une seule table Carrieres (poste + affectation), cohérente avec
--         le seed V009 réel où TYPE_ETABLISSEMENT est déjà résolu en
--         SourceResolution='CARRIERE'.
--   D-C — Agrégat Contrat différé : TypeContrat reste un champ de Carrieres.
--   D-D — AgentAttributs/AgentRubriques/AgentRubriqueParametres/
--         AvertissementsHistorique créées ici, DDL portée verbatim depuis
--         docs/analysis/J3H_MODELE_AFFECTATION.md §4/7/8 (validée, zéro
--         question ouverte restante sur ces 4 tables).
--
-- Cette migration est auto-découverte par MigrationLoader.LoadFromAssembly
-- via le pattern <EmbeddedResource Include="Migrations\*.sql" />.

-- ──────────────────────────────────────────────────────────────────────────
-- Agents (gestion, ADR-0004 : PK GUID) — identité pure.
-- ──────────────────────────────────────────────────────────────────────────

CREATE TABLE Agents (
    Id                 TEXT NOT NULL PRIMARY KEY,
    Matricule          TEXT NOT NULL UNIQUE,
    Nom                TEXT NOT NULL,
    Prenom             TEXT NOT NULL,
    DateNaissance      TEXT NOT NULL,                 -- ISO 8601 YYYY-MM-DD
    DateRecrutement    TEXT NOT NULL,
    Sexe               TEXT NOT NULL CHECK (Sexe IN ('M', 'F')),
    SituationFamiliale TEXT NOT NULL DEFAULT 'CELIBATAIRE'
                         CHECK (SituationFamiliale IN ('CELIBATAIRE', 'MARIE', 'DIVORCE', 'VEUF')),
    -- Pas de colonne `Actif` séparée : `Statut` porte déjà cette information
    -- (RADIE = inactif) — un booléen redondant introduirait un invariant
    -- Statut/Actif à policer pour rien (D-A, YAGNI).
    Statut             TEXT NOT NULL DEFAULT 'ACTIF' CHECK (Statut IN ('ACTIF', 'SUSPENDU', 'RADIE')),
    CreatedAt          TEXT NOT NULL,
    UpdatedAt          TEXT
);

-- ──────────────────────────────────────────────────────────────────────────
-- Carrieres (gestion, versionnée) — source unique de vérité pour tous les
-- critères SourceResolution='CARRIERE' de CriteresEligibilite (V009) :
-- FILIERE (dérivé via GradeId → Corps.FiliereId), CORPS (via GradeId →
-- Corps), GRADE, CATEGORIE, FONCTION, TYPE_CONTRAT, ECHELON,
-- TYPE_ETABLISSEMENT (via EtablissementId → Etablissements.Type). D-B.
-- ──────────────────────────────────────────────────────────────────────────

CREATE TABLE Carrieres (
    Id              TEXT NOT NULL PRIMARY KEY,
    AgentId         TEXT NOT NULL REFERENCES Agents(Id),
    GradeId         TEXT NOT NULL REFERENCES Grades(Id),
    CategorieId     TEXT NOT NULL REFERENCES Categories(Id),
    EchelonId       TEXT NOT NULL REFERENCES Echelons(Id),
    FonctionId      TEXT REFERENCES Fonctions(Id),         -- nullable : pas de fonction particulière
    -- D-C : TypeContrat reste un champ simple ici — l'agrégat Contrat (Tome B
    -- §6 : renouvellements, temps de travail) attend un besoin RH concret.
    TypeContrat     TEXT NOT NULL CHECK (TypeContrat IN ('STATUTAIRE', 'CONTRACTUEL')),
    EtablissementId TEXT REFERENCES Etablissements(Id),    -- nullable : affectation non renseignée
    DateEffet       TEXT NOT NULL,
    DateFin         TEXT,
    Motif           TEXT NOT NULL,          -- "Recrutement", "Promotion de grade", "Avancement d'échelon"...
    NumeroDecision  TEXT,                   -- référence de la décision administrative (RM-027)
    Source          TEXT,
    CreatedAt       TEXT NOT NULL
);
CREATE INDEX IX_Carrieres_AgentId ON Carrieres (AgentId);
CREATE UNIQUE INDEX IX_Carrieres_Agent_DateEffet ON Carrieres (AgentId, DateEffet);

-- ──────────────────────────────────────────────────────────────────────────
-- Periodes (Tome B vol. 8 §12) — cycle de vie requis par ADR-0008 : une
-- période CLOTUREE n'est jamais recalculée, seule une ligne de rappel peut
-- corriger un montant déjà payé.
-- ──────────────────────────────────────────────────────────────────────────

CREATE TABLE Periodes (
    Id            TEXT NOT NULL PRIMARY KEY,     -- "YYYY-MM"
    Annee         INTEGER NOT NULL,
    Mois          INTEGER NOT NULL CHECK (Mois BETWEEN 1 AND 12),
    Etat          TEXT NOT NULL DEFAULT 'OUVERTE' CHECK (Etat IN
                     ('OUVERTE', 'EN_CALCUL', 'VALIDEE', 'CLOTUREE', 'ARCHIVEE')),
    DateOuverture TEXT NOT NULL,
    DateCloture   TEXT,
    CreatedAt     TEXT NOT NULL,
    UpdatedAt     TEXT
);
CREATE UNIQUE INDEX IX_Periodes_Annee_Mois ON Periodes (Annee, Mois);

-- ──────────────────────────────────────────────────────────────────────────
-- AgentAttributs (gestion — J3H §4). Symétrique de GradeAttributs (V008) :
-- porte les critères propres à la personne, sans colonnes en dur. Requis dès
-- cette tranche pour résoudre ORIGINE_STATUTAIRE (seul critère
-- SourceResolution='ATTRIBUT_AGENT' seedé en V009).
-- ──────────────────────────────────────────────────────────────────────────

CREATE TABLE AgentAttributs (
    Id        TEXT NOT NULL PRIMARY KEY,
    AgentId   TEXT NOT NULL REFERENCES Agents(Id),
    Attribut  TEXT NOT NULL,                     -- CleResolution d'un critère ATTRIBUT_AGENT
    Valeur    TEXT NOT NULL,
    DateEffet TEXT NOT NULL,
    DateFin   TEXT,
    Source    TEXT,                              -- pièce justificative / décision admin.
    CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_AgentAttributs_Agent_Attr_Date
    ON AgentAttributs (AgentId, Attribut, DateEffet);

-- ──────────────────────────────────────────────────────────────────────────
-- AgentRubriques (+ AgentRubriqueParametres) — J3H §7. L'affectation vue et
-- décidée par l'utilisateur (module d'affectation assistée, ADR-0006).
-- ──────────────────────────────────────────────────────────────────────────

CREATE TABLE AgentRubriques (
    Id                TEXT NOT NULL PRIMARY KEY,
    AgentId           TEXT NOT NULL REFERENCES Agents(Id),
    RubriqueId        TEXT NOT NULL REFERENCES Rubriques(Id),
    Occurrence        INTEGER NOT NULL DEFAULT 1 CHECK (Occurrence >= 1),
    LibelleOccurrence TEXT,                      -- retenues à montant fixe : "Prêt social"...
    Statut            TEXT NOT NULL CHECK (Statut IN
                        ('SUGGEREE', 'ACCEPTEE', 'SUPPRIMEE', 'SUSPENDUE')),
    Origine           TEXT NOT NULL,             -- 'MANUELLE' ou 'GROUPE:<Id>@<DateEffet>'
    DateEffet         TEXT NOT NULL,
    DateFin           TEXT,
    CreatedAt         TEXT NOT NULL,
    UpdatedAt         TEXT
);
CREATE UNIQUE INDEX IX_AgentRubriques_Agent_Rub_Occ_Date
    ON AgentRubriques (AgentId, RubriqueId, Occurrence, DateEffet);

CREATE TABLE AgentRubriqueParametres (
    Id               TEXT NOT NULL PRIMARY KEY,
    AgentRubriqueId  TEXT NOT NULL REFERENCES AgentRubriques(Id),
    Cle              TEXT NOT NULL,
    Valeur           TEXT NOT NULL,
    DateEffet        TEXT NOT NULL,
    DateFin          TEXT,
    CreatedAt        TEXT NOT NULL
);

-- ──────────────────────────────────────────────────────────────────────────
-- AvertissementsHistorique — J3H §8. Append-only (ni UPDATE ni DELETE) :
-- MessageAffiche est un instantané du texte résolu au moment de l'émission.
-- ──────────────────────────────────────────────────────────────────────────

CREATE TABLE AvertissementsHistorique (
    Id              TEXT NOT NULL PRIMARY KEY,
    EmisLe          TEXT NOT NULL,
    Utilisateur     TEXT,                        -- libellé libre en V1 (mode autonome)
    AgentId         TEXT NOT NULL REFERENCES Agents(Id),
    RubriqueId      TEXT NOT NULL REFERENCES Rubriques(Id),
    GroupeId        TEXT REFERENCES GroupesEligibilite(Id),   -- règle déclencheuse (NULL si manuel)
    GroupeDateEffet TEXT,                        -- version de la règle à l'émission
    Severite        TEXT NOT NULL,
    MessageAffiche  TEXT NOT NULL,
    Decision        TEXT NOT NULL CHECK (Decision IN ('ACCEPTE', 'IGNORE', 'SUPPRIME')),
    CreatedAt       TEXT NOT NULL
);
CREATE INDEX IX_AvertissementsHistorique_AgentId ON AvertissementsHistorique (AgentId);
