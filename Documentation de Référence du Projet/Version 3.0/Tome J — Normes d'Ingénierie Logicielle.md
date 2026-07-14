# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome J — Normes d'Ingénierie Logicielle**

# **Volume 25**

# **Normes de Développement, Standards C#/.NET 10 et Qualité Logicielle**

**Version :** 3.0
**Statut :** Référentiel officiel de développement

---

# 1. Objet

Ce volume définit les règles de développement applicables à l'ensemble du projet **PaieEducation ERP**.

Ses objectifs sont :

* garantir une architecture homogène ;
* assurer la maintenabilité du code ;
* limiter la dette technique ;
* faciliter les revues de code ;
* améliorer la testabilité ;
* préparer les évolutions futures.

Ces règles s'appliquent à tous les modules de l'application.

---

# 2. Stack technologique de référence

| Composant                | Technologie retenue                      |
| ------------------------ | ---------------------------------------- |
| Runtime                  | .NET 10 LTS                              |
| Langage                  | C# (version correspondant à .NET 10)     |
| Interface                | WPF                                      |
| Pattern                  | MVVM                                     |
| Toolkit MVVM             | CommunityToolkit.Mvvm                    |
| Injection de dépendances | Microsoft.Extensions.DependencyInjection |
| Journalisation           | Microsoft.Extensions.Logging             |
| Base de données          | SQLite                                   |
| Accès aux données        | Microsoft.Data.Sqlite                    |
| Rapports PDF             | QuestPDF                                 |
| Export Excel             | ClosedXML                                |

Toute nouvelle technologie doit faire l'objet d'une validation architecturale.

---

# 3. Principes fondamentaux

Le code doit respecter :

* **SOLID** ;
* **DRY** (*Don't Repeat Yourself*) ;
* **KISS** (*Keep It Simple, Stupid*) ;
* **YAGNI** (*You Aren't Gonna Need It*) ;
* séparation des responsabilités ;
* dépendances orientées vers les abstractions.

---

# 4. Architecture modulaire

La solution est organisée en projets indépendants :

```text
PaieEducation.Presentation
PaieEducation.Application
PaieEducation.Domain
PaieEducation.Infrastructure
PaieEducation.Persistence
PaieEducation.Reporting
PaieEducation.Shared
```

Les références entre projets suivent strictement les dépendances définies par la Clean Architecture.

---

# 5. Conventions de nommage

| Élément         | Convention   | Exemple              |
| --------------- | ------------ | -------------------- |
| Classe          | PascalCase   | `PayrollEngine`      |
| Interface       | Préfixe `I`  | `IAgentRepository`   |
| Méthode         | PascalCase   | `CalculatePayroll()` |
| Propriété       | PascalCase   | `GrossSalary`        |
| Champ privé     | `_camelCase` | `_repository`        |
| Variable locale | camelCase    | `employee`           |
| Constante       | PascalCase   | `DefaultPageSize`    |
| Enumération     | PascalCase   | `PayrollStatus`      |

Les abréviations sont évitées sauf lorsqu'elles sont universellement reconnues.

---

# 6. Organisation des fichiers

Une classe publique par fichier.

Le nom du fichier correspond au nom de la classe.

Exemple :

```text
PayrollEngine.cs
```

Les fichiers volumineux (> 500 lignes) doivent être réévalués et, si nécessaire, découpés.

---

# 7. Style de codage

* Accolades obligatoires, même pour un bloc d'une ligne.
* Indentation de 4 espaces.
* Utilisation des expressions modernes de C# lorsque cela améliore la lisibilité.
* Éviter les régions (`#region`) pour masquer une mauvaise organisation du code.
* Préférer les méthodes courtes et explicites.

---

# 8. Gestion des dépendances

Les dépendances sont injectées via le conteneur DI.

Les instanciations directes (`new`) sont limitées aux objets sans dépendances (par exemple certains objets valeur).

Les services, repositories et composants techniques sont toujours résolus par injection.

---

# 9. Gestion des exceptions

Les exceptions sont classées en deux catégories :

| Catégorie | Traitement                                                      |
| --------- | --------------------------------------------------------------- |
| Métier    | Propagées sous forme d'exceptions fonctionnelles explicites     |
| Technique | Journalisées puis encapsulées avant de remonter à l'application |

Les exceptions ne doivent jamais être utilisées pour piloter un flux normal.

---

# 10. Asynchronisme

Les opérations longues utilisent `async` / `await`.

Exemples :

* chargement des listes ;
* calculs de masse ;
* génération des rapports ;
* exports Excel ;
* sauvegardes.

Les opérations courtes et purement CPU peuvent rester synchrones si cela simplifie le code.

---

# 11. Gestion des ressources

Toutes les ressources implémentant `IDisposable` sont correctement libérées.

Les flux, connexions SQLite et documents PDF sont utilisés dans des blocs de gestion de durée de vie appropriés.

---

# 12. Journalisation

La journalisation suit une structure cohérente :

* contexte de l'opération ;
* identifiant de l'entité concernée ;
* niveau de gravité ;
* message explicite ;
* exception éventuelle.

Les informations sensibles ne sont jamais inscrites dans les journaux.

---

# 13. Tests

Les tests sont organisés par couche.

| Couche         | Type de tests                    |
| -------------- | -------------------------------- |
| Domain         | Tests unitaires                  |
| Application    | Tests unitaires et d'intégration |
| Infrastructure | Tests d'intégration              |
| Presentation   | Tests fonctionnels ciblés        |

Les règles métier critiques doivent être systématiquement couvertes.

---

# 14. Documentation du code

Le code public est documenté avec les commentaires XML (`///`).

Les commentaires expliquent le **pourquoi**, jamais le **commentaire évident**.

Les décisions d'architecture importantes sont documentées dans des fichiers dédiés (ADR – *Architecture Decision Records*).

---

# 15. Gestion des branches

Une stratégie Git simple est recommandée :

* `main` : version stable ;
* `develop` : intégration continue des développements ;
* branches de fonctionnalité : `feature/nom-fonctionnalite` ;
* branches de correction : `fix/nom-correctif`.

Les fusions vers `main` doivent être validées par revue.

---

# 16. Revue de code

Chaque revue vérifie notamment :

* respect de l'architecture ;
* absence de duplication ;
* lisibilité ;
* couverture de tests ;
* conformité aux conventions ;
* gestion correcte des erreurs.

Les revues constituent un levier de partage des connaissances.

---

# 17. Performance

Des objectifs mesurables sont définis :

| Indicateur                   | Cible |
| ---------------------------- | ----: |
| Temps de démarrage           | < 3 s |
| Calcul d'un bulletin         | < 3 s |
| Chargement de 500 agents     | < 1 s |
| Génération d'un bulletin PDF | < 1 s |

Les optimisations doivent être guidées par des mesures, et non par des suppositions.

---

# 18. Sécurité

Les pratiques suivantes sont obligatoires :

* validation des entrées utilisateur ;
* requêtes SQL paramétrées ;
* contrôle des chemins de fichiers ;
* limitation des privilèges ;
* chiffrement des sauvegardes si elles contiennent des données sensibles.

La sécurité est intégrée dès la conception.

---

# 19. Évolution de la plateforme

Le projet adopte une stratégie de stabilité :

* mise à niveau uniquement vers les versions **LTS** de .NET ;
* migration préparée par une analyse d'impact ;
* compatibilité vérifiée avec les bibliothèques tierces.

L'objectif est de limiter les risques liés aux évolutions technologiques.

---

# 20. Critères d'acceptation

Le code est conforme lorsque :

* les conventions de nommage sont respectées ;
* les dépendances sont injectées ;
* les règles SOLID sont appliquées ;
* les tests couvrent les fonctionnalités critiques ;
* la documentation est maintenue à jour ;
* les revues de code sont réalisées.

---

# 21. Référentiels complémentaires

Ce volume s'appuie sur :

* les volumes 20 à 24 pour l'architecture ;
* les décisions d'architecture (ADR) ;
* les guides de style internes ;
* les conventions Git du projet.

Toute évolution des normes doit être approuvée et documentée.

---

# Conclusion

Le **Volume 25** constitue le **manuel de développement** de **PaieEducation ERP**. En imposant des standards communs, il favorise une base de code cohérente, robuste et durable, adaptée à un ERP de grande longévité.

## Proposition pour la suite

À partir de ce stade, le projet dispose déjà d'une documentation d'architecture très complète. Pour atteindre un niveau comparable aux ERP industriels, je recommande de poursuivre avec une nouvelle série de volumes spécialisés :

* **Volume 26** : Architecture de la couche **Persistence** (SQLite, repositories, migrations, optimisation des requêtes, indexation).
* **Volume 27** : Architecture du **Reporting** (QuestPDF, modèles de documents, génération des bulletins, attestations et états).
* **Volume 28** : **Sécurité, audit et traçabilité** (journal d'audit, historisation des opérations, intégrité des données).
* **Volume 29** : **Déploiement, installation et maintenance** (installation locale, sauvegardes, restauration, mises à jour).
* **Volume 30** : **Guide d'exploitation utilisateur** (administration, procédures opérationnelles, support et bonnes pratiques).

Cette seconde série permettra de constituer une **documentation de niveau ERP**, couvrant non seulement le développement, mais également l'exploitation et la maintenance de **PaieEducation ERP** sur le long terme.


