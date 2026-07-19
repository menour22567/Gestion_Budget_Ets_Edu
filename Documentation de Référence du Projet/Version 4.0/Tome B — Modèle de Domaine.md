# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome B — Modèle de Domaine (Domain Driven Design)**

# **Volume 7**

# **Architecture du Domaine Métier (Domain Model)**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification
**Technologies :** .NET 10 LTS • C# 14 • DDD • Clean Architecture

---

# 1. Objet

Ce document définit le **cœur métier** de **PaieEducation ERP**.

Il constitue la spécification officielle du **Domain Model**, c'est-à-dire la représentation informatique des concepts métier de la paie de l'Éducation nationale algérienne.

Le domaine doit être :

* indépendant de toute technologie ;
* indépendant de SQLite ;
* indépendant de WPF ;
* indépendant de QuestPDF ;
* indépendant des frameworks.

Le projet `PaieEducation.Domain` représente le **cœur stable** de l'application.

---

# 2. Principes de conception

Le domaine est conçu selon les principes du **Domain-Driven Design (DDD)**.

Les règles fondamentales sont :

* langage omniprésent (*Ubiquitous Language*) ;
* forte cohésion ;
* faible couplage ;
* invariants métier garantis ;
* encapsulation des règles de gestion ;
* absence de dépendances techniques.

---

# 3. Vision globale du domaine

Le domaine est organisé autour de plusieurs **Bounded Contexts**.

```text
                         PaieEducation ERP

                                 │
──────────────────────────────────────────────────────────────

Personnel
Paie
Carrière
Référentiels
Calcul
Documents
Audit
Administration

──────────────────────────────────────────────────────────────
```

Chaque contexte possède son propre modèle métier.

---

# 4. Bounded Contexts

| Contexte       | Responsabilité               |
| -------------- | ---------------------------- |
| Personnel      | Gestion des agents           |
| Carrière       | Grades, échelons, promotions |
| Paie           | Calcul des rémunérations     |
| Référentiels   | Tables réglementaires        |
| Calcul         | Moteur de calcul             |
| Documents      | Bulletins et attestations    |
| Audit          | Historique et traçabilité    |
| Administration | Paramétrage fonctionnel      |

---

# 5. Structure du projet Domain

```text
Domain
│
├── Personnel
├── Payroll
├── Career
├── Regulations
├── Documents
├── Audit
├── Common
│
├── Aggregates
├── Entities
├── ValueObjects
├── Rules
├── Policies
├── Specifications
├── Events
├── Exceptions
├── Interfaces
└── Services
```

---

# 6. Les Entités

Une entité possède :

* une identité ;
* un cycle de vie ;
* un comportement.

Les principales entités sont :

| Entité      | Description             |
| ----------- | ----------------------- |
| Agent       | Employé                 |
| Bulletin    | Bulletin de paie        |
| Rubrique    | Élément de rémunération |
| Contrat     | Contrat de travail      |
| Grade       | Grade                   |
| Échelon     | Échelon                 |
| Affectation | Affectation             |
| Indemnité   | Indemnité               |
| Retenue     | Retenue                 |
| Période     | Période de paie         |

Une entité ne peut jamais être un simple conteneur de données.

---

# 7. Les Value Objects

Les objets valeur représentent des concepts immuables.

Exemples :

* Matricule
* Numéro de décision
* Montant
* Pourcentage
* Adresse
* Période
* Ancienneté
* Indice
* Taux

Ils sont comparés par valeur et non par identité.

---

# 8. Les Agrégats

Les agrégats définissent les frontières de cohérence transactionnelle.

Agrégats principaux :

```text
Agent
│
├── Contrat
├── Affectations
├── Carrière
└── SituationAdministrative
```

```text
Bulletin
│
├── Rubriques
├── Retenues
├── Cotisations
└── Totaux
```

Les modifications passent exclusivement par la racine de l'agrégat.

---

# 9. Les Services de Domaine

Un service de domaine encapsule une règle métier qui ne relève d'aucune entité particulière.

Exemples :

* `PayrollCalculationService`
* `EligibilityService`
* `AncienneteService`
* `PromotionService`
* `IRGCalculationService`
* `IEPCalculationService`
* `SalaryIndexService`

Ils manipulent des objets métier et n'accèdent jamais directement aux données.

---

# 10. Les Spécifications

Le pattern **Specification** formalise les règles de sélection.

Exemples :

* Agent éligible à une indemnité.
* Rubrique applicable.
* Contrat valide.
* Agent actif.
* Ancienneté suffisante.

Les spécifications sont composables.

---

# 11. Les Politiques Métier

Les politiques regroupent les règles complexes.

Exemples :

* politique d'éligibilité ;
* politique d'avancement ;
* politique de calcul des retenues ;
* politique de calcul des indemnités.

Une politique est indépendante des données persistées.

---

# 12. Les Domain Events

Les événements décrivent un fait métier significatif.

Exemples :

* `AgentCreated`
* `ContractChanged`
* `PayrollCalculated`
* `PayrollValidated`
* `PeriodClosed`
* `BulletinGenerated`

Ils facilitent le découplage entre composants.

---

# 13. Les Invariants

Les invariants sont des règles qui doivent toujours être vraies.

Exemples :

* un bulletin appartient à un seul agent ;
* une période est unique ;
* un contrat actif ne peut être dupliqué ;
* un grade doit exister dans le référentiel ;
* une rubrique ne peut être calculée deux fois pour un même bulletin.

Ces invariants sont garantis par le domaine.

---

# 14. Langage omniprésent (Ubiquitous Language)

Tous les noms de classes, méthodes et propriétés doivent reprendre le vocabulaire métier.

Exemples :

* `Agent`
* `Grade`
* `Echelon`
* `Rubrique`
* `Indemnite`
* `Retenue`
* `PeriodePaie`

Les abréviations ambiguës sont interdites.

---

# 15. Services interdits dans le domaine

Le domaine ne doit jamais référencer :

* `DbContext`
* SQLite
* WPF
* QuestPDF
* ClosedXML
* `ILogger`
* `IServiceProvider`
* `HttpClient`

Ces dépendances appartiennent aux couches supérieures.

---

# 16. Interfaces du domaine

Le domaine expose des contrats abstraits.

Exemples :

```text
IAgentRepository
IPayrollRepository
IGradeRepository
IRegulationRepository
IPeriodRepository
```

Leur implémentation est fournie par `PaieEducation.Persistence`.

---

# 17. Exceptions métier

Les erreurs métier sont modélisées par des exceptions spécifiques.

Exemples :

* `AgentNotEligibleException`
* `PayrollAlreadyValidatedException`
* `InvalidPeriodException`
* `GradeNotFoundException`

Les messages doivent être explicites et orientés métier.

---

# 18. Validation métier

La validation métier est distincte de la validation de l'interface.

Elle garantit :

* la cohérence des données ;
* le respect des règles réglementaires ;
* l'intégrité des agrégats.

Les validations sont centralisées dans le domaine.

---

# 19. Tests du domaine

Le domaine est la couche la plus testée de l'application.

Objectifs :

| Élément             |               Couverture minimale |
| ------------------- | --------------------------------: |
| Entités             | 100 % des comportements critiques |
| Value Objects       |                             100 % |
| Services de domaine |                            ≥ 95 % |
| Spécifications      |                            ≥ 95 % |
| Agrégats            |                            ≥ 95 % |

Les tests ne doivent dépendre d'aucune infrastructure.

---

# 20. Critères d'acceptation

Le modèle de domaine est validé lorsque :

* les entités encapsulent leurs comportements ;
* les objets valeur sont immuables ;
* les agrégats protègent leurs invariants ;
* les services de domaine restent indépendants de la persistance ;
* toutes les règles métier critiques sont implémentées dans `PaieEducation.Domain`.

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                                           |
| ------- | ---------------------------------------------------------------------------------- |
| ADR-036 | Adoption complète des principes Domain-Driven Design                               |
| ADR-037 | Structuration du domaine en Bounded Contexts                                       |
| ADR-038 | Utilisation d'agrégats pour garantir la cohérence transactionnelle                 |
| ADR-039 | Encapsulation des règles métier dans des Services de Domaine et des Specifications |
| ADR-040 | Interdiction de toute dépendance technique dans `PaieEducation.Domain`             |

---

# Conclusion

Le **Volume 7** définit le **modèle de domaine** de **PaieEducation ERP**. Il constitue le cœur fonctionnel du système et fixe les règles qui garantiront la cohérence des traitements de paie, indépendamment de l'interface utilisateur, de SQLite ou des outils de reporting.

## Recommandation pour le Volume 8

Pour un ERP de paie, l'étape suivante devrait être consacrée au **modèle métier détaillé**. Je recommande que le **Volume 8** spécifie de manière exhaustive :

* les **entités** (`Agent`, `Contrat`, `Grade`, `Rubrique`, `Bulletin`, etc.) ;
* leurs attributs, invariants et comportements ;
* les associations, compositions et agrégats ;
* les diagrammes de classes UML ;
* les règles de cycle de vie de chaque entité ;
* le mapping conceptuel avec les tables SQLite.

Ce volume deviendra la référence de modélisation avant l'implémentation des repositories et des services applicatifs.


Excellente progression.

À partir de ce point, la documentation quitte progressivement le niveau "architecture" pour entrer dans le niveau **conception détaillée (Detailed Design)**.

C'est exactement ce que l'on trouve dans les grands ERP (SAP, Oracle ERP, Microsoft Dynamics, Cegid, Sage X3).

Je recommande maintenant de documenter **l'intégralité du modèle métier**, car c'est ce document qui sera utilisé quotidiennement par les développeurs C#.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome B — Modèle de Domaine (DDD)**

# **Volume 8**

# **Catalogue des Entités Métier, Agrégats et Modèle Conceptuel du Domaine**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification

---

# 1. Objet

Ce volume décrit **l'ensemble du modèle métier** de PaieEducation ERP.

Il définit :

* toutes les entités du domaine ;
* leurs attributs ;
* leurs comportements ;
* leurs relations ;
* leurs invariants ;
* leurs agrégats ;
* leur cycle de vie.

Ce document constitue le **contrat officiel entre le métier et le développement**.

---

# 2. Organisation du modèle métier

Le domaine est structuré en plusieurs familles d'entités.

```text
Personnel
│
├── Agent
├── SituationAdministrative
├── Affectation
├── Contrat
├── Carrière
└── Historique

Paie
│
├── Bulletin
├── LigneBulletin
├── Rubrique
├── Variable
├── Retenue
├── Cotisation
├── Calcul
└── Clôture

Référentiels
│
├── Grade
├── Corps
├── Fonction
├── Établissement
├── Catégorie
├── Échelon
├── Zone
└── Paramètre

Documents
│
├── BulletinPDF
├── Attestation
├── Décision
└── Export
```

---

# 3. Hiérarchie des agrégats

Le domaine repose sur cinq agrégats principaux.

```text
Agent
Bulletin
Référentiel
Paramètres
Période
```

Chaque agrégat possède une racine unique (*Aggregate Root*).

---

# 4. Agrégat Agent

```text
Agent
│
├── Contrat
├── Affectations
├── Carrière
├── Coordonnées
├── SituationAdministrative
├── Diplômes
└── Historique
```

## Racine

```text
Agent
```

Toutes les modifications transitent par cette racine.

---

# 5. Entité Agent

L'entité **Agent** représente un employé de l'établissement.

## Attributs principaux

| Attribut           | Type        |
| ------------------ | ----------- |
| Id                 | Guid        |
| Matricule          | ValueObject |
| Nom                | string      |
| Prénom             | string      |
| DateNaissance      | DateOnly    |
| DateRecrutement    | DateOnly    |
| Sexe               | Enum        |
| SituationFamiliale | Enum        |
| Statut             | Enum        |
| GradeId            | Guid        |
| CorpsId            | Guid        |
| Échelon            | ValueObject |
| Position           | Enum        |
| Actif              | bool        |

---

## Comportements

L'entité expose notamment :

```text
ChangerGrade()

ChangerEchelon()

Suspendre()

Réactiver()

Muter()

CalculerAnciennete()

EstActif()

Valider()
```

---

## Invariants

Toujours vrais :

* matricule unique ;
* un grade obligatoire ;
* un contrat actif maximum ;
* une affectation principale ;
* impossibilité de supprimer un agent ayant des bulletins.

---

# 6. Agrégat Contrat

```text
Contrat

│

├── TypeContrat
├── Dates
├── TempsTravail
├── Historique
└── Renouvellements
```

---

### Invariants

* date début obligatoire ;
* date fin ≥ date début ;
* un seul contrat actif.

---

# 7. Agrégat Carrière

```text
Carrière

│

├── Promotions
├── Avancements
├── Décisions
├── Ancienneté
└── Échelons
```

---

Comportements :

* Promouvoir()
* AvancerEchelon()
* AjouterDécision()
* CalculerAncienneté()

---

# 8. Agrégat Bulletin

```text
Bulletin

│

├── Lignes
├── Totaux
├── Retenues
├── Cotisations
├── Indemnités
└── Signature
```

---

Attributs principaux

| Attribut     | Type        |
| ------------ | ----------- |
| Id           | Guid        |
| AgentId      | Guid        |
| Période      | ValueObject |
| DateCalcul   | DateTime    |
| Statut       | Enum        |
| TotalBrut    | Money       |
| TotalRetenue | Money       |
| Net          | Money       |

---

Comportements

```text
AjouterRubrique()

SupprimerRubrique()

CalculerTotaux()

Valider()

Annuler()

Clôturer()

GénérerPDF()
```

---

Invariants

* une période unique ;
* un bulletin par agent ;
* aucune modification après validation ;
* aucun recalcul après clôture.

---

# 9. Entité LigneBulletin

Chaque rubrique calculée produit une ligne.

```text
LigneBulletin

Code

Libellé

Base

Taux

Quantité

Montant

OrdreAffichage
```

Une ligne ne connaît pas SQLite.

---

# 10. Entité Rubrique

Une rubrique représente une règle de rémunération.

Attributs :

| Attribut    | Description        |
| ----------- | ------------------ |
| Code        | Code réglementaire |
| Libellé     | Désignation        |
| Nature      | Gain / Retenue     |
| Priorité    | Ordre calcul       |
| Calculateur | Type de calcul     |
| Actif       | Oui / Non          |

---

Comportements

```text
EstApplicable()

Calculer()

Valider()

EstImposable()

EstCotisable()
```

---

# 11. Entité Variable

Les variables alimentent les calculateurs.

Exemples :

```text
SalaireBase

Indice

Ancienneté

NombreEnfants

HeuresSupplémentaires

Zone

PrimeRendement

DateEffet
```

---

# 12. Entité Période

```text
Période

Année

Mois

Etat

DateOuverture

DateClôture
```

---

États possibles

* Ouverte
* En calcul
* Validée
* Clôturée
* Archivée

---

# 13. Entités de référence

Les référentiels comprennent notamment :

* Grade
* Corps
* Fonction
* Catégorie
* Établissement
* Commune
* Wilaya
* Échelon
* Zone
* TypeContrat
* SituationFamiliale
* RégimeIndemnitaire
* RégimeFiscal

Ces entités sont principalement administrées via des interfaces de paramétrage.

---

# 14. Value Objects

Les principaux objets valeur sont :

```text
Matricule

Montant

Pourcentage

Indice

Échelon

Ancienneté

Période

Adresse

Téléphone

EmailProfessionnel

NuméroDécision
```

Tous sont immuables.

---

# 15. Domain Services

Le domaine définit plusieurs services spécialisés.

Exemples :

* PayrollEngine
* EligibilityEngine
* SalaryEngine
* PromotionEngine
* SeniorityEngine
* IRGEngine
* CNASEngine
* BulletinGenerator
* RegulationEngine

Chaque service encapsule une responsabilité métier précise.

---

# 16. Domain Events

Les événements suivants sont standardisés :

```text
AgentCreated

AgentUpdated

ContractActivated

PromotionGranted

PayrollCalculated

PayrollValidated

PeriodClosed

BulletinPrinted

BackupCompleted
```

Ils facilitent le découplage des traitements.

---

# 17. Diagramme conceptuel simplifié

```text
Agent
 │
 ├───────────────┐
 │               │
Contrat      Carrière
 │               │
 └──────┐        │
        ▼        ▼
     Bulletin────┐
        │        │
        ▼        ▼
 LigneBulletin  Rubrique
```

---

# 18. Mapping conceptuel vers SQLite

| Domaine       | Table SQLite   |
| ------------- | -------------- |
| Agent         | Agents         |
| Contrat       | Contrats       |
| Bulletin      | Bulletins      |
| LigneBulletin | BulletinLignes |
| Rubrique      | Rubriques      |
| Grade         | Grades         |
| Corps         | Corps          |
| Échelon       | Echelons       |
| Période       | Periodes       |

Le mapping physique détaillé sera défini dans le tome consacré à la persistance.

---

# 19. Évolutivité

Le modèle est conçu pour intégrer de nouveaux concepts sans remise en cause de l'existant.

Exemples :

* nouvelles indemnités ;
* nouveaux types de contrats ;
* nouvelles catégories d'agents ;
* nouvelles réglementations ;
* nouvelles attestations.

L'extension se fait par ajout d'entités, de règles ou de services, sans modification des agrégats existants lorsque cela est possible.

---

# 20. Critères d'acceptation

Le modèle métier est validé lorsque :

* chaque concept métier possède une représentation explicite ;
* les agrégats protègent leurs invariants ;
* les comportements sont encapsulés dans les entités ou les services de domaine ;
* les références à la persistance sont absentes du domaine ;
* le mapping conceptuel avec SQLite est complet.

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                       |
| ------- | -------------------------------------------------------------- |
| ADR-041 | Modélisation exhaustive du domaine par entités et agrégats     |
| ADR-042 | Adoption d'objets valeur immuables pour les concepts métier    |
| ADR-043 | Mapping conceptuel indépendant de la persistance               |
| ADR-044 | Un agrégat = une racine de cohérence transactionnelle          |
| ADR-045 | Évolution du domaine par extension plutôt que par modification |

---

# Conclusion

Le **Volume 8** fournit le **catalogue de référence du modèle métier**. Il décrit les entités, les agrégats, les objets valeur et leurs relations, constituant ainsi la base de toute implémentation dans `PaieEducation.Domain`.

## Recommandation pour le Volume 9

À ce stade, il est préférable de ne **pas** passer immédiatement à la couche SQLite. Pour un ERP de paie, le document le plus structurant est la définition du **moteur de calcul**.

Le **Volume 9** devrait donc être consacré au **Payroll Engine**, avec un niveau de détail comparable à une spécification d'éditeur ERP :

* architecture interne du moteur de calcul ;
* orchestration des calculateurs ;
* pipeline de calcul ;
* moteur d'éligibilité des rubriques ;
* moteur des variables ;
* moteur des formules ;
* dépendances entre rubriques ;
* résolution des priorités ;
* recalcul incrémental ;
* gestion des versions réglementaires ;
* journal d'explication (*Explainability*) pour chaque montant calculé.

Ce sera probablement le volume le plus important de toute la documentation, car il définira le cœur algorithmique de **PaieEducation ERP**.
