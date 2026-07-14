Tome A — Vision & Architecture


**référentiel d'architecture ERP**, comparable à ceux utilisés dans les grands projets (SAP, Microsoft Dynamics, Oracle ERP, Odoo Enterprise), mais adapté à un ERP de paie algérien développé en C#.

# Vision de la Version 2.0

Le document ne sera plus simplement un cahier des charges.

Il deviendra la **documentation de référence du projet**.

Il sera utilisé par :

* l'architecte logiciel ;
* les développeurs C# ;
* les développeurs WPF ;
* les développeurs SQLite ;
* les testeurs ;
* les auditeurs ;
* les futurs mainteneurs.

---

# Nouvelle identité du projet

## PaieEducation ERP

### Version 2.0

**Architecture cible**

* .NET 10 LTS
* WPF Desktop
* MVVM
* SQLite
* CommunityToolkit.Mvvm
* Microsoft.Extensions.DependencyInjection
* Microsoft.Extensions.Logging
* QuestPDF
* ClosedXML
* Architecture modulaire

---

# Nouvelle structure documentaire

Au lieu de 10 volumes indépendants, je recommande une **collection cohérente de 20 volumes**, organisée en quatre grands ensembles.

## Tome A — Vision & Architecture

| Volume | Contenu                 |
| ------ | ----------------------- |
| 1      | Vision générale         |
| 2      | Analyse fonctionnelle   |
| 3      | Architecture métier     |
| 4      | Architecture logicielle |
| 5      | Architecture technique  |

---

## Tome B — Fonctionnel

| Volume | Contenu                     |
| ------ | --------------------------- |
| 6      | Catalogue des modules       |
| 7      | Référentiel des données     |
| 8      | Catalogue fonctionnel       |
| 9      | Catalogue des règles métier |
| 10     | Catalogue des écrans WPF    |

---

## Tome C — Développement

| Volume | Contenu                |
| ------ | ---------------------- |
| 11     | Services Applicatifs   |
| 12     | Architecture MVVM      |
| 13     | Persistence SQLite     |
| 14     | Reporting QuestPDF     |
| 15     | Export Excel ClosedXML |

---

## Tome D — Exploitation

| Volume | Contenu        |
| ------ | -------------- |
| 16     | Sécurité       |
| 17     | Journalisation |
| 18     | Tests          |
| 19     | Déploiement    |
| 20     | Maintenance    |

---

# Technologies officielles

## Framework

```text
.NET 10 LTS
```

---

## UI

```text
WPF
```

---

## Pattern

```text
MVVM
```

---

## Toolkit

```text
CommunityToolkit.Mvvm
```

---

## Injection de dépendances

```text
Microsoft.Extensions.DependencyInjection
```

---

## Logging

```text
Microsoft.Extensions.Logging
```

---

## Base de données

```text
SQLite
```

---

## Reporting

```text
QuestPDF
```

---

## Export Excel

```text
ClosedXML
```

---

## Configuration

```text
appsettings.json
```

---

## Architecture

```text
Presentation

↓

Application

↓

Domain

↓

Infrastructure

↓

Persistence.SQLite

↓

Reporting
```

---

# Principes d'architecture

La Version 2.0 reposera sur les principes suivants :

* séparation stricte des responsabilités ;
* absence de logique métier dans les Views ;
* toute règle métier implémentée dans le Domain ;
* services applicatifs orchestrant les cas d'utilisation ;
* accès aux données exclusivement via la couche Persistence ;
* reporting indépendant grâce à `IReportService` ;
* journalisation centralisée ;
* injection de dépendances systématique ;
* testabilité de chaque composant.

---

# Structure cible de la solution

```text
PaieEducation.sln
│
├── PaieEducation.Presentation.WPF
│   ├── Views
│   ├── ViewModels
│   ├── Commands
│   ├── Dialogs
│   ├── Controls
│   ├── Converters
│   ├── Behaviors
│   ├── Themes
│   └── Resources
│
├── PaieEducation.Application
│   ├── Interfaces
│   ├── Services
│   ├── DTO
│   ├── Validators
│   └── UseCases
│
├── PaieEducation.Domain
│   ├── Entities
│   ├── ValueObjects
│   ├── Rules
│   ├── Calculators
│   ├── Specifications
│   └── Events
│
├── PaieEducation.Infrastructure
│   ├── Logging
│   ├── Configuration
│   ├── Localization
│   └── DependencyInjection
│
├── PaieEducation.Persistence.SQLite
│   ├── Context
│   ├── Repositories
│   ├── Migrations
│   └── Scripts
│
├── PaieEducation.Reporting
│   ├── QuestPdf
│   ├── Templates
│   └── Printers
│
├── PaieEducation.Shared
│
└── PaieEducation.Tests
```

---

# Évolutions majeures par rapport à la Version 1

| Version 1                  | Version 2                           |
| -------------------------- | ----------------------------------- |
| .NET 8                     | .NET 10 LTS                         |
| WinForms                   | WPF                                 |
| Événements (`Click`)       | MVVM + `RelayCommand`               |
| Couplage UI / logique      | Séparation stricte View / ViewModel |
| RDLC comme référence       | QuestPDF comme moteur principal     |
| DAL classique              | Persistence modulaire               |
| Documentation de migration | Référentiel d'architecture ERP      |

---

# Objectif final

L'objectif n'est plus seulement de recréer l'application **PaieEducation** existante, mais de construire un **ERP de paie moderne, modulaire, testable et évolutif**, capable d'accompagner les évolutions réglementaires algériennes pendant de nombreuses années.

Je recommande donc de considérer cette **Version 2.0** comme un **nouveau référentiel officiel**, remplaçant les volumes précédents. Les **Volumes 1 à 10** seront réécrits pour intégrer dès l'origine **.NET 10 LTS**, **WPF**, **MVVM**, **SQLite**, **CommunityToolkit.Mvvm**, **Microsoft.Extensions.DependencyInjection**, **Microsoft.Extensions.Logging**, **QuestPDF**, **ClosedXML** et une **architecture modulaire** cohérente. Les volumes suivants (11 à 20) viendront compléter cette base pour constituer une documentation de niveau ERP, utilisable comme référence unique pendant tout le cycle de vie du projet.


Parfait. Je pense que nous avons désormais suffisamment mûri l'architecture pour changer de démarche.

Je ne vais plus considérer ce projet comme un simple **cahier des charges**, mais comme une **Documentation de Référence du Projet (Project Reference Architecture)**.

Cette documentation devra être suffisamment complète pour qu'une équipe de développement puisse réaliser l'application **sans avoir à réinventer l'architecture**.

Je propose donc de repartir du **Volume 1** et de produire une **Version 2.0** entièrement réécrite, en intégrant dès le départ tous les choix technologiques et architecturaux validés.

---

# Documentation de Référence du Projet

# PaieEducation ERP

## Version 2.0

### Architecture de référence

* **Framework :** .NET 10 LTS
* **Interface :** WPF
* **Pattern UI :** MVVM
* **Base de données :** SQLite
* **Architecture :** Modulaire (Presentation / Application / Domain / Infrastructure / Persistence / Reporting)
* **Injection de dépendances :** Microsoft.Extensions.DependencyInjection
* **Journalisation :** Microsoft.Extensions.Logging
* **Reporting :** QuestPDF
* **Export Excel :** ClosedXML
* **Toolkit MVVM :** CommunityToolkit.Mvvm

---

# Tome A

# Vision & Architecture

## Volume 1

# Vision du Projet et Principes d'Architecture

**Version :** 2.0

**Statut :** Document fondateur

---

# 1. Objet du document

Ce document définit la vision stratégique, fonctionnelle et technique du projet **PaieEducation ERP**.

Il constitue le référentiel de plus haut niveau de l'application et sert de base à tous les développements, tests, audits et évolutions futures.

Toutes les décisions d'architecture devront être conformes à ce document.

---

# 2. Présentation du projet

## Nom

**PaieEducation ERP**

---

## Nature

Application de bureau (Desktop)

Fonctionnement totalement autonome

Mode hors ligne

---

## Domaine

Gestion de la paie des établissements publics d'éducation en Algérie.

---

## Public cible

* Gestionnaires de paie
* Services administratifs
* Directions d'établissement
* Services financiers
* Auditeurs
* Administrateurs techniques

---

# 3. Objectifs

L'application doit permettre :

* la gestion complète des dossiers administratifs des agents ;
* le calcul conforme des rémunérations selon la réglementation algérienne ;
* la production des bulletins de paie et des documents administratifs ;
* l'historisation des données ;
* la traçabilité des calculs ;
* la préparation des évolutions réglementaires sans refonte de l'application.

---

# 4. Vision d'architecture

Le projet est conçu comme un ERP modulaire.

Chaque module est indépendant, faiblement couplé et fortement cohésif.

La séparation des responsabilités est une exigence non négociable.

---

# 5. Principes fondateurs

Les principes suivants sont obligatoires.

## P1 — Séparation des responsabilités

Chaque couche possède un rôle unique.

La logique métier n'est jamais implémentée dans la couche de présentation.

---

## P2 — Architecture orientée domaine

Le domaine métier est le cœur du système.

Toutes les règles réglementaires y sont centralisées.

---

## P3 — Découplage

Les couches communiquent uniquement au travers d'interfaces clairement définies.

Aucune dépendance directe vers SQLite depuis l'interface utilisateur.

---

## P4 — Testabilité

Tout composant doit pouvoir être testé indépendamment.

Les ViewModels, services applicatifs et calculateurs devront être testables sans interface graphique.

---

## P5 — Évolutivité

Toute évolution réglementaire devra pouvoir être intégrée par l'ajout ou la modification de règles métier, sans remettre en cause l'architecture.

---

## P6 — Traçabilité

Toute opération importante devra laisser une trace exploitable.

Exemples :

* calcul d'un bulletin ;
* validation ;
* régularisation ;
* modification réglementaire.

---

## P7 — Performance

Les performances doivent rester constantes même lorsque le volume de données augmente.

L'objectif est un temps de réponse inférieur à trois secondes pour le calcul d'un bulletin dans le contexte cible (moins de 500 agents).

---

# 6. Périmètre fonctionnel

Le projet couvre notamment :

* Référentiels
* Ressources humaines
* Carrière
* Contrats
* Affectations
* Variables mensuelles
* Calcul de paie
* Bulletins
* Attestations
* États réglementaires
* Exports Excel
* Administration
* Journalisation
* Sauvegarde
* Maintenance

---

# 7. Contraintes techniques

Les contraintes suivantes sont impératives :

* fonctionnement 100 % hors ligne ;
* aucune dépendance à un service Cloud ;
* aucune API externe requise pour les fonctionnalités métier ;
* base de données SQLite locale ;
* compatibilité avec Windows 10 et Windows 11 ;
* déploiement simplifié pour les établissements.

---

# 8. Technologies retenues

| Domaine                  | Technologie                              |
| ------------------------ | ---------------------------------------- |
| Langage                  | C#                                       |
| Framework                | .NET 10 LTS                              |
| Interface                | WPF                                      |
| Architecture UI          | MVVM                                     |
| Toolkit MVVM             | CommunityToolkit.Mvvm                    |
| Base de données          | SQLite                                   |
| Injection de dépendances | Microsoft.Extensions.DependencyInjection |
| Journalisation           | Microsoft.Extensions.Logging             |
| Reporting                | QuestPDF                                 |
| Export Excel             | ClosedXML                                |
| Configuration            | appsettings.json                         |
| Tests                    | xUnit (recommandé)                       |

---

# 9. Architecture logique

```text
Présentation (WPF)
        │
        ▼
ViewModels (MVVM)
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
Persistence.SQLite
        │
        ▼
Base SQLite
```

Le module **Reporting** (QuestPDF) est transversal et consomme les données validées issues des couches Application et Domain.

---

# 10. Principes de qualité

Le projet adopte les objectifs suivants :

* lisibilité du code ;
* modularité ;
* faible couplage ;
* forte cohésion ;
* testabilité ;
* maintenabilité ;
* évolutivité ;
* traçabilité.

Chaque évolution devra respecter ces principes.

---

# 11. Gouvernance technique

Toute décision technique devra être évaluée selon les critères suivants :

1. Conformité à la réglementation.
2. Impact sur le domaine métier.
3. Maintenabilité.
4. Performance.
5. Sécurité.
6. Testabilité.
7. Compatibilité avec l'architecture modulaire.

Une solution techniquement élégante mais contraire à ces principes ne sera pas retenue.

---

# 12. Convention documentaire

Chaque volume de cette documentation suivra une structure uniforme :

* **Objectif**
* **Périmètre**
* **Décisions d'architecture**
* **Exigences**
* **Spécifications**
* **Traçabilité**
* **Critères d'acceptation**

Cette homogénéité facilitera les revues techniques et les audits.

---

## Feuille de route de la Documentation de Référence

Je propose désormais la production séquentielle des volumes suivants :

### Tome A — Vision & Architecture

1. **Volume 1** — Vision du Projet et Principes d'Architecture *(en cours)*
2. **Volume 2** — Architecture Fonctionnelle Générale
3. **Volume 3** — Architecture Métier (Domain-Driven)
4. **Volume 4** — Architecture Logicielle Modulaire
5. **Volume 5** — Architecture Technique (.NET 10, WPF, MVVM, SQLite)

### Tome B — Fonctionnel

6. Catalogue des Modules
7. Référentiel des Données
8. Catalogue Fonctionnel
9. Catalogue des Règles Métier
10. Catalogue des Écrans WPF

### Tome C — Développement

11. Services Applicatifs
12. Architecture MVVM
13. Persistence SQLite
14. Reporting QuestPDF
15. Export Excel (ClosedXML)

### Tome D — Exploitation

16. Sécurité
17. Journalisation
18. Tests
19. Déploiement
20. Maintenance

Je recommande de traiter cette documentation comme un **véritable référentiel d'architecture** : chaque volume sera suffisamment détaillé pour servir directement de base aux développements, aux tests et à la maintenance, avec une cohérence stricte entre les décisions fonctionnelles, les choix techniques et l'implémentation. Le **Volume 2** établira cette cohérence en définissant l'architecture fonctionnelle générale de l'ensemble de l'ERP.


# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome A — Vision & Architecture**

# **Volume 2**

# **Architecture Fonctionnelle Générale**

**Version :** 2.0
**Statut :** Document d'architecture fonctionnelle de référence

---

# 1. Objet du document

Ce volume définit l'architecture fonctionnelle globale de **PaieEducation ERP**.

Son objectif est de décrire **ce que fait le système**, **comment les différents domaines fonctionnels collaborent** et **comment les données circulent**, indépendamment des détails d'implémentation.

Il constitue le lien entre les exigences métier (réglementation, procédures administratives) et l'architecture logicielle qui sera détaillée dans les volumes suivants.

---

# 2. Principes directeurs

L'architecture fonctionnelle repose sur les principes suivants :

* **Modularité** : chaque domaine métier est isolé dans un module clairement identifié.
* **Faible couplage** : un module ne dépend pas directement de l'implémentation d'un autre.
* **Forte cohésion** : chaque module regroupe des fonctionnalités homogènes.
* **Traçabilité** : toute opération importante est historisée.
* **Paramétrabilité** : les évolutions réglementaires doivent être intégrées par le paramétrage et non par des modifications de code lorsque cela est possible.
* **Centralisation des règles métier** : une règle est définie une seule fois et réutilisée par tous les modules concernés.

---

# 3. Vision fonctionnelle globale

L'ERP est organisé autour d'un **cycle de gestion de la paie**.

```text
Administration
        │
        ▼
Référentiels
        │
        ▼
Gestion RH
        │
        ▼
Carrière & Situation Administrative
        │
        ▼
Variables de Paie
        │
        ▼
Moteur de Calcul
        │
        ▼
Contrôle
        │
        ▼
Bulletins
        │
        ▼
États Réglementaires
        │
        ▼
Exports & Archivage
```

Chaque étape produit des données validées qui alimentent la suivante.

---

# 4. Cartographie fonctionnelle

## Domaine A — Administration

### Objectif

Configurer l'application et assurer son exploitation.

### Modules

* Paramètres généraux
* Sauvegardes
* Restauration
* Journalisation
* Paramètres techniques
* Paramètres réglementaires
* Maintenance

---

## Domaine B — Référentiels

### Objectif

Centraliser toutes les données de référence.

### Référentiels

* Établissements
* Corps
* Grades
* Échelons
* Catégories
* Fonctions
* Types de contrat
* Types de personnel
* Rubriques
* Organismes
* Banques
* Paramètres fiscaux
* Paramètres CNAS
* Paramètres retraite
* Paramètres IRG
* Zones géographiques
* Calendrier réglementaire

---

## Domaine C — Gestion des Ressources Humaines

### Objectif

Administrer les dossiers des agents.

### Modules

* Employés
* Situation familiale
* Diplômes
* Affectations
* Carrière
* Promotions
* Avancements
* Contrats
* Congés
* Suspensions
* Disponibilités
* Retraites

---

## Domaine D — Variables Mensuelles

### Objectif

Gérer les éléments variables influençant la paie.

### Modules

* Heures supplémentaires
* Absences
* Retenues
* Primes exceptionnelles
* Rappels
* Régularisations
* Indemnités ponctuelles
* Avances
* Oppositions
* Saisies

---

## Domaine E — Calcul de la Paie

### Objectif

Calculer la rémunération conformément à la réglementation.

### Sous-modules

* Construction du contexte de calcul
* Vérification des droits
* Détermination des rubriques
* Calcul des gains
* Calcul des retenues
* Calcul des cotisations
* Calcul de l'IRG
* Calcul du net
* Contrôles réglementaires
* Journal détaillé des calculs

---

## Domaine F — Documents

### Objectif

Produire les documents réglementaires.

### Documents

* Bulletin de paie
* Attestation de travail
* Attestation de salaire
* Certificat administratif
* État récapitulatif
* Livre de paie
* Bordereaux
* Journal comptable
* Statistiques

---

## Domaine G — Exports

### Objectif

Produire des données exploitables par d'autres outils.

### Exports

* Excel (ClosedXML)
* PDF (QuestPDF)
* CSV
* Impression

---

# 5. Cycle de vie d'un bulletin de paie

Le cycle de vie d'un bulletin est défini comme suit :

1. Sélection de la période de paie.
2. Chargement des paramètres réglementaires.
3. Chargement des données de l'agent.
4. Vérification de la cohérence des informations.
5. Construction du contexte de calcul.
6. Détermination des rubriques applicables.
7. Calcul des gains.
8. Calcul des retenues.
9. Calcul des cotisations.
10. Calcul de l'IRG.
11. Détermination du net à payer.
12. Contrôles réglementaires.
13. Validation.
14. Génération du bulletin.
15. Génération des états réglementaires.
16. Archivage logique.

---

# 6. Flux fonctionnels

## Flux principal

```text
Référentiels
        │
        ▼
Employé
        │
        ▼
Carrière
        │
        ▼
Variables Mensuelles
        │
        ▼
Moteur de Calcul
        │
        ▼
Bulletin
        │
        ▼
États
        │
        ▼
Exports
```

---

## Flux de contrôle

Chaque étape effectue automatiquement :

* validation des données ;
* contrôle des prérequis ;
* vérification réglementaire ;
* journalisation des anomalies.

---

# 7. Matrice des responsabilités fonctionnelles

| Domaine        | Responsabilité principale        |
| -------------- | -------------------------------- |
| Administration | Paramétrage et exploitation      |
| Référentiels   | Gestion des données de référence |
| RH             | Gestion des agents               |
| Variables      | Gestion des éléments mensuels    |
| Calcul         | Calcul réglementaire             |
| Documents      | Production documentaire          |
| Exports        | Diffusion des données            |
| Journalisation | Traçabilité                      |
| Maintenance    | Intégrité du système             |

---

# 8. Interactions entre les domaines

| Domaine source    | Domaine cible  | Nature de l'interaction                     |
| ----------------- | -------------- | ------------------------------------------- |
| Référentiels      | RH             | Fourniture des données de référence         |
| RH                | Variables      | Fourniture du contexte de l'agent           |
| Variables         | Calcul         | Transmission des éléments variables         |
| Calcul            | Documents      | Génération des résultats de paie            |
| Documents         | Exports        | Production des fichiers PDF et Excel        |
| Tous les domaines | Journalisation | Enregistrement des événements significatifs |

Les échanges fonctionnels sont unidirectionnels et suivent le cycle de gestion de la paie afin d'éviter les dépendances circulaires.

---

# 9. Exigences fonctionnelles transverses

Les exigences suivantes s'appliquent à l'ensemble des modules :

* validation systématique des données saisies ;
* cohérence des référentiels avant toute opération de calcul ;
* traçabilité des modifications importantes ;
* possibilité de rejouer un calcul à partir des mêmes données d'entrée ;
* gestion des dates d'effet pour les paramètres réglementaires ;
* conservation de l'historique des changements lorsque cela est requis par les règles métier.

---

# 10. Principes de navigation fonctionnelle

L'interface WPF devra permettre un accès logique aux fonctionnalités :

```text
Accueil
│
├── Référentiels
├── Ressources Humaines
├── Variables Mensuelles
├── Calcul de la Paie
├── Documents
├── Exports
├── Administration
└── Outils
```

L'utilisateur ne doit jamais être obligé de quitter un module pour terminer une tâche courante, sauf lorsqu'une dépendance fonctionnelle l'exige.

---

# 11. Exigences de cohérence

L'architecture fonctionnelle doit garantir que :

* une donnée possède une source unique de vérité ;
* une règle métier est définie une seule fois ;
* un document est généré à partir de données validées ;
* aucun calcul réglementaire n'est dupliqué dans plusieurs modules.

---

# 12. Traçabilité

Chaque exigence fonctionnelle définie dans ce volume devra être reliée :

* aux règles métier (Volumes 8 et 9) ;
* aux services applicatifs (Volume 11) ;
* aux écrans WPF (Volume 10) ;
* aux tests fonctionnels (Volume 18).

Cette traçabilité garantira que chaque fonctionnalité est spécifiée, implémentée, testée et maintenable.

---

## Évolution par rapport à la Version 1

Ce **Volume 2** dépasse une simple description des modules. Il établit une **cartographie fonctionnelle de l'ERP**, indépendante de l'implémentation technique, qui servira de référence pour les développements, les revues d'architecture et les tests.

Le **Volume 3 – Architecture Métier (Domain-Driven)** détaillera ensuite le cœur du système : les concepts métier (Agent, Carrière, Rubrique, Bulletin, Paramètre réglementaire, etc.), leurs relations, les agrégats, les services de domaine et les invariants qui garantiront la conformité réglementaire et la robustesse du moteur de paie.


# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome A — Vision & Architecture**

# **Volume 3**

# **Architecture Métier (Domain Architecture)**

**Version :** 2.0
**Statut :** Référentiel officiel de l'architecture métier

---

# 1. Objet du document

Ce volume définit le **cœur métier** de **PaieEducation ERP**.

Il formalise les concepts métier, leurs relations, leurs responsabilités et les règles qui gouvernent leur comportement.

Il constitue la référence absolue pour :

* les développeurs C# ;
* les architectes logiciels ;
* les concepteurs SQLite ;
* les testeurs ;
* les futurs mainteneurs.

Aucune logique métier ne devra être implémentée ailleurs que dans cette architecture.

---

# 2. Vision de l'architecture métier

Le cœur du système repose sur un **modèle de domaine riche (Rich Domain Model)**.

Les règles de calcul, les validations et les décisions métier appartiennent au domaine et non à l'interface utilisateur ni à la base de données.

```text
Présentation (WPF)
        │
        ▼
Application
        │
        ▼
DOMAIN
        │
        ▼
Persistence SQLite
```

Le **Domain** ne dépend d'aucune technologie.

---

# 3. Principes fondamentaux

## D1 — Le domaine est indépendant

Le projet doit pouvoir fonctionner indépendamment :

* de WPF ;
* de SQLite ;
* de QuestPDF ;
* de ClosedXML.

Le domaine ne connaît que les concepts métier.

---

## D2 — Une règle métier n'existe qu'à un seul endroit

Exemples :

* calcul IEP ;
* calcul IRG ;
* calcul CNAS ;
* éligibilité d'une indemnité ;
* calcul du salaire brut.

Chaque règle est implémentée une seule fois.

---

## D3 — Les entités protègent leur cohérence

Une entité ne peut jamais être placée dans un état invalide.

Exemple :

Un agent ne peut pas avoir :

* deux grades actifs simultanément ;
* deux contrats principaux actifs ;
* une date de nomination postérieure à la date de radiation.

---

## D4 — Le domaine ignore la présentation

Le domaine ne connaît jamais :

* les ViewModels ;
* les Views WPF ;
* les contrôles XAML.

---

# 4. Cartographie du domaine

Le domaine est découpé en **Bounded Contexts**.

```text
Référentiels
        │
        ▼
Ressources Humaines
        │
        ▼
Carrière
        │
        ▼
Paie
        │
        ▼
Documents
        │
        ▼
Administration
```

Chaque contexte possède son propre modèle.

---

# 5. Bounded Context : Référentiels

## Responsabilité

Fournir les données de référence.

### Entités principales

* Établissement
* Corps
* Grade
* Échelon
* Catégorie
* Fonction
* Rubrique
* Organisme
* Banque
* Paramètre réglementaire

---

# 6. Bounded Context : Ressources Humaines

## Agrégat racine

### Agent

L'agent constitue l'agrégat principal.

```text
Agent
│
├── État civil
├── Adresse
├── Situation familiale
├── Diplômes
├── Affectations
├── Contrats
├── Carrière
├── Variables
└── Bulletins
```

Toutes les opérations RH transitent par cet agrégat.

---

# 7. Bounded Context : Carrière

Responsabilités :

* nominations ;
* promotions ;
* avancements ;
* mutations ;
* changements de grade ;
* changements d'échelon ;
* cessations.

### Entités

* Carrière
* Nomination
* Promotion
* Avancement
* Affectation

---

# 8. Bounded Context : Paie

Il constitue le cœur fonctionnel de l'ERP.

## Agrégat principal

```text
Bulletin
│
├── Lignes
├── Gains
├── Retenues
├── Cotisations
├── IRG
├── Net
└── Journal de calcul
```

---

# 9. Entités métier

## Agent

Responsabilités :

* identité ;
* carrière ;
* situation administrative ;
* historique.

---

## Contrat

Responsabilités :

* nature du contrat ;
* dates ;
* statut ;
* temps de travail.

---

## Rubrique

Responsabilités :

* code réglementaire ;
* type ;
* formule ;
* priorité ;
* règles d'éligibilité.

---

## Bulletin

Responsabilités :

* période ;
* calcul ;
* validation ;
* génération documentaire.

---

## Paramètre Réglementaire

Responsabilités :

* valeur ;
* période de validité ;
* historique.

---

# 10. Value Objects

Les objets valeurs sont immuables.

## Exemples

### PériodePaie

```text
Mois
Année
```

---

### MontantMonétaire

```text
Valeur
Devise (DZD)
```

---

### Pourcentage

```text
Valeur
```

---

### Adresse

```text
Wilaya
Commune
Code Postal
```

---

# 11. Services de domaine

Les services encapsulent les traitements complexes.

## Services principaux

### BulletinCalculationService

Responsable du calcul complet.

---

### EligibilityService

Détermination des rubriques applicables.

---

### IRGCalculationService

Calcul de l'impôt.

---

### CNASCalculationService

Cotisations sociales.

---

### SeniorityService

Calcul de l'ancienneté.

---

### ReminderCalculationService

Calcul des rappels.

---

# 12. Calculateurs métier

Chaque calcul réglementaire devient un composant indépendant.

```text
BaseSalaryCalculator

↓

IEPCalculator

↓

PrimeCalculator

↓

CNASCalculator

↓

IRGCalculator

↓

NetSalaryCalculator
```

Ils sont orchestrés par le moteur de paie.

---

# 13. Moteur de paie

Le moteur exécute les calculateurs selon un pipeline déterministe.

```text
Construction du contexte

↓

Contrôles

↓

Éligibilité

↓

Calcul Brut

↓

Cotisations

↓

IRG

↓

Net

↓

Contrôles

↓

Journal
```

Chaque étape est indépendante et testable.

---

# 14. Spécifications métier

Les règles d'éligibilité sont encapsulées dans des **Specifications**.

Exemples :

* GradeSpecification
* CorpsSpecification
* ContratSpecification
* AncienneteSpecification
* EtablissementSpecification

Ces composants permettent de composer des règles complexes sans multiplier les conditions dans le code.

---

# 15. Invariants métier

Les invariants suivants sont obligatoires :

* un agent possède un matricule unique ;
* un bulletin est associé à une seule période ;
* un contrat principal actif est unique ;
* une rubrique possède un code unique ;
* une ligne de bulletin est rattachée à un seul bulletin.

Ils sont vérifiés par le domaine avant toute persistance.

---

# 16. Événements métier

Le domaine publie des événements internes.

Exemples :

* AgentCréé
* ContratModifié
* BulletinCalculé
* BulletinValidé
* ParamètreRéglementaireModifié
* BulletinImprimé

Ces événements favorisent le découplage entre modules.

---

# 17. Domaine des documents

Le domaine documentaire est indépendant.

Documents produits :

* Bulletin de paie
* Attestation de travail
* Attestation de salaire
* Décision
* Certificat
* État récapitulatif
* Livre de paie

Le domaine fournit les données ; la couche **Reporting** (QuestPDF) se charge uniquement de leur mise en forme.

---

# 18. Domaine de l'audit

Toutes les opérations critiques sont historisées.

Exemples :

* création d'un agent ;
* modification d'une carrière ;
* recalcul d'un bulletin ;
* validation d'une paie.

Cette traçabilité est essentielle pour les contrôles internes.

---

# 19. Dépendances autorisées

```text
Presentation
      │
      ▼
Application
      │
      ▼
Domain
```

Le **Domain** ne référence aucune couche supérieure.

Il ne dépend ni de WPF, ni de SQLite, ni de QuestPDF, ni de ClosedXML.

---

# 20. Cartographie des agrégats

| Agrégat     | Racine                  | Entités associées                                       |
| ----------- | ----------------------- | ------------------------------------------------------- |
| Agent       | Agent                   | État civil, Situation familiale, Affectations, Contrats |
| Carrière    | Carrière                | Nominations, Promotions, Avancements                    |
| Bulletin    | Bulletin                | Lignes, Gains, Retenues, Cotisations, Journal           |
| Référentiel | Rubrique                | Formule, Paramètres, Conditions                         |
| Paramètres  | Paramètre réglementaire | Valeurs, Dates d'effet, Historique                      |

---

# 21. Catalogue des services métier

| Service                       | Responsabilité                          |
| ----------------------------- | --------------------------------------- |
| `BulletinCalculationService`  | Orchestration du calcul complet         |
| `EligibilityService`          | Détermination des rubriques applicables |
| `IRGCalculationService`       | Calcul de l'IRG                         |
| `CNASCalculationService`      | Calcul des cotisations sociales         |
| `ReminderCalculationService`  | Gestion des rappels                     |
| `SeniorityService`            | Calcul de l'ancienneté                  |
| `RegulationValidationService` | Contrôle de conformité réglementaire    |

---

# 22. Critères d'acceptation

L'architecture métier est conforme si :

* toute règle réglementaire est implémentée dans le **Domain** ;
* aucune règle métier n'est présente dans les Views ou ViewModels ;
* les calculateurs sont indépendants et testables ;
* les agrégats garantissent leurs invariants ;
* les services de domaine sont faiblement couplés ;
* les événements métier permettent de découpler les traitements transverses.

---

# Conclusion

Ce **Volume 3** établit le **modèle métier de référence** de PaieEducation ERP. Il définit les concepts, les agrégats, les services et les invariants qui garantiront la cohérence réglementaire et la robustesse du moteur de paie.

Le **Volume 4 – Architecture Logicielle Modulaire** traduira ensuite ce modèle métier en une organisation logicielle concrète : structure de la solution Visual Studio, projets, dépendances, conventions de développement, interfaces, injection de dépendances et organisation des composants autour de l'architecture **Presentation / Application / Domain / Infrastructure / Persistence / Reporting**. Ce volume fera le lien entre la conception métier et l'implémentation technique.


# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome A — Vision & Architecture**

# **Volume 4**

# **Architecture Logicielle Modulaire**

**Version :** 2.0
**Statut :** Référentiel officiel de l'architecture logicielle

---

# 1. Objet du document

Ce volume définit l'architecture logicielle de **PaieEducation ERP**.

Il traduit l'architecture métier (Volume 3) en une architecture logicielle concrète destinée à être implémentée avec :

* **.NET 10 LTS**
* **WPF**
* **MVVM**
* **SQLite**
* **CommunityToolkit.Mvvm**
* **Microsoft.Extensions.DependencyInjection**
* **Microsoft.Extensions.Logging**
* **QuestPDF**
* **ClosedXML**

Il constitue le document de référence pour toute l'organisation de la solution Visual Studio, des projets, des dépendances et des conventions de développement.

---

# 2. Principes d'architecture

L'architecture repose sur les principes suivants :

* séparation stricte des responsabilités ;
* dépendances unidirectionnelles ;
* inversion de dépendance (Dependency Inversion Principle) ;
* forte cohésion des modules ;
* faible couplage ;
* testabilité complète ;
* extensibilité.

---

# 3. Architecture globale

L'application est organisée en couches.

```text
                 +--------------------------------------+
                 | Presentation (WPF)                  |
                 +--------------------------------------+
                               │
                               ▼
                 +--------------------------------------+
                 | Application                         |
                 +--------------------------------------+
                               │
                               ▼
                 +--------------------------------------+
                 | Domain                              |
                 +--------------------------------------+
                               ▲
                               │
                 +--------------------------------------+
                 | Infrastructure                      |
                 +--------------------------------------+
                               │
                               ▼
                 +--------------------------------------+
                 | Persistence.SQLite                  |
                 +--------------------------------------+
```

Le module **Reporting** est transversal.

---

# 4. Solution Visual Studio

```text
PaieEducation.sln

│
├── src
│   │
│   ├── PaieEducation.Presentation.WPF
│   │
│   ├── PaieEducation.Application
│   │
│   ├── PaieEducation.Domain
│   │
│   ├── PaieEducation.Infrastructure
│   │
│   ├── PaieEducation.Persistence.SQLite
│   │
│   ├── PaieEducation.Reporting
│   │
│   ├── PaieEducation.Shared
│   │
│   └── PaieEducation.Bootstrap
│
├── tests
│   │
│   ├── Domain.Tests
│   ├── Application.Tests
│   ├── Persistence.Tests
│   └── Integration.Tests
│
└── docs
```

---

# 5. Dépendances entre projets

Les dépendances sont volontairement limitées.

```text
Presentation.WPF
        │
        ▼
Application
        │
        ▼
Domain

Infrastructure ─────► Domain

Persistence.SQLite ─► Domain

Reporting ──────────► Application

Bootstrap ──────────► Tous
```

Le **Domain** ne dépend d'aucun autre projet.

---

# 6. Projet Presentation.WPF

## Responsabilités

* Interface utilisateur
* Navigation
* Thèmes
* Dialogues
* DataBinding
* Validation visuelle

## Structure

```text
Views

ViewModels

Controls

Dialogs

Themes

Resources

Converters

Behaviors

Templates
```

Aucune logique métier n'est autorisée dans ce projet.

---

# 7. Projet Application

Cette couche orchestre les cas d'utilisation.

## Contenu

```text
UseCases

Services

Interfaces

DTO

Validators

Commands

Queries

Mappings
```

Elle coordonne les traitements entre l'interface, le domaine et la persistance.

---

# 8. Projet Domain

Le cœur métier.

Structure proposée :

```text
Entities

ValueObjects

Aggregates

Rules

Specifications

Events

Calculators

Services
```

Ce projet est indépendant de toute technologie.

---

# 9. Projet Infrastructure

Fonctions techniques communes.

```text
Configuration

Logging

Localization

DependencyInjection

Security

Caching

Clock

FileSystem
```

Il fournit des services transverses sans contenir de logique métier.

---

# 10. Projet Persistence.SQLite

Responsable de l'accès aux données.

```text
Context

Repositories

UnitOfWork

Mappings

Scripts

Seed

Migrations
```

Aucun calcul métier n'y est implémenté.

---

# 11. Projet Reporting

Responsable de la génération documentaire.

Structure :

```text
QuestPdf

Templates

Styles

Printers

Preview

PdfExport
```

Toutes les sorties PDF sont produites ici.

---

# 12. Projet Shared

Bibliothèque commune.

Contenu :

* constantes ;
* types communs ;
* exceptions ;
* résultats (`Result<T>`) ;
* extensions ;
* utilitaires.

---

# 13. Projet Bootstrap

Point d'entrée de l'application.

Responsabilités :

* configuration ;
* démarrage ;
* injection de dépendances ;
* chargement des paramètres ;
* ouverture de la fenêtre principale.

---

# 14. Architecture MVVM

```text
View
      │
Binding
      │
      ▼
ViewModel
      │
RelayCommand
      │
      ▼
Application Service
      │
      ▼
Domain Service
      │
      ▼
Repository
      │
      ▼
SQLite
```

Les **Views** ne connaissent jamais la couche Domain.

---

# 15. Injection de dépendances

Toutes les dépendances sont enregistrées dans le projet Bootstrap.

Exemple de catégories :

* Services applicatifs
* Repositories
* Domain Services
* Reporting
* Logging
* Configuration

L'injection par constructeur est privilégiée.

---

# 16. Communication entre couches

Les règles suivantes sont obligatoires :

* les Views communiquent uniquement avec leurs ViewModels ;
* les ViewModels communiquent avec des interfaces de services applicatifs ;
* les services applicatifs orchestrent les traitements métier ;
* les repositories sont utilisés uniquement pour la persistance.

---

# 17. Gestion des erreurs

Les erreurs sont classées en quatre catégories :

| Catégorie  | Exemple                         |
| ---------- | ------------------------------- |
| Validation | Champ obligatoire manquant      |
| Métier     | Rubrique non éligible           |
| Technique  | Base SQLite indisponible        |
| Système    | Fichier de configuration absent |

Les exceptions ne doivent pas être utilisées pour piloter la logique métier ; elles sont réservées aux situations exceptionnelles.

---

# 18. Journalisation

Toute opération critique doit être journalisée.

Exemples :

* ouverture de session ;
* calcul d'un bulletin ;
* génération d'un PDF ;
* export Excel ;
* erreur de persistance.

La journalisation est centralisée via `Microsoft.Extensions.Logging`.

---

# 19. Configuration

La configuration est stockée dans `appsettings.json`.

Exemples de paramètres :

* chemin de la base SQLite ;
* répertoire des sauvegardes ;
* paramètres d'impression ;
* niveau de journalisation ;
* options d'export.

Les paramètres sont injectés via le **Options Pattern**.

---

# 20. Asynchronisme

Les opérations potentiellement longues sont asynchrones :

* chargement des données ;
* calculs de masse ;
* génération des PDF ;
* export Excel ;
* sauvegardes.

L'usage de `async`/`await` est privilégié pour préserver la réactivité de l'interface WPF.

---

# 21. Découplage du reporting

Le reste de l'application ne dépend jamais directement de QuestPDF.

Une abstraction est imposée :

```csharp
public interface IReportService
{
    Task<byte[]> GenerateBulletinAsync(Guid bulletinId);

    Task<byte[]> GenerateAttestationAsync(Guid agentId);

    Task<byte[]> GeneratePayrollBookAsync(int year, int month);
}
```

Cette approche permettra de remplacer le moteur de reporting sans modifier les couches métier.

---

# 22. Découplage de la persistance

Le domaine ne connaît pas SQLite.

Les accès aux données passent par des interfaces telles que :

```csharp
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id);

    Task SaveAsync(Agent agent);

    Task DeleteAsync(Guid id);
}
```

Les implémentations SQLite résident exclusivement dans `PaieEducation.Persistence.SQLite`.

---

# 23. Règles de dépendance

| Projet             | Peut dépendre de                 |
| ------------------ | -------------------------------- |
| Presentation.WPF   | Application                      |
| Application        | Domain                           |
| Domain             | Aucun projet métier ou technique |
| Infrastructure     | Domain                           |
| Persistence.SQLite | Domain                           |
| Reporting          | Application                      |
| Shared             | Aucun projet métier              |

Toute dépendance inverse est interdite.

---

# 24. Critères d'acceptation

L'architecture logicielle est conforme si :

* chaque projet a une responsabilité unique ;
* les dépendances respectent la hiérarchie définie ;
* aucune logique métier n'est présente dans la couche de présentation ;
* le domaine est indépendant des technologies ;
* les services techniques sont injectés via des interfaces ;
* les composants sont testables isolément.

---

# Conclusion

Ce **Volume 4** définit la structure logicielle officielle de **PaieEducation ERP**. Il fournit un cadre stable pour l'organisation de la solution, la répartition des responsabilités et les dépendances entre les composants.

Le **Volume 5 – Architecture Technique** approfondira cette conception en détaillant les choix techniques concrets : configuration de **.NET 10 LTS**, conventions de codage C#, structure des projets WPF, intégration de **CommunityToolkit.Mvvm**, configuration de **Microsoft.Extensions.DependencyInjection** et **Microsoft.Extensions.Logging**, stratégie de persistance SQLite, ainsi que les standards de qualité, de sécurité et de performance qui devront être appliqués à l'ensemble du projet.


# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome A — Vision & Architecture**

# **Volume 5**

# **Architecture Technique (.NET 10 LTS, WPF, MVVM, SQLite)**

**Version :** 2.0
**Statut :** Référentiel technique officiel

---

# 1. Objet du document

Ce volume définit l'ensemble des standards techniques de **PaieEducation ERP**.

Il constitue la référence officielle pour :

* l'environnement de développement ;
* les conventions de codage ;
* les bibliothèques autorisées ;
* l'architecture WPF ;
* la configuration .NET 10 ;
* les performances ;
* la sécurité ;
* les standards de qualité.

Toute implémentation devra respecter les prescriptions de ce document.

---

# 2. Pile technologique officielle

| Domaine                  | Technologie retenue                              |
| ------------------------ | ------------------------------------------------ |
| Langage                  | C# 14 (selon la version livrée avec .NET 10 LTS) |
| Framework                | .NET 10 LTS                                      |
| UI                       | WPF                                              |
| Pattern UI               | MVVM                                             |
| Toolkit MVVM             | CommunityToolkit.Mvvm                            |
| Base de données          | SQLite                                           |
| Accès aux données        | Microsoft.Data.Sqlite                            |
| Injection de dépendances | Microsoft.Extensions.DependencyInjection         |
| Journalisation           | Microsoft.Extensions.Logging                     |
| Configuration            | Microsoft.Extensions.Configuration               |
| Options                  | Microsoft.Extensions.Options                     |
| Reporting                | QuestPDF                                         |
| Excel                    | ClosedXML                                        |
| Tests                    | xUnit                                            |

---

# 3. Objectifs techniques

L'architecture doit garantir :

* robustesse ;
* simplicité de maintenance ;
* évolutivité ;
* performances constantes ;
* forte testabilité ;
* faible couplage ;
* modularité.

---

# 4. Standards du langage C#

Les fonctionnalités modernes de C# devront être privilégiées lorsqu'elles améliorent la lisibilité et la sécurité du code.

## Obligatoires

* Nullable Reference Types
* File Scoped Namespace
* Global Using
* Pattern Matching
* Expression-bodied Members (lorsqu'ils améliorent la lisibilité)
* Records pour les DTO immuables
* `required` pour les propriétés obligatoires
* `init` lorsque la mutabilité n'est pas nécessaire

## À utiliser avec discernement

* Primary Constructors
* Collection Expressions
* Async Streams

L'objectif est de moderniser le code sans sacrifier sa compréhension.

---

# 5. Architecture WPF

## Organisation

```text
Presentation.WPF

├── App.xaml
├── MainWindow.xaml
│
├── Views
├── ViewModels
├── Controls
├── Dialogs
├── Behaviors
├── Converters
├── Resources
├── Themes
├── Templates
└── Assets
```

---

# 6. Pattern MVVM

Chaque fonctionnalité est composée de trois éléments :

```text
View

↓

ViewModel

↓

Application Service
```

### Responsabilités

#### View

* affichage ;
* DataBinding ;
* animations ;
* styles.

#### ViewModel

* état de l'écran ;
* commandes (`RelayCommand`) ;
* validation de présentation ;
* orchestration des cas d'utilisation.

#### Service applicatif

* exécution du cas d'utilisation ;
* coordination avec le domaine.

---

# 7. CommunityToolkit.Mvvm

L'ensemble des ViewModels hérite de `ObservableObject`.

Fonctionnalités retenues :

* `[ObservableProperty]`
* `[RelayCommand]`
* `WeakReferenceMessenger` (pour les notifications inter-modules si nécessaire)
* `ObservableRecipient` pour les composants ayant besoin de recevoir des messages

Cette approche réduit le code répétitif et améliore la lisibilité.

---

# 8. Injection de dépendances

Toutes les dépendances sont enregistrées au démarrage de l'application.

Catégories de services :

| Catégorie            | Exemple                 |
| -------------------- | ----------------------- |
| Services applicatifs | `PayrollService`        |
| Repositories         | `AgentRepository`       |
| Domain Services      | `EligibilityService`    |
| Reporting            | `QuestPdfReportService` |
| Infrastructure       | `LoggingService`        |

L'injection par constructeur est la règle générale.

---

# 9. Configuration

La configuration est externalisée.

## Fichier

```text
appsettings.json
```

### Contenu

* chemin de la base SQLite ;
* répertoire des sauvegardes ;
* paramètres d'impression ;
* options d'export ;
* niveau de journalisation ;
* paramètres régionaux.

Les paramètres sont consommés via `IOptions<T>`.

---

# 10. Journalisation

La journalisation est centralisée avec `Microsoft.Extensions.Logging`.

## Niveaux

* Trace
* Debug
* Information
* Warning
* Error
* Critical

Les journaux doivent être exploitables pour le diagnostic sans exposer de données sensibles.

---

# 11. Persistance SQLite

## Principes

* base locale unique ;
* intégrité référentielle ;
* transactions atomiques ;
* requêtes paramétrées ;
* indexation des colonnes de recherche ;
* gestion des migrations.

La couche de persistance ne contient aucune logique métier.

---

# 12. Repositories

Chaque agrégat racine dispose de son repository.

Exemples :

* `IAgentRepository`
* `IBulletinRepository`
* `IRubriqueRepository`
* `IReglementRepository`

Les ViewModels n'accèdent jamais directement à SQLite.

---

# 13. Gestion des transactions

Les opérations critiques sont exécutées dans une transaction.

Exemples :

* calcul et validation d'un bulletin ;
* création d'un agent ;
* régularisation de paie.

En cas d'erreur, l'état de la base doit rester cohérent.

---

# 14. Reporting

Le moteur officiel est **QuestPDF**.

## Documents

* bulletin de paie ;
* attestation de travail ;
* attestation de salaire ;
* certificat administratif ;
* livre de paie ;
* états récapitulatifs.

Les modèles de présentation sont séparés des données métier.

---

# 15. Export Excel

Le moteur officiel est **ClosedXML**.

Exports pris en charge :

* listes d'agents ;
* journaux de paie ;
* états récapitulatifs ;
* statistiques ;
* contrôles.

Les exports doivent produire des fichiers `.xlsx` compatibles avec Microsoft Excel et LibreOffice.

---

# 16. Validation

La validation est répartie sur trois niveaux.

| Niveau       | Responsable |
| ------------ | ----------- |
| Présentation | ViewModel   |
| Métier       | Domain      |
| Persistance  | SQLite      |

Chaque niveau traite uniquement les validations qui relèvent de sa responsabilité.

---

# 17. Gestion des erreurs

Les erreurs sont normalisées.

## Catégories

* validation ;
* métier ;
* technique ;
* infrastructure.

Les messages destinés à l'utilisateur doivent être compréhensibles et ne pas exposer de détails techniques.

---

# 18. Performance

Objectifs :

* ouverture de l'application : < 3 secondes ;
* chargement d'une fiche agent : < 1 seconde ;
* calcul d'un bulletin individuel : < 3 secondes ;
* génération d'un bulletin PDF : < 2 secondes ;
* export Excel de 500 agents : < 10 secondes.

Ces objectifs servent de référence pour les campagnes de tests.

---

# 19. Concurrence

Même si l'application est mono-poste, elle doit rester réactive.

Les traitements longs sont exécutés de manière asynchrone :

* génération de rapports ;
* exports ;
* sauvegardes.

L'interface WPF ne doit jamais être bloquée pendant ces opérations.

---

# 20. Sécurité technique

Mesures minimales :

* requêtes SQL paramétrées ;
* validation des entrées utilisateur ;
* contrôle des chemins de fichiers ;
* gestion des exceptions centralisée ;
* journalisation des erreurs.

---

# 21. Sauvegarde

Le système doit proposer :

* sauvegarde complète de la base SQLite ;
* restauration sécurisée ;
* vérification de l'intégrité avant restauration ;
* possibilité d'automatiser les sauvegardes locales.

---

# 22. Standards de qualité

Le code doit respecter :

* **SOLID** ;
* **DRY** (Don't Repeat Yourself) ;
* **KISS** (Keep It Simple, Stupid) ;
* **YAGNI** (You Aren't Gonna Need It), afin d'éviter l'ajout de fonctionnalités inutiles ;
* séparation des responsabilités ;
* lisibilité ;
* testabilité.

---

# 23. Compatibilité

L'application est conçue pour :

* Windows 10 (64 bits) ;
* Windows 11 (64 bits).

Le fonctionnement est entièrement hors ligne.

---

# 24. Évolutivité

L'architecture doit permettre :

* l'ajout de nouveaux modules métier ;
* l'évolution des règles réglementaires ;
* l'ajout de nouveaux documents QuestPDF ;
* l'ajout de nouveaux exports ClosedXML ;
* l'évolution du schéma SQLite via des migrations maîtrisées.

Aucune évolution ne doit nécessiter de refonte de l'architecture.

---

# 25. Critères d'acceptation

L'architecture technique est conforme si :

* la solution compile sous **.NET 10 LTS** ;
* toutes les fonctionnalités WPF respectent le modèle **MVVM** ;
* la persistance est exclusivement assurée par **SQLite** ;
* les dépendances sont injectées via **Microsoft.Extensions.DependencyInjection** ;
* la journalisation est centralisée avec **Microsoft.Extensions.Logging** ;
* les documents sont générés avec **QuestPDF** ;
* les exports Excel utilisent **ClosedXML** ;
* les objectifs de performance sont atteints.

---

# Conclusion

Ce **Volume 5** clôt le **Tome A – Vision & Architecture**. Il établit les fondations techniques de PaieEducation ERP et fixe les normes qui guideront l'ensemble du développement.

À partir du **Volume 6**, nous entrons dans le **Tome B – Fonctionnel**, où chaque domaine métier sera décrit avec un niveau de détail comparable à celui d'un ERP industriel. Nous commencerons par le **Catalogue des Modules**, qui définira exhaustivement chaque module, ses responsabilités, ses interactions, ses cas d'utilisation, ses dépendances et ses critères d'acceptation. Ce volume servira de référence pour le découpage fonctionnel de l'application et la planification des développements.

