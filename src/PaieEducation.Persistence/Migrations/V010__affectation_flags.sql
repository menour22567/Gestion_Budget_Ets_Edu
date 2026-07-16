-- V010 — Flags d'affectation manuelle (D1, D4) sur Rubriques.
--
-- Décision J4.e § 4.1/§5 (validée jalon A, 15/07/2026) : V010 reste strictement
-- référentielle — aucune table de gestion (AgentAttributs/AgentRubriques/
-- AvertissementsHistorique restent créées avec `Agents` en Phase 5, R1 V009).
--
-- EstAffectableManuellement : la rubrique peut être ajoutée/retirée librement
-- par l'utilisateur pour un agent (module d'affectation assistée). D1 :
-- sûr par défaut (0) — les cotisations, l'impôt et les rubriques systémiques
-- ne sont jamais affectables manuellement. Seed des valeurs par
-- ReglementaireSeeder/FormulesSeeder (tableau validé J4E § 5), pas dans
-- cette migration : la donnée réglementaire vit dans le seed, pas le schéma.
--
-- OccurrencesMultiples : D4 — un agent peut avoir plusieurs occurrences de la
-- même rubrique (retenues optionnelles à montant fixe, rappels Q7). 0 par
-- défaut : aucune rubrique réglementaire du périmètre actuel n'en a besoin.

ALTER TABLE Rubriques ADD COLUMN EstAffectableManuellement INTEGER NOT NULL DEFAULT 0
    CHECK (EstAffectableManuellement IN (0, 1));

ALTER TABLE Rubriques ADD COLUMN OccurrencesMultiples INTEGER NOT NULL DEFAULT 0
    CHECK (OccurrencesMultiples IN (0, 1));
