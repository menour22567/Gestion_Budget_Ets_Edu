-- V012 — Bulletins (Phase 5, use case ValiderBulletin)
--
-- Persiste le snapshot d'un bulletin validé (RM-105, Snapshot Engine J4.d).
-- ADR-0008 : un bulletin validé n'est jamais réécrit — l'unicité
-- (AgentId, DatePaie) l'empêche. SnapshotJson porte le PayrollInput +
-- Bulletin complets (formules, barèmes, conditions, cotisations, règle IRG,
-- lignes, audit) : rejouer CalculationPipeline.Calculer(snapshot.Input)
-- reproduit le résultat à l'identique, sans réévaluation du passé — c'est
-- contre ce snapshot qu'un rappel se calcule, jamais contre l'état courant.
-- Net/TotalGains/AssietteImposable/Irg sont dupliqués hors du JSON pour une
-- lecture rapide (listes, reporting) sans désérialisation.

CREATE TABLE Bulletins (
    Id                TEXT NOT NULL PRIMARY KEY,      -- GUID (ADR-0004, table de gestion)
    AgentId           TEXT NOT NULL REFERENCES Agents(Id),
    DatePaie          TEXT NOT NULL,                  -- ISO 8601 YYYY-MM-DD
    Net               REAL NOT NULL,
    TotalGains        REAL NOT NULL,
    AssietteImposable REAL NOT NULL,
    Irg               REAL NOT NULL,
    SnapshotJson      TEXT NOT NULL,
    ValideLe          TEXT NOT NULL,
    CreatedAt         TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_Bulletins_Agent_DatePaie ON Bulletins (AgentId, DatePaie);
