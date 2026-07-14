Excellente idée. Après avoir couvert les **18 volumes** du cahier des charges, il est préférable de ne pas s'arrêter. Les ERP professionnels disposent généralement d'une documentation de **30 à 60 documents techniques**.

Je propose donc de démarrer la **Version 3.0**, qui ne remplace pas les volumes précédents mais les approfondit. Cette documentation sera davantage orientée **architecture logicielle** et **ingénierie de développement**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome G — Architecture Applicative Avancée**

# **Volume 19**

# **Architecture WPF, MVVM et Framework de Présentation**

**Version :** 3.0
**Statut :** Référentiel officiel de la couche Presentation

---

# 1. Objectif

Ce volume définit l'architecture complète de la couche **Presentation**.

Il constitue la référence officielle pour :

* WPF (.NET 10 LTS)
* MVVM
* Navigation
* Fenêtres
* Dialogues
* Contrôles personnalisés
* Thèmes
* Styles
* Ressources
* Validation
* UX

Aucune logique métier n'est autorisée dans cette couche.

---

# 2. Position dans l'architecture

```text
Presentation (WPF)

↓

Application

↓

Domain

↓

Infrastructure

↓

Persistence

↓

SQLite
```

La couche WPF ne communique jamais directement avec SQLite.

---

# 3. Objectifs de conception

L'interface doit être :

* moderne
* rapide
* intuitive
* responsive
* cohérente
* accessible
* maintenable
* découplée

---

# 4. Organisation du projet

```text
PaieEducation.Presentation

│

├── App

├── Themes

├── Styles

├── Resources

├── Views

├── ViewModels

├── Controls

├── Behaviors

├── Converters

├── Validation

├── Navigation

├── Dialogs

├── Icons

├── Assets

└── Extensions
```

---

# 5. Architecture MVVM

```text
Vue

↓

Binding

↓

ViewModel

↓

Application Service

↓

Domain
```

Chaque couche possède une responsabilité unique.

---

# 6. Règles MVVM

Une View :

* ne contient aucun calcul métier ;
* ne connaît jamais SQLite ;
* ne connaît jamais les repositories ;
* contient uniquement l'interface.

Le ViewModel :

* orchestre les commandes ;
* dialogue avec Application ;
* expose les propriétés ;
* notifie les changements.

---

# 7. CommunityToolkit.Mvvm

Le projet utilise exclusivement :

* ObservableObject
* ObservableProperty
* RelayCommand
* AsyncRelayCommand
* WeakReferenceMessenger
* ObservableRecipient

L'utilisation de `INotifyPropertyChanged` manuel est proscrite.

---

# 8. Structure des Views

```text
Views

│

├── Shell

├── Dashboard

├── Agents

├── Payroll

├── Parameters

├── Reports

├── Administration

└── Shared
```

Les vues sont regroupées par domaine fonctionnel.

---

# 9. Structure des ViewModels

```text
ViewModels

│

├── Shell

├── Dashboard

├── Payroll

├── Agent

├── Report

├── Settings

└── Shared
```

Chaque View possède son ViewModel dédié.

---

# 10. Navigation

La navigation est centralisée via un service dédié.

```text
NavigationService

↓

CurrentViewModel

↓

CurrentView
```

Les vues ne créent jamais d'autres vues directement.

---

# 11. Fenêtre principale (Shell)

La fenêtre principale comprend :

* barre de titre ;
* menu latéral ;
* barre d'outils ;
* zone de navigation ;
* barre d'état ;
* notifications.

Elle constitue le conteneur principal de l'application.

---

# 12. Navigation hiérarchique

```text
Accueil

├── RH

│     ├── Agents

│     ├── Contrats

│     └── Carrière

├── Paie

│     ├── Calcul

│     ├── Bulletins

│     └── Variables

├── Référentiels

├── Reporting

└── Administration
```

La structure reflète les domaines métier.

---

# 13. Dialogues

Les fenêtres modales sont remplacées autant que possible par des dialogues WPF réutilisables.

Catégories :

* Confirmation
* Information
* Erreur
* Sélection
* Recherche
* Paramétrage

---

# 14. Contrôles réutilisables

Le projet définit une bibliothèque de contrôles internes.

Exemples :

* SearchBox
* NumericTextBox
* CurrencyBox
* PeriodPicker
* EmployeeSelector
* RubricSelector
* Toolbar
* StatusBar
* LoadingOverlay
* ValidationSummary

Ces composants sont mutualisés dans toute l'application.

---

# 15. Styles

Tous les styles sont centralisés.

```text
Themes

↓

Colors.xaml

↓

Typography.xaml

↓

Buttons.xaml

↓

DataGrid.xaml

↓

Forms.xaml

↓

Dialogs.xaml
```

Aucun style inline n'est autorisé.

---

# 16. Système de thèmes

Deux thèmes sont prévus :

* Clair
* Sombre

Le changement de thème est dynamique et ne nécessite pas de redémarrage.

---

# 17. Palette graphique

Palette recommandée :

* Bleu institutionnel
* Blanc
* Gris clair
* Gris foncé
* Vert (succès)
* Orange (avertissement)
* Rouge (erreur)

Les couleurs sont définies sous forme de ressources.

---

# 18. Typographie

Police principale :

* Inter

Police secondaire :

* Segoe UI

Les tailles sont normalisées.

| Élément         | Taille |
| --------------- | -----: |
| Titre principal |  24 px |
| Titre           |  18 px |
| Sous-titre      |  16 px |
| Corps           |  14 px |
| Légende         |  12 px |

---

# 19. Validation des formulaires

Toutes les validations utilisent :

* IDataErrorInfo ou INotifyDataErrorInfo ;
* ValidationRules WPF si nécessaire ;
* validation métier dans la couche Application.

Les erreurs sont affichées de manière homogène.

---

# 20. DataGrid standard

Toutes les grilles respectent les règles suivantes :

* tri ;
* filtrage ;
* recherche instantanée ;
* pagination si nécessaire ;
* sélection multiple optionnelle ;
* export.

Les colonnes sont configurables.

---

# 21. UX Guidelines

Chaque écran doit respecter les principes suivants :

* moins de trois clics pour les actions courantes ;
* raccourcis clavier pour les opérations fréquentes ;
* messages d'erreur explicites ;
* chargements asynchrones ;
* indicateurs visuels pendant les traitements.

---

# 22. Performances

Objectifs :

| Élément                              | Objectif |
| ------------------------------------ | -------: |
| Ouverture de l'application           |    < 3 s |
| Navigation entre vues                | < 300 ms |
| Chargement d'une liste de 500 agents |    < 1 s |
| Rafraîchissement d'une grille        | < 500 ms |

---

# 23. Critères d'acceptation

La couche Presentation est conforme lorsque :

* aucune logique métier n'est présente dans les Views ;
* les ViewModels sont testables ;
* la navigation est centralisée ;
* les styles sont mutualisés ;
* les contrôles sont réutilisables ;
* les performances répondent aux objectifs.

---

# Conclusion

Le **Volume 19** établit le référentiel complet de la couche **Presentation**. Il fournit un cadre architectural cohérent pour une application WPF moderne, fondée sur **MVVM**, **CommunityToolkit.Mvvm** et **.NET 10 LTS**.

## Recommandation d'évolution

À partir de ce point, je recommande de faire évoluer la documentation vers un niveau encore plus proche des ERP industriels en consacrant les prochains volumes à des sujets très ciblés :

* **Volume 20** : **Modèle de Domaine (Domain-Driven Design)** : agrégats, entités, objets valeur, services de domaine, événements de domaine, spécifications et invariants.
* **Volume 21** : **Architecture de la couche Application** : cas d'utilisation, orchestration, médiation, DTO, validation, transactions.
* **Volume 22** : **Catalogue complet des rubriques de paie** : dépendances, ordre de calcul, règles d'éligibilité, formules, paramètres et historique.
* **Volume 23** : **Référentiel réglementaire** : historisation des paramètres, gestion des textes officiels, dates d'effet et traçabilité.
* **Volume 24** : **Architecture Infrastructure** : fichiers, configuration, impression, horloge, notifications, services système.
* **Volume 25** : **Normes de développement** : conventions C#/.NET 10, qualité du code, revues, documentation, couverture de tests, règles de codage.

Cette seconde série de volumes constituera un véritable **manuel d'architecture logicielle** pour **PaieEducation ERP**, permettant à toute équipe de développement de maintenir et faire évoluer le produit sur le long terme.

Excellente décision. Nous entrons maintenant dans ce qui est, à mon avis, **le cœur technique de l'ERP**.

Les grands ERP (SAP, Dynamics 365, Oracle ERP, Sage X3, Infor…) sont avant tout bâtis autour d'un **modèle de domaine extrêmement solide**. C'est lui qui garantit la pérennité du logiciel pendant 20 à 30 ans.

Pour **PaieEducation ERP**, je recommande d'adopter un **Domain-Driven Design (DDD) pragmatique** (sans complexité inutile), parfaitement adapté à une application de bureau WPF.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome G — Architecture Applicative Avancée**

# **Volume 20**

# **Architecture du Domaine (Domain-Driven Design)**

**Version :** 3.0
**Statut :** Référentiel officiel de la couche Domain

---

# 1. Objet

Ce volume définit l'architecture de la couche **Domain**, qui constitue le cœur fonctionnel de l'ERP.

Le domaine :

* ne connaît pas WPF ;
* ne connaît pas SQLite ;
* ne connaît pas QuestPDF ;
* ne connaît pas ClosedXML ;
* ne connaît aucune technologie d'infrastructure.

Il contient exclusivement les **règles métier**.

---

# 2. Position dans l'architecture

```text
Presentation (WPF)

↓

Application

↓

DOMAIN

↓

Infrastructure

↓

Persistence
```

La couche Domain ne dépend d'aucune autre couche.

Toutes les dépendances sont orientées vers le domaine.

---

# 3. Principes fondamentaux

Le domaine doit respecter les principes suivants :

* indépendance technologique ;
* forte cohésion ;
* faible couplage ;
* encapsulation ;
* immutabilité lorsque possible ;
* invariants métier garantis ;
* absence de logique d'interface.

---

# 4. Organisation du projet

```text
PaieEducation.Domain

│

├── Entities

├── ValueObjects

├── Aggregates

├── Events

├── Specifications

├── Policies

├── Services

├── Rules

├── Enumerations

├── Exceptions

├── Interfaces

└── Common
```

Chaque dossier correspond à un concept DDD clairement identifié.

---

# 5. Les Entités (Entities)

Une entité possède une identité métier stable.

Exemples :

* Agent
* Contrat
* Affectation
* Carrière
* Bulletin
* Période de paie
* Rubrique
* Paramètre réglementaire
* Établissement
* Corps
* Grade

Une entité reste la même même si ses attributs évoluent.

---

# 6. Les Objets Valeur (Value Objects)

Les objets valeur sont immuables et définis uniquement par leurs propriétés.

Exemples :

* Montant (DZD)
* Pourcentage
* Taux
* Adresse
* Nom complet
* Période
* DateEffective
* Ancienneté
* Quotité de travail

Ils n'ont pas d'identifiant.

---

# 7. Recommandation : encapsuler les montants

Afin d'éviter les erreurs de calcul, tous les montants financiers doivent être représentés par un objet valeur dédié.

Exemple conceptuel :

```csharp
Money
```

Responsabilités :

* stocker un montant en **DZD** ;
* appliquer les règles d'arrondi ;
* gérer les opérations arithmétiques ;
* empêcher les comparaisons ambiguës.

Aucun `decimal` ne devrait représenter directement une valeur monétaire dans le domaine.

---

# 8. Les Agrégats (Aggregates)

Les agrégats garantissent la cohérence des objets métier.

Agrégats proposés :

| Agrégat     | Racine            |
| ----------- | ----------------- |
| Agent       | Agent             |
| Bulletin    | Bulletin          |
| Contrat     | Contrat           |
| Paie        | PayrollRun        |
| Paramétrage | ConfigurationPaie |

Chaque agrégat possède une racine unique.

---

# 9. Agrégat Agent

```text
Agent

│

├── Identité

├── Situation familiale

├── Carrière

├── Affectations

├── Contrats

├── Variables

└── Historique
```

Toutes les modifications passent par l'entité `Agent`.

---

# 10. Agrégat Bulletin

```text
Bulletin

│

├── Rubriques

├── Gains

├── Retenues

├── Cotisations

├── IRG

├── Totaux

└── Validation
```

Le bulletin garantit que ses totaux restent cohérents après toute modification.

---

# 11. Services de domaine

Les services de domaine encapsulent des traitements ne relevant d'aucune entité particulière.

Exemples :

* CalculSalaireService
* EligibilityService
* PayrollEngine
* AvancementService
* CalculAncienneteService
* IRGService
* CotisationService

Ils orchestrent les règles métier sans dépendre de l'infrastructure.

---

# 12. Politiques métier (Policies)

Les politiques regroupent des règles complexes applicables à plusieurs contextes.

Exemples :

* Politique d'éligibilité aux indemnités ;
* Politique de validation d'un bulletin ;
* Politique de clôture d'une période ;
* Politique de recalcul.

Ces règles restent centralisées et réutilisables.

---

# 13. Spécifications (Specifications)

Les spécifications représentent des règles métier composables.

Exemples :

* AgentActifSpecification
* EligiblePrimeRendementSpecification
* BulletinValidableSpecification
* ContratValideSpecification

Elles permettent de combiner des critères sans dupliquer le code.

---

# 14. Événements de domaine

Les événements décrivent des faits métier déjà réalisés.

Exemples :

* BulletinCalcule
* BulletinValide
* ContratModifie
* AgentPromu
* PeriodeCloturee
* RubriqueAjoutee

Ils facilitent l'extension du système sans couplage fort.

---

# 15. Invariants métier

Chaque agrégat garantit des règles qui ne peuvent jamais être violées.

Exemples :

* un bulletin validé ne peut plus être modifié ;
* une période clôturée ne peut plus être recalculée sans réouverture ;
* un agent doit appartenir à un établissement ;
* un contrat doit avoir une date de début valide.

Ces invariants sont contrôlés dans le domaine, jamais dans l'interface.

---

# 16. Exceptions métier

Les erreurs fonctionnelles sont représentées par des exceptions explicites.

Exemples :

* BulletinAlreadyValidatedException
* ClosedPayrollPeriodException
* InvalidSalaryGridException
* IneligibleAllowanceException

Les messages techniques ne remontent jamais directement à l'utilisateur.

---

# 17. Interfaces du domaine

Le domaine définit uniquement les contrats nécessaires.

Exemples :

* IAgentRepository
* IBulletinRepository
* IPeriodeRepository
* IConfigurationRepository

Leur implémentation appartient à la couche **Infrastructure/Persistence**.

---

# 18. Énumérations métier

Les constantes métier sont regroupées dans des types dédiés.

Exemples :

* TypeContrat
* TypeEmploye
* Corps
* Grade
* NatureRubrique
* ModeCalcul
* StatutBulletin

Cela améliore la lisibilité et réduit les erreurs.

---

# 19. Domaine et moteur de paie

Le moteur de calcul est une responsabilité du domaine.

```text
PayrollEngine

↓

Eligibility Engine

↓

Calculation Engine

↓

Rule Engine

↓

Bulletin Aggregate
```

Chaque composant possède une responsabilité clairement définie.

---

# 20. Règles d'évolution

Toute nouvelle fonctionnalité métier doit :

1. être modélisée dans le domaine ;
2. respecter les invariants existants ;
3. être couverte par des tests unitaires ;
4. éviter les dépendances techniques.

Le domaine reste ainsi stable malgré les évolutions de l'interface ou de la base de données.

---

# 21. Bonnes pratiques

* Les entités ne manipulent jamais directement SQLite.
* Les objets valeur sont immuables.
* Les agrégats protègent leur cohérence.
* Les services de domaine restent sans état (*stateless*).
* Les événements de domaine décrivent uniquement des faits passés.
* Les règles métier sont centralisées et non dupliquées.

---

# 22. Critères d'acceptation

La couche **Domain** est conforme lorsque :

* aucune dépendance vers WPF, SQLite ou QuestPDF n'existe ;
* les invariants métier sont garantis par les agrégats ;
* les montants monétaires sont encapsulés dans un objet valeur dédié (`Money`) exprimé en **DZD** ;
* les règles d'éligibilité, de calcul et de validation sont implémentées dans des services ou des spécifications du domaine ;
* toutes les règles critiques sont couvertes par des tests unitaires.

---

# Conclusion

Le **Volume 20** établit le **cœur métier** de **PaieEducation ERP**. Il fournit une architecture de domaine robuste, indépendante des technologies et conçue pour évoluer sur le long terme.

### Recommandation architecturale

Je recommande d'introduire, dès le démarrage du développement, les objets valeur suivants :

* `Money` (montants en DZD avec règles d'arrondi),
* `Percentage`,
* `DateRange`,
* `PayrollPeriod`,
* `EmployeeNumber`,
* `RubricCode`.

Ces types renforceront considérablement la sécurité du modèle de domaine et limiteront les erreurs métier.

## Orientation pour le Volume 21

Le **Volume 21** détaillera **l'architecture de la couche Application** : cas d'utilisation (*Use Cases*), orchestration, DTO, validation, gestion des transactions, interfaces de services, coordination entre le domaine et l'interface WPF, afin de compléter le modèle d'architecture modulaire de **PaieEducation ERP**.


Excellent. Nous arrivons maintenant à une couche qui est souvent sous-estimée alors qu'elle est essentielle dans une architecture **Clean Architecture + DDD**. Si le **Domain** représente le **cœur métier**, la couche **Application** représente le **chef d'orchestre** de l'ERP.

Elle coordonne les cas d'utilisation, applique les règles de sécurité, gère les transactions, appelle le domaine et fournit les données à WPF. Dans une architecture moderne, **toute interaction entre l'interface et le domaine passe obligatoirement par cette couche**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome G — Architecture Applicative Avancée**

# **Volume 21**

# **Architecture de la Couche Application (Use Cases, DTO, Services et Orchestration)**

**Version :** 3.0
**Statut :** Référentiel officiel de la couche **Application**

---

# 1. Objet

La couche **Application** constitue le point d'entrée de toutes les opérations métier.

Elle est responsable de :

* l'orchestration des cas d'utilisation ;
* la coordination entre WPF et le domaine ;
* la gestion des transactions ;
* la validation applicative ;
* la sécurité fonctionnelle ;
* la conversion entre objets métier et DTO ;
* la communication avec l'infrastructure.

Elle ne contient **aucune règle métier**.

---

# 2. Position dans l'architecture

```text
Presentation (WPF)
        │
        ▼
Application
        │
        ▼
Domain
        │
        ▼
Infrastructure
        │
        ▼
Persistence (SQLite)
```

Toutes les requêtes provenant de l'interface transitent exclusivement par la couche **Application**.

---

# 3. Responsabilités

La couche Application :

* exécute les cas d'utilisation ;
* ouvre et clôture les transactions ;
* invoque les services du domaine ;
* appelle les repositories ;
* construit les DTO destinés à la présentation ;
* applique les validations applicatives ;
* publie les événements applicatifs ;
* journalise les opérations si nécessaire.

Elle ne calcule jamais les règles de paie.

---

# 4. Organisation du projet

```text
PaieEducation.Application

│
├── UseCases
├── Commands
├── Queries
├── DTOs
├── Interfaces
├── Services
├── Validators
├── Mapping
├── Transactions
├── Security
├── Notifications
├── Events
├── Exceptions
└── Common
```

Cette organisation favorise une forte séparation des responsabilités.

---

# 5. Architecture des cas d'utilisation

Chaque fonctionnalité métier est modélisée comme un **Use Case**.

Exemples :

```text
CréerAgent

ModifierAgent

CalculerPaie

ValiderBulletin

ExporterExcel

GénérerPDF

ClôturerPériode

SauvegarderBase
```

Chaque cas d'utilisation possède une responsabilité unique.

---

# 6. Modèle Command / Query (CQRS léger)

Sans introduire une complexité excessive, l'ERP adopte une séparation logique :

* **Commands** : opérations qui modifient l'état du système.
* **Queries** : opérations de lecture.

Exemples :

### Commands

* CreateAgentCommand
* UpdateContractCommand
* CalculatePayrollCommand
* ValidatePayrollCommand

### Queries

* GetAgentByIdQuery
* GetPayrollSummaryQuery
* GetEmployeeListQuery
* GetBulletinQuery

Cette séparation améliore la lisibilité et facilite les tests.

---

# 7. Services applicatifs

Les services applicatifs orchestrent les traitements.

Exemples :

| Service                         | Responsabilité           |
| ------------------------------- | ------------------------ |
| AgentApplicationService         | Gestion des agents       |
| PayrollApplicationService       | Calcul de la paie        |
| ReportingApplicationService     | Génération des documents |
| ConfigurationApplicationService | Paramétrage              |
| BackupApplicationService        | Sauvegardes              |

Ils ne contiennent pas de logique métier propre.

---

# 8. DTO (Data Transfer Objects)

Les DTO transportent les données entre les couches.

Règles :

* pas de logique métier ;
* immuables lorsque possible ;
* adaptés aux besoins de l'interface.

Exemples :

* AgentDto
* BulletinDto
* PayrollSummaryDto
* RubriqueDto
* ParametreDto

---

# 9. Mapping

Les conversions sont centralisées.

```text
Domain Entity
      │
      ▼
Mapper
      │
      ▼
DTO
```

Aucune conversion ne doit être effectuée directement dans les ViewModels.

Le mapping peut être réalisé manuellement ou via une bibliothèque dédiée si les bénéfices sont démontrés.

---

# 10. Validation applicative

Cette couche vérifie notamment :

* la présence des données obligatoires ;
* les autorisations ;
* la cohérence des paramètres fournis ;
* le respect du workflow.

Les validations métier restent dans le **Domain**.

---

# 11. Gestion des transactions

Chaque cas d'utilisation s'exécute dans une transaction maîtrisée.

Exemple :

```text
Début transaction
        │
        ▼
Validation
        │
        ▼
Appel Domain
        │
        ▼
Persistance
        │
        ▼
Commit
```

En cas d'erreur :

```text
Rollback
```

Les transactions sont les plus courtes possible.

---

# 12. Gestion des exceptions

Les exceptions sont converties en erreurs applicatives compréhensibles.

Exemples :

| Exception Domain                  | Réponse Application                  |
| --------------------------------- | ------------------------------------ |
| BulletinAlreadyValidatedException | Message utilisateur explicite        |
| ClosedPayrollPeriodException      | Blocage de l'opération               |
| InvalidEligibilityException       | Notification avec détail fonctionnel |

Les détails techniques restent dans les journaux.

---

# 13. Notifications applicatives

Les traitements produisent des notifications structurées :

* succès ;
* avertissement ;
* erreur ;
* information.

Ces notifications sont consommées par la couche WPF.

---

# 14. Journalisation

Chaque cas d'utilisation important est journalisé.

Exemples :

* création d'un agent ;
* calcul de paie ;
* validation d'un bulletin ;
* clôture de période ;
* restauration d'une sauvegarde.

La journalisation s'appuie sur **Microsoft.Extensions.Logging**.

---

# 15. Gestion des événements applicatifs

Contrairement aux événements du domaine, les événements applicatifs concernent la coordination des traitements.

Exemples :

* BulletinGeneratedEvent
* ReportExportedEvent
* BackupCompletedEvent
* PayrollRunCompletedEvent

Ils facilitent l'extension du système sans créer de dépendances fortes.

---

# 16. Contrats de services

La couche Application expose uniquement des interfaces.

Exemples :

```text
IAgentApplicationService

IPayrollApplicationService

IReportingApplicationService

IBackupApplicationService

IConfigurationApplicationService
```

Les implémentations sont injectées via **Microsoft.Extensions.DependencyInjection**.

---

# 17. Injection de dépendances

Toutes les dépendances sont enregistrées dans le conteneur d'injection.

Exemple de structure :

```text
Presentation
        │
        ▼
Application
        │
        ▼
Interfaces
        │
        ▼
Infrastructure
```

Aucune instanciation directe (`new`) de services applicatifs n'est autorisée dans les ViewModels.

---

# 18. Communication avec la couche Presentation

Les ViewModels ne manipulent que des DTO et des interfaces de services.

Flux recommandé :

```text
View

↓

ViewModel

↓

Application Service

↓

Domain

↓

Repository

↓

SQLite
```

Cette chaîne garantit un découplage complet.

---

# 19. Cas d'utilisation types

Chaque cas d'utilisation suit une structure standard.

Exemple : **Calcul d'un bulletin**

1. Validation des paramètres.
2. Ouverture de la transaction.
3. Chargement de l'agent.
4. Appel du moteur de paie.
5. Persistance du bulletin.
6. Génération des événements.
7. Validation de la transaction.
8. Retour d'un `BulletinDto`.

Cette séquence est identique pour toutes les opérations critiques.

---

# 20. Conventions de nommage

| Élément   | Convention              |
| --------- | ----------------------- |
| Service   | `...ApplicationService` |
| Command   | `...Command`            |
| Query     | `...Query`              |
| DTO       | `...Dto`                |
| Validator | `...Validator`          |
| Mapper    | `...Mapper`             |

Une convention homogène facilite la maintenance.

---

# 21. Performances

Objectifs de la couche Application :

| Opération             |         Temps cible |
| --------------------- | ------------------: |
| Création d'un agent   |            < 500 ms |
| Chargement d'un agent |            < 300 ms |
| Calcul d'un bulletin  |               < 3 s |
| Génération d'un PDF   |               < 1 s |
| Export Excel          | < 15 s (500 agents) |

Les traitements longs doivent être asynchrones et ne jamais bloquer l'interface.

---

# 22. Critères d'acceptation

La couche **Application** est conforme lorsque :

* aucun ViewModel n'accède directement aux repositories ;
* les cas d'utilisation sont clairement identifiés ;
* les transactions sont centralisées ;
* les DTO sont indépendants du domaine ;
* les validations applicatives sont distinctes des validations métier ;
* les services sont injectés via `Microsoft.Extensions.DependencyInjection`.

---

# 23. Évolution future

La conception retenue permet d'ajouter, sans remettre en cause l'architecture :

* une API REST interne ;
* une interface Web ;
* une application mobile de consultation ;
* des traitements planifiés ;
* des intégrations avec d'autres systèmes.

Ces évolutions réutiliseront les mêmes services applicatifs.

---

# Conclusion

Le **Volume 21** formalise la couche **Application**, véritable orchestrateur de **PaieEducation ERP**. En séparant clairement les cas d'utilisation, les DTO, les validations et les transactions du cœur métier, il garantit une architecture modulaire, testable et évolutive.

## Orientation pour le Volume 22

Le **Volume 22** sera consacré au **Catalogue des Rubriques de Paie et au Moteur de Calcul**. Il détaillera notamment :

* le modèle complet des rubriques (gains, retenues, cotisations, régularisations) ;
* les dépendances entre rubriques ;
* l'ordre d'évaluation ;
* les formules paramétrables ;
* les critères d'éligibilité ;
* la gestion des dates d'effet et de l'historisation ;
* le moteur d'exécution des calculs.

Ce volume constituera le référentiel technique du **Payroll Engine**, cœur fonctionnel de **PaieEducation ERP**.



