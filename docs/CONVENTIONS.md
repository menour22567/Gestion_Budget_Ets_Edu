# Conventions de développement — PaieEducation ERP

> Complète la V4 (Tome J, Volume 25). Toute dérogation doit être justifiée et tracée en ADR.

## 1. Stack & cibles

- **.NET 10 LTS** (SDK épinglé par `global.json` = 10.0.301).
- Bibliothèques : `net10.0`. WPF (`Presentation`, `Bootstrapper`) : `net10.0-windows`.
- Projet de tests d'architecture : `net10.0-windows` (référence tous les projets).
- Langage : **C# 14**, `Nullable` activé, `ImplicitUsings` activés.
- `TreatWarningsAsErrors = true` — le code livré est **sans avertissement**.

## 2. Gestion des paquets

- **Central Package Management** : versions dans `Directory.Packages.props`, jamais dans les `.csproj`.
- Ajout via `dotnet add package <id>` (résout et centralise la version automatiquement).
- **NuGetAuditMode = direct** (ADR-0003) : audit sur dépendances directes ; les vulnérabilités
  transitives amont sont surveillées et réévaluées, pas masquées.

## 3. Architecture (Clean Architecture + DDD)

Matrice de dépendances **normative** (le test `Tests.Architecture` la vérifie) :

| Projet | Peut référencer |
|--------|-----------------|
| `Domain` | *(aucun projet)* |
| `Shared` | *(aucun projet)* |
| `Application` | Domain |
| `Infrastructure` | Domain, Shared |
| `Persistence` | Domain, Shared |
| `Reporting` | Application, Shared |
| `Presentation` | Application, Shared |
| `Bootstrapper` | tous |
| `Tools` | Application, Domain, Infrastructure, Persistence, Shared |

Interdits absolus : `Domain → (WPF | SQLite | QuestPDF | ClosedXML | Dapper)` ; `Presentation → SQLite`.

## 4. Nommage

| Élément | Convention | Exemple |
|---------|-----------|---------|
| Interface | `I...` | `IAgentRepository` |
| Service applicatif | `...Service` | `PayrollService` |
| Repository | `...Repository` | `BulletinRepository` |
| Vue / ViewModel | `...View` / `...ViewModel` | `AgentView` / `AgentViewModel` |
| DTO / Command / Query | `...Dto` / `...Command` / `...Query` | `BulletinDto` |
| Calculateur de paie | `...Calculator` | `IrgCalculator` |

Un type public = un fichier. PascalCase (types/méthodes/propriétés), `_camelCase` (champs privés).

## 5. Règles de code

- **Domaine sans dépendance technique** ; les rubriques/valeurs réglementaires **ne sont jamais codées en dur** (tout en base, versionné par date d'effet).
- Montants monétaires via l'objet valeur `Money` (DZD) — jamais `decimal` nu dans le domaine.
- **Cas métier attendus → `Result` / `Error`** ; exceptions réservées aux situations exceptionnelles.
- **MVVM strict** : aucune logique métier dans les `*.xaml.cs` ; navigation via services.
- `async`/`await` pour les opérations longues (I/O, calcul de masse, PDF, export).
- Accolades obligatoires, indentation 4 espaces.

## 6. Tests

- `Tests.Unit` : Domain / Application / Shared (sans I/O).
- `Tests.Integration` : Persistence / Infrastructure / Reporting (SQLite temporaire).
- `Tests.Presentation` : ViewModels / commandes / navigation (`net10.0-windows` — seul moyen de référencer `Presentation`, WPF ; ports mockés, aucune I/O réelle).
- `Tests.Architecture` : règles de dépendance (NetArchTest).
- Cibles de couverture (V4) : Domaine ≥ 95 % ; calculateurs de paie 100 % des cas critiques.
- Framework : xUnit + Moq.

## 7. Git

- Branche stable : `main`. Fonctionnalités : `feature/...`. Correctifs : `fix/...`.
- Commits signés du co-auteur outillage. Pas de secrets ni de bases `*.db` versionnés.

## 8. Décisions d'architecture (ADR)

Toute décision structurante est consignée dans `docs/adr/` (voir l'index). Format court :
contexte → décision → conséquences.
