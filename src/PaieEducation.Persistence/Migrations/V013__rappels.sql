-- V013 — Rappels (Phase 5, use case GenererRappels, D9)
--
-- Persiste les lignes de rappel produites par RappelCalculator (Phase 4)
-- lorsqu'un recalcul « à droit constant actuel » d'un bulletin déjà validé
-- diverge du montant payé à l'époque (évolution réglementaire rétroactive,
-- J3C §11, RM-102/RM-103, ADR-0008).
--
-- Écart assumé vis-à-vis de la conception documentée (J3C §11, J4E §8) :
-- celle-ci décrit le rappel comme une ligne d'un futur bulletin ouvert
-- (rubrique de rappel), pas une table à part. Aucun mécanisme n'existe
-- aujourd'hui pour injecter des lignes dans un futur bulletin (le moteur
-- de calcul n'a pas de notion de « rappels en attente ») — cette table est
-- une simplification volontaire pour rendre les rappels calculables,
-- traçables et auditables sans toucher au moteur ; le rattachement à un
-- bulletin réel reste une évolution future (voir mémoire phase5-genererrappels).
--
-- Un bulletin validé n'est jamais réécrit (ADR-0008) : le rappel est
-- calculé contre son snapshot, jamais contre une réévaluation du passé.

CREATE TABLE Rappels (
    Id               TEXT NOT NULL PRIMARY KEY,      -- GUID (ADR-0004, table de gestion)
    AgentId          TEXT NOT NULL REFERENCES Agents(Id),
    DatePaieOrigine  TEXT NOT NULL,                  -- date de paie du bulletin validé recalculé
    RubriqueId       TEXT NOT NULL,
    MontantAncien    REAL NOT NULL,
    MontantNouveau   REAL NOT NULL,
    Delta            REAL NOT NULL,
    GenereLe         TEXT NOT NULL,
    CreatedAt        TEXT NOT NULL
);
CREATE INDEX IX_Rappels_Agent_DatePaieOrigine ON Rappels (AgentId, DatePaieOrigine);
