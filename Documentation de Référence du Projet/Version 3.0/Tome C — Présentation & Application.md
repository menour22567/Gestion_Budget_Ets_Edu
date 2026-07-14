# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome C — Présentation & Application**

# **Volume 10**

# **Architecture de l'Interface Utilisateur WPF (UI/UX & MVVM)**

**Version :** 2.0
**Statut :** Référentiel officiel de l'interface utilisateur

---

# 1. Objet du document

Ce volume définit l'architecture complète de l'interface utilisateur de **PaieEducation ERP**.

Il constitue la référence officielle pour :

* l'ergonomie ;
* l'architecture WPF ;
* le Shell de l'application ;
* les ViewModels ;
* les conventions MVVM ;
* les composants réutilisables ;
* les standards UI/UX.

L'objectif est de construire une interface de niveau ERP moderne, cohérente, performante et intuitive.

---

# 2. Principes UX

L'interface devra respecter les principes suivants :

* simplicité ;
* cohérence visuelle ;
* réduction du nombre de clics ;
* navigation constante ;
* lisibilité ;
* accessibilité ;
* rapidité d'exécution.

Chaque écran doit permettre à un gestionnaire de paie d'effectuer son travail avec un minimum de manipulations.

---

# 3. Architecture générale

L'application repose sur une **Shell Window** unique.

```text
App.xaml

        │

        ▼

MainShell

 ├── Ribbon / Toolbar
 ├── Menu latéral
 ├── Zone de navigation
 ├── Workspace principal
 ├── Notifications
 ├── Barre d'état
```

Toutes les fonctionnalités sont ouvertes dans le **Workspace principal**.

---

# 4. Structure du projet WPF

```text
Presentation.WPF

├── App.xaml
├── MainWindow.xaml
│
├── Views
├── ViewModels
├── Controls
├── Dialogs
├── Navigation
├── Themes
├── Resources
├── Styles
├── Icons
├── Assets
├── Behaviors
├── Converters
└── Templates
```

---

# 5. Navigation

Le système utilise une navigation centralisée.

```text
Shell

↓

NavigationService

↓

ViewModel

↓

Vue
```

Aucune vue ne doit créer directement une autre vue.

Toutes les ouvertures passent par le **NavigationService**.

---

# 6. Organisation des écrans

Les écrans sont regroupés par module.

| Module         | Nombre estimé d'écrans |
| -------------- | ---------------------: |
| Administration |                      8 |
| Référentiels   |                     18 |
| RH             |                     20 |
| Carrière       |                     15 |
| Variables      |                     10 |
| Calcul         |                      8 |
| Bulletins      |                     12 |
| Reporting      |                     10 |
| Documents      |                      8 |
| Maintenance    |                      6 |

Soit environ **115 écrans WPF**.

---

# 7. Shell principal

Le Shell est composé de :

## Barre supérieure

Contient :

* logo ;
* nom de l'établissement ;
* exercice courant ;
* période ;
* utilisateur (si la gestion des comptes est activée) ;
* paramètres ;
* aide.

---

## Menu latéral

Organisation :

```text
Administration

Référentiels

Ressources Humaines

Carrière

Variables

Calcul

Bulletins

Documents

Reporting

Exports

Maintenance
```

Le menu est repliable.

---

## Zone principale

Elle héberge les vues.

Une seule vue est active.

Les vues secondaires s'ouvrent sous forme de dialogues ou de panneaux.

---

## Barre d'état

Informations affichées :

* base SQLite connectée ;
* période active ;
* nombre d'agents ;
* version ;
* état des sauvegardes.

---

# 8. Design System

Le projet adopte un **Design System** unique.

## Palette

| Élément       | Couleur             |
| ------------- | ------------------- |
| Primaire      | Bleu institutionnel |
| Secondaire    | Gris clair          |
| Succès        | Vert                |
| Avertissement | Orange              |
| Erreur        | Rouge               |
| Information   | Bleu clair          |

Les couleurs exactes seront centralisées dans les ressources WPF.

---

# 9. Typographie

Police recommandée :

* Segoe UI
* Inter
* Aptos (alternative)

Hiérarchie :

| Élément         | Taille |
| --------------- | -----: |
| Titre principal |     24 |
| Titre           |     20 |
| Sous-titre      |     16 |
| Texte           |     14 |
| Détail          |     12 |

---

# 10. Icônes

Une bibliothèque unique sera utilisée (par exemple Fluent UI ou Material Design Icons).

Les icônes représentent uniquement des actions métier.

Exemples :

* Ajouter
* Modifier
* Supprimer
* Imprimer
* Exporter
* Rechercher
* Calculer
* Sauvegarder
* Historique

---

# 11. Conventions MVVM

Chaque écran est constitué de :

```text
Vue

↓

ViewModel

↓

Application Service
```

Le **ViewModel** ne contient aucune logique métier.

---

# 12. Organisation des ViewModels

```text
ViewModels

├── Administration
├── Referentiels
├── RH
├── Carriere
├── Variables
├── Calcul
├── Bulletins
├── Reporting
└── Maintenance
```

Tous les ViewModels héritent d'une classe de base commune.

---

# 13. Classe de base des ViewModels

Responsabilités :

* état de chargement ;
* messages utilisateur ;
* validation ;
* navigation ;
* annulation ;
* fermeture.

Cette factorisation limite la duplication de code.

---

# 14. Navigation MVVM

Le **NavigationService** fournit :

* ouverture d'une vue ;
* fermeture ;
* retour ;
* navigation paramétrée ;
* ouverture modale.

Les ViewModels ne manipulent jamais directement les fenêtres WPF.

---

# 15. Gestion des dialogues

Deux catégories sont définies :

## Dialogues modaux

Exemples :

* suppression ;
* confirmation ;
* impression.

## Panneaux non modaux

Exemples :

* recherche avancée ;
* filtres ;
* aperçu.

---

# 16. Contrôles réutilisables

Une bibliothèque interne de contrôles sera créée.

Exemples :

* `SearchBox`
* `AgentSelector`
* `RubriqueSelector`
* `DateRangePicker`
* `PeriodSelector`
* `MoneyTextBox`
* `NumericEditor`
* `PercentEditor`
* `StatusBadge`
* `ValidationSummary`

Ces contrôles garantissent une expérience utilisateur homogène.

---

# 17. Validation visuelle

La validation est immédiate.

États possibles :

* valide ;
* avertissement ;
* erreur.

Les erreurs sont affichées au plus près du champ concerné.

---

# 18. Recherche

Toutes les listes disposent d'une recherche instantanée.

Fonctionnalités :

* texte libre ;
* filtres multiples ;
* tri ;
* pagination si nécessaire ;
* mémorisation des critères.

---

# 19. Listes de données

Toutes les grilles suivent les mêmes conventions.

Fonctionnalités obligatoires :

* tri ;
* filtre ;
* colonnes configurables ;
* export Excel ;
* double-clic pour ouvrir la fiche ;
* menu contextuel ;
* sélection multiple lorsque pertinente.

---

# 20. Formulaires

Chaque formulaire respecte la structure suivante :

```text
En-tête

↓

Informations principales

↓

Informations complémentaires

↓

Historique

↓

Actions
```

Les champs obligatoires sont clairement identifiés.

---

# 21. Gestion des notifications

Trois niveaux de notification :

| Niveau        | Exemple              |
| ------------- | -------------------- |
| Information   | Sauvegarde effectuée |
| Avertissement | Paramètre manquant   |
| Erreur        | Calcul impossible    |

Les notifications sont non bloquantes lorsque cela est possible.

---

# 22. Gestion des tâches longues

Les traitements longs affichent :

* une barre de progression ;
* une estimation ;
* un bouton d'annulation si l'opération le permet.

L'interface reste réactive pendant l'exécution.

---

# 23. Accessibilité

L'interface respecte les principes suivants :

* navigation clavier ;
* ordre logique de tabulation ;
* contrastes suffisants ;
* textes redimensionnables ;
* messages explicites.

---

# 24. Responsive Desktop

L'application est optimisée pour :

* Full HD (1920 × 1080) ;
* QHD (2560 × 1440) ;
* écrans 4K avec mise à l'échelle Windows.

Les fenêtres doivent être redimensionnables sans perte de fonctionnalité.

---

# 25. Conventions de nommage

| Élément   | Convention               |
| --------- | ------------------------ |
| View      | `AgentView.xaml`         |
| ViewModel | `AgentViewModel.cs`      |
| Dialogue  | `DeleteAgentDialog.xaml` |
| Contrôle  | `AgentSelector.xaml`     |
| Style     | `ButtonPrimary.xaml`     |

---

# 26. Critères d'acceptation

L'interface est conforme si :

* toutes les vues respectent MVVM ;
* aucune logique métier n'est présente dans les vues ou les ViewModels ;
* la navigation est centralisée ;
* les composants sont réutilisables ;
* l'ergonomie est homogène sur l'ensemble des modules ;
* les performances de navigation sont fluides ;
* les règles d'accessibilité sont respectées.

---

# Conclusion

Ce **Volume 10** établit l'architecture officielle de l'interface utilisateur de **PaieEducation ERP**. Il garantit une expérience utilisateur cohérente et une séparation stricte entre la présentation et le domaine métier.

## Recommandation pour la suite

À partir du **Volume 11**, la documentation va franchir une nouvelle étape en décrivant **l'architecture applicative**. Ce volume détaillera les **cas d'utilisation (Use Cases)**, les **services applicatifs**, les **DTO**, les **commandes**, les **requêtes**, les **interfaces**, ainsi que les règles d'orchestration entre la couche **Presentation** et le **Domain**. Il constituera la spécification de référence pour la couche **Application**, cœur de l'architecture modulaire retenue (`Presentation → Application → Domain → Infrastructure → Persistence → Reporting`). Ce sera l'un des volumes les plus importants de l'ensemble de la documentation.

Je recommande une légère évolution de la structure à partir de ce point.

Les volumes précédents décrivent **ce que fait l'ERP**. Désormais, nous allons documenter **comment il est construit**. À partir du **Volume 11**, la documentation devient une **spécification d'architecture logicielle** comparable à celle utilisée dans les grands ERP (SAP, Microsoft Dynamics 365, Oracle Fusion), tout en restant adaptée à une application de bureau **.NET 10 / WPF / SQLite**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome D — Architecture Applicative**

# **Volume 11**

# **Architecture de la Couche Application (Application Layer)**

**Version :** 2.0
**Statut :** Référentiel officiel de la couche Application

---

# 1. Objet du document

La couche **Application** constitue le point de coordination entre :

* la présentation WPF ;
* le domaine métier ;
* la persistance SQLite ;
* le reporting ;
* les services techniques.

Elle orchestre les cas d'utilisation sans contenir les règles métier fondamentales, qui demeurent dans la couche **Domain**.

---

# 2. Position dans l'architecture

```text
                    Presentation (WPF)
                           │
                           ▼
                    Application Layer
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
      Domain         Infrastructure      Reporting
        │                  │                  │
        └─────────────── Persistence (SQLite)
```

### Dépendances autorisées

| Couche         | Dépend de            |
| -------------- | -------------------- |
| Presentation   | Application          |
| Application    | Domain               |
| Infrastructure | Domain               |
| Persistence    | Domain               |
| Reporting      | Application + Domain |

La couche **Domain** ne dépend d'aucune autre couche.

---

# 3. Responsabilités

La couche Application est responsable de :

* l'exécution des cas d'utilisation ;
* l'orchestration des traitements ;
* les transactions ;
* les validations applicatives ;
* la coordination entre repositories et services de domaine ;
* la gestion des DTO.

Elle **ne doit pas** :

* calculer la paie ;
* contenir des règles réglementaires ;
* accéder directement à l'interface WPF.

---

# 4. Organisation du projet

```text
PaieEducation.Application

├── Common
│   ├── Exceptions
│   ├── Interfaces
│   ├── Models
│   ├── Results
│   └── Validation
│
├── Administration
├── Referentiels
├── Agents
├── Carriere
├── Variables
├── Payroll
├── Bulletins
├── Documents
├── Reporting
├── Maintenance
└── Audit
```

Chaque dossier représente un **module fonctionnel**.

---

# 5. Cas d'utilisation (Use Cases)

Chaque fonctionnalité est encapsulée dans un **Use Case**.

Exemples :

```text
CreateAgentUseCase
UpdateAgentUseCase
CalculatePayrollUseCase
GeneratePayslipUseCase
ExportPayrollToExcelUseCase
BackupDatabaseUseCase
```

Chaque cas d'utilisation répond à une responsabilité unique.

---

# 6. Interfaces de services applicatifs

Chaque module expose des interfaces.

Exemples :

```text
IAgentApplicationService
IPayrollApplicationService
IBulletinApplicationService
IReportingApplicationService
IMaintenanceApplicationService
```

Les ViewModels consomment uniquement ces interfaces.

---

# 7. Flux d'un cas d'utilisation

```text
Vue WPF
      │
      ▼
ViewModel
      │
      ▼
Application Service
      │
      ▼
Domain Services
      │
      ▼
Repositories
      │
      ▼
SQLite
```

Ce flux est identique pour tous les modules.

---

# 8. DTO (Data Transfer Objects)

Les DTO transportent les données entre les couches.

### Exemples

```text
AgentDto
BulletinDto
PayrollResultDto
RubriqueDto
HistoriqueDto
```

### Règles

* immuables si possible (`record`) ;
* sans logique métier ;
* sérialisables ;
* indépendants des entités du domaine.

---

# 9. Résultats applicatifs

Les services retournent un objet résultat standardisé.

Exemple conceptuel :

```text
Result<T>

• Succès
• Valeur
• Erreurs
• Avertissements
• Messages
```

Cette approche évite l'utilisation des exceptions pour les cas métier attendus.

---

# 10. Validation applicative

Deux niveaux de validation :

### Validation de forme

* champs obligatoires ;
* formats ;
* cohérence simple.

### Validation métier

Déléguée au **Domain Layer**.

---

# 11. Gestion des transactions

Les opérations suivantes sont transactionnelles :

* calcul d'un bulletin ;
* clôture d'une période ;
* création d'un exercice ;
* restauration d'une sauvegarde ;
* régularisation.

Les transactions sont courtes et atomiques.

---

# 12. Orchestration des services

Exemple : calcul d'un bulletin

```text
CalculatePayrollUseCase

│

├── Charger Agent
├── Charger Contrat
├── Charger Paramètres
├── Construire le Contexte
├── Appeler le Moteur Métier
├── Persister le Bulletin
└── Générer le Résultat
```

La logique métier est exécutée par le moteur du domaine ; le Use Case orchestre uniquement les étapes.

---

# 13. Gestion des erreurs

Les erreurs sont classées en quatre catégories :

| Type       | Exemple                  |
| ---------- | ------------------------ |
| Validation | Champ obligatoire        |
| Métier     | Rubrique non éligible    |
| Technique  | Base SQLite indisponible |
| Système    | Exception non prévue     |

Chaque catégorie possède un traitement spécifique.

---

# 14. Notifications applicatives

Les services peuvent produire :

* informations ;
* avertissements ;
* erreurs fonctionnelles ;
* confirmations.

Les ViewModels décident de la manière de les présenter à l'utilisateur.

---

# 15. Injection de dépendances

Tous les services sont enregistrés via **Microsoft.Extensions.DependencyInjection**.

Exemple de durée de vie :

| Élément              | Durée                               |
| -------------------- | ----------------------------------- |
| Application Services | Scoped (par opération)              |
| Domain Services      | Scoped ou Singleton selon leur état |
| Repositories         | Scoped                              |
| Logging              | Singleton                           |
| Configuration        | Singleton                           |

> **Note d'architecture :** dans une application WPF, la notion de *Scoped* devra être implémentée avec un **scope explicite par cas d'utilisation** (`IServiceScopeFactory`) ou adaptée à des services **Transient** lorsque cela est plus approprié. Cette décision sera formalisée dans le Volume 15.

---

# 16. Gestion des dépendances

Le principe d'inversion des dépendances est appliqué.

```text
ViewModel

↓

Interface

↓

Implémentation
```

Les ViewModels ne connaissent jamais les implémentations concrètes.

---

# 17. Communication entre modules

Les modules communiquent uniquement via :

* interfaces ;
* DTO ;
* événements applicatifs (si nécessaire).

Toute dépendance directe entre modules est interdite.

---

# 18. Performances

Objectifs :

* ouverture d'un dossier agent : < 500 ms ;
* lancement d'un cas d'utilisation simple : < 200 ms ;
* calcul individuel : < 3 s ;
* calcul de masse (500 agents) : < 2 min.

Ces objectifs seront vérifiés lors des tests de performance.

---

# 19. Testabilité

Chaque Use Case doit être :

* isolé ;
* testable ;
* indépendant de WPF ;
* indépendant de SQLite (via des abstractions).

Les tests unitaires utilisent des doubles (mocks, stubs ou fakes) pour les dépendances externes.

---

# 20. Traçabilité

Chaque Use Case est relié à :

* un module fonctionnel (Volume 6) ;
* une fonctionnalité (Volume 8) ;
* des règles métier (Volume 9) ;
* des entités (Volume 7) ;
* des scénarios de test (Volume 18).

Cette traçabilité permet de suivre l'impact d'une évolution réglementaire ou fonctionnelle.

---

# 21. Critères d'acceptation

La couche Application est conforme lorsque :

* tous les traitements sont implémentés sous forme de Use Cases ;
* aucune logique métier réglementaire n'est présente dans les ViewModels ;
* les DTO sont découplés des entités du domaine ;
* les services sont injectés via l'injection de dépendances ;
* les transactions sont maîtrisées ;
* les cas d'erreur sont normalisés ;
* les tests unitaires couvrent les principaux scénarios.

---

# Conclusion

Ce **Volume 11** formalise l'architecture de la couche **Application**, véritable chef d'orchestre de l'ERP. Il garantit une séparation nette entre l'interface WPF, le domaine métier et la persistance, conformément aux principes d'une architecture modulaire moderne.

## Orientation pour le Volume 12

Le **Volume 12** sera consacré au **modèle de données SQLite**. Il ne s'agira pas seulement d'une description des tables : il constituera un **dictionnaire de données complet**, incluant le schéma physique, les relations, les index, les contraintes, les règles de migration, les conventions de nommage et les stratégies d'évolution. Il définira également les correspondances entre les entités du domaine, les DTO et les structures de persistance, afin de garantir une cohérence totale entre les couches **Domain**, **Persistence** et **SQLite**. Ce volume servira de référence pour toutes les migrations et optimisations futures de la base de données.


# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome D — Architecture Technique**

# **Volume 12**

# **Architecture de la Persistance SQLite et Dictionnaire de Données**

**Version :** 2.0
**Statut :** Référentiel officiel de la couche Persistence

---

# 1. Objet du document

Ce volume définit l'architecture complète de la **couche de persistance** de **PaieEducation ERP**.

Il constitue la référence officielle pour :

* le schéma SQLite ;
* le dictionnaire de données ;
* les conventions de nommage ;
* les relations entre entités ;
* les index ;
* les contraintes ;
* les migrations ;
* les performances ;
* les stratégies de sauvegarde.

La base SQLite constitue **l'unique source de persistance** de l'application.

---

# 2. Position de la couche Persistence

```text
               Presentation (WPF)
                       │
                       ▼
                 Application Layer
                       │
                       ▼
                  Domain Layer
                       │
                       ▼
               Persistence Layer
                       │
                       ▼
                 SQLite Database
```

La couche **Persistence** est responsable uniquement du stockage et de la récupération des données.

Elle ne contient **aucune logique métier**.

---

# 3. Organisation du projet

```text
PaieEducation.Persistence

│
├── DbContext
│
├── Configuration
│
├── Repositories
│
├── Queries
│
├── Commands
│
├── Migrations
│
├── Seed
│
├── Mapping
│
├── Sql
│
└── Extensions
```

---

# 4. Organisation physique de la base

La base est composée de cinq grandes familles.

| Domaine             | Préfixe |
| ------------------- | ------- |
| Référentiels        | REF     |
| Ressources Humaines | RH      |
| Paie                | PAY     |
| Reporting           | REP     |
| Système             | SYS     |

---

# 5. Classification des tables

## Référentiels

Exemples :

```text
T_Etablissement

T_Corps

T_Grade

T_Echelon

T_Fonction

T_Rubrique

T_Banque

T_Organisme
```

---

## Ressources Humaines

```text
T_Agent

T_Contrat

T_Carriere

T_Affectation

T_Famille

T_Diplome
```

---

## Paie

```text
T_Bulletin

T_BulletinLigne

T_Variable

T_Parametre

T_BaremeIRG

T_Cotisation

T_Rappel

T_Calcul
```

---

## Documents

```text
T_Document

T_ModeleDocument

T_HistoriqueDocument
```

---

## Audit

```text
T_Log

T_Audit

T_Backup

T_Migration
```

---

# 6. Dictionnaire de données

Chaque table est documentée selon le modèle suivant.

| Élément         | Description |
| --------------- | ----------- |
| Nom logique     | Agent       |
| Nom physique    | T_Agent     |
| Domaine         | RH          |
| Description     | Personnel   |
| Clé primaire    | AgentId     |
| Clés étrangères | ...         |
| Index           | ...         |
| Contraintes     | ...         |
| Historisée      | Oui / Non   |

---

# 7. Exemple : T_Agent

| Colonne            | Type SQLite | Nullable |
| ------------------ | ----------- | -------- |
| AgentId            | TEXT (GUID) | Non      |
| Matricule          | TEXT        | Non      |
| Nom                | TEXT        | Non      |
| Prenom             | TEXT        | Non      |
| DateNaissance      | TEXT        | Non      |
| Sexe               | INTEGER     | Non      |
| SituationFamiliale | INTEGER     | Non      |
| DateCreation       | TEXT        | Non      |
| DateModification   | TEXT        | Oui      |
| EstActif           | INTEGER     | Non      |

### Contraintes

* PK AgentId
* UK Matricule

---

# 8. Exemple : T_Bulletin

| Colonne     | Type    |
| ----------- | ------- |
| BulletinId  | TEXT    |
| AgentId     | TEXT    |
| Periode     | TEXT    |
| SalaireBrut | REAL    |
| SalaireNet  | REAL    |
| DateCalcul  | TEXT    |
| Etat        | INTEGER |

---

# 9. Exemple : T_BulletinLigne

| Colonne    | Description |
| ---------- | ----------- |
| LigneId    | Identifiant |
| BulletinId | FK          |
| RubriqueId | FK          |
| Base       | Valeur      |
| Taux       | Pourcentage |
| Montant    | Valeur      |

---

# 10. Relations principales

```text
Agent
│
├──── Contrat

├──── Affectation

├──── Carriere

├──── Variable

└──── Bulletin

Bulletin

├──── BulletinLigne

├──── Audit

└──── Document
```

---

# 11. Clés primaires

Toutes les tables utilisent un GUID.

Exemple :

```text
AgentId

BulletinId

RubriqueId
```

Les GUID sont stockés sous forme de `TEXT` dans SQLite.

---

# 12. Clés métier

Chaque entité possède également un identifiant fonctionnel.

Exemples :

| Entité        | Clé métier        |
| ------------- | ----------------- |
| Agent         | Matricule         |
| Rubrique      | CodeRubrique      |
| Corps         | CodeCorps         |
| Grade         | CodeGrade         |
| Etablissement | CodeEtablissement |

Les traitements métier utilisent prioritairement ces codes.

---

# 13. Contraintes d'intégrité

Les contraintes minimales sont :

* unicité des codes ;
* intégrité référentielle ;
* dates cohérentes ;
* non-nullité des champs obligatoires ;
* suppression logique lorsque l'historique est requis.

---

# 14. Index

Les index sont obligatoires sur :

```text
Matricule

Periode

CodeRubrique

CodeGrade

CodeCorps

DateEffet

Etat
```

Des index composites sont prévus sur les couples `(AgentId, Periode)` et `(CodeRubrique, DateEffet)` pour optimiser les calculs et les recherches.

---

# 15. Suppression logique

Les données réglementaires ne sont jamais supprimées physiquement.

Toutes les tables concernées possèdent :

```text
EstActif

DateSuppression

MotifSuppression
```

La suppression physique est réservée aux données techniques temporaires.

---

# 16. Historisation

Certaines tables sont entièrement historisées.

| Table       | Historique |
| ----------- | ---------- |
| Contrat     | Oui        |
| Carriere    | Oui        |
| Parametre   | Oui        |
| Rubrique    | Oui        |
| Affectation | Oui        |
| Bulletin    | Oui        |
| Audit       | Oui        |

Les changements sont conservés afin de reproduire fidèlement un calcul passé.

---

# 17. Migrations

Toutes les évolutions du schéma sont réalisées par des migrations versionnées.

Convention de nommage :

```text
V001_InitialSchema

V002_AddPayrollTables

V003_AddReporting

V004_AddIndexes
```

Chaque migration est :

* idempotente ;
* documentée ;
* réversible lorsque cela est possible.

---

# 18. Versionnement de la base

Une table dédiée (`T_Migration`) enregistre :

* version ;
* date d'application ;
* auteur ;
* description ;
* succès / échec.

Cela permet de connaître précisément l'état du schéma.

---

# 19. Transactions

Toutes les écritures critiques sont encapsulées dans une transaction SQLite.

Exemples :

* calcul d'un bulletin ;
* clôture de période ;
* restauration ;
* régularisation.

En cas d'échec, un **rollback** garantit la cohérence des données.

---

# 20. Optimisation SQLite

Les recommandations suivantes sont retenues :

* utilisation du mode **WAL (Write-Ahead Logging)** pour améliorer les performances de lecture/écriture ;
* activation des **foreign keys** (`PRAGMA foreign_keys = ON`) ;
* indexation des colonnes fréquemment interrogées ;
* limitation des requêtes `SELECT *` au profit de projections ciblées ;
* utilisation systématique de requêtes paramétrées.

Ces optimisations sont compatibles avec un fonctionnement **100 % local et hors ligne**.

---

# 21. Sauvegarde et restauration

La base SQLite doit pouvoir être :

* sauvegardée à chaud après validation des transactions ;
* restaurée après contrôle d'intégrité ;
* compressée pour l'archivage si nécessaire ;
* vérifiée avant toute réouverture de l'application.

Chaque opération est journalisée.

---

# 22. Sécurité des données

La couche de persistance doit garantir :

* absence d'injection SQL (requêtes paramétrées) ;
* contrôle des accès aux fichiers de base ;
* validation des chemins de sauvegarde ;
* gestion centralisée des exceptions d'accès aux données.

---

# 23. Correspondance Domain ↔ Persistence

| Domaine      | Entité métier           | Table SQLite    |
| ------------ | ----------------------- | --------------- |
| RH           | Agent                   | T_Agent         |
| RH           | Contrat                 | T_Contrat       |
| Carrière     | Affectation             | T_Affectation   |
| Paie         | Bulletin                | T_Bulletin      |
| Paie         | Ligne de bulletin       | T_BulletinLigne |
| Référentiels | Rubrique                | T_Rubrique      |
| Référentiels | Paramètre réglementaire | T_Parametre     |

Cette matrice constitue la référence pour les mappings et les repositories.

---

# 24. Critères d'acceptation

La couche Persistence est conforme lorsque :

* le schéma SQLite est normalisé et documenté ;
* chaque table dispose d'un dictionnaire de données ;
* les contraintes d'intégrité sont définies ;
* les index répondent aux besoins de performance ;
* les migrations sont versionnées ;
* les données réglementaires sont historisées ;
* les transactions assurent la cohérence des écritures.

---

# Conclusion

Le **Volume 12** établit le référentiel complet de la persistance SQLite de **PaieEducation ERP**. Il garantit une base de données robuste, évolutive et alignée sur le modèle métier.

## Orientation pour le Volume 13

Le **Volume 13** sera consacré au **Moteur de Calcul de la Paie**. Il décrira son architecture interne (pipeline de calcul, moteur de règles, calculateurs spécialisés, stratégie d'éligibilité, résolution des dépendances entre rubriques, gestion des paramètres réglementaires, journal d'exécution et mécanismes d'explicabilité). Ce volume sera le cœur technique de l'ERP et définira précisément comment les règles métier du **Volume 9** sont implémentées dans la couche **Domain** avec une architecture extensible, testable et adaptée aux évolutions de la réglementation algérienne.














