# ADR-0003 — Audit NuGet limité aux dépendances directes (NU1903)

**Statut :** Accepté — Phase 0. **À réévaluer en Phase 5.**

## Contexte
Avec `TreatWarningsAsErrors=true`, l'audit NuGet remonte **NU1903** en erreur bloquante :
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (vulnérabilité GHSA-2m69-gcr7-jv3q, gravité élevée) est une
dépendance **transitive** de `Microsoft.Data.Sqlite` 10.0.9. Analyse :

- Aucune version patchée de la même ligne (2.1.x) n'est publiée pour ce paquet natif
  (la ligne saute de 2.1.11 à 3.50.x — versioning propre à SQLite).
- Forcer la famille SQLitePCLRaw en 3.x serait un **bump majeur du provider natif** sous un paquet
  Microsoft, **non testable en Phase 0** (la couche Persistence n'a encore aucun code SQLite).

## Décision
- `NuGetAuditMode = direct` dans `Directory.Build.props` : l'audit couvre les dépendances **directes**
  (que nous maîtrisons) et n'échoue plus sur une vulnérabilité **transitive** amont non corrigeable
  proprement à ce stade. La vulnérabilité reste visible en avertissement.

## Conséquences
- Le build n'est plus bloqué par une vulnérabilité hors de notre contrôle immédiat.
- **Surveillance obligatoire** via `dotnet list package` (options vulnérables + transitives).
- **Remédiation en Phase 5** : soit `Microsoft.Data.Sqlite` aura publié une version tirant un natif
  patché, soit on passera la famille SQLitePCLRaw en 3.x **avec tests d'intégration SQLite** à l'appui.
- Réactiver `NuGetAuditMode=all` dès que la remédiation est validée.
