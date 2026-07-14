Excellente progression. Nous arrivons maintenant au document qui décrit **le cœur du moteur de paie**. C'est probablement le volume le plus important de toute la documentation, car il formalise le **catalogue des rubriques**, leurs dépendances et leur exécution. Pour un ERP de paie, ce document joue le rôle de **spécification du moteur de calcul**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome H — Moteur de Paie et Calcul**

# **Volume 22**

# **Catalogue des Rubriques de Paie et Architecture du Moteur de Calcul**

**Version :** 3.0
**Statut :** Référentiel officiel du **Payroll Engine**

---

# 1. Objet

Ce volume définit :

* le modèle des rubriques de paie ;
* le moteur de calcul ;
* les dépendances entre rubriques ;
* les règles d'éligibilité ;
* les formules de calcul ;
* les priorités d'exécution ;
* la traçabilité des calculs ;
* l'historisation des paramètres.

L'objectif est de disposer d'un moteur **100 % paramétrable**, sans logique codée en dur (*hardcoding*), conformément aux principes établis dans les volumes précédents.

---

# 2. Principes du moteur de calcul

Le moteur repose sur les principes suivants :

* calcul déterministe ;
* idempotence (un même contexte produit toujours le même résultat) ;
* exécution reproductible ;
* traçabilité complète ;
* séparation entre règles métier et paramètres ;
* historisation des paramètres réglementaires.

---

# 3. Architecture générale

```text
Payroll Engine
        │
        ├── Context Builder
        ├── Eligibility Engine
        ├── Formula Engine
        ├── Dependency Resolver
        ├── Calculation Engine
        ├── Validation Engine
        ├── Trace Engine
        └── Bulletin Aggregate
```

Chaque composant possède une responsabilité unique.

---

# 4. Contexte de calcul

Avant tout calcul, le moteur construit un **contexte de paie** comprenant :

* période de paie ;
* agent ;
* établissement ;
* contrat ;
* affectation ;
* carrière ;
* corps ;
* grade ;
* catégorie ;
* ancienneté ;
* régime de travail ;
* paramètres réglementaires applicables.

Le contexte est immuable pendant toute l'exécution.

---

# 5. Structure d'une rubrique

Chaque rubrique est définie par un ensemble de métadonnées.

| Attribut     | Description                                |
| ------------ | ------------------------------------------ |
| CodeRubrique | Identifiant métier unique                  |
| Libellé      | Désignation                                |
| Nature       | Gain, retenue, cotisation, régularisation  |
| Famille      | Classification fonctionnelle               |
| OrdreCalcul  | Priorité d'exécution                       |
| TypeCalcul   | Montant fixe, pourcentage, formule, barème |
| Formule      | Expression paramétrable                    |
| Arrondi      | Règle d'arrondi                            |
| DateEffet    | Début de validité                          |
| DateFin      | Fin de validité                            |
| Actif        | Oui / Non                                  |

Aucun identifiant technique n'est utilisé dans les règles métier.

---

# 6. Catégories de rubriques

Le moteur distingue les familles suivantes :

| Famille                | Exemples                            |
| ---------------------- | ----------------------------------- |
| Salaire de base        | Traitement indiciaire               |
| Indemnités             | IEP, primes, indemnités spécifiques |
| Heures supplémentaires | Paiement des heures                 |
| Avantages              | Nature ou espèces                   |
| Retenues               | Absences, avances                   |
| Cotisations            | Sécurité sociale, retraite          |
| Fiscalité              | IRG                                 |
| Régularisations        | Rappels, corrections                |
| Totaux                 | Brut, imposable, net                |

Chaque famille suit des règles d'exécution spécifiques.

---

# 7. Moteur d'éligibilité

Avant de calculer une rubrique, le moteur vérifie son éligibilité.

Critères possibles :

* type de contrat ;
* type d'employé ;
* corps ;
* grade ;
* catégorie ;
* fonction exercée ;
* établissement ;
* niveau d'enseignement ;
* ancienneté ;
* période d'effet.

Les critères sont stockés dans la base SQLite et évalués par un service dédié.

---

# 8. Résolution des dépendances

Certaines rubriques dépendent des résultats d'autres rubriques.

Exemple :

```text
Traitement indiciaire
        │
        ▼
Indemnités
        │
        ▼
Salaire brut
        │
        ▼
Cotisations
        │
        ▼
IRG
        │
        ▼
Net à payer
```

Le moteur construit un graphe de dépendances et vérifie l'absence de cycles avant l'exécution.

---

# 9. Ordonnancement des calculs

Les rubriques sont exécutées selon leur `OrdreCalcul`.

Exemple :

| Ordre | Étape                  |
| ----: | ---------------------- |
|   100 | Salaire de base        |
|   200 | Indemnités             |
|   300 | Heures supplémentaires |
|   400 | Brut                   |
|   500 | Cotisations            |
|   600 | IRG                    |
|   700 | Retenues diverses      |
|   800 | Net à payer            |

Cette séquence est paramétrable.

---

# 10. Types de calcul

Le moteur prend en charge :

* montant fixe ;
* pourcentage ;
* barème ;
* formule paramétrable ;
* calcul conditionnel ;
* agrégation de rubriques ;
* calcul par tranche.

Chaque type est implémenté comme une stratégie indépendante.

---

# 11. Expressions de calcul

Les formules sont stockées sous forme d'expressions paramétrables.

Exemple conceptuel :

```text
TraitementBase × Taux × Quotité
```

Les références portent sur les **codes métier** des rubriques ou paramètres, jamais sur des identifiants techniques.

---

# 12. Historisation des paramètres

Chaque paramètre réglementaire possède :

* une date d'effet ;
* une date de fin de validité (optionnelle) ;
* une version ;
* une source réglementaire.

Le moteur sélectionne automatiquement la version applicable à la période calculée.

---

# 13. Gestion des arrondis

Les règles d'arrondi sont centralisées.

Exemples :

* arrondi au dinar ;
* arrondi au centime lorsque requis ;
* mode d'arrondi configurable.

Toutes les opérations monétaires utilisent l'objet valeur `Money` défini dans le Volume 20.

---

# 14. Journal d'exécution

Chaque calcul produit une trace détaillée.

Exemple :

| Étape               | Résultat |
| ------------------- | -------- |
| Rubrique exécutée   | Oui      |
| Formule appliquée   | Oui      |
| Paramètres utilisés | Oui      |
| Résultat obtenu     | Oui      |
| Durée               | Oui      |

Ces informations facilitent les audits et le diagnostic.

---

# 15. Gestion des erreurs

Les erreurs possibles comprennent :

* dépendance circulaire ;
* formule invalide ;
* paramètre manquant ;
* rubrique inactive ;
* critère d'éligibilité incohérent.

Le moteur interrompt le calcul et génère un rapport d'erreur exploitable.

---

# 16. Recalcul

Le moteur distingue :

* recalcul individuel ;
* recalcul collectif ;
* recalcul d'une rubrique ;
* recalcul d'une période.

Les recalculs respectent les mêmes règles que les calculs initiaux.

---

# 17. Performances

Objectifs :

| Opération                  | Temps cible |
| -------------------------- | ----------: |
| Calcul d'un bulletin       |       < 3 s |
| Calcul de 500 bulletins    |     < 2 min |
| Résolution des dépendances |    < 100 ms |
| Évaluation d'une règle     |      < 5 ms |

Ces objectifs seront vérifiés par les campagnes de performance décrites dans le Volume 17.

---

# 18. Extensibilité

L'ajout d'une nouvelle rubrique ne doit nécessiter :

* aucune modification du moteur ;
* aucune recompilation de la logique de calcul ;
* uniquement l'ajout ou la mise à jour des paramètres réglementaires et des formules.

Ce principe garantit la pérennité du système face aux évolutions réglementaires.

---

# 19. Gouvernance des rubriques

Chaque rubrique est soumise à un cycle de vie :

1. création ;
2. validation fonctionnelle ;
3. activation ;
4. utilisation ;
5. modification réglementaire ;
6. archivage.

Toutes les modifications sont historisées.

---

# 20. Critères d'acceptation

Le **Payroll Engine** est conforme lorsque :

* toutes les rubriques sont définies par un **code métier** unique ;
* les dépendances sont résolues automatiquement ;
* les formules sont paramétrables ;
* les paramètres sont historisés ;
* les calculs sont traçables et reproductibles ;
* les montants sont manipulés exclusivement via l'objet valeur `Money` (DZD) ;
* les tests réglementaires démontrent la conformité des résultats.



---

# Recommandations d'architecture

Pour renforcer encore la robustesse du moteur, je recommande d'introduire les composants suivants :

| Composant                     | Rôle                                                  |
| ----------------------------- | ----------------------------------------------------- |
| `CalculationGraph`            | Construction et validation du graphe des dépendances  |
| `FormulaParser`               | Analyse syntaxique des expressions de calcul          |
| `ExpressionEvaluator`         | Évaluation sécurisée des formules                     |
| `EligibilityContext`          | Centralisation des critères d'éligibilité             |
| `CalculationTrace`            | Journal détaillé des calculs                          |
| `RegulatoryParameterProvider` | Sélection des paramètres applicables selon la période |
| `CalculationPipeline`         | Orchestration séquentielle des étapes du moteur       |

Ces composants permettront d'obtenir un moteur de calcul **modulaire, testable, évolutif et conforme aux principes de Clean Architecture**, tout en restant adaptable aux évolutions de la réglementation sans modification du code métier.

## Orientation pour le Volume 23

Le **Volume 23** sera consacré au **Référentiel réglementaire et à l'historisation des paramètres**. Il décrira :

* le modèle de gestion des textes réglementaires ;
* la structure des barèmes, grilles indiciaires et taux ;
* l'historisation des valeurs avec dates d'effet ;
* la traçabilité des sources réglementaires ;
* le versionnement des paramètres ;
* les procédures de mise à jour réglementaire.

Ce volume complétera le **Payroll Engine** en fournissant un cadre rigoureux pour la gestion des évolutions légales, garantissant la conformité de **PaieEducation ERP** dans la durée.


Excellent. Nous abordons maintenant un volume qui distingue réellement un **logiciel de paie** d'un **ERP de paie**.

Dans la plupart des applications, les taux, grilles indiciaires et paramètres sont dispersés dans des tables. Dans un ERP moderne, ils constituent un **Référentiel Réglementaire (Regulatory Repository)**, entièrement historisé, versionné, traçable et auditable.

Pour **PaieEducation ERP**, je recommande d'élever ce référentiel au rang de **sous-système autonome**, indépendant du moteur de calcul.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome H — Moteur de Paie et Référentiel Réglementaire**

# **Volume 23**

# **Référentiel Réglementaire, Paramétrage Historisé et Gouvernance des Données Métier**

**Version :** 3.0
**Statut :** Référentiel officiel du sous-système réglementaire

---

# 1. Objet

Ce volume définit l'architecture et la gouvernance du **Référentiel Réglementaire**, qui centralise l'ensemble des paramètres métier utilisés par le moteur de paie.

Le référentiel contient notamment :

* les textes réglementaires ;
* les grilles indiciaires ;
* les barèmes fiscaux ;
* les taux de cotisation ;
* les indemnités ;
* les règles d'éligibilité ;
* les paramètres de calcul ;
* les constantes réglementaires.

Le moteur de paie ne contient aucune valeur réglementaire codée en dur.

---

# 2. Principes de conception

Le référentiel repose sur les principes suivants :

* centralisation ;
* historisation ;
* versionnement ;
* traçabilité ;
* auditabilité ;
* reproductibilité des calculs ;
* indépendance du moteur de calcul.

Chaque valeur réglementaire est identifiable, datée et justifiée.

---

# 3. Position dans l'architecture

```text
                 Domain
                    │
                    ▼
            Payroll Engine
                    │
                    ▼
      Regulatory Parameter Provider
                    │
                    ▼
      Regulatory Repository (SQLite)
```

Le **Regulatory Parameter Provider** constitue l'unique point d'accès aux paramètres réglementaires.

---

# 4. Catégories de paramètres

Le référentiel est structuré par familles.

| Famille             | Exemples                            |
| ------------------- | ----------------------------------- |
| Grilles indiciaires | Indices, échelons                   |
| Barèmes fiscaux     | IRG                                 |
| Cotisations         | Sécurité sociale, retraite          |
| Indemnités          | IEP, primes, indemnités spécifiques |
| Paramètres généraux | SMIG, plafond, constantes           |
| Éligibilité         | Critères d'attribution              |
| Arrondis            | Règles monétaires                   |
| Calendriers         | Périodes et dates de référence      |

Cette classification facilite les recherches et la maintenance.

---

# 5. Modèle générique d'un paramètre

Chaque paramètre comporte au minimum :

| Champ         | Description                     |
| ------------- | ------------------------------- |
| CodeParametre | Identifiant métier unique       |
| Libellé       | Désignation                     |
| Catégorie     | Famille réglementaire           |
| Valeur        | Donnée métier                   |
| Unité         | DZD, %, indice, nombre, texte   |
| DateEffet     | Début d'application             |
| DateFin       | Fin d'application (optionnelle) |
| Version       | Numéro de version               |
| Source        | Référence réglementaire         |
| Statut        | Actif / Archivé                 |

Les montants sont exprimés exclusivement en **DZD**.

---

# 6. Sources réglementaires

Chaque paramètre doit être relié à une source officielle.

Exemples :

* loi ;
* décret ;
* arrêté ;
* instruction ministérielle ;
* circulaire ;
* note d'application.

La référence est conservée afin de garantir la traçabilité des calculs.

---

# 7. Historisation

Aucune valeur n'est écrasée.

Chaque modification crée une nouvelle version.

Exemple :

| Code      | Valeur | Date d'effet |
| --------- | ------ | ------------ |
| PARAM_001 | 100    | 01/01/2024   |
| PARAM_001 | 120    | 01/01/2025   |

Le moteur sélectionne automatiquement la version applicable à la période de paie.

---

# 8. Versionnement

Le référentiel adopte un versionnement métier.

Exemple :

```text
Référentiel 2026.1

↓

Référentiel 2026.2

↓

Référentiel 2027.1
```

Chaque publication est accompagnée d'une note de version.

---

# 9. Gouvernance des mises à jour

Une mise à jour réglementaire suit le processus suivant :

1. réception du texte officiel ;
2. analyse d'impact ;
3. création ou modification des paramètres ;
4. validation fonctionnelle ;
5. tests réglementaires ;
6. publication.

Aucune mise à jour directe en production n'est autorisée sans validation.

---

# 10. Modèle de données

Les principales tables du référentiel sont :

* `RegulatoryTexts`
* `RegulatoryParameters`
* `ParameterVersions`
* `IndicatorGrids`
* `TaxBrackets`
* `ContributionRates`
* `AllowanceDefinitions`
* `EligibilityRules`
* `ParameterCategories`

Chaque table possède un historique et des métadonnées.

---

# 11. Sélection des paramètres

Le moteur sélectionne un paramètre selon :

* son code métier ;
* sa date d'effet ;
* sa date de fin ;
* son statut ;
* son éventuelle portée (établissement, catégorie, etc.).

Cette logique est centralisée dans le `RegulatoryParameterProvider`.

---

# 12. Référentiel des grilles indiciaires

Les grilles sont modélisées de manière paramétrique.

Chaque ligne comporte :

* catégorie ;
* grade ;
* échelon ;
* indice ;
* date d'effet.

Les grilles successives coexistent afin de permettre le recalcul de périodes antérieures.

---

# 13. Référentiel des barèmes

Les barèmes (IRG, cotisations, etc.) sont décrits sous forme de tranches.

Exemple conceptuel :

| Rang | Borne inférieure | Borne supérieure | Taux |
| ---: | ---------------: | ---------------: | ---: |
|    1 |                … |                … |    … |
|    2 |                … |                … |    … |

Le moteur interprète ces tranches sans code spécifique.

---

# 14. Référentiel des indemnités

Chaque indemnité est définie par :

* son code ;
* son libellé ;
* sa formule ;
* ses critères d'éligibilité ;
* ses paramètres ;
* ses dates de validité.

Les formules et critères sont externalisés.

---

# 15. Audit et traçabilité

Toute modification est enregistrée avec :

* l'identifiant de la modification ;
* la date ;
* l'auteur ;
* le motif ;
* les anciennes et nouvelles valeurs.

Aucun changement n'est anonyme.

---

# 16. Contrôles de cohérence

Avant publication, le référentiel est vérifié :

* unicité des codes ;
* absence de chevauchement des périodes ;
* cohérence des dates ;
* validité des unités ;
* complétude des sources réglementaires.

Les anomalies bloquent la publication.

---

# 17. Performances

Objectifs :

| Opération               | Temps cible |
| ----------------------- | ----------: |
| Lecture d'un paramètre  |      < 5 ms |
| Chargement d'une grille |     < 50 ms |
| Sélection d'une version |     < 10 ms |

Les paramètres fréquemment utilisés peuvent être mis en cache au niveau de la couche Application, avec invalidation contrôlée.

---

# 18. Sécurité

Les paramètres réglementaires sont en lecture seule pour les utilisateurs opérationnels.

Les modifications sont réservées aux profils habilités et soumises à une validation fonctionnelle.

---

# 19. Critères d'acceptation

Le sous-système réglementaire est conforme lorsque :

* aucune valeur réglementaire n'est codée en dur ;
* toutes les valeurs sont historisées ;
* chaque paramètre possède une source réglementaire ;
* les dates d'effet sont respectées ;
* les versions antérieures restent consultables ;
* le moteur de paie obtient les paramètres exclusivement via le `RegulatoryParameterProvider`.

---

# 20. Recommandations d'architecture

Pour renforcer la maintenabilité, il est recommandé de structurer le sous-système autour des composants suivants :

| Composant                     | Rôle                                                 |
| ----------------------------- | ---------------------------------------------------- |
| `RegulatoryParameterProvider` | Point d'accès unique aux paramètres                  |
| `RegulatoryRepository`        | Lecture des données réglementaires                   |
| `RegulatoryVersionResolver`   | Sélection de la bonne version selon la période       |
| `RegulatoryValidator`         | Vérification de la cohérence avant publication       |
| `RegulatoryAuditService`      | Historique et traçabilité des modifications          |
| `RegulatoryImportService`     | Import contrôlé des nouvelles données réglementaires |

---

# Conclusion

Le **Volume 23** transforme les paramètres de paie en un **référentiel réglementaire gouverné**, versionné et historisé. Cette approche garantit que les calculs réalisés par **PaieEducation ERP** restent reproductibles, auditables et conformes aux textes applicables à la période concernée.

## Orientation pour le Volume 24

Le **Volume 24** sera consacré à l'**Architecture de la couche Infrastructure**. Il décrira notamment :

* les services de fichiers ;
* la gestion de la configuration ;
* les sauvegardes et restaurations ;
* les services d'impression (QuestPDF) ;
* les exports (ClosedXML) ;
* la journalisation (`Microsoft.Extensions.Logging`) ;
* les services système (horloge, identifiants, environnement) ;
* les adaptateurs vers SQLite.

Ce volume complétera l'architecture modulaire en définissant l'ensemble des services techniques nécessaires au fonctionnement de **PaieEducation ERP**, tout en maintenant une séparation stricte entre les préoccupations métier et les aspects d'infrastructure.


