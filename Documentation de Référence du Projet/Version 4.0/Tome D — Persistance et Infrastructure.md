# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome D — Persistance et Infrastructure**

# **Volume 11**

# **Architecture de la Persistance SQLite, Repositories et Unit of Work**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP**)
**Technologies :** .NET 10 LTS • SQLite • Dapper • Clean Architecture • DDD

---

# Préambule

Ce volume marque le début du **Tome D**, consacré à l'ensemble de la couche **Infrastructure**.

Il définit l'architecture de persistance qui permettra au moteur métier de fonctionner de manière fiable, performante et totalement hors ligne.

L'objectif n'est pas simplement d'utiliser SQLite comme une base de données, mais d'en faire un **moteur de persistance robuste**, comparable à ceux utilisés dans les ERP modernes.

---

# 1. Objectifs

La couche de persistance doit garantir :

* performances élevées ;
* intégrité des données ;
* transactions ACID ;
* maintenance simplifiée ;
* évolutivité ;
* indépendance vis-à-vis de WPF ;
* indépendance vis-à-vis du moteur de calcul.

---

# 2. Architecture générale

```text
                Domain

                   ▲
                   │
             Repository Interfaces
                   │
────────────────────────────────────────────

             Persistence Layer

────────────────────────────────────────────

Repositories

UnitOfWork

SQLite Connection

SQLite Database

────────────────────────────────────────────
```

Le domaine ne dépend jamais de SQLite.

---

# 3. Organisation des projets

```text
PaieEducation.Persistence

│

├── Configuration
├── Database
├── Repositories
├── Queries
├── Commands
├── Mapping
├── Migrations
├── Transactions
├── Backup
├── Audit
├── Cache
└── Services
```

Chaque dossier possède une responsabilité unique.

---

# 4. Architecture Repository

Tous les accès aux données passent exclusivement par des **Repositories**.

Aucune couche supérieure n'accède directement à SQLite.

Exemple :

```text
Application

↓

Repository

↓

SQLite
```

---

# 5. Interfaces Repository

Le projet Domain expose uniquement les contrats.

Exemples :

```text
IAgentRepository

IBulletinRepository

IPeriodeRepository

IRubriqueRepository

IGradeRepository

IContratRepository
```

Le projet Persistence fournit les implémentations.

---

# 6. Repository générique

Les opérations communes sont factorisées.

```csharp
public interface IRepository<TEntity,TKey>
{
    Task<TEntity?> GetAsync(TKey id);

    Task AddAsync(TEntity entity);

    Task UpdateAsync(TEntity entity);

    Task DeleteAsync(TKey id);
}
```

Les repositories spécialisés complètent ce contrat lorsque nécessaire.

---

# 7. Unit of Work

Toutes les opérations métier sont regroupées dans une **Unit of Work**.

Responsabilités :

* ouvrir une transaction ;
* coordonner plusieurs repositories ;
* valider ;
* annuler.

```text
Application Service

↓

UnitOfWork

↓

Repositories

↓

SQLite
```

---

# 8. Contrat Unit of Work

```csharp
public interface IUnitOfWork
{
    Task BeginAsync();

    Task CommitAsync();

    Task RollbackAsync();
}
```

Une opération métier complexe correspond à une seule unité de travail.

---

# 9. Gestion des transactions

Les transactions SQLite doivent respecter les principes ACID.

Types :

| Transaction | Usage         |
| ----------- | ------------- |
| Lecture     | Consultation  |
| Écriture    | Modification  |
| Batch       | Calcul massif |
| Maintenance | Migration     |

Les transactions imbriquées sont interdites.

---

# 10. Stratégie de connexion

Les connexions SQLite sont ouvertes :

* le plus tard possible ;
* fermées le plus tôt possible.

Les connexions persistantes sont évitées.

---

# 11. Mapping Objet ↔ SQLite

Le mapping est centralisé.

```text
Agent

↓

AgentMapper

↓

Table Agents
```

Chaque entité possède un mapper dédié.

---

# 12. Politique des identifiants

Toutes les entités utilisent des identifiants techniques de type **GUID**.

Les codes métier (matricule, code rubrique, code grade, etc.) restent des attributs fonctionnels et ne sont jamais utilisés comme clés primaires.

---

# 13. Convention des tables

Règles :

* noms explicites ;
* singulier ou pluriel uniforme (à définir et appliquer partout) ;
* clés primaires nommées `Id` ;
* clés étrangères explicites ;
* index nommés.

Exemple :

```text
Agents

Contrats

Bulletins

Rubriques

Grades

Corps
```

---

# 14. Gestion des migrations

Le schéma SQLite évolue uniquement via des migrations versionnées.

Chaque migration :

* est idempotente lorsque possible ;
* possède un identifiant unique ;
* est documentée ;
* peut être rejouée sur une nouvelle base.

Les modifications manuelles du schéma en production sont proscrites.

---

# 15. Journal des migrations

Une table dédiée conserve l'historique.

```text
SchemaVersions

Version

DateApplication

Description

Auteur

Checksum
```

Elle permet de vérifier la cohérence du schéma.

---

# 16. Optimisation SQLite

Les optimisations recommandées comprennent :

* index adaptés aux recherches fréquentes ;
* utilisation du mode **WAL (Write-Ahead Logging)** ;
* contraintes d'intégrité (`FOREIGN KEY`) activées ;
* analyse périodique (`ANALYZE`) ;
* optimisation (`VACUUM`) lors des opérations de maintenance.

---

# 17. Gestion des accès concurrents

L'application est principalement mono-poste, mais la couche de persistance doit :

* sérialiser les écritures ;
* éviter les verrous prolongés ;
* gérer les erreurs de verrouillage SQLite avec une stratégie de nouvelle tentative (*retry*) limitée.

---

# 18. Sauvegarde

Le système fournit un service dédié.

Fonctionnalités :

* sauvegarde complète ;
* restauration ;
* vérification d'intégrité ;
* compression optionnelle ;
* conservation de métadonnées (date, version, taille).

Les sauvegardes sont réalisées hors transaction active.

---

# 19. Journalisation technique

Chaque opération importante est tracée :

* ouverture de connexion ;
* transaction ;
* erreur SQL ;
* migration ;
* sauvegarde ;
* restauration.

La journalisation s'appuie sur `Microsoft.Extensions.Logging`.

---

# 20. Performances

Objectifs :

| Opération                    | Temps cible |
| ---------------------------- | ----------: |
| Lecture d'un agent           |     < 10 ms |
| Chargement d'un bulletin     |     < 20 ms |
| Enregistrement d'un bulletin |     < 50 ms |
| Calcul + sauvegarde          |    < 500 ms |
| Sauvegarde de la base        |       < 5 s |

---

# 21. Sécurité des données

La couche de persistance garantit :

* validation des paramètres ;
* requêtes paramétrées (protection contre les injections SQL) ;
* contrôle des transactions ;
* vérification des contraintes d'intégrité.

Les mots de passe éventuels (si ajout futur d'une authentification) ne sont jamais stockés en clair.

---

# 22. Tests

Les repositories doivent être couverts par :

* tests unitaires avec doubles de test ;
* tests d'intégration sur une base SQLite temporaire ;
* tests de performance ;
* tests de migration.

Chaque migration est testée sur une base vide et sur une base existante.

---

# 23. Critères d'acceptation

La couche de persistance est validée lorsque :

* toutes les opérations passent par les repositories ;
* le domaine ne dépend d'aucune bibliothèque SQLite ;
* les transactions sont gérées par `IUnitOfWork` ;
* les migrations sont versionnées et reproductibles ;
* les sauvegardes et restaurations sont fiables ;
* les performances respectent les objectifs définis.

---

# 24. ADR (Architecture Decision Records)

| ADR     | Décision                                                       |
| ------- | -------------------------------------------------------------- |
| ADR-056 | SQLite est l'unique moteur de persistance de l'ERP             |
| ADR-057 | Adoption du pattern Repository pour tous les accès aux données |
| ADR-058 | Gestion transactionnelle centralisée par `IUnitOfWork`         |
| ADR-059 | Migrations versionnées et historisées                          |
| ADR-060 | Optimisation de SQLite via WAL, index et maintenance planifiée |

---

# Conclusion

Le **Volume 11** définit l'architecture de la couche **Persistence** de **PaieEducation ERP**. Il établit les règles de conception des repositories, de l'unité de travail, des transactions, des migrations et de l'optimisation de SQLite, en garantissant une séparation stricte entre le domaine métier et l'infrastructure.

## Recommandation pour le Volume 12

Le **Volume 12** devrait être consacré au **schéma physique de la base SQLite**. Il décrira avec un niveau de détail comparable à un dictionnaire de données ERP :

* les tables ;
* les colonnes ;
* les types SQLite ;
* les clés primaires et étrangères ;
* les index ;
* les contraintes ;
* les vues ;
* les déclencheurs (*triggers*) si nécessaires ;
* les règles de nommage ;
* le mapping complet entre les entités du domaine et les objets de la base.

Ce document deviendra la référence officielle pour l'implémentation et l'évolution de la base de données de **PaieEducation ERP**.

Je pense qu'à partir de maintenant, il est judicieux de faire évoluer la documentation vers un **niveau d'architecture comparable aux grands ERP**.

En effet, jusqu'au Volume 11, nous avons principalement décrit **les composants**.

À partir du **Volume 12**, nous allons définir **la structure physique complète de la base de données**, avec un niveau de détail permettant de développer directement le projet.

C'est également le bon moment pour introduire une amélioration importante par rapport aux versions précédentes.

> **Décision d'architecture (ADR-061)** : abandon du Repository générique (`IRepository<TEntity, TKey>`) au profit de **Repositories spécialisés par agrégat**.

Cette décision est plus conforme au **Domain-Driven Design** et aux pratiques des ERP modernes. Les repositories génériques deviennent souvent une limitation dès que les requêtes métier se complexifient (calcul de paie, recherche d'historique, projections, statistiques, etc.). Chaque agrégat disposera donc d'un repository dédié (`IAgentRepository`, `IBulletinRepository`, etc.) avec des méthodes métier explicites.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome D — Persistance et Infrastructure**

# **Volume 12**

# **Modèle Physique SQLite – Schéma de Base de Données, Conventions et Mapping du Domaine**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP**)
**Technologies :** SQLite • Dapper • .NET 10 LTS • Clean Architecture

---

# Préambule

Ce document définit le **modèle physique officiel** de la base de données de **PaieEducation ERP**.

Il constitue la référence unique pour :

* la création du schéma SQLite ;
* les migrations ;
* les index ;
* les contraintes ;
* les conventions de nommage ;
* le mapping avec le modèle de domaine.

Toute évolution du schéma devra être compatible avec cette spécification.

---

# 1. Principes de conception

Le modèle physique respecte les principes suivants :

* séparation stricte entre modèle métier et modèle physique ;
* absence de duplication inutile ;
* normalisation (3NF au minimum, dénormalisation uniquement justifiée par des besoins de performance) ;
* intégrité référentielle systématique ;
* compatibilité avec les migrations incrémentales.

---

# 2. Organisation logique du schéma

Les tables sont regroupées par domaine fonctionnel.

```text
Personnel
├── Agents
├── Contrats
├── Affectations
├── Carrieres

Paie
├── Bulletins
├── BulletinLignes
├── VariablesPaie
├── Rubriques

Référentiels
├── Grades
├── Corps
├── Echelons
├── Categories
├── Etablissements

Administration
├── Parametres
├── Utilisateurs (optionnel)
├── Journaux

Technique
├── SchemaVersions
├── AuditLog
├── BackupHistory
```

---

# 3. Conventions de nommage

Toutes les tables suivent les mêmes règles :

| Élément             | Convention                                    |
| ------------------- | --------------------------------------------- |
| Tables              | PascalCase au pluriel (`Agents`, `Bulletins`) |
| Clé primaire        | `Id`                                          |
| Clé étrangère       | `<Entité>Id` (`AgentId`, `GradeId`)           |
| Colonnes booléennes | Préfixe `Is` (`IsActive`)                     |
| Dates               | Préfixe `Date` (`DateCreation`)               |
| Index               | `IX_<Table>_<Colonnes>`                       |
| Contraintes         | `FK_`, `PK_`, `CK_`, `UQ_`                    |

Cette uniformité facilite la maintenance et les migrations.

---

# 4. Types SQLite retenus

| Concept métier  | Type SQLite         |
| --------------- | ------------------- |
| Guid            | TEXT                |
| Chaîne          | TEXT                |
| Booléen         | INTEGER (0/1)       |
| DateOnly        | TEXT (ISO 8601)     |
| DateTime        | TEXT (UTC ISO 8601) |
| Decimal / Money | NUMERIC             |
| Integer         | INTEGER             |
| Enum            | INTEGER             |
| BLOB            | BLOB                |

Les dates sont stockées au format ISO 8601 afin de garantir leur portabilité.

---

# 5. Politique des identifiants

Toutes les tables utilisent :

```text
Id TEXT PRIMARY KEY
```

Les GUID sont générés par l'application.

Les codes métier (`Matricule`, `CodeRubrique`, `CodeGrade`, etc.) sont soumis à des contraintes d'unicité lorsqu'ils l'exigent, mais ne servent jamais de clé primaire.

---

# 6. Exemple : table `Agents`

| Colonne          | Type    | Contraintes |
| ---------------- | ------- | ----------- |
| Id               | TEXT    | PK          |
| Matricule        | TEXT    | UNIQUE      |
| Nom              | TEXT    | NOT NULL    |
| Prenom           | TEXT    | NOT NULL    |
| DateNaissance    | TEXT    | NOT NULL    |
| DateRecrutement  | TEXT    | NOT NULL    |
| GradeId          | TEXT    | FK          |
| CorpsId          | TEXT    | FK          |
| EchelonId        | TEXT    | FK          |
| IsActive         | INTEGER | NOT NULL    |
| DateCreation     | TEXT    | NOT NULL    |
| DateModification | TEXT    | NULL        |

---

# 7. Exemple : table `Bulletins`

| Colonne        | Type    | Contraintes |
| -------------- | ------- | ----------- |
| Id             | TEXT    | PK          |
| AgentId        | TEXT    | FK          |
| PeriodeId      | TEXT    | FK          |
| Statut         | INTEGER | NOT NULL    |
| TotalBrut      | NUMERIC | NOT NULL    |
| TotalRetenues  | NUMERIC | NOT NULL    |
| TotalNet       | NUMERIC | NOT NULL    |
| DateCalcul     | TEXT    | NOT NULL    |
| DateValidation | TEXT    | NULL        |

Contrainte d'unicité :

```text
UNIQUE (AgentId, PeriodeId)
```

Un agent ne peut posséder qu'un seul bulletin par période.

---

# 8. Table `BulletinLignes`

Cette table matérialise les rubriques calculées.

Colonnes principales :

* Id
* BulletinId
* RubriqueId
* OrdreCalcul
* Base
* Taux
* Quantite
* Montant
* MontantImposable
* MontantCotisable
* Libelle

Les lignes sont ordonnées par `OrdreCalcul` afin de reproduire fidèlement le pipeline du moteur de paie.

---

# 9. Intégrité référentielle

Les contraintes de clés étrangères sont activées (`PRAGMA foreign_keys = ON`).

Toute suppression ou modification doit respecter les règles métier.

Exemples :

* suppression d'un agent interdite s'il possède des bulletins ;
* suppression d'un grade interdite s'il est référencé ;
* suppression d'une période clôturée interdite.

---

# 10. Index

Les index sont définis en fonction des usages métiers.

Exemples :

| Nom                          | Colonnes   |
| ---------------------------- | ---------- |
| IX_Agents_Matricule          | Matricule  |
| IX_Bulletins_AgentId         | AgentId    |
| IX_Bulletins_PeriodeId       | PeriodeId  |
| IX_BulletinLignes_BulletinId | BulletinId |
| IX_Rubriques_Code            | Code       |

Toute création d'index doit être justifiée par un scénario d'utilisation.

---

# 11. Contraintes d'unicité

Les contraintes d'unicité concernent notamment :

* matricule d'agent ;
* code rubrique ;
* code grade ;
* code corps ;
* période (année + mois) ;
* code établissement.

---

# 12. Données de référence

Les référentiels (grades, corps, catégories, rubriques réglementaires, etc.) sont livrés via des jeux de données versionnés (*seed data*).

Chaque jeu est associé à une version réglementaire et chargé lors de l'installation ou des migrations.

---

# 13. Mapping Domaine ↔ SQLite

Le mapping est explicite et documenté.

| Entité Domaine | Table SQLite   |
| -------------- | -------------- |
| Agent          | Agents         |
| Contrat        | Contrats       |
| Carriere       | Carrieres      |
| Bulletin       | Bulletins      |
| LigneBulletin  | BulletinLignes |
| Rubrique       | Rubriques      |
| Grade          | Grades         |
| Corps          | Corps          |
| Echelon        | Echelons       |
| Periode        | Periodes       |

Aucune entité ne doit être mappée implicitement.

---

# 14. Vues SQL

Les vues sont réservées aux besoins de consultation.

Exemples :

* `vw_BulletinsResume`
* `vw_AgentsActifs`
* `vw_RubriquesApplicables`

Elles ne remplacent jamais les règles métier du domaine.

---

# 15. Déclencheurs (Triggers)

L'utilisation de triggers est limitée aux besoins techniques, par exemple :

* mise à jour automatique de `DateModification` ;
* journalisation technique ;
* contrôle de cohérence simple.

Les calculs de paie et les règles métier restent exclusivement dans le domaine applicatif.

---

# 16. Évolution du schéma

Toute évolution suit ce processus :

1. rédaction d'une ADR ;
2. mise à jour du modèle conceptuel ;
3. création d'une migration ;
4. tests de migration ;
5. mise à jour de cette documentation.

Aucune modification directe en production n'est autorisée.

---

# 17. Performances

Objectifs :

| Opération                        | Temps cible |
| -------------------------------- | ----------: |
| Recherche par matricule          |      < 5 ms |
| Chargement d'un bulletin         |     < 20 ms |
| Lecture des lignes d'un bulletin |     < 10 ms |
| Chargement des référentiels      |    < 100 ms |

---

# 18. Critères d'acceptation

Le schéma physique est validé lorsque :

* toutes les entités du domaine disposent d'un mapping documenté ;
* les clés étrangères sont cohérentes ;
* les index couvrent les principaux cas d'usage ;
* les contraintes d'intégrité sont respectées ;
* les migrations permettent de reconstruire la base à partir de zéro.

---

# 19. ADR (Architecture Decision Records)

| ADR     | Décision                                                               |
| ------- | ---------------------------------------------------------------------- |
| ADR-061 | Repositories spécialisés par agrégat, abandon du Repository générique  |
| ADR-062 | GUID comme clé primaire pour toutes les tables                         |
| ADR-063 | Stockage des dates au format ISO 8601                                  |
| ADR-064 | Référentiels livrés sous forme de jeux de données versionnés           |
| ADR-065 | Les règles métier ne sont jamais implémentées dans des triggers SQLite |

---

# Conclusion

Le **Volume 12** établit le **schéma physique de référence** de la base SQLite. Il définit les conventions, les structures de tables, les contraintes, les index et les règles d'évolution qui serviront de fondation à toute la couche de persistance.

## Recommandation pour le Volume 13

Le prochain volume devrait être consacré à la **stratégie de mapping et d'accès aux données**. Il décrira notamment :

* l'utilisation de **Dapper** comme micro-ORM ;
* les conventions de mapping entre les entités de domaine et les objets persistés ;
* les repositories spécialisés (`IAgentRepository`, `IBulletinRepository`, etc.) ;
* les requêtes optimisées ;
* les commandes (*Commands*) et projections (*Queries*) ;
* la séparation lecture/écriture (CQRS léger, sans complexité inutile) ;
* la gestion des transactions et des performances.

Ce volume fera le lien entre le modèle de domaine et l'implémentation concrète de la couche `Persistence`.

Je recommande de faire évoluer légèrement l'architecture à partir de ce volume.

Dans les ERP modernes développés avec **.NET 10**, **DDD**, **WPF** et **SQLite**, il est préférable de **ne pas mapper directement les entités du domaine avec Dapper**. Cette pratique finit souvent par coupler le domaine à la persistance et rend les évolutions plus coûteuses.

Je recommande donc d'introduire une couche supplémentaire de **Persistence Models (Data Models)** :

```text
Domain Entity
        │
        ▼
Mapper
        │
        ▼
Persistence Model
        │
        ▼
Dapper
        │
        ▼
SQLite
```

Cette séparation est utilisée dans de nombreuses architectures professionnelles, car elle permet :

* de préserver un domaine totalement pur ;
* de faciliter les migrations du schéma ;
* de simplifier les optimisations SQL ;
* de limiter l'impact des évolutions de la base sur le modèle métier.

Cette décision sera formalisée par **ADR-066**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome D — Persistance et Infrastructure**

# **Volume 13**

# **Architecture d'Accès aux Données – Dapper, Persistence Models, Mapping et Repositories**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP**)
**Technologies :** .NET 10 LTS • SQLite • Dapper • Clean Architecture • DDD

---

# 1. Objet

Ce volume définit la stratégie d'accès aux données de **PaieEducation ERP**.

Il précise :

* les responsabilités de Dapper ;
* les modèles de persistance ;
* les mappers ;
* les repositories spécialisés ;
* les stratégies de lecture et d'écriture ;
* les règles de performance.

L'objectif est d'assurer une séparation stricte entre le **domaine métier** et la **base SQLite**.

---

# 2. Architecture générale

```text
                 Domain

      Agent      Bulletin

           ▲
           │

     Domain Repository

           ▲
           │

────────────────────────────────────

     Persistence Layer

────────────────────────────────────

Repositories

Persistence Models

Entity Mappers

Dapper

SQLite
```

Le domaine ne dépend jamais de Dapper.

---

# 3. Flux d'une lecture

```text
SQLite

↓

Dapper

↓

AgentDataModel

↓

AgentMapper

↓

Agent (Domain)

↓

Application
```

Le modèle de domaine est créé uniquement par le mapper.

---

# 4. Flux d'une écriture

```text
Agent

↓

Mapper

↓

AgentDataModel

↓

Dapper

↓

SQLite
```

Le domaine ne génère jamais directement du SQL.

---

# 5. Persistence Models

Chaque table possède un modèle dédié.

Exemples :

```text
AgentDataModel

BulletinDataModel

BulletinLineDataModel

GradeDataModel

ContractDataModel

RubriqueDataModel
```

Ces modèles reflètent la structure physique de SQLite.

---

# 6. Domain Mappers

Les mappers assurent la conversion bidirectionnelle.

Exemples :

```text
AgentMapper

BulletinMapper

GradeMapper

ContractMapper

RubriqueMapper
```

Chaque mapper expose deux opérations :

* Domain → Persistence ;
* Persistence → Domain.

Ils ne contiennent aucune logique métier.

---

# 7. Repositories spécialisés

Chaque agrégat possède son propre repository.

Exemples :

```text
IAgentRepository

IBulletinRepository

IPeriodeRepository

IContratRepository

IRubriqueRepository

IGradeRepository
```

Les implémentations sont situées dans `PaieEducation.Persistence`.

---

# 8. Exemples de méthodes métier

Les méthodes sont orientées métier plutôt que techniques.

Exemple pour `IAgentRepository` :

```csharp
Task<Agent?> GetByIdAsync(Guid id);

Task<Agent?> GetByMatriculeAsync(string matricule);

Task<IReadOnlyList<Agent>> GetActiveAgentsAsync();

Task SaveAsync(Agent agent);

Task ArchiveAsync(Guid id);
```

Les signatures reflètent les besoins fonctionnels.

---

# 9. Dapper

Dapper est utilisé pour :

* l'exécution des requêtes SQL ;
* le mapping vers les modèles de persistance ;
* les opérations transactionnelles.

Il n'a pas connaissance du domaine.

---

# 10. Requêtes SQL

Les requêtes SQL sont centralisées.

Organisation proposée :

```text
Queries
├── AgentQueries.cs
├── BulletinQueries.cs
├── GradeQueries.cs
├── RubriqueQueries.cs

Commands
├── AgentCommands.cs
├── BulletinCommands.cs
├── GradeCommands.cs
```

Cette séparation facilite la maintenance.

---

# 11. Paramétrage des requêtes

Toutes les requêtes utilisent des paramètres.

Exemple :

```sql
SELECT *
FROM Agents
WHERE Matricule = @Matricule;
```

Les concaténations de chaînes SQL sont interdites.

---

# 12. Gestion des transactions

Les repositories ne créent pas leurs propres transactions.

Ils utilisent toujours l'`IUnitOfWork` active.

Ainsi, plusieurs opérations peuvent être validées de manière atomique.

---

# 13. Lecture optimisée

Les requêtes de consultation privilégient :

* les projections ciblées ;
* les sélections explicites de colonnes ;
* les index définis au Volume 12.

Le `SELECT *` est interdit dans le code de production, sauf justification documentée.

---

# 14. Écriture optimisée

Les mises à jour portent uniquement sur les colonnes modifiées lorsque cela apporte un bénéfice mesurable.

Les insertions et suppressions sont réalisées dans des transactions courtes.

---

# 15. Gestion des erreurs

Les exceptions techniques (SQLite, Dapper, connexion) sont interceptées dans la couche `Persistence`.

Elles sont converties en exceptions applicatives compréhensibles pour les couches supérieures.

---

# 16. Lecture seule

Les requêtes de consultation ne modifient jamais l'état du domaine.

Pour les besoins de listes ou de tableaux WPF, des **DTO de lecture** peuvent être utilisés afin d'éviter de charger des agrégats complets.

---

# 17. Performances

Objectifs :

| Opération                   | Temps cible |
| --------------------------- | ----------: |
| Chargement d'un agent       |     < 10 ms |
| Chargement d'un bulletin    |     < 20 ms |
| Recherche par matricule     |      < 5 ms |
| Chargement des référentiels |    < 100 ms |

Des mesures régulières sont réalisées afin de vérifier le respect de ces objectifs.

---

# 18. Tests

Les repositories sont validés par :

* tests unitaires des mappers ;
* tests d'intégration avec SQLite ;
* tests de transactions ;
* tests de performances.

Les mappers sont testés dans les deux sens (domaine ↔ persistance).

---

# 19. Sécurité

La couche d'accès aux données applique systématiquement :

* requêtes paramétrées ;
* validation des entrées ;
* contrôle des transactions ;
* journalisation des erreurs techniques.

---

# 20. Critères d'acceptation

La couche d'accès aux données est validée lorsque :

* le domaine ne dépend ni de Dapper ni de SQLite ;
* chaque agrégat dispose d'un repository spécialisé ;
* les modèles de persistance sont distincts des entités du domaine ;
* les mappers couvrent toutes les conversions ;
* les performances répondent aux objectifs définis.

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                                |
| ------- | ----------------------------------------------------------------------- |
| ADR-066 | Introduction de **Persistence Models** distincts des entités de domaine |
| ADR-067 | Mapping bidirectionnel via des mappers dédiés                           |
| ADR-068 | Repositories spécialisés par agrégat uniquement                         |
| ADR-069 | Dapper limité au rôle de micro-ORM                                      |
| ADR-070 | DTO de lecture autorisés pour les écrans WPF à forte volumétrie         |

---

# Conclusion

Le **Volume 13** formalise une architecture de persistance découplée, où les **entités de domaine**, les **modèles de persistance** et les **repositories** ont des responsabilités clairement séparées. Cette approche favorise la maintenabilité, les performances et l'évolutivité de **PaieEducation ERP**.

## Recommandation pour le Volume 14

Le prochain volume devrait être consacré à **l'architecture WPF et MVVM**, en détaillant :

* l'organisation des projets `Presentation` ;
* la structure des `Views`, `ViewModels` et `Services` ;
* l'utilisation de `CommunityToolkit.Mvvm` ;
* la navigation ;
* l'injection de dépendances ;
* les dialogues, notifications et validations ;
* les bonnes pratiques de performance (virtualisation, chargement asynchrone, etc.).

Ce volume constituera la référence pour l'implémentation de l'interface utilisateur moderne de **PaieEducation ERP**.

