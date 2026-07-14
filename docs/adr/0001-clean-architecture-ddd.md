# ADR-0001 — Clean Architecture + DDD, 12 projets

**Statut :** Accepté — Phase 0.

## Contexte
PaieEducation ERP doit rester maintenable et évolutif sur 15+ ans, avec un domaine métier
(paie réglementaire algérienne) au cœur, indépendant de WPF, SQLite, QuestPDF et ClosedXML.
La documentation V4 (Tomes A/B) impose Clean Architecture + Domain-Driven Design.

## Décision
Solution découpée en 12 projets :

- `src/` : `Domain`, `Application`, `Infrastructure`, `Persistence`, `Reporting`, `Shared`,
  `Presentation` (WPF), `Bootstrapper` (WPF, Composition Root).
- `tests/` : `Tests.Unit`, `Tests.Integration`, `Tests.Architecture`.
- `tools/` : `Tools` (import/seed des données de référence).

Matrice de dépendances normative (voir `docs/CONVENTIONS.md`), vérifiée automatiquement par
`Tests.Architecture` (NetArchTest). `Domain` et `Shared` ne référencent aucun autre projet.

## Conséquences
- Le domaine est testable sans infrastructure ; les règles réglementaires vivent en base (zéro hardcoding).
- Un test d'architecture échoue à la moindre dépendance interdite (garde-fou permanent).
- Le `Bootstrapper` est le seul point d'assemblage des implémentations concrètes.
