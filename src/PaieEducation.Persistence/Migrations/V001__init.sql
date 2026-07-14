-- V001 — Initialisation : crée la table d'audit et ses index.
--
-- Note : SchemaVersions (la table méta qui trace les migrations) est créé
-- automatiquement par le bootstrap du SqliteMigrator (CREATE TABLE IF NOT EXISTS),
-- elle n'est donc pas dupliquée ici. Cette première migration se contente
-- de poser AuditLog, qui doit elle-même être versionnée et reproductible.
--
-- AuditLog enregistre toute action métier sensible (création/MAJ/suppression
-- d'un agent, calcul d'un bulletin, validation, modification d'un paramètre
-- réglementaire, etc.) avec horodatage UTC, acteur, type d'entité, identifiant
-- métier et payload JSON optionnel.

CREATE TABLE AuditLog (
    Id         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    OccurredAt TEXT    NOT NULL,                       -- ISO 8601 UTC
    Actor      TEXT    NOT NULL,                       -- utilisateur / job / service
    Action     TEXT    NOT NULL,                       -- INSERT / UPDATE / DELETE / CALCUL / VALIDATE ...
    EntityType TEXT    NOT NULL,                       -- ex. 'Agent', 'Bulletin', 'Rubrique', 'Cotisation'
    EntityId   TEXT,                                   -- identifiant métier (Matricule, IdBulletin, CodeRubrique...)
    Payload    TEXT,                                   -- JSON optionnel (diff, snapshot, contexte de calcul)
    Comment    TEXT                                    -- note libre
);

CREATE INDEX IX_AuditLog_OccurredAt
    ON AuditLog (OccurredAt);

CREATE INDEX IX_AuditLog_EntityType_EntityId
    ON AuditLog (EntityType, EntityId);
