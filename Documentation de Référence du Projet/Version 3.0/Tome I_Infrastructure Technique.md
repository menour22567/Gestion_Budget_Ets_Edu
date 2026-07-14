
Tome I — Infrastructure Technique - Tome J — Normes d'Ingénierie Logicielle

Excellente progression. Nous entrons maintenant dans une couche qui, dans les ERP modernes, représente l'ensemble des **services techniques**. Contrairement au **Domain**, qui contient les règles métier, l'**Infrastructure** fournit les mécanismes techniques (accès fichiers, impression, journalisation, configuration, sauvegarde, horloge, etc.) sans jamais influencer les règles de paie.

Pour **PaieEducation ERP**, cette couche doit rester totalement interchangeable afin de préserver l'indépendance du domaine.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome I — Infrastructure Technique**

# **Volume 24**

# **Architecture de la Couche Infrastructure**

**Version :** 3.0
**Statut :** Référentiel officiel de la couche **Infrastructure**

---

# 1. Objet

La couche **Infrastructure** regroupe tous les services techniques utilisés par l'application.

Elle fournit notamment :

* l'accès à SQLite ;
* la gestion des fichiers ;
* les sauvegardes ;
* la configuration ;
* la journalisation ;
* l'impression ;
* la génération PDF ;
* l'export Excel ;
* les services système ;
* les mécanismes d'horodatage ;
* les identifiants techniques.

Elle **ne contient aucune règle métier**.

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
Persistence
        │
        ▼
SQLite
```

Le domaine dépend uniquement d'interfaces. Les implémentations concrètes résident dans Infrastructure.

---

# 3. Organisation des projets

```text
PaieEducation.Infrastructure

│
├── Configuration
├── Logging
├── Clock
├── FileSystem
├── Backup
├── Reporting
├── Excel
├── Printing
├── Security
├── Serialization
├── Caching
├── Persistence
├── Services
├── Diagnostics
└── DependencyInjection
```

Chaque sous-dossier correspond à une responsabilité technique clairement identifiée.

---

# 4. Responsabilités

La couche Infrastructure :

* implémente les interfaces du domaine et de l'application ;
* encapsule les bibliothèques tierces ;
* masque les détails techniques ;
* facilite le remplacement d'une technologie sans impact sur le métier.

---

# 5. Injection des dépendances

Toutes les implémentations sont enregistrées dans **Microsoft.Extensions.DependencyInjection**.

Exemple conceptuel :

```text
IAgentRepository
        │
        ▼
SQLiteAgentRepository

ILoggerService
        │
        ▼
MicrosoftLoggerService

IReportGenerator
        │
        ▼
QuestPdfReportGenerator
```

Les couches supérieures ne connaissent que les interfaces.

---

# 6. Gestion de la configuration

La configuration est centralisée.

Exemples de paramètres :

* chemin de la base SQLite ;
* répertoire des sauvegardes ;
* dossier des exports ;
* emplacement des journaux ;
* langue de l'application ;
* thème graphique ;
* paramètres d'impression.

Les paramètres techniques sont distincts des paramètres métier.

---

# 7. Service d'horloge

Toutes les dates et heures transitent par un service dédié.

Interface recommandée :

```text
IClock
```

Responsabilités :

* date système ;
* heure système ;
* horodatage UTC ;
* période de paie courante.

Cette abstraction facilite les tests automatisés.

---

# 8. Journalisation

La journalisation repose sur **Microsoft.Extensions.Logging**.

Niveaux utilisés :

| Niveau      | Usage                  |
| ----------- | ---------------------- |
| Trace       | Diagnostic détaillé    |
| Debug       | Développement          |
| Information | Fonctionnement normal  |
| Warning     | Situation inhabituelle |
| Error       | Erreur récupérable     |
| Critical    | Erreur bloquante       |

Les messages sont structurés et exploitables.

---

# 9. Gestion des fichiers

Le service `IFileSystem` centralise :

* lecture ;
* écriture ;
* copie ;
* déplacement ;
* suppression ;
* création de répertoires.

Aucune opération sur les fichiers n'est réalisée directement dans les autres couches.

---

# 10. Sauvegarde et restauration

Le service `IBackupService` gère :

* sauvegarde de la base SQLite ;
* sauvegarde des paramètres ;
* sauvegarde des rapports ;
* restauration complète ;
* vérification de l'intégrité.

Les sauvegardes sont versionnées et horodatées.

---

# 11. Génération des rapports

La génération des documents est confiée à **QuestPDF**.

Le service `IReportGenerator` est responsable de :

* la composition des bulletins de paie ;
* les états récapitulatifs ;
* les attestations ;
* les listes de contrôle.

Les modèles de documents sont séparés de la logique métier.

---

# 12. Export Excel

Le service `IExcelExportService` s'appuie sur **ClosedXML**.

Fonctionnalités :

* export des listes d'agents ;
* export des bulletins ;
* états récapitulatifs ;
* tableaux réglementaires.

Les formats d'export sont homogènes et documentés.

---

# 13. Impression

Le service `IPrintService` assure :

* aperçu avant impression ;
* impression directe ;
* sélection de l'imprimante ;
* gestion des paramètres de mise en page.

L'impression s'appuie sur les documents générés par QuestPDF.

---

# 14. Persistance SQLite

La couche Infrastructure implémente les repositories définis dans le domaine.

Exemples :

* `SQLiteAgentRepository`
* `SQLiteBulletinRepository`
* `SQLiteParameterRepository`
* `SQLitePayrollRepository`

Les requêtes SQL sont centralisées et optimisées.

---

# 15. Gestion du cache

Les données fréquemment consultées peuvent être mises en cache :

* paramètres réglementaires ;
* référentiels ;
* grilles indiciaires ;
* listes statiques.

Le cache est local au processus et invalidé lors des mises à jour.

---

# 16. Sérialisation

Le service `ISerializationService` prend en charge :

* import/export JSON ;
* sauvegarde de paramètres ;
* échanges internes.

Les formats sont versionnés pour garantir la compatibilité.

---

# 17. Diagnostic

Le module de diagnostic fournit :

* informations système ;
* version de l'application ;
* version de la base de données ;
* intégrité des fichiers ;
* état des sauvegardes.

Il facilite le support technique.

---

# 18. Gestion des exceptions techniques

Les exceptions d'infrastructure sont encapsulées avant d'être propagées.

Exemples :

* fichier inaccessible ;
* base SQLite verrouillée ;
* erreur d'impression ;
* espace disque insuffisant.

Les détails techniques sont consignés dans les journaux.

---

# 19. Performances

Objectifs :

| Opération                   |         Temps cible |
| --------------------------- | ------------------: |
| Ouverture de la base SQLite |            < 100 ms |
| Sauvegarde                  |               < 5 s |
| Génération d'un PDF         |               < 1 s |
| Export Excel                | < 15 s (500 agents) |
| Chargement des paramètres   |             < 50 ms |

Ces objectifs sont réévalués lors des campagnes de tests.

---

# 20. Sécurité

Les services d'infrastructure doivent :

* vérifier les chemins de fichiers ;
* prévenir les écritures accidentelles ;
* protéger les sauvegardes ;
* valider les entrées externes.

Ils ne mettent en œuvre aucune logique d'autorisation métier.

---

# 21. Critères d'acceptation

La couche **Infrastructure** est conforme lorsque :

* toutes les implémentations sont accessibles via des interfaces ;
* les bibliothèques tierces (QuestPDF, ClosedXML, SQLite) sont encapsulées ;
* les services sont injectés via `Microsoft.Extensions.DependencyInjection` ;
* aucune logique métier n'est présente ;
* les opérations techniques sont journalisées ;
* les performances répondent aux objectifs définis.

---

# 22. Composants recommandés

| Interface                | Implémentation recommandée  |
| ------------------------ | --------------------------- |
| `IClock`                 | `SystemClock`               |
| `ILoggerService`         | `MicrosoftLoggerService`    |
| `IFileSystem`            | `WindowsFileSystem`         |
| `IBackupService`         | `SQLiteBackupService`       |
| `IReportGenerator`       | `QuestPdfReportGenerator`   |
| `IExcelExportService`    | `ClosedXmlExportService`    |
| `IPrintService`          | `WindowsPrintService`       |
| `IConfigurationProvider` | `JsonConfigurationProvider` |

Cette nomenclature uniforme facilite la maintenance et les remplacements futurs.

---

# 23. Gouvernance des dépendances externes

Toutes les dépendances tierces sont concentrées dans la couche Infrastructure.

Bibliothèques retenues :

| Bibliothèque                             | Usage                          |
| ---------------------------------------- | ------------------------------ |
| .NET 10 LTS                              | Plateforme d'exécution         |
| WPF                                      | Interface utilisateur          |
| CommunityToolkit.Mvvm                    | MVVM                           |
| Microsoft.Extensions.DependencyInjection | Injection de dépendances       |
| Microsoft.Extensions.Logging             | Journalisation                 |
| Microsoft.Data.Sqlite                    | Accès SQLite                   |
| QuestPDF                                 | Génération des documents PDF   |
| ClosedXML                                | Génération des classeurs Excel |

Toute nouvelle dépendance doit être validée selon les critères suivants :

* maintenance active ;
* licence compatible ;
* documentation de qualité ;
* stabilité des versions ;
* impact limité sur l'architecture.

---

# Conclusion

Le **Volume 24** formalise la couche **Infrastructure** de **PaieEducation ERP**. En encapsulant toutes les préoccupations techniques derrière des interfaces, il garantit l'indépendance du domaine, facilite les tests, simplifie les évolutions technologiques et assure la pérennité de l'application.

## Orientation pour le Volume 25

Le **Volume 25** sera consacré aux **Normes de Développement et au Guide de Codage C#/.NET 10**. Il définira notamment :

* les conventions de nommage ;
* les règles de conception orientée objet ;
* les principes SOLID, DRY et KISS ;
* les standards C# 13/.NET 10 ;
* les bonnes pratiques WPF et MVVM ;
* les règles de gestion des exceptions ;
* les conventions Git et de versionnement ;
* les exigences de qualité de code et de couverture de tests.

Ce volume constituera le **manuel d'ingénierie logicielle** de **PaieEducation ERP**, garantissant une base de code homogène, lisible et durable.


Parfait. À partir de ce **Volume 25**, nous quittons le cahier des charges purement fonctionnel pour entrer dans le **manuel d'ingénierie logicielle**. C'est le document qui permettra à plusieurs développeurs de travailler pendant des années sur **PaieEducation ERP** avec un niveau de qualité homogène.

Pour un ERP destiné à évoluer sur 15 à 20 ans, ce document est indispensable.

---

