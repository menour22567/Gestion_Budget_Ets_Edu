# PaieEducation ERP — Description générale du projet

> **Document :** Vue d'ensemble du projet PaieEducation v1.0
> **Date de rédaction :** 21 juillet 2026
> **Statut :** Document de présentation générale (synchronisé avec `README.md`, `docs/PLAN_ACTION.md`, `docs/GLOSSAIRE.md`, `docs/CONVENTIONS.md` et `docs/AUDIT_2026-07-17_DOCUMENTATION_TECHNIQUE.md`)

---

## 1. Résumé exécutif

**PaieEducation ERP** est une application **desktop, monolithique, 100 % hors-ligne** de gestion de la paie destinée aux **établissements publics de l'Éducation nationale algérienne** et aux corps assimilés. Elle est **mono-utilisateur**, fonctionne en **mode autonome** (sans authentification en V1) et cible l'environnement Windows 10/11.

Le projet vise à fournir un moteur de calcul de paie **paramétrable, versionné et traçable**, conforme à la réglementation algérienne, dans lequel **aucune règle ni valeur réglementaire n'est codée en dur** : tout vit en base de données, versionné par date d'effet.

**Périmètre V1 :** le corps **pilote « enseignants »** d'abord, pour valider le moteur, puis extension aux autres corps (administration, inspection, santé publique, ouvriers).

**Version actuelle :** 0.1.0 (en développement — phases 0 à 3 largement réalisées, phases 3bis à 6 avancées mais partielles, phases 9 et 10 non implémentées).

---

## 2. Finalité et contexte métier

### 2.1 Problème résolu

Les établissements de l'Éducation nationale algérienne doivent produire, chaque mois, des bulletins de paie conformes à une réglementation complexe et en évolution permanente :

- grilles indiciaires par catégorie (1 à 17 + Hors Catégorie),
- corps, grades, échelons (1 à 12),
- valeur du point indiciaire (45 DA, paramétrable),
- barèmes de l'IRG (2008, 2020, 2021, 2022+),
- taux de cotisation (Sécurité Sociale, retraite),
- régimes indemnitaires (IEP, PAPP, qualification, documentation, ISSRP, IFC, etc.),
- retenues facultatives (œuvres sociales, mutuelles),
- rappels et régularisations rétroactifs,
- états de paie et documents officiels.

### 2.2 Principe cardinal

> **Zéro hardcoding.** Grilles, indices, point indiciaire, barèmes IRG, taux de cotisation, rubriques, formules, critères d'éligibilité, arrondi : tout est **paramétré en base SQLite** et **versionné par date d'effet**. Aucune recompilation n'est nécessaire pour appliquer une évolution réglementaire.

L'utilisateur dispose d'un **Workbench réglementaire** (arborescence d'écrans spécialisés) pour piloter lui-même l'évolution des règles, avec **dry-run obligatoire** avant commit.

### 2.3 Périmètre fonctionnel V1

| Domaine | Contenu V1 |
|---|---|
| **Référentiel** | Corps, grades, échelons, catégories, filières, types de contrat |
| **Agents** | Fonctionnaires + contractuels, ancienneté, attributs, statuts |
| **Rubriques** | IEP, PAPP, qualification, documentation, ISSRP, IFC, cotisations, retenues |
| **Calcul de paie** | Traitement de base, traitement, indemnités, cotisations, IRG, net à payer |
| **Périodes** | États : Ouverte, En calcul, Validée, Clôturée, Archivée |
| **Bulletins** | Agrégats immuables et versionnés (PDF + Excel) |
| **Rappels** | Régularisations rétroactives à la date d'effet réglementaire |
| **Audit** | Traçabilité des décisions réglementaires (dry-run, commit, rollback) |

### 2.4 Décisions structurantes

- **Q1 — Formule de traitement** : `traitement de base = indice_min × valeur_point` ; `traitement = (indice_min + indice_échelon) × valeur_point`.
- **Q4/Q4b — IRG** : barème 2008 (4 tranches) + barème 2022+ (6 tranches) + lissages 2020/2021/2022 stockés en base.
- **Q7 — Rappels** : gestion dès la V1, règle = date d'effet réglementaire.
- **Q8 — Ancienneté** : années de service effectif, sans déduction des interruptions.
- **Q9b — Arrondi** : centralisé et uniforme, défaut « au dinar le plus proche », paramétrable.
- **Q10 — Périmètre** : corps pilote « enseignants » en V1.
- **Q12 — Authentification** : mode autonome sans authentification en V1.

---

## 3. Pile technologique

| Domaine | Technologie | Détail |
|---|---|---|
| **Runtime** | **.NET 10 LTS** (SDK 10.0.301) | Épinglé par `global.json` (`rollForward: latestFeature`) |
| **Langage** | **C# 14** | `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest` |
| **UI** | **WPF + MVVM** | `CommunityToolkit.Mvvm` (source generators) |
| **Persistance** | **SQLite + Dapper** | Micro-ORM, SQL explicite, migrations versionnées (V001 → V014) |
| **Reporting** | **QuestPDF** (PDF), **ClosedXML** (Excel) | Référencés pour la production des bulletins |
| **Tests** | **xUnit, Moq, NetArchTest** | 6 projets de test, 470 tests verts |
| **Build** | **MSBuild + Central Package Management** | `Directory.Packages.props`, `Directory.Build.props`, format `.slnx` |

**Réglages MSBuild communs** (`Directory.Build.props`) :
- `TreatWarningsAsErrors=true` — code livré sans avertissement
- `Deterministic=true`
- `EnforceCodeStyleInBuild=true`
- `NuGetAuditMode=direct` (ADR-0003)
- `NeutralLanguage=fr`
- Version actuelle : `0.1.0`

---

## 4. Architecture logicielle

### 4.1 Style architectural

**Clean Architecture + DDD (Domain-Driven Design)** — voir ADR-0001.

Le projet est organisé en **couches concentriques** où le **Domaine est au centre, sans dépendance sortante**. La règle de dépendance est **vérifiée automatiquement** par les tests d'architecture (`NetArchTest`).

### 4.2 Bounded contexts (Domain)

| Contexte | Responsabilité |
|---|---|
| **Agents** | Agrégat racine Agent, carrière, attributs |
| **Calcul** | Moteur de paie pur (rubriques, formules, calculateurs) |
| **Workbench** | Évolution réglementaire, dry-run, audit |
| **Référentiels** | Corps, grades, échelons, catégories, grilles |

### 4.3 Structure de la solution

La solution `PaieEducation.slnx` (format XML moderne) regroupe **9 projets de production** et **6 projets de test** :

```
src/
├── PaieEducation.Domain         → Cœur métier pur (règles, calcul, workbench)
├── PaieEducation.Shared         → Result<T>, Error, Guard, IClock
├── PaieEducation.Application    → 20 use cases (CQRS léger), orchestration
├── PaieEducation.Infrastructure → Repositories Dapper, DI, sérialisation
├── PaieEducation.Persistence    → Migrateur SQLite + 14 migrations
├── PaieEducation.Presentation   → Shell WPF, ViewModels, vues, navigation
├── PaieEducation.Reporting      → Production PDF (QuestPDF) / Excel (ClosedXML)
├── PaieEducation.Seeding        → Seeds du référentiel et de la démo
└── PaieEducation.Bootstrapper   → Composition Root WPF (point d'entrée)

tests/
├── PaieEducation.Tests.Unit          → Domain / Application / Shared
├── PaieEducation.Tests.Integration   → Persistence / Infrastructure / Reporting
├── PaieEducation.Tests.Presentation  → ViewModels / commandes / navigation
├── PaieEducation.Tests.Architecture  → Règles de dépendance (NetArchTest)
├── PaieEducation.Tests.Tools         → Outils CLI
└── PaieEducation.Tests.OutilsTests   → (selon plan)

tools/
└── PaieEducation.Tools → CLI migrate / seed / validate
```

### 4.4 Matrice de dépendances (normative)

| Projet | Peut référencer |
|---|---|
| `Domain` | *(aucun projet — cœur pur)* |
| `Shared` | *(aucun projet)* |
| `Application` | Domain, Shared |
| `Infrastructure` | Domain, Shared |
| `Persistence` | Domain, Shared |
| `Reporting` | Application, Shared |
| `Presentation` | Application, Shared (+ Domain pour DTO) |
| `Bootstrapper` | tous |
| `Tools` | Application, Domain, Infrastructure, Persistence, Shared |

**Interdits absolus** : `Domain → (WPF | SQLite | QuestPDF | ClosedXML | Dapper)` ; `Presentation → SQLite`.

### 4.5 Diagramme de composants

```
                    ┌─────────────────────┐
                    │   Bootstrapper      │  ← WPF entry point + host + migrations
                    └──────────┬──────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        ▼                      ▼                      ▼
┌──────────────┐      ┌──────────────┐       ┌──────────────┐
│ Presentation │      │ Application  │       │  Reporting   │
│   (WPF/MVVM) │      │  (Use cases) │       │ (PDF/Excel)  │
└──────┬───────┘      └──────┬───────┘       └──────┬───────┘
       │                     │                      │
       │            ┌────────┴────────┐             │
       │            ▼                 ▼             │
       │     ┌────────────┐    ┌────────────┐       │
       │     │  Domain    │    │  Shared    │       │
       │     │ (cœur pur) │    │(Result/Err)│       │
       │     └─────▲──────┘    └────────────┘       │
       │           │                                 │
       │     ┌─────┴──────┐                          │
       │     │Infrastructure│                       │
       │     │(Dapper repos)│                       │
       │     └─────┬──────┘                          │
       │           │                                 │
       │     ┌─────┴──────┐                          │
       │     │ Persistence │                         │
       │     │  (SQLite +  │                         │
       │     │ migrations) │                         │
       │     └────────────┘                          │
       │                                              │
       └──────────────► (use cases uniquement) ◄─────┘
```

---

## 5. Modèle métier (aperçu)

### 5.1 Langage omniprésent (extrait du glossaire)

| Terme | Définition |
|---|---|
| **Agent** | Fonctionnaire ou contractuel de l'établissement (agrégat racine RH) |
| **Corps** | Ensemble de grades d'une même filière (ex. Professeurs de l'Enseignement Moyen) |
| **Grade** | Rang au sein d'un corps |
| **Échelon** | Position d'avancement (1 à 12) — détermine l'indice d'échelon |
| **Catégorie** | Niveau de classement (1 à 17 + Hors Catégorie) — fixe l'indice minimal |
| **Filière** | ENSEIGNANT, ADMIN, INSPECTION, SANTE_PUBLIQUE, OUVRIERS_AGENTS |
| **Ancienneté** | Années de service effectif (sans déduction des interruptions) |
| **Période** | Mois + année ; états : Ouverte, En calcul, Validée, Clôturée, Archivée |
| **Bulletin** | Agrégat immuable et versionné, résultat certifié d'un calcul |
| **Rappel** | Recalcul rétroactif à la date d'effet réglementaire |
| **Explicabilité** | Justification traçable de chaque montant (base, taux, formule, arrondi) |

### 5.2 Rubriques principales (pilote enseignants)

| Code | Libellé | Base | Règle |
|---|---|---|---|
| **IEP** | Indemnité d'expérience professionnelle | TRT | `IE × VPI` (fonctionnaires) ; `TBASE × min(ANC_PUB×1,4 % + ANC_PRIV×0,7 % ; 60 %)` (contractuels) |
| **EXP_PEDAG** | Indemnité d'expérience pédagogique | TBASE | `4 % × n° échelon` (corps EN hors Intendance/Labo) |
| **PAPP** | Prime d'amélioration des performances pédagogiques | TRT | 0–40 % selon notation, trimestrielle |
| **Ind. Qualification** | Indemnité de qualification | TBASE | 40 % (cat. ≤ 12) / 45 % (cat. ≥ 13) |
| **Ind. Documentation** | Indemnité de documentation pédagogique | Forfait | 2 000 / 2 500 / 3 000 DA selon catégorie |
| **ISSRP** | Indemnité de soutien scolaire et remédiation | TRT | 45 % (groupe élargi) / 30 % / 15 % selon corps |
| **Cotisation SS** | Sécurité sociale / retraite (part ouvrière) | Assiette cotisable | Taux paramétrable (défaut 9 %) |
| **IRG** | Impôt sur le Revenu Global | Assiette imposable | Barème par tranches + règles de période |

### 5.3 Conventions de code

- **Type public = un fichier**, PascalCase, `_camelCase` pour les champs privés.
- **`Result<T> / Error`** pour les cas métier attendus, exceptions réservées aux situations exceptionnelles.
- **MVVM strict** : aucune logique métier dans les `*.xaml.cs`.
- **Montants monétaires** : `decimal` C# + `ArrondiService` centralisé (ADR-0011) — aucun `Math.Round` / `decimal.Round` dans le code métier.
- **Async/await** pour les opérations longues (I/O, calcul de masse, PDF, export).

---

## 6. Persistance et données

### 6.1 Schéma SQLite

- **Moteur** : SQLite (fichier local).
- **Migrations** : 14 versions (V001 → V014) gérées par le `PaieEducation.Persistence`.
- **Tables clés** : `Agents`, `Corps`, `Grades`, `Echelons`, `Categories`, `Rubriques`, `RubriqueParametres`, `RubriqueBaremes`, `CriteresEligibilite`, `GroupesEligibilite`, `Formules`, `PeriodesPaie`, `Bulletins`, `LignesBulletin`, `AuditLog`, `ReglesEligibilite`, `SourcesValeur`, `AgentAttributs`, `AgentRubriques`, `AvertissementsHistorique`.

### 6.2 Versionnement par date d'effet

Toutes les valeurs paramétrables (point indiciaire, indices, taux, barèmes, formules, critères d'éligibilité) sont **historisées** par date de début et date de fin de validité. Le moteur de calcul résout la version applicable à la date de la paie.

### 6.3 Référentiels métier fournis

Le projet embarque un **dossier `Reglementation/`** contenant les textes sources :

```
Reglementation/
├── Dossier_Trait_EN__16526
├── elements_paie_corps_commun_ouvriers_prof
├── elements_paie_historique_14726
├── IFC_2008+2015
├── Indemn Zone
├── IRG_Algerie_2008_2026
├── Regim indem des paramédicaux
├── Regimes-indemnitaires_2010-2025_Educ
├── Reglementation fixant la remuneration enseignants contractuels
├── Statuts particuliers
└── Textes AF 2022
```

Et un dossier **`Documentation de Référence du Projet/`** contenant la V3.0 (métier/fonctionnel) et la V4.0 (spécification d'implémentation, tomes A → F) :
- Tome A — Architecture Applicative
- Tome B — Modèle de Domaine
- Tome C — Moteur de Calcul de Paie
- Tome D — Persistance et Infrastructure
- Tome E — Présentation (WPF)
- Tome F — Reporting et Production Documentaire

---

## 7. Décisions d'architecture (ADR)

11 ADR ont été validées et sont tracées dans `docs/adr/` :

| ADR | Sujet |
|---|---|
| 0001 | Clean Architecture + DDD |
| 0002 | Solution `.slnx` + Central Package Management |
| 0003 | NuGet Audit Mode = `direct` |
| 0004 | Clés métier des référentiels |
| 0005 | Moteur de calcul synchrone |
| 0006 | Affectation assistée des rubriques |
| 0007 | Workbench réglementaire |
| 0008 | Immutabilité des périodes clôturées |
| 0009 | Abstention réglementaire |
| 0010 | Abstention phase paiement |
| 0011 | Décision `Money` → `decimal` + `ArrondiService` centralisé |

---

## 8. Phases du projet

| Phase | Intitulé | Statut |
|---|---|---|
| 0 | Cadrage & fondations | ✅ Réalisée |
| 1 | Référentiel paramétrable (schéma SQLite) | ✅ Réalisée |
| 2 | Ingestion & seed des données de référence | ✅ Réalisée |
| 3 | Moteur de calcul de paie (pilote enseignants) | ✅ Largement réalisée |
| 3bis | Affectation assistée des rubriques | 🟡 Avancée, partielle |
| 4 | Workbench réglementaire (DNF, SourcesValeur) | 🟡 Avancée, partielle |
| 5 | Simulateur d'impact / barèmes override | 🟡 Avancée, partielle |
| 6 | Audit, traçabilité, matrice de couverture | 🟡 Avancée, partielle |
| 7 | Reporting (PDF/Excel bulletins) | 🟡 Limité |
| 8 | Validation réglementaire sur bulletins réels | 🟡 Partielle |
| 9 | Optimisations, performance, packaging | ❌ Non implémentée |
| 10 | Déploiement, installation, documentation utilisateur | ❌ Non implémentée |

**État au 17/07/2026** (dernier audit) : `dotnet build` 0 erreur / 0 warning, **470 tests verts** (Unit 152, Presentation 34, Tools 47, Architecture 3, Integration 234).

---

## 9. Sécurité, qualité, conformité

- **Mode autonome V1** : pas d'authentification (décision Q12) ; modèle de rôles prévu mais désactivé.
- **Traçabilité** : `AuditLog` enregistre toute évolution réglementaire (dry-run, commit, bypass admin).
- **Intégrité comptable** : périodes clôturées **immuables** (ADR-0008) ; bulletins validés non modifiables ; les évolutions rétroactives créent des **rappels** (lignes additionnelles), pas de modification de l'historique.
- **Audit NuGet** : mode `direct` (ADR-0003) — vulnérabilités sur dépendances directes uniquement ; transitives surveillées via `dotnet list package --vulnerable --include-transitive`.
- **Build sans avertissement** : `TreatWarningsAsErrors=true`.
- **Règles d'architecture automatiques** : `NetArchTest` empêche toute violation de la matrice de dépendances.

---

## 10. Glossaire des acronymes

| Acronyme | Signification |
|---|---|
| **ERP** | Enterprise Resource Planning |
| **V1** | Version 1 (périmètre initial) |
| **WPF** | Windows Presentation Foundation |
| **MVVM** | Model-View-ViewModel |
| **DDD** | Domain-Driven Design |
| **DNF** | Forme Normale Disjonctive (ET dans groupe, OU entre groupes) |
| **ADR** | Architecture Decision Record |
| **CPM** | Central Package Management |
| **IRG** | Impôt sur le Revenu Global |
| **IEP** | Indemnité d'Expérience Professionnelle |
| **EXP_PEDAG** | Indemnité d'Expérience Pédagogique |
| **PAPP** | Prime d'Amélioration des Performances Pédagogiques |
| **ISSRP** | Indemnité de Soutien Scolaire et Remédiation Pédagogique |
| **IFC** | Indemnité Forfaitaire de Charge |
| **SS** | Sécurité Sociale |
| **TBASE / TRT** | Traitement de base / Traitement |
| **VPI** | Valeur du Point Indiciaire |
| **DNF** | Disjunctive Normal Form |
| **IE** | Indice d'Échelon |
| **DZD** | Dinar Algérien (devise) |

---

## 11. Commandes de base

```bash
# Restaurer les paquets
dotnet restore PaieEducation.slnx

# Compiler en Debug
dotnet build PaieEducation.slnx -c Debug

# Lancer les tests
dotnet test PaieEducation.slnx -c Debug

# Lancer l'application (Bootstrapper WPF)
dotnet run --project src/PaieEducation.Bootstrapper
```

---

## 12. Documentation associée

- **Plan d'action** : [`docs/PLAN_ACTION.md`](PLAN_ACTION.md) — phases, jalons, journal des décisions Q1–Q10.
- **Glossaire métier** : [`docs/GLOSSAIRE.md`](GLOSSAIRE.md) — langage omniprésent.
- **Conventions** : [`docs/CONVENTIONS.md`](CONVENTIONS.md) — règles de développement.
- **Dictionnaire de données** : [`docs/DICTIONNAIRE_DONNEES.md`](DICTIONNAIRE_DONNEES.md) — modèle de données.
- **ADR** : [`docs/adr/`](adr/) — 11 décisions d'architecture.
- **Audit technique** : [`docs/AUDIT_2026-07-17_DOCUMENTATION_TECHNIQUE.md`](AUDIT_2026-07-17_DOCUMENTATION_TECHNIQUE.md) — état au 17/07/2026.
- **Audit plan d'implémentation** : [`docs/AUDIT_2026-07-17_PLAN_IMPLEMENTATION.md`](AUDIT_2026-07-17_PLAN_IMPLEMENTATION.md).
- **Documentation V4.0** : `Documentation de Référence du Projet/Version 4.0/` (Tomes A–F).
- **Référentiels réglementaires** : `Reglementation/`.

---

*Document rédigé le 21/07/2026 — synchronisé avec l'état du dépôt à cette date.*
