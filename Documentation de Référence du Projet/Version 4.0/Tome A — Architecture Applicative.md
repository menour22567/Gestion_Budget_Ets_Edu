# Documentation de Référence du Projet

# **PaieEducation ERP**

# **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome A — Architecture Applicative**

# **Volume 1**

# **Architecture Globale de la Solution**

**Version :** 4.0
**Statut :** Document de référence d'implémentation (**DDS – Detailed Design Specification**)
**Technologies :** .NET 10 LTS – WPF – MVVM – SQLite – CommunityToolkit.Mvvm – QuestPDF

---

# 1. Objectif du document

Ce document constitue la **spécification technique de référence** de l'architecture globale de **PaieEducation ERP**.

Contrairement au cahier des charges (Version 3.0), ce volume décrit **la manière dont l'application sera réellement construite**.

Il servira de référence :

* aux architectes logiciels ;
* aux développeurs C#/.NET ;
* aux responsables qualité ;
* aux futurs mainteneurs du projet.

Aucune décision architecturale majeure ne devra être prise en dehors des règles définies dans ce document.

---

# 2. Vision générale

L'application est un **ERP de paie** destiné aux établissements publics de l'Éducation nationale algérienne.

Elle est conçue selon les principes suivants :

* application Desktop moderne
* architecture modulaire
* fonctionnement 100 % hors ligne
* architecture orientée domaine
* séparation stricte des responsabilités
* forte testabilité
* faible couplage
* haute maintenabilité

L'ensemble du système est conçu pour pouvoir évoluer pendant plus de quinze ans sans remise en cause de son architecture fondamentale.

---

# 3. Principes d'architecture

L'architecture repose simultanément sur plusieurs modèles éprouvés :

| Principe                   | Utilisation                     |
| -------------------------- | ------------------------------- |
| Clean Architecture         | séparation des couches          |
| Domain Driven Design (DDD) | modélisation métier             |
| SOLID                      | conception orientée objet       |
| MVVM                       | interface WPF                   |
| Dependency Injection       | inversion des dépendances       |
| Repository Pattern         | accès aux données               |
| Unit Of Work               | gestion transactionnelle        |
| CQRS léger                 | séparation lecture/écriture     |
| Composition Root           | initialisation de l'application |

---

# 4. Objectifs architecturaux

L'architecture doit garantir :

* une indépendance totale entre l'interface et le métier ;
* une indépendance du moteur SQLite ;
* une forte évolutivité ;
* une testabilité maximale ;
* une maintenance facilitée ;
* une excellente lisibilité du code ;
* des performances adaptées à un ERP de paie.

---

# 5. Vue logique de l'application

```text
                    Utilisateur
                         │
                         ▼
                 WPF Presentation
                         │
                  ViewModels (MVVM)
                         │
                  Application Layer
                         │
                 Domain Services
                         │
                 Infrastructure
                         │
                  Persistence
                         │
                     SQLite
```

Chaque couche dépend uniquement de la couche immédiatement inférieure.

---

# 6. Architecture physique

La solution Visual Studio est organisée en plusieurs projets indépendants.

```text
PaieEducation.sln

│
├── PaieEducation.Presentation
├── PaieEducation.Application
├── PaieEducation.Domain
├── PaieEducation.Infrastructure
├── PaieEducation.Persistence
├── PaieEducation.Reporting
├── PaieEducation.Shared
├── PaieEducation.Tests.Unit
├── PaieEducation.Tests.Integration
└── PaieEducation.Tools
```

Chaque projet possède un rôle unique.

---

# 7. Description des projets

## 7.1 Presentation

Responsabilités :

* WPF
* MVVM
* Navigation
* Fenêtres
* Pages
* Dialogues
* Validation visuelle

Aucune règle métier.

---

## 7.2 Application

Responsabilités :

* cas d'utilisation
* orchestration
* DTO
* commandes
* requêtes
* services applicatifs

Aucune dépendance vers WPF.

---

## 7.3 Domain

Contient exclusivement :

* Entités
* Value Objects
* Services Métier
* Agrégats
* Domain Events
* Interfaces métier

Cette couche ne dépend d'aucune autre.

---

## 7.4 Infrastructure

Responsabilités :

* implémentation technique
* fichiers
* horloge système
* génération d'identifiants
* impression
* journalisation
* services système

---

## 7.5 Persistence

Responsabilités :

* SQLite
* Repository
* Transactions
* Migrations
* Backup

---

## 7.6 Reporting

Responsabilités :

* QuestPDF
* ClosedXML
* Impression
* Export

---

## 7.7 Shared

Contient :

* constantes
* exceptions communes
* utilitaires
* extensions
* types communs

---

# 8. Dépendances autorisées

```text
Presentation
      │
Application
      │
Domain

Infrastructure
      │
Persistence
```

Le projet **Presentation** ne doit jamais accéder directement à SQLite.

Le projet **Reporting** ne doit jamais connaître WPF.

Le projet **Domain** ne doit connaître aucun framework.

---

# 9. Règles de dépendance

Autorisé :

```text
Presentation
        ↓
Application
        ↓
Domain
```

Interdit :

```text
Presentation
        ↓
SQLite
```

Interdit :

```text
Domain
        ↓
WPF
```

Interdit :

```text
Application
        ↓
Window.xaml
```

Ces règles sont impératives.

---

# 10. Organisation des dossiers

Exemple pour Presentation :

```text
Presentation

Views
ViewModels
Navigation
Converters
Resources
Themes
Controls
Dialogs
Templates
Assets
```

---

# 11. Cycle de vie d'une requête

Exemple :

```text
Utilisateur

↓

Vue WPF

↓

ViewModel

↓

Use Case

↓

Domain Service

↓

Repository

↓

SQLite

↓

DTO

↓

ViewModel

↓

Vue
```

La vue n'appelle jamais directement le Repository.

---

# 12. Cycle de vie d'un calcul de paie

```text
Calcul demandé

↓

Application

↓

Payroll Engine

↓

Eligibility Engine

↓

Calculation Engine

↓

Repositories

↓

SQLite

↓

Résultat

↓

Bulletin DTO

↓

QuestPDF
```

Chaque moteur possède une responsabilité clairement définie.

---

# 13. Communication entre modules

Les modules communiquent uniquement :

* via interfaces
* via DTO
* via événements métier
* via Dependency Injection

Aucun module ne crée directement un autre module.

---

# 14. Injection de dépendances

Toutes les dépendances sont enregistrées dans un **Composition Root** unique.

Exemple :

```csharp
services.AddSingleton<IPayrollEngine, PayrollEngine>();

services.AddScoped<IAgentRepository, SQLiteAgentRepository>();

services.AddTransient<GeneratePayrollUseCase>();
```

Aucun `new Repository()` ne sera autorisé dans les ViewModels ou les services applicatifs.

---

# 15. Gestion des configurations

La configuration est centralisée.

Exemple :

```text
appsettings.json

↓

ConfigurationService

↓

Services
```

Aucun composant ne lit directement un fichier JSON.

---

# 16. Communication avec SQLite

Toute communication suit le flux suivant :

```text
Application

↓

Repository

↓

SQLiteConnectionFactory

↓

SQLite
```

Les connexions SQLite ne sont jamais manipulées directement par les ViewModels.

---

# 17. Architecture des services

Chaque service possède :

* une interface
* une implémentation
* une responsabilité unique

Exemple :

```text
IPayrollService

↓

PayrollService
```

---

# 18. Architecture des Repositories

Même principe.

```text
IAgentRepository

↓

SQLiteAgentRepository
```

---

# 19. Règles d'évolution

Toute nouvelle fonctionnalité devra respecter :

* SOLID
* DDD
* MVVM
* Clean Architecture

Aucune exception.

---

# 20. Objectifs de qualité

L'architecture devra satisfaire les critères suivants :

| Critère         | Objectif    |
| --------------- | ----------- |
| Couplage        | Faible      |
| Cohésion        | Élevée      |
| Testabilité     | Très élevée |
| Maintenabilité  | Très élevée |
| Lisibilité      | Très élevée |
| Évolutivité     | Très élevée |
| Réutilisabilité | Élevée      |

---

# 21. Décisions d'architecture (ADR)

Les décisions structurantes sont fixées dès cette première version :

| ADR     | Décision                                                               |
| ------- | ---------------------------------------------------------------------- |
| ADR-001 | Adoption de .NET 10 LTS                                                |
| ADR-002 | Interface WPF                                                          |
| ADR-003 | Pattern MVVM                                                           |
| ADR-004 | CommunityToolkit.Mvvm                                                  |
| ADR-005 | SQLite comme moteur local                                              |
| ADR-006 | QuestPDF pour les rapports                                             |
| ADR-007 | ClosedXML pour les exports Excel                                       |
| ADR-008 | Clean Architecture comme modèle de référence                           |
| ADR-009 | Domain-Driven Design pour la modélisation métier                       |
| ADR-010 | Injection de dépendances avec Microsoft.Extensions.DependencyInjection |

Ces ADR constituent les fondations du projet et ne pourront être remis en cause qu'au travers d'un processus formel de gouvernance architecturale.

# 22. Critères d'acceptation

L'architecture sera considérée conforme lorsque :

* aucun couplage interdit n'existe entre les couches ;
* toutes les dépendances transitent par des interfaces ;
* les projets respectent leur responsabilité unique ;
* les ViewModels ne contiennent aucune logique métier ;
* le domaine est indépendant de WPF, SQLite et QuestPDF ;
* toutes les dépendances sont résolues via l'injection de dépendances ;
* les règles de Clean Architecture et de DDD sont respectées.

---

# Conclusion

Le **Volume 1** établit les fondations techniques de **PaieEducation ERP Version 4.0**. Il définit l'architecture globale, les responsabilités de chaque projet, les règles de dépendance et les décisions d'architecture qui guideront l'ensemble du développement.

Il servira de référence à tous les volumes suivants afin de garantir une implémentation cohérente, robuste et durable.

### Volume suivant

Le **Volume 2** détaillera l'**architecture interne de chaque projet de la solution** (`Presentation`, `Application`, `Domain`, `Infrastructure`, `Persistence`, `Reporting` et `Shared`), avec leur arborescence complète, leurs conventions de nommage, leurs responsabilités détaillées et les interactions entre composants. Ce document constituera la base d'organisation de la solution Visual Studio avant le début du développement.

Parfait. À partir de maintenant, nous entrons dans une documentation d'implémentation comparable à celle produite dans les grands projets ERP. Le **Volume 2** ne décrit plus seulement les projets : il définit leur **structure interne**, leurs responsabilités, les conventions et les dépendances autorisées. Il servira directement de guide pour créer la solution Visual Studio.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

# **Version 4.0**

# **Documentation de Spécification d'Implémentation**

## **Tome A — Architecture Applicative**

# **Volume 2**

# **Architecture Interne des Projets de la Solution**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification

---

# 1. Objectif

Ce document définit l'organisation interne de chaque projet de la solution **PaieEducation ERP**.

Il répond aux questions suivantes :

* Quels projets composent la solution ?
* Quel est le rôle de chacun ?
* Quels dossiers doivent être créés ?
* Quelles dépendances sont autorisées ?
* Où doit être implémentée chaque fonctionnalité ?

L'objectif est de garantir une architecture homogène et maintenable.

---

# 2. Structure de la solution

La solution Visual Studio est composée des projets suivants :

```text
PaieEducation.sln
│
├── PaieEducation.Presentation
├── PaieEducation.Application
├── PaieEducation.Domain
├── PaieEducation.Infrastructure
├── PaieEducation.Persistence
├── PaieEducation.Reporting
├── PaieEducation.Shared
├── PaieEducation.Bootstrapper
├── PaieEducation.Tests.Unit
├── PaieEducation.Tests.Integration
└── PaieEducation.Tools
```

### Évolution par rapport à la V3

L'ajout d'un projet **PaieEducation.Bootstrapper** est recommandé.

Il devient le **Composition Root** de l'application :

* configuration ;
* injection des dépendances ;
* démarrage ;
* chargement des paramètres ;
* initialisation des services ;
* migrations SQLite.

Ainsi, **Presentation** ne contient plus aucune logique de démarrage.

---

# 3. Graphe des dépendances

```text
                          Bootstrapper
                               │
                               ▼
                        Presentation
                               │
                               ▼
                        Application
                               │
                               ▼
                            Domain
                     ▲          ▲
                     │          │
             Infrastructure  Persistence
                     │
                     ▼
                 Reporting
```

### Règles

* **Domain** ne dépend d'aucun autre projet.
* **Application** dépend uniquement de **Domain**.
* **Presentation** ne connaît jamais SQLite.
* **Reporting** consomme des DTO, jamais des entités métier.
* **Bootstrapper** est le seul projet qui référence tous les autres.

---

# 4. Projet `PaieEducation.Domain`

Le domaine représente le cœur de l'ERP.

### Arborescence recommandée

```text
Domain
│
├── Entities
├── Aggregates
├── ValueObjects
├── Enumerations
├── DomainServices
├── Specifications
├── Rules
├── Events
├── Exceptions
├── Interfaces
├── Policies
├── Calculations
├── Payroll
├── Regulations
├── Validation
└── Common
```

### Responsabilités

* Entités métier.
* Invariants.
* Règles métier.
* Calculs fondamentaux.
* Interfaces des repositories.
* Objets valeur.
* Domain Events.

### Interdictions

Le projet Domain ne doit jamais référencer :

* WPF ;
* SQLite ;
* QuestPDF ;
* ClosedXML ;
* Microsoft.Extensions.Logging.

---

# 5. Projet `PaieEducation.Application`

Il orchestre les cas d'utilisation.

### Arborescence

```text
Application
│
├── UseCases
├── Commands
├── Queries
├── Handlers
├── DTOs
├── Interfaces
├── Services
├── Validators
├── Mapping
├── Notifications
├── Pipelines
├── Authorization
├── Transactions
└── Common
```

### Responsabilités

* Exécution des cas d'utilisation.
* Orchestration des services métier.
* Validation applicative.
* Mapping DTO.
* Gestion des transactions.

Aucune logique d'interface.

---

# 6. Projet `PaieEducation.Presentation`

Ce projet contient exclusivement la couche WPF.

### Arborescence

```text
Presentation
│
├── Views
├── ViewModels
├── Controls
├── Dialogs
├── Pages
├── Navigation
├── Themes
├── Resources
├── Converters
├── Behaviors
├── Templates
├── Assets
├── Localization
└── Validation
```

### Responsabilités

* Affichage.
* Navigation.
* Validation visuelle.
* Liaison MVVM.
* Gestion des commandes utilisateur.

### Interdictions

* Aucun SQL.
* Aucun calcul de paie.
* Aucune règle métier.

---

# 7. Projet `PaieEducation.Infrastructure`

Cette couche fournit les implémentations techniques.

```text
Infrastructure
│
├── Logging
├── FileSystem
├── Printing
├── Configuration
├── Security
├── Time
├── Localization
├── Serialization
├── Diagnostics
├── Backup
├── Cache
└── Platform
```

### Responsabilités

* accès au système de fichiers ;
* horloge ;
* configuration ;
* journalisation ;
* chiffrement (si activé) ;
* services Windows.

---

# 8. Projet `PaieEducation.Persistence`

Couche d'accès aux données.

```text
Persistence
│
├── Context
├── Repositories
├── SQLite
├── Migrations
├── Seed
├── Queries
├── Commands
├── Transactions
├── Backup
├── Indexes
└── Configuration
```

### Responsabilités

* accès SQLite ;
* transactions ;
* migrations ;
* optimisation des requêtes ;
* sauvegardes.

---

# 9. Projet `PaieEducation.Reporting`

Sous-système documentaire.

```text
Reporting
│
├── Documents
├── Components
├── Layouts
├── Styles
├── Templates
├── Pdf
├── Excel
├── Printing
├── Preview
├── Assets
└── Fonts
```

### Responsabilités

* QuestPDF ;
* ClosedXML ;
* impression ;
* prévisualisation ;
* export.

---

# 10. Projet `PaieEducation.Shared`

Bibliothèque commune.

```text
Shared
│
├── Constants
├── Exceptions
├── Extensions
├── Helpers
├── Localization
├── Results
├── Guards
├── Primitives
└── Utilities
```

Aucune logique métier.

---

# 11. Projet `PaieEducation.Bootstrapper`

Ce projet est le point d'entrée de l'application.

### Responsabilités

* lecture de `appsettings.json` ;
* configuration des journaux ;
* enregistrement des services ;
* exécution des migrations ;
* création de la fenêtre principale ;
* initialisation des ressources.

Aucune logique métier.

---

# 12. Projets de tests

### Tests unitaires

```text
Tests.Unit
│
├── Domain
├── Application
├── Infrastructure
└── Shared
```

### Tests d'intégration

```text
Tests.Integration
│
├── SQLite
├── Reporting
├── Persistence
├── Payroll
└── EndToEnd
```

---

# 13. Conventions de nommage

| Élément      | Convention      |
| ------------ | --------------- |
| Interface    | `I...`          |
| Service      | `...Service`    |
| Repository   | `...Repository` |
| ViewModel    | `...ViewModel`  |
| Vue WPF      | `...View`       |
| DTO          | `...Dto`        |
| Commande     | `...Command`    |
| Requête      | `...Query`      |
| Gestionnaire | `...Handler`    |

Les noms doivent refléter clairement leur responsabilité.

---

# 14. Règles de communication

Les projets communiquent uniquement par :

* interfaces ;
* DTO ;
* événements métier ;
* injection de dépendances.

Aucune référence circulaire n'est autorisée.

---

# 15. Gestion des références

| Projet         | Peut référencer           |
| -------------- | ------------------------- |
| Domain         | Aucun projet métier       |
| Application    | Domain                    |
| Infrastructure | Domain, Shared            |
| Persistence    | Domain, Shared            |
| Reporting      | Application (DTO), Shared |
| Presentation   | Application, Shared       |
| Bootstrapper   | Tous                      |

Cette matrice est normative.

---

# 16. Packaging et distribution

Chaque projet produit un assembly dédié (`.dll`), à l'exception du **Bootstrapper**, qui produit l'exécutable principal (`PaieEducation.exe`). Cette séparation facilite :

* les tests ;
* la maintenance ;
* le remplacement d'un module ;
* les évolutions futures.

---

# 17. Critères de qualité

Chaque projet doit respecter :

* une responsabilité unique ;
* une documentation XML publique ;
* une couverture de tests adaptée ;
* des dépendances minimales ;
* des conventions de codage homogènes.

---

# 18. ADR complémentaires

| ADR     | Décision                                                       |
| ------- | -------------------------------------------------------------- |
| ADR-011 | Introduction d'un projet `Bootstrapper` comme Composition Root |
| ADR-012 | Un projet = une responsabilité principale                      |
| ADR-013 | Communication entre couches via interfaces et DTO uniquement   |
| ADR-014 | Aucune référence circulaire entre projets                      |
| ADR-015 | Arborescence standardisée et commune à tous les modules        |

---

# 19. Critères d'acceptation

La structure de la solution est validée lorsque :

* tous les projets sont créés conformément à cette spécification ;
* les dépendances respectent la matrice définie ;
* les conventions de nommage sont appliquées ;
* chaque fonctionnalité est implémentée dans le projet approprié ;
* aucune logique métier ne se trouve dans `Presentation` ou `Bootstrapper`.

---

# Conclusion

Le **Volume 2** constitue le **plan de construction** de la solution Visual Studio. Il normalise la structure des projets, les responsabilités, les dépendances et les conventions, afin de garantir une implémentation cohérente et évolutive.

## Recommandation avant le Volume 3

Pour aller encore plus loin, je recommande que le **Volume 3** ne se limite pas à la navigation WPF. Il devrait définir un **Application Shell Framework**, comparable à ceux utilisés dans les ERP modernes (SAP, Dynamics 365, Odoo Desktop ou Cegid).

Il décrira notamment :

* le Shell principal (`MainWindow`) ;
* la gestion des régions et des panneaux ;
* le système de navigation centralisé ;
* les menus, le ruban (*Ribbon*), la barre d'état et les tableaux de bord ;
* la gestion des fenêtres, des dialogues et des notifications ;
* les services de navigation (`INavigationService`) et leur intégration MVVM.

Ce document constituera la base de toute l'expérience utilisateur de **PaieEducation ERP**.

# Documentation de Référence du Projet

# **PaieEducation ERP**

# **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome A — Architecture Applicative**

# **Volume 3**

# **Architecture du Shell WPF, Navigation et Framework d'Interface Utilisateur**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification
**Technologies :** .NET 10 LTS • WPF • MVVM • CommunityToolkit.Mvvm

---

# 1. Objet

Ce document définit l'architecture complète de l'interface utilisateur de **PaieEducation ERP**.

Il ne s'agit pas uniquement de décrire la fenêtre principale (**MainWindow**), mais de spécifier le **Framework d'Interface Utilisateur** qui servira de fondation à l'ensemble de l'ERP.

L'objectif est de garantir :

* une interface homogène ;
* une navigation cohérente ;
* une expérience utilisateur professionnelle ;
* une évolutivité sans remise en cause de l'architecture.

---

# 2. Principes directeurs

Le Shell doit respecter les principes suivants :

* séparation stricte entre Vue et ViewModel ;
* navigation pilotée par des services ;
* absence de logique métier dans les vues ;
* découplage complet entre les modules ;
* extensibilité par ajout de modules.

---

# 3. Architecture générale

```text
+------------------------------------------------------------+
|                       MainWindow                           |
|------------------------------------------------------------|
| Ribbon / Barre d'outils                                    |
|------------------------------------------------------------|
| Menu principal                                              |
|------------------------------------------------------------|
| Breadcrumb / Navigation                                    |
|------------------------------------------------------------|
|                                                            |
|            Région de contenu (Content Region)              |
|                                                            |
|------------------------------------------------------------|
| Barre d'état                                                |
+------------------------------------------------------------+
```

Le **MainWindow** agit comme un **conteneur**. Les écrans métier sont chargés dynamiquement dans la région de contenu.

---

# 4. Le Shell de l'application

Le Shell constitue le point d'entrée visuel de l'ERP.

Il est composé de :

* fenêtre principale ;
* menu principal ;
* ruban d'actions (*Ribbon*) ;
* panneau de navigation ;
* zone de contenu ;
* barre d'état ;
* notifications.

Aucun écran métier n'est hébergé directement dans `MainWindow.xaml`.

---

# 5. Structure du projet Presentation

```text
Presentation

Views
│
├── Shell
│     MainWindow.xaml
│
├── Dashboard
├── Payroll
├── Employees
├── Administration
├── Reports
├── Parameters
├── Settings
└── Common
```

Chaque module dispose de son propre dossier.

---

# 6. Structure des ViewModels

```text
ViewModels

Shell
Dashboard
Payroll
Employees
Reports
Administration
Settings
Common
```

Chaque vue possède exactement un ViewModel dédié.

---

# 7. Hiérarchie de navigation

```text
MainWindow

↓

Dashboard

↓

Module

↓

Page

↓

Dialogue
```

Cette hiérarchie garantit une navigation simple et prévisible.

---

# 8. Navigation centralisée

La navigation repose sur un service unique.

```text
INavigationService

↓

NavigationService
```

Toutes les demandes de navigation passent par cette interface.

Les vues ne créent jamais directement d'autres vues.

---

# 9. Responsabilités du NavigationService

Le service est chargé de :

* ouvrir une page ;
* remplacer une page ;
* revenir en arrière ;
* ouvrir une fenêtre modale ;
* gérer l'historique ;
* transmettre les paramètres de navigation.

---

# 10. Navigation MVVM

Flux standard :

```text
Bouton

↓

RelayCommand

↓

ViewModel

↓

INavigationService

↓

Vue demandée

↓

Affichage
```

Aucune référence directe à `Frame.Navigate()` n'est autorisée dans les ViewModels.

---

# 11. Types de navigation

Le framework prend en charge :

| Type                    | Description                |
| ----------------------- | -------------------------- |
| Navigation principale   | Changement de module       |
| Navigation secondaire   | Changement de page         |
| Navigation modale       | Fenêtres de dialogue       |
| Navigation contextuelle | Assistant, recherche, aide |
| Navigation historique   | Retour / Avancer           |

---

# 12. Menu principal

Le menu principal est organisé par domaines fonctionnels.

Exemple :

```text
Accueil

Personnel

Paie

Référentiels

Documents

Administration

Outils

Paramètres

Aide
```

Cette structure est indépendante des implémentations.

---

# 13. Ruban d'actions (Ribbon)

Chaque module expose ses actions dans un ruban contextuel.

Exemple :

**Personnel**

* Nouveau
* Modifier
* Supprimer
* Rechercher
* Exporter

**Paie**

* Calculer
* Recalculer
* Valider
* Générer les bulletins
* Clôturer

---

# 14. Tableau de bord (Dashboard)

Le Dashboard constitue la page d'accueil.

Il affiche :

* période courante ;
* nombre d'agents ;
* derniers traitements ;
* alertes ;
* sauvegarde la plus récente ;
* indicateurs de paie ;
* raccourcis.

Il est entièrement configurable.

---

# 15. Barre d'état

La barre d'état affiche en permanence :

* utilisateur courant ;
* période de paie ;
* version de l'application ;
* état de la base SQLite ;
* état des sauvegardes ;
* heure système.

---

# 16. Dialogues

Tous les dialogues sont gérés via un service.

```text
IDialogService

↓

DialogService
```

Exemples :

* confirmation ;
* erreur ;
* information ;
* sélection ;
* progression.

Les ViewModels ne créent jamais directement une `Window`.

---

# 17. Notifications

Le système de notifications gère :

* informations ;
* avertissements ;
* erreurs ;
* succès ;
* tâches en cours.

Les notifications sont non bloquantes lorsque cela est possible.

---

# 18. Gestion des fenêtres

Le Shell distingue plusieurs types de fenêtres :

| Type          | Usage                  |
| ------------- | ---------------------- |
| MainWindow    | Conteneur principal    |
| DialogWindow  | Confirmation et saisie |
| ToolWindow    | Outils annexes         |
| PreviewWindow | Prévisualisation PDF   |
| LookupWindow  | Sélection de données   |

Chaque type possède un comportement standardisé.

---

# 19. Navigation par paramètres

Le service de navigation doit permettre le passage de paramètres.

Exemple :

```text
EmployeeList

↓

Sélection

↓

EmployeeDetail

↓

Matricule = 000145
```

Les paramètres sont transmis de manière typée.

---

# 20. Gestion de l'historique

Le framework conserve un historique des pages consultées afin de permettre :

* retour ;
* avance ;
* réouverture du dernier écran.

---

# 21. Thèmes graphiques

Le Shell prend en charge :

* thème clair ;
* thème sombre ;
* couleurs institutionnelles ;
* personnalisation limitée.

Les ressources sont centralisées dans des dictionnaires WPF (`ResourceDictionary`).

---

# 22. Accessibilité

L'interface doit respecter les principes suivants :

* navigation clavier complète ;
* raccourcis clavier documentés ;
* contraste suffisant ;
* polices redimensionnables ;
* messages explicites.

---

# 23. Performances

Objectifs :

| Opération                         | Temps cible |
| --------------------------------- | ----------: |
| Ouverture du Shell                |       < 2 s |
| Changement de module              |    < 300 ms |
| Ouverture d'une boîte de dialogue |    < 200 ms |
| Rafraîchissement d'une page       |    < 500 ms |

---

# 24. Interfaces publiques

Les interfaces suivantes sont définies comme standards :

```text
INavigationService
IDialogService
INotificationService
IWindowService
IBreadcrumbService
IStatusBarService
```

Toute implémentation devra respecter ces contrats.

---

# 25. Architecture des ViewModels

Tous les ViewModels héritent d'une classe de base commune :

```text
ObservableObject
        │
BaseViewModel
        │
-----------------------------
│            │             │
ShellVM   EmployeeVM   PayrollVM
```

`BaseViewModel` fournit les fonctionnalités communes :

* gestion de l'état (`IsBusy`, `HasErrors`) ;
* notifications de propriétés ;
* commandes globales ;
* accès aux services injectés.

---

# 26. Critères d'acceptation

L'architecture du Shell est validée lorsque :

* toute navigation passe par `INavigationService` ;
* aucune vue ne contient de logique métier ;
* aucun ViewModel n'instancie directement une `Window` ;
* les dialogues sont gérés par `IDialogService` ;
* les notifications utilisent `INotificationService` ;
* le Shell reste indépendant des modules métier.

---

# 27. ADR (Architecture Decision Records)

| ADR     | Décision                                                                   |
| ------- | -------------------------------------------------------------------------- |
| ADR-016 | Le Shell est le conteneur unique de l'application                          |
| ADR-017 | Navigation centralisée via `INavigationService`                            |
| ADR-018 | Dialogues gérés exclusivement par `IDialogService`                         |
| ADR-019 | Dashboard comme page d'accueil par défaut                                  |
| ADR-020 | Architecture modulaire avec chargement des vues dans une région de contenu |

---

# Conclusion

Le **Volume 3** définit le **Framework d'Interface Utilisateur** de **PaieEducation ERP**. Il normalise la structure du Shell, les services de navigation, les dialogues, les notifications et l'organisation générale de l'expérience utilisateur. Ce document constitue la base sur laquelle tous les écrans métiers seront construits.

## Recommandation pour le Volume 4

Je recommande que le **Volume 4** soit consacré au **Design System WPF**. Il décrira de manière exhaustive :

* la charte graphique de l'ERP ;
* les palettes de couleurs et la typographie ;
* les styles et `ResourceDictionary` ;
* les composants réutilisables (boutons, DataGrid, formulaires, cartes, tableaux de bord) ;
* les règles d'ergonomie et d'accessibilité ;
* les conventions XAML.

Ce volume permettra d'obtenir une interface uniforme, professionnelle et facilement maintenable sur l'ensemble de l'application.

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome A — Architecture Applicative**

# **Volume 4**

# **Design System WPF – Charte Graphique, Composants UI et Standards d'Ergonomie**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification
**Technologies :** .NET 10 LTS • WPF • MVVM • CommunityToolkit.Mvvm

---

# 1. Objet

Ce document définit le **Design System officiel** de **PaieEducation ERP**.

Son objectif est d'assurer une interface :

* homogène ;
* professionnelle ;
* ergonomique ;
* accessible ;
* facilement maintenable.

Le Design System constitue un **référentiel transversal** : toute nouvelle vue, tout contrôle personnalisé et tout module devront s'y conformer.

---

# 2. Principes de conception

L'interface utilisateur repose sur les principes suivants :

* simplicité ;
* cohérence visuelle ;
* hiérarchie de l'information ;
* réduction de la charge cognitive ;
* rapidité d'exécution des tâches ;
* accessibilité ;
* réutilisation maximale des composants.

L'utilisateur doit retrouver les mêmes conventions dans tous les modules.

---

# 3. Architecture des ressources graphiques

```text
Presentation
│
└── Resources
    │
    ├── Themes
    │     LightTheme.xaml
    │     DarkTheme.xaml
    │
    ├── Colors.xaml
    ├── Typography.xaml
    ├── Icons.xaml
    ├── Brushes.xaml
    ├── Styles.xaml
    ├── Controls.xaml
    ├── DataGrid.xaml
    ├── Forms.xaml
    ├── Buttons.xaml
    ├── Dialogs.xaml
    └── Animations.xaml
```

Les ressources sont regroupées dans des `ResourceDictionary` afin de favoriser la modularité et la réutilisation.

---

# 4. Identité visuelle

L'ERP adopte une identité visuelle sobre, adaptée à un contexte administratif.

Principes :

* fond clair par défaut ;
* accentuation limitée aux actions importantes ;
* couleurs utilisées pour la signification fonctionnelle (succès, avertissement, erreur) ;
* icônes vectorielles homogènes.

Les éléments décoratifs sont limités au strict nécessaire.

---

# 5. Palette de couleurs

La palette est organisée par rôles plutôt que par valeurs RGB.

| Rôle        | Utilisation                  |
| ----------- | ---------------------------- |
| Primary     | Actions principales          |
| Secondary   | Actions secondaires          |
| Surface     | Fonds des cartes et panneaux |
| Background  | Fond général                 |
| Success     | Validation                   |
| Warning     | Avertissements               |
| Error       | Erreurs                      |
| Information | Messages informatifs         |
| Disabled    | Contrôles inactifs           |

Toutes les couleurs sont définies sous forme de ressources nommées.

---

# 6. Typographie

Une police système moderne est recommandée (ex. **Segoe UI Variable** ou équivalent).

Échelle typographique :

| Style   | Usage                    |
| ------- | ------------------------ |
| Display | Tableau de bord          |
| H1      | Titres principaux        |
| H2      | Sections                 |
| H3      | Sous-sections            |
| Body    | Texte courant            |
| Caption | Informations secondaires |
| Mono    | Données techniques       |

Les tailles sont centralisées dans `Typography.xaml`.

---

# 7. Grille de mise en page

Le système adopte une grille de **8 px**.

Espacements standard :

| Élément    | Valeur |
| ---------- | ------ |
| Très petit | 4 px   |
| Petit      | 8 px   |
| Moyen      | 16 px  |
| Grand      | 24 px  |
| Très grand | 32 px  |

Cette grille garantit une interface harmonieuse.

---

# 8. Composants fondamentaux

Les composants suivants sont normalisés :

* bouton ;
* champ de saisie ;
* liste déroulante ;
* case à cocher ;
* bouton radio ;
* calendrier ;
* sélecteur de période ;
* DataGrid ;
* TreeView ;
* ListView ;
* onglets ;
* cartes ;
* panneaux extensibles.

Chaque composant dispose d'un style unique.

---

# 9. Boutons

Catégories :

| Type      | Usage             |
| --------- | ----------------- |
| Primary   | Action principale |
| Secondary | Action secondaire |
| Outline   | Action discrète   |
| Icon      | Action rapide     |
| Danger    | Suppression       |
| Success   | Validation        |

Les boutons utilisent les mêmes dimensions et marges dans toute l'application.

---

# 10. Formulaires

Les formulaires suivent des règles communes :

* alignement vertical des champs ;
* libellés explicites ;
* indication des champs obligatoires ;
* validation immédiate ;
* regroupement logique des informations.

Les formulaires ne dépassent pas une largeur favorisant une lecture confortable.

---

# 11. DataGrid

Le DataGrid est un composant central de l'ERP.

Fonctionnalités standard :

* tri ;
* filtrage ;
* recherche ;
* redimensionnement des colonnes ;
* gel de colonnes ;
* export ;
* sélection multiple ;
* virtualisation.

Les styles sont définis dans un dictionnaire dédié.

---

# 12. Cartes (Cards)

Les tableaux de bord utilisent des cartes pour afficher :

* indicateurs ;
* alertes ;
* statistiques ;
* raccourcis.

Les cartes partagent une apparence homogène.

---

# 13. Icônes

Les icônes sont :

* vectorielles ;
* monochromes par défaut ;
* cohérentes sur l'ensemble de l'ERP.

Une même action utilise toujours la même icône.

---

# 14. États visuels

Chaque contrôle possède des états standardisés :

* normal ;
* survol ;
* focus ;
* actif ;
* désactivé ;
* erreur ;
* validation.

Ces états sont gérés exclusivement par des styles WPF.

---

# 15. Validation visuelle

Les erreurs de saisie sont présentées de manière uniforme :

* mise en évidence du champ ;
* message explicite ;
* icône d'avertissement ;
* conservation de la valeur saisie.

Les validations bloquantes et non bloquantes sont distinguées.

---

# 16. Messages et notifications

Les notifications sont classées selon quatre niveaux :

| Niveau        | Couleur sémantique |
| ------------- | ------------------ |
| Information   | Information        |
| Succès        | Success            |
| Avertissement | Warning            |
| Erreur        | Error              |

Les messages doivent être rédigés dans un langage clair et orienté action.

---

# 17. Accessibilité

Le Design System respecte les principes suivants :

* navigation complète au clavier ;
* contraste conforme aux recommandations WCAG ;
* focus visible ;
* taille de police adaptable ;
* compatibilité avec les lecteurs d'écran lorsque cela est pertinent.

---

# 18. Internationalisation

Tous les textes de l'interface sont externalisés dans des ressources.

Le système doit permettre l'ajout de nouvelles langues sans modification des vues.

---

# 19. Performances

Objectifs :

| Élément                        |    Cible |
| ------------------------------ | -------: |
| Chargement d'une vue           | < 300 ms |
| Ouverture d'un formulaire      | < 200 ms |
| Rafraîchissement d'un DataGrid | < 500 ms |
| Changement de thème            |    < 1 s |

Les composants doivent limiter les recalculs et privilégier la virtualisation.

---

# 20. Bibliothèque de composants

Les composants réutilisables sont regroupés dans `Presentation/Controls`.

Exemples :

* `EmployeeSelector`
* `PayrollPeriodPicker`
* `CurrencyTextBox`
* `SearchBox`
* `StatusBadge`
* `StatisticCard`
* `PdfPreviewControl`
* `ValidationSummary`
* `LoadingOverlay`
* `BusyIndicator`

Chaque composant est documenté et testé.

---

# 21. Conventions XAML

Les règles suivantes sont obligatoires :

* utilisation des ressources statiques ou dynamiques plutôt que de valeurs codées en dur ;
* styles centralisés ;
* absence de logique métier dans le code-behind ;
* séparation claire entre mise en forme et comportement.

Le code XAML doit rester lisible et cohérent.

---

# 22. Documentation des composants

Chaque composant de la bibliothèque doit être accompagné :

* d'une description fonctionnelle ;
* de ses propriétés publiques ;
* d'exemples d'utilisation ;
* de ses dépendances ;
* de ses contraintes.

Cette documentation facilite la réutilisation.

---

# 23. Critères d'acceptation

Le Design System est considéré conforme lorsque :

* tous les écrans utilisent les styles communs ;
* aucune couleur ou police n'est codée en dur ;
* les composants réutilisables sont privilégiés aux implémentations spécifiques ;
* les règles d'accessibilité sont respectées ;
* les performances des composants sont conformes aux objectifs.

---

# 24. ADR (Architecture Decision Records)

| ADR     | Décision                                                 |
| ------- | -------------------------------------------------------- |
| ADR-021 | Adoption d'un Design System centralisé                   |
| ADR-022 | Ressources graphiques organisées en `ResourceDictionary` |
| ADR-023 | Bibliothèque de contrôles réutilisables                  |
| ADR-024 | Palette de couleurs définie par rôles fonctionnels       |
| ADR-025 | Composants UI documentés et testés individuellement      |

---

# Conclusion

Le **Volume 4** établit le **Design System officiel** de **PaieEducation ERP**. Il garantit une expérience utilisateur cohérente, accessible et professionnelle, tout en simplifiant le développement grâce à des composants réutilisables et des conventions communes.

## Recommandation pour le Volume 5

À ce stade, je recommande une légère évolution du plan initial. Avant d'aborder d'autres aspects de l'architecture générale, il est stratégique de documenter le **Framework MVVM** qui servira de socle à toute l'application.

Le **Volume 5** devrait donc couvrir :

* l'architecture MVVM retenue avec **CommunityToolkit.Mvvm** ;
* la hiérarchie des `ViewModel` (`BaseViewModel`, `ScreenViewModel`, etc.) ;
* les commandes (`RelayCommand`, `AsyncRelayCommand`) ;
* la gestion de l'état (`IsBusy`, `HasErrors`, `IsDirty`) ;
* la validation (`ObservableValidator`) ;
* la messagerie (`WeakReferenceMessenger`) ;
* les bonnes pratiques d'injection de dépendances et de test des ViewModels.

Ce volume deviendra le **guide de développement MVVM** de référence pour l'ensemble du projet.


Je recommande une légère amélioration de la structure avant de poursuivre.

Jusqu'ici, nous avons documenté :

* **Volume 1** : Architecture globale
* **Volume 2** : Organisation des projets
* **Volume 3** : Shell WPF
* **Volume 4** : Design System
* **Volume 5** : Framework MVVM

Dans un véritable ERP, il manque encore une brique fondamentale avant d'aborder les services techniques : **le modèle de composition de l'application**. Cette couche décrit comment les modules sont découverts, initialisés, activés et communiquent entre eux. Sans elle, les volumes suivants risqueraient de faire des hypothèses différentes sur le cycle de vie des modules.

Je propose donc de consacrer le **Volume 6** à cette architecture.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome A — Architecture Applicative**

# **Volume 6**

# **Architecture Modulaire, Composition Root et Cycle de Vie de l'Application**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification

---

# 1. Objectif

Ce document définit :

* l'architecture modulaire de l'ERP ;
* le démarrage de l'application ;
* l'initialisation des services ;
* le chargement des modules ;
* le cycle de vie de chaque composant ;
* les règles d'enregistrement dans l'injection de dépendances.

Il garantit que chaque module s'intègre de manière uniforme, sans dépendances implicites.

---

# 2. Principe de modularité

L'application est composée de modules fonctionnels indépendants.

Exemples :

```text
Accueil
Personnel
Paie
Rubriques
Référentiels
Documents
Paramètres
Administration
Audit
Reporting
```

Chaque module possède :

* ses vues ;
* ses ViewModels ;
* ses cas d'utilisation ;
* ses services applicatifs ;
* ses ressources.

---

# 3. Définition d'un module

Un module est une unité fonctionnelle autonome qui :

* expose un point d'entrée ;
* enregistre ses dépendances ;
* fournit ses écrans ;
* n'accède pas directement aux modules voisins.

Un module ne doit pas connaître l'implémentation interne d'un autre module.

---

# 4. Composition Root

Le **Composition Root** est implémenté dans `PaieEducation.Bootstrapper`.

Responsabilités :

* lecture de la configuration ;
* création du conteneur DI ;
* enregistrement des services ;
* initialisation des modules ;
* vérification des migrations SQLite ;
* ouverture du Shell.

Il s'agit du seul endroit où les dépendances concrètes sont instanciées.

---

# 5. Cycle de démarrage

```text
Lancement de l'application
        │
        ▼
Lecture de la configuration
        │
        ▼
Initialisation du logging
        │
        ▼
Création du conteneur DI
        │
        ▼
Enregistrement des modules
        │
        ▼
Initialisation SQLite
        │
        ▼
Vérification des migrations
        │
        ▼
Création du MainWindow
        │
        ▼
Chargement du Dashboard
```

Chaque étape est validée avant de poursuivre.

---

# 6. Contrat d'un module

Chaque module implémente une interface commune.

Exemple :

```csharp
public interface IModule
{
    string Name { get; }

    void RegisterServices(IServiceCollection services);

    Task InitializeAsync();
}
```

Ainsi, le Bootstrapper peut découvrir et initialiser tous les modules de manière uniforme.

---

# 7. Enregistrement des services

Les modules enregistrent uniquement leurs propres dépendances.

Exemple :

```csharp
services.AddScoped<IEmployeeService, EmployeeService>();
services.AddTransient<EmployeeListViewModel>();
services.AddTransient<EmployeeDetailViewModel>();
```

Aucun module ne doit enregistrer les services d'un autre.

---

# 8. Cycle de vie des services

Les durées de vie sont normalisées :

| Type      | Durée                              |
| --------- | ---------------------------------- |
| Singleton | Toute la durée de l'application    |
| Scoped    | Une opération métier               |
| Transient | Nouvelle instance à chaque demande |

Le choix de la durée de vie doit être justifié dans la documentation du service.

---

# 9. Initialisation des modules

L'initialisation comprend notamment :

* chargement des paramètres ;
* vérification des ressources ;
* préchargement des données de référence si nécessaire ;
* enregistrement des commandes globales.

Les traitements longs sont exécutés de manière asynchrone.

---

# 10. Communication entre modules

La communication directe est interdite.

Les échanges passent par :

* interfaces ;
* DTO ;
* événements applicatifs ;
* messagerie (`WeakReferenceMessenger`) lorsque la communication est locale à l'interface.

Cette règle réduit le couplage.

---

# 11. Gestion des événements

Les événements applicatifs servent à notifier des changements d'état.

Exemples :

* période de paie modifiée ;
* agent mis à jour ;
* sauvegarde terminée ;
* clôture de période effectuée.

Les abonnements doivent être libérés correctement afin d'éviter les fuites mémoire.

---

# 12. Initialisation différée

Les modules rarement utilisés peuvent être chargés à la demande (*lazy loading*).

Exemples :

* Administration ;
* Audit ;
* Outils de maintenance.

Cette approche réduit le temps de démarrage.

---

# 13. Gestion des erreurs au démarrage

Les erreurs sont classées :

| Niveau        | Comportement                            |
| ------------- | --------------------------------------- |
| Bloquante     | Arrêt de l'application                  |
| Dégradée      | Démarrage avec fonctionnalités limitées |
| Avertissement | Notification et poursuite               |

Toutes les erreurs sont journalisées.

---

# 14. Configuration des modules

Chaque module peut disposer d'une section dédiée dans la configuration de l'application.

La lecture de cette configuration est réalisée via un service centralisé, jamais directement dans les ViewModels.

---

# 15. Extensibilité

L'architecture permet d'ajouter un nouveau module sans modifier les modules existants.

Le processus consiste à :

1. créer le projet ou le dossier du module ;
2. implémenter `IModule` ;
3. enregistrer les services ;
4. déclarer les routes de navigation ;
5. ajouter les entrées de menu.

Aucune modification du cœur de l'application n'est nécessaire.

---

# 16. Journalisation du cycle de vie

Les étapes suivantes sont enregistrées :

* démarrage ;
* initialisation des modules ;
* erreurs critiques ;
* fermeture ;
* durée de chargement.

Ces informations facilitent le diagnostic.

---

# 17. Critères d'acceptation

L'architecture modulaire est validée lorsque :

* tous les modules implémentent le contrat `IModule` ;
* le Bootstrapper est le seul Composition Root ;
* les dépendances sont enregistrées par le module propriétaire ;
* la communication respecte les interfaces définies ;
* le chargement différé est utilisé pour les modules non essentiels.

---

# 18. ADR (Architecture Decision Records)

| ADR     | Décision                                                      |
| ------- | ------------------------------------------------------------- |
| ADR-031 | Architecture modulaire fondée sur un contrat `IModule`        |
| ADR-032 | Bootstrapper unique comme Composition Root                    |
| ADR-033 | Enregistrement décentralisé des services par module           |
| ADR-034 | Communication entre modules par interfaces, DTO et événements |
| ADR-035 | Chargement différé des modules secondaires                    |

---

# Conclusion

Le **Volume 6** formalise le **cycle de vie complet de l'application** et l'architecture modulaire. Il garantit que les modules de **PaieEducation ERP** pourront évoluer indépendamment, tout en restant intégrés de manière cohérente.

## Recommandation pour le Volume 7

Le prochain volume devrait être consacré à un élément essentiel qui conditionne toute l'implémentation : le **contrat d'architecture de la couche Domain**.

Il définira notamment :

* les entités métier ;
* les agrégats ;
* les objets valeur (*Value Objects*) ;
* les règles métier (*Business Rules*) ;
* les services de domaine ;
* les événements de domaine ;
* les spécifications (*Specifications*) ;
* les invariants.

Ce document sera la pierre angulaire du moteur de paie et de l'ensemble de la logique métier.
