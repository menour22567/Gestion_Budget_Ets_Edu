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
- Montants monétaires : `decimal` natif C# (System.Decimal) — pas de Value Object `Money` en V1.
  Cette prescription est **suspendue par [ADR-0011](adr/0011-money-decision-decimal.md)** au profit
  de `decimal` + `ArrondiService` centralisé ; tout `Money` introduit dans le code sans amendement
  préalable d'ADR-0011 doit être refusé en revue. L'arrondi est obligatoire et passe par
  `PaieEducation.Domain/Calcul/Services/ArrondiService.cs` exclusivement — aucune instruction
  `Math.Round` / `decimal.Round` / `Math.Floor` / `Math.Ceiling` / `Math.Truncate` dans le code
  métier de `Domain` et `Application` (test d'architecture `Arrondi_centralise_uniquement_dans_ArrondiService`).
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

### 7.1 Stratégie de branche (validée P0, 22/07/2026)

- **Une branche courte par item du plan (`Px` ou nouveau chantier)**, créée depuis
  `main` à jour : `git checkout -b feature/<nom-metier-court> main`.
  Le nom reflète le **métier**, pas l'outil : `feature/workbench-reliquat-p7-p9-agents-seeder`
  (et **pas** `blackboxai/fake-agents-seeder` ou autre nom généré par AI agent).
- **Merge dans `main` à chaque item clos** (critères d'acceptation du plan atteints,
  suite de tests verte, build 0 warning). Merge en **fast-forward** quand l'historique
  le permet (cas nominal) ; merge commit descriptif sinon, jamais de rebase destructif
  sur `main` après qu'un commit soit parti.
- **Branche locale supprimée** après merge réussi (`git branch -d feature/...`) si elle
  n'a plus d'usage. Les vieilles branches orphelines (ex. `feature/p7-matrice-couverture-pivotee`
  absorbée par P7) sont à nettoyer régulièrement — ne jamais les laisser dormir.
- **Tag de référence** sur les jalons projet : `v0-pilote-moteur` (snapshot
  post-audit 19/07/2026, baseline 730/730 tests verts), futurs `v0.1`, `v1.0` à
  chaque jalon `Jn` validé.
- **Push** : `git push origin main feature/<branche-courante> --tags` (branche + tag).
  Le push n'est pas bloquant pour le merge local (le repo peut être inaccessible
  temporairement) — l'historique local fait foi en attendant.

### 7.2 Hygiène de l'arbre de travail

- **Aucun fichier non tracké ne doit dormir** plus d'une session de travail. Si du
  code est terminé, le committer ; si la spec change, le supprimer ou l'archiver dans
  un dossier explicitement gitignoré (`/Temp/`, `/Backups/`, `.git-trash-<date>/` —
  ce dernier est dans `.gitignore` et réservé aux déplacements avant suppression
  définitive).
- **Logs runtime** (`*.log`, `*.trc`) : gitignorés, jamais commités. Si un `.log`
  apparaît dans `git status`, c'est un signal que `.gitignore` est incomplet.

## 8. Décisions d'architecture (ADR)

Toute décision structurante est consignée dans `docs/adr/` (voir l'index). Format court :
contexte → décision → conséquences.
