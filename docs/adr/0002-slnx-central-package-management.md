# ADR-0002 — Solution `.slnx` + Central Package Management

**Statut :** Accepté — Phase 0.

## Contexte
Le SDK .NET 10 génère par défaut une solution au format XML `.slnx`. Par ailleurs, gérer les
versions de paquets projet par projet est source d'incohérences sur une solution à 12 projets.

## Décision
- Conserver le format **`.slnx`** (`PaieEducation.slnx`) — moderne, lisible, supporté par le SDK 10 et l'IDE.
- Activer le **Central Package Management** : versions dans `Directory.Packages.props`
  (`ManagePackageVersionsCentrally=true`, `CentralPackageTransitivePinningEnabled=true`).
  Les `.csproj` référencent les paquets **sans version**.
- Réglages MSBuild communs centralisés dans `Directory.Build.props` (Nullable, ImplicitUsings,
  LangVersion, TreatWarningsAsErrors, etc.).

## Conséquences
- Versions homogènes et point de mise à jour unique.
- Les templates injectant des versions (xUnit) sont normalisés en forme CPM.
- `dotnet add package` alimente automatiquement `Directory.Packages.props`.
