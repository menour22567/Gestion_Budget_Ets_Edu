

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome B — Fonctionnel**

# **Volume 6**

# **Catalogue des Modules Fonctionnels**

**Version :** 2.0

> ⚠️ **RÉFÉRENCE ARCHIVÉE — Version 3.0.**
> Ce tome est conservé pour mémoire historique. Les concepts qu'il décrit ont été
> **revus et partiellement écartés** dans la Version 4.0 du projet.
>
> **Concepts de la phase paiement NON REPRIS en V4** : section *Finance* (Banques,
> Agences, Comptes — l. 133-137), table conceptuelle T_Banque (Tome C v3.0 l. 1083),
> référentiels *Banques* (Tome A v3.0 l. 762) et *Paramètres CNAS* (idem), entités
> Banque (l. 1187) et Organisme (l. 1186) côté Agent.
>
> **Justification** : l'application PaieEducation est désormais positionnée comme un
> **système de gestion administrative de la paie** (production de documents : bulletins,
> attestations, relevés d'émoluments). L'exécution du paiement (virements, ordres de
> paiement, gestion des comptes bancaires) relève d'un système d'information bancaire
> distinct et est **hors périmètre**. Voir **docs/adr/0010-abstention-phase-paiement.md**
> (ADR-0010, proposé le 19/07/2026) pour la décision d'architecture actant cette
> abstention.
>
> **À lire comme** : un inventaire historique des concepts envisagés à un moment donné,
> **pas** comme une spécification à implémenter. Toute table, migration, use case ou
> endpoint inspiré de cette V3 et relevant de la phase paiement doit être rejeté en revue.

**Statut :** Référentiel officiel des modules de l'ERP

---

# 1. Objet du document

Ce volume définit le **catalogue officiel des modules fonctionnels** de **PaieEducation ERP**.

Chaque module constitue un domaine fonctionnel autonome possédant :

* un périmètre clairement défini ;
* des responsabilités précises ;
* ses propres écrans WPF ;
* ses propres services applicatifs ;
* ses propres règles métier ;
* ses propres référentiels.

Ce découpage garantit une architecture modulaire, maintenable et évolutive.

---

# 2. Cartographie générale des modules

```text
PaieEducation ERP

├── M01 Administration
├── M02 Référentiels
├── M03 Ressources Humaines
├── M04 Carrière
├── M05 Variables de Paie
├── M06 Moteur de Calcul
├── M07 Bulletins de Paie
├── M08 Documents Administratifs
├── M09 Reporting & États
├── M10 Exports
├── M11 Journalisation & Audit
├── M12 Sauvegarde & Maintenance
```

---

# 3. Matrice des modules

| Code | Module              | Critique | Priorité |
| ---- | ------------------- | -------- | -------- |
| M01  | Administration      | Haute    | P1       |
| M02  | Référentiels        | Haute    | P1       |
| M03  | Ressources Humaines | Critique | P1       |
| M04  | Carrière            | Critique | P1       |
| M05  | Variables de Paie   | Critique | P1       |
| M06  | Moteur de Calcul    | Critique | P1       |
| M07  | Bulletins           | Critique | P1       |
| M08  | Documents           | Moyenne  | P2       |
| M09  | Reporting           | Haute    | P2       |
| M10  | Exports             | Moyenne  | P2       |
| M11  | Audit               | Haute    | P2       |
| M12  | Maintenance         | Moyenne  | P3       |

---

# 4. Module M01 — Administration

## Mission

Assurer la configuration générale de l'application.

## Sous-modules

* Paramètres généraux
* Exercices de paie
* Périodes
* Paramètres système
* Préférences utilisateur
* Gestion des sauvegardes
* Paramètres d'impression

## Fonctions principales

* ouverture d'un exercice ;
* clôture d'une période ;
* configuration de l'application ;
* gestion des paramètres techniques.

---

# 5. Module M02 — Référentiels

## Mission

Centraliser toutes les données de référence utilisées par l'ERP.

## Référentiels principaux

### Organisation

* Établissements
* Académies (si applicable)
* Services
* Structures administratives

### Ressources humaines

* Corps
* Grades
* Échelons
* Fonctions
* Catégories

### Réglementation

* Rubriques de paie
* Paramètres CNAS
* Paramètres IRG
* Paramètres retraite
* Paramètres réglementaires

### Finance

* Banques
* Agences
* Comptes

Tous les référentiels doivent être historisés lorsqu'ils sont soumis à une évolution réglementaire.

---

# 6. Module M03 — Ressources Humaines

## Mission

Gérer le cycle de vie administratif des agents.

## Fonctions

* création d'un agent ;
* modification du dossier ;
* état civil ;
* situation familiale ;
* coordonnées ;
* diplômes ;
* affectations ;
* historique administratif.

## Données gérées

* matricule ;
* identité ;
* NIR (si utilisé) ;
* date de naissance ;
* adresse ;
* téléphone ;
* courriel ;
* situation familiale ;
* enfants à charge.

---

# 7. Module M04 — Carrière

## Mission

Gérer l'évolution administrative de chaque agent.

## Sous-domaines

* nomination ;
* titularisation ;
* promotions ;
* avancement d'échelon ;
* changement de grade ;
* mutation ;
* détachement ;
* disponibilité ;
* retraite ;
* cessation d'activité.

## Exigences

Toutes les décisions doivent être historisées avec :

* date d'effet ;
* référence réglementaire ;
* document justificatif (si disponible).

---

# 8. Module M05 — Variables de Paie

## Mission

Gérer les éléments variables intervenant dans le calcul mensuel.

## Variables

* heures supplémentaires ;
* absences ;
* congés ;
* rappels ;
* régularisations ;
* retenues diverses ;
* primes exceptionnelles ;
* indemnités ponctuelles ;
* avances ;
* oppositions.

## Particularités

Chaque variable doit être :

* datée ;
* justifiée ;
* traçable ;
* modifiable selon les droits définis par l'application.

---

# 9. Module M06 — Moteur de Calcul

## Mission

Exécuter le calcul réglementaire de la paie.

## Étapes

1. Chargement du contexte.
2. Vérification de la cohérence.
3. Détermination des droits.
4. Calcul du salaire de base.
5. Calcul des indemnités.
6. Calcul des retenues.
7. Calcul des cotisations.
8. Calcul de l'IRG.
9. Calcul du net.
10. Contrôles.
11. Validation.

## Exigences

Le moteur doit être :

* déterministe ;
* reproductible ;
* journalisé ;
* testable.

---

# 10. Module M07 — Bulletins de Paie

## Mission

Produire le bulletin officiel de l'agent.

## Fonctions

* aperçu ;
* impression ;
* génération PDF ;
* historique ;
* recalcul ;
* annulation (selon règles métier) ;
* réédition.

Chaque bulletin doit être associé à une période unique et conserver un historique des versions si une régénération est autorisée.

---

# 11. Module M08 — Documents Administratifs

## Mission

Produire les documents liés à la gestion administrative.

## Documents

* attestation de travail ;
* attestation de salaire ;
* certificat administratif ;
* décision ;
* état individuel ;
* fiche agent.

Tous les documents sont générés via **QuestPDF**.

---

# 12. Module M09 — Reporting & États

## Mission

Produire les états réglementaires et de gestion.

## États

* livre de paie ;
* état mensuel ;
* récapitulatif des retenues ;
* statistiques ;
* synthèses.

Les états doivent pouvoir être filtrés par période, établissement et autres critères fonctionnels.

---

# 13. Module M10 — Exports

## Mission

Exporter les données.

## Formats

* Excel (.xlsx)
* PDF
* CSV

Le moteur officiel d'export Excel est **ClosedXML**.

---

# 14. Module M11 — Journalisation & Audit

## Mission

Garantir la traçabilité.

## Événements

* création ;
* modification ;
* suppression logique ;
* calcul ;
* impression ;
* export ;
* sauvegarde ;
* restauration.

Chaque événement est horodaté et identifié.

---

# 15. Module M12 — Sauvegarde & Maintenance

## Mission

Assurer la pérennité des données.

## Fonctions

* sauvegarde SQLite ;
* restauration ;
* vérification de cohérence ;
* optimisation de la base ;
* nettoyage des journaux ;
* diagnostics.

---

# 16. Dépendances fonctionnelles

| Module      | Dépend de                             |
| ----------- | ------------------------------------- |
| RH          | Référentiels                          |
| Carrière    | RH                                    |
| Variables   | RH, Carrière                          |
| Calcul      | Référentiels, RH, Carrière, Variables |
| Bulletins   | Calcul                                |
| Reporting   | Bulletins                             |
| Exports     | Reporting                             |
| Audit       | Tous                                  |
| Maintenance | Tous                                  |

Les dépendances sont orientées dans le sens du processus métier afin d'éviter les cycles.

---

# 17. Flux fonctionnel global

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
Variables de Paie
      │
      ▼
Moteur de Calcul
      │
      ▼
Bulletins
      │
      ▼
Reporting
      │
      ▼
Exports
```

---

# 18. Priorisation des développements

## Phase 1 — Noyau ERP

* Administration
* Référentiels
* Ressources Humaines
* Carrière

## Phase 2 — Moteur de paie

* Variables
* Calcul
* Bulletins

## Phase 3 — Production documentaire

* Documents
* Reporting
* Exports

## Phase 4 — Exploitation

* Audit
* Sauvegarde
* Maintenance

---

# 19. Critères d'acceptation

Chaque module est considéré comme achevé lorsque :

* son périmètre fonctionnel est entièrement couvert ;
* ses écrans WPF sont opérationnels ;
* ses services applicatifs sont implémentés ;
* ses règles métier sont validées ;
* ses tests unitaires et fonctionnels sont concluants ;
* sa documentation est à jour.

---

# 20. Évolution prévue

Ce catalogue constitue la base fonctionnelle de l'ERP. Les modules pourront être enrichis (gestion multi-établissements, tableaux de bord, workflows, signatures électroniques, etc.) sans remettre en cause leur découpage.

---

## Orientation pour le Volume 7

Le **Volume 7 – Référentiel des Données** décrira en détail l'ensemble du modèle de données de l'application. Il ne se limitera pas à la structure SQLite : il définira le dictionnaire de données complet, les entités métier, leurs attributs, les relations, les contraintes d'intégrité, les conventions de nommage, les index, les clés, les historiques, les dates d'effet et les stratégies de migration. Ce volume deviendra la référence unique pour toute évolution du schéma de données et garantira la cohérence entre le domaine métier, la persistance SQLite et les services applicatifs.


Excellente progression. À partir de ce **Volume 7**, la documentation entre dans une phase beaucoup plus structurante. Ce volume devient le **référentiel de données officiel** de l'ERP. C'est lui qui servira de référence unique pour le modèle métier, la base SQLite, les DTO, les repositories, les services applicatifs et les migrations.

Je vais également introduire une notion importante qui manquait dans les premières versions : la distinction entre le **modèle métier (Domain Model)** et le **modèle de persistance (SQLite Model)**, ce qui est une pratique d'architecture ERP moderne.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome B — Fonctionnel**

# **Volume 7**

# **Référentiel des Données et Modèle d'Information**

**Version :** 2.0
**Statut :** Référentiel officiel des données

---

# 1. Objet du document

Ce document constitue la **référence unique du patrimoine de données** de **PaieEducation ERP**.

Il définit :

* le modèle d'information métier ;
* les entités fonctionnelles ;
* les objets du domaine ;
* les relations entre les données ;
* les règles d'intégrité ;
* les conventions de nommage ;
* les principes de persistance SQLite.

Aucun développement ne devra créer de nouvelles structures de données sans être conforme à ce référentiel.

---

# 2. Principes de conception

Le modèle de données repose sur les principes suivants :

* **une donnée possède une seule source de vérité** ;
* **aucune duplication d'information métier** ;
* **les identifiants techniques sont internes** ;
* **les codes réglementaires sont les identifiants fonctionnels** ;
* **l'historisation est privilégiée à l'écrasement des données** ;
* **les dates d'effet sont systématiquement prises en compte pour les données réglementaires et administratives**.

---

# 3. Les quatre niveaux du modèle de données

```text
                 Modèle Métier (Domain)
                         │
                         ▼
              DTO / Objets Applicatifs
                         │
                         ▼
          Modèle de Persistance SQLite
                         │
                         ▼
              Base SQLite Physique
```

Chaque niveau possède une responsabilité distincte.

---

# 4. Classification des données

Les données sont regroupées en familles.

| Famille                | Description                              |
| ---------------------- | ---------------------------------------- |
| Référentiels           | Données stables et partagées             |
| Données RH             | Informations administratives des agents  |
| Données de carrière    | Évolution de la situation administrative |
| Données réglementaires | Paramètres de calcul                     |
| Variables mensuelles   | Éléments variables de paie               |
| Données de calcul      | Résultats intermédiaires                 |
| Bulletins              | Résultats définitifs                     |
| Documents              | Productions documentaires                |
| Audit                  | Historique des opérations                |
| Configuration          | Paramètres techniques                    |

---

# 5. Domaine « Référentiels »

## Entités principales

| Entité        | Description                 |
| ------------- | --------------------------- |
| Etablissement | Organisme employeur         |
| Corps         | Corps de rattachement       |
| Grade         | Grade réglementaire         |
| Echelon       | Échelon administratif       |
| Fonction      | Fonction exercée            |
| TypeContrat   | Nature du contrat           |
| TypePersonnel | Classification du personnel |
| RubriquePaie  | Élément de rémunération     |
| Organisme     | CNAS, CNR, Trésor, etc.     |
| Banque        | Banque de domiciliation     |

---

# 6. Domaine « Ressources Humaines »

## Agrégat : Agent

### Attributs principaux

| Attribut           | Type        | Obligatoire |
| ------------------ | ----------- | ----------- |
| AgentId            | Guid        | Oui         |
| Matricule          | String      | Oui         |
| Nom                | String      | Oui         |
| Prénom             | String      | Oui         |
| DateNaissance      | Date        | Oui         |
| Sexe               | Enum        | Oui         |
| SituationFamiliale | Enum        | Oui         |
| NombreEnfants      | Integer     | Oui         |
| Adresse            | ValueObject | Oui         |
| Téléphone          | String      | Non         |
| Email              | String      | Non         |

Le **matricule** est l'identifiant fonctionnel. L'`AgentId` est un identifiant technique interne.

---

# 7. Domaine « Carrière »

Chaque événement de carrière est historisé.

### Entités

* Affectation
* Nomination
* Promotion
* Avancement
* Mutation
* Détachement
* Disponibilité
* Cessation

### Attributs communs

* DateDébut
* DateFin
* Référence
* Motif
* Texte réglementaire
* Observation

---

# 8. Domaine « Contrats »

## Entité Contrat

| Attribut     | Description |
| ------------ | ----------- |
| ContratId    | Identifiant |
| TypeContrat  | Référence   |
| TempsTravail | Quotité     |
| DateDébut    | Début       |
| DateFin      | Fin         |
| Actif        | Oui / Non   |

Un agent ne peut posséder qu'un seul contrat principal actif.

---

# 9. Domaine « Rubriques de paie »

Chaque rubrique constitue un objet métier autonome.

### Attributs

| Attribut     | Description         |
| ------------ | ------------------- |
| RubriqueId   | Technique           |
| CodeRubrique | Fonctionnel         |
| Libellé      | Désignation         |
| Nature       | Gain / Retenue      |
| Formule      | Référence de calcul |
| Priorité     | Ordre d'exécution   |
| DateEffet    | Début de validité   |
| DateFin      | Fin de validité     |

---

# 10. Domaine « Paramètres réglementaires »

Les paramètres réglementaires sont historisés.

### Exemples

* valeur du point indiciaire ;
* barème IRG ;
* taux CNAS ;
* taux retraite ;
* plafond de cotisation ;
* seuils réglementaires.

Chaque paramètre est défini par une période de validité.

---

# 11. Domaine « Variables mensuelles »

Les variables sont indépendantes du calcul.

### Exemples

* heures supplémentaires ;
* absences ;
* congés ;
* primes exceptionnelles ;
* retenues diverses ;
* rappels.

Chaque variable est liée à :

* un agent ;
* une période ;
* une justification éventuelle.

---

# 12. Domaine « Bulletin de paie »

## Agrégat

```text
Bulletin
│
├── Lignes
├── Gains
├── Retenues
├── Cotisations
├── Impôts
└── Journal
```

Un bulletin est immuable après validation, sauf procédure réglementaire de régularisation.

---

# 13. Domaine « Journal d'audit »

Chaque événement possède les attributs suivants :

| Attribut    | Description                  |
| ----------- | ---------------------------- |
| DateHeure   | Horodatage                   |
| Utilisateur | Origine                      |
| Module      | Module concerné              |
| Action      | Nature de l'opération        |
| Résultat    | Succès / Échec               |
| Détails     | Informations complémentaires |

---

# 14. Relations entre les agrégats

```text
Agent
│
├── Contrats
├── Affectations
├── Carrière
├── Variables
└── Bulletins

Bulletin
│
├── Lignes
├── Rubriques
├── Cotisations
└── Journal
```

Les relations sont modélisées de manière à limiter les dépendances fortes et à faciliter les évolutions.

---

# 15. Conventions de nommage

## Entités

* Singulier
* PascalCase

Exemples :

* `Agent`
* `Bulletin`
* `RubriquePaie`

## Propriétés

PascalCase :

```text
Nom

DateNaissance

DateEffet

SalaireBrut
```

## Tables SQLite

Préfixe recommandé :

```text
T_Agent

T_Bulletin

T_Rubrique

T_Contrat
```

## Colonnes

Même nom que les propriétés métier pour limiter les conversions.

---

# 16. Identifiants

Chaque entité possède deux identifiants :

| Type                         | Usage                         |
| ---------------------------- | ----------------------------- |
| Guid                         | Identifiant technique interne |
| Code réglementaire ou métier | Identifiant fonctionnel       |

Les interfaces utilisateur afficheront toujours le code fonctionnel lorsque cela est pertinent.

---

# 17. Intégrité des données

Les contraintes suivantes sont obligatoires :

* unicité du matricule ;
* unicité du code rubrique ;
* unicité du code établissement ;
* cohérence des dates (début ≤ fin) ;
* clés étrangères obligatoires lorsque la relation est requise ;
* suppression logique privilégiée lorsque l'historique doit être conservé.

---

# 18. Historisation

Les données suivantes sont historisées :

* carrière ;
* paramètres réglementaires ;
* rubriques ;
* affectations ;
* contrats ;
* bulletins ;
* journal d'audit.

Aucune information réglementaire ne doit être perdue lors d'une mise à jour.

---

# 19. Modèle de persistance SQLite

Le schéma SQLite devra respecter :

* normalisation (jusqu'à la 3ᵉ forme normale lorsque cela est pertinent) ;
* index sur les colonnes fréquemment recherchées (matricule, période, codes) ;
* contraintes d'intégrité référentielle ;
* migrations versionnées et réversibles.

Le modèle physique pourra être optimisé pour les performances sans altérer le modèle métier.

---

# 20. Critères d'acceptation

Le référentiel des données est conforme si :

* chaque donnée possède un propriétaire clairement identifié ;
* les redondances sont évitées ;
* les règles d'intégrité sont documentées ;
* les conventions de nommage sont respectées ;
* le modèle de persistance est cohérent avec le modèle métier ;
* l'historisation des données réglementaires est garantie.

---

# Évolution de la documentation

À partir de ce point, la documentation quitte la phase de **description structurelle** pour entrer dans la **spécification fonctionnelle détaillée**.

Le **Volume 8 – Catalogue Fonctionnel** décrira, module par module, les cas d'utilisation, les acteurs, les scénarios nominaux et alternatifs, les règles de validation, les préconditions, les postconditions, les écrans concernés, les services applicatifs sollicités et les interactions avec le domaine métier. Il constituera la base des développements WPF, des tests fonctionnels et de la rédaction des guides utilisateurs. C'est également à partir de ce volume que nous commencerons à atteindre un niveau de documentation comparable aux ERP industriels.


# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome B — Fonctionnel**

# **Volume 8**

# **Catalogue Fonctionnel Détaillé (Spécifications Fonctionnelles ERP)**

**Version :** 2.0
**Statut :** Référentiel officiel des fonctionnalités

---

# 1. Objet du document

Ce document constitue la **spécification fonctionnelle détaillée** de **PaieEducation ERP**.

Contrairement au **Volume 6**, qui décrit les modules, ce volume décrit **chaque fonctionnalité** de manière exploitable par :

* les développeurs ;
* les concepteurs UI/UX ;
* les testeurs ;
* les analystes fonctionnels ;
* les rédacteurs de documentation.

Chaque fonctionnalité est décrite selon un format normalisé afin d'assurer la traçabilité entre le besoin métier, le développement et les tests.

---

# 2. Structure normalisée d'une fonctionnalité

Toutes les fonctionnalités devront respecter la structure suivante :

| Élément                | Description                 |
| ---------------------- | --------------------------- |
| Identifiant            | Code unique (ex. F-RH-001)  |
| Module                 | Module fonctionnel concerné |
| Nom                    | Nom de la fonctionnalité    |
| Description            | Objectif métier             |
| Acteurs                | Utilisateurs concernés      |
| Préconditions          | Conditions nécessaires      |
| Déclencheur            | Événement initiant l'action |
| Scénario nominal       | Déroulement standard        |
| Scénarios alternatifs  | Cas particuliers            |
| Postconditions         | Résultat attendu            |
| Règles métier          | Références au Volume 9      |
| Données manipulées     | Entités concernées          |
| Écrans WPF             | Référence au Volume 10      |
| Services applicatifs   | Services utilisés           |
| Critères d'acceptation | Conditions de validation    |

---

# 3. Catalogue des fonctionnalités

Les fonctionnalités sont regroupées par domaine.

| Domaine             | Préfixe |
| ------------------- | ------- |
| Administration      | ADM     |
| Référentiels        | REF     |
| Ressources Humaines | RH      |
| Carrière            | CAR     |
| Variables           | VAR     |
| Calcul              | PAY     |
| Bulletins           | BUL     |
| Documents           | DOC     |
| Reporting           | REP     |
| Export              | EXP     |
| Audit               | AUD     |
| Maintenance         | MNT     |

---

# 4. Module Administration

## F-ADM-001 — Initialisation de l'application

### Objectif

Configurer l'application lors de son premier démarrage.

### Acteurs

* Administrateur

### Préconditions

* Base SQLite disponible.

### Scénario nominal

1. Vérification de la structure SQLite.
2. Vérification de la version du schéma.
3. Chargement des paramètres.
4. Initialisation des services.
5. Ouverture de la fenêtre principale.

### Postconditions

Application opérationnelle.

---

## F-ADM-002 — Gestion des exercices de paie

Fonctions :

* créer un exercice ;
* ouvrir un exercice ;
* clôturer un exercice ;
* verrouiller un exercice ;
* réouvrir un exercice (si autorisé).

---

# 5. Module Référentiels

## F-REF-001 — Gestion des établissements

Fonctions :

* création ;
* modification ;
* consultation ;
* désactivation logique.

### Données

* Code
* Nom
* Adresse
* Wilaya
* Commune

---

## F-REF-002 — Gestion des grades

Fonctions :

* création ;
* modification ;
* historique.

Validation :

Un grade ne peut être supprimé s'il est utilisé par un agent.

---

## F-REF-003 — Gestion des rubriques

Fonctions :

* création ;
* paramétrage ;
* activation ;
* désactivation.

Les rubriques sont liées aux règles d'éligibilité et aux formules de calcul.

---

# 6. Module Ressources Humaines

## F-RH-001 — Création d'un agent

### Données obligatoires

* matricule ;
* nom ;
* prénom ;
* date de naissance ;
* sexe ;
* situation familiale.

### Vérifications

* unicité du matricule ;
* cohérence des dates ;
* références valides.

### Résultat

Création du dossier administratif.

---

## F-RH-002 — Modification d'un agent

Les informations modifiables sont définies par les règles métier.

Certaines données (par exemple le matricule) sont protégées contre les modifications après création.

---

## F-RH-003 — Consultation

Fonctions :

* recherche multicritère ;
* consultation détaillée ;
* historique.

---

# 7. Module Carrière

## F-CAR-001 — Nomination

L'utilisateur doit pouvoir enregistrer :

* corps ;
* grade ;
* échelon ;
* date d'effet ;
* référence administrative.

Une nomination crée un événement de carrière historisé.

---

## F-CAR-002 — Promotion

La promotion déclenche automatiquement une mise à jour des informations nécessaires au calcul de la paie à partir de sa date d'effet.

---

## F-CAR-003 — Mutation

Le système conserve l'historique des affectations successives.

---

# 8. Module Variables de Paie

## F-VAR-001 — Gestion des absences

L'utilisateur peut :

* enregistrer une absence ;
* modifier une absence ;
* annuler une absence.

Chaque absence possède :

* une période ;
* un motif ;
* une référence.

---

## F-VAR-002 — Gestion des heures supplémentaires

Informations :

* nombre d'heures ;
* période ;
* type ;
* justification.

---

## F-VAR-003 — Gestion des rappels

Le système doit permettre :

* création ;
* recalcul ;
* annulation (si autorisée).

---

# 9. Module Calcul de la Paie

## F-PAY-001 — Calcul individuel

Déroulement :

1. Chargement de l'agent.
2. Construction du contexte.
3. Vérification des droits.
4. Calcul des gains.
5. Calcul des retenues.
6. Calcul des cotisations.
7. Calcul de l'IRG.
8. Calcul du net.
9. Validation.

---

## F-PAY-002 — Calcul de masse

Le système doit pouvoir calculer les bulletins de tous les agents d'une période.

Fonctionnalités :

* suivi de progression ;
* interruption contrôlée ;
* reprise ;
* journal des erreurs.

---

## F-PAY-003 — Recalcul

Le recalcul doit conserver une traçabilité complète des opérations.

---

# 10. Module Bulletins

## F-BUL-001 — Consultation

Fonctions :

* aperçu ;
* historique ;
* comparaison de versions (si activée).

---

## F-BUL-002 — Impression

Production PDF via QuestPDF.

---

## F-BUL-003 — Réédition

Possible uniquement selon les règles définies par le moteur métier.

---

# 11. Module Documents

## F-DOC-001 — Attestation de travail

Données :

* identité ;
* fonction ;
* établissement.

---

## F-DOC-002 — Attestation de salaire

Le document reprend les informations issues des bulletins validés.

---

# 12. Module Reporting

## F-REP-001 — Livre de paie

Critères :

* exercice ;
* période ;
* établissement.

---

## F-REP-002 — États réglementaires

Les états doivent être exportables en PDF et Excel lorsque cela est applicable.

---

# 13. Module Export

## F-EXP-001 — Export Excel

Le système exporte les données sélectionnées au format `.xlsx`.

---

## F-EXP-002 — Export PDF

Tous les documents PDF sont générés via le module Reporting.

---

# 14. Module Audit

## F-AUD-001 — Consultation du journal

Filtres disponibles :

* période ;
* module ;
* type d'action ;
* utilisateur (si la gestion des utilisateurs est activée).

---

# 15. Module Maintenance

## F-MNT-001 — Sauvegarde

Fonctions :

* sauvegarde complète ;
* sauvegarde manuelle ;
* sauvegarde planifiée (optionnelle).

---

## F-MNT-002 — Restauration

Avant toute restauration :

* contrôle de cohérence ;
* confirmation utilisateur ;
* journalisation de l'opération.

---

# 16. Catalogue des cas d'utilisation

Chaque fonctionnalité sera associée à un ou plusieurs cas d'utilisation.

| Cas d'utilisation | Fonctionnalités concernées |
| ----------------- | -------------------------- |
| UC-001            | Création d'un agent        |
| UC-002            | Gestion de la carrière     |
| UC-003            | Calcul individuel          |
| UC-004            | Calcul de masse            |
| UC-005            | Impression d'un bulletin   |
| UC-006            | Export Excel               |
| UC-007            | Sauvegarde                 |

Ces cas d'utilisation seront détaillés dans la documentation de conception fonctionnelle.

---

# 17. Traçabilité

Chaque fonctionnalité est reliée à :

* un module (Volume 6) ;
* une ou plusieurs règles métier (Volume 9) ;
* une ou plusieurs entités (Volume 7) ;
* un ou plusieurs écrans WPF (Volume 10) ;
* un ou plusieurs services applicatifs (Volume 11) ;
* des scénarios de test (Volume 18).

Cette matrice garantit une couverture complète du cycle de développement.

---

# 18. Critères d'acceptation

Une fonctionnalité est considérée comme conforme lorsque :

* le scénario nominal est implémenté ;
* les scénarios alternatifs sont traités ;
* les règles métier sont respectées ;
* les validations sont opérationnelles ;
* les tests unitaires et fonctionnels sont validés ;
* la documentation est mise à jour.

---

# 19. Matrice de priorité

| Priorité | Description                                       |
| -------- | ------------------------------------------------- |
| P1       | Fonction indispensable au fonctionnement de l'ERP |
| P2       | Fonction importante mais non bloquante            |
| P3       | Fonction d'amélioration ou de confort             |

Cette priorisation servira à organiser les sprints de développement.

---

# Conclusion

Ce **Volume 8** fournit la **spécification fonctionnelle détaillée** de PaieEducation ERP. Chaque fonctionnalité est identifiée, documentée et reliée aux modules, aux données et aux futurs développements.

Le **Volume 9 – Catalogue des Règles Métier** constituera le véritable **noyau réglementaire** de l'ERP. Il décrira de manière formelle toutes les règles de gestion, les conditions d'éligibilité, les algorithmes de calcul, les validations et les dépendances réglementaires. C'est ce volume qui fera le lien direct entre les textes réglementaires algériens, le moteur de calcul de la paie et les services du domaine. Il deviendra la référence absolue pour toute évolution réglementaire.


Je recommande de faire évoluer la documentation d'un cran supplémentaire. Un ERP de paie n'est pas seulement un logiciel : c'est un **système réglementaire**. Le **Volume 9** ne devrait donc pas être une simple liste de règles, mais un **référentiel normatif** qui pilotera directement le moteur de calcul.

Nous allons structurer les règles de manière à ce qu'elles puissent être implémentées presque automatiquement dans le **Domain Layer** (.NET 10), testées unitairement et historisées.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome B — Fonctionnel**

# **Volume 9**

# **Référentiel des Règles Métier et du Moteur de Calcul**

**Version :** 2.0
**Statut :** Référentiel normatif officiel

---

# 1. Objet du document

Ce document constitue la **source officielle de toutes les règles métier** de **PaieEducation ERP**.

Il formalise :

* les règles réglementaires ;
* les règles de gestion ;
* les algorithmes de calcul ;
* les règles de validation ;
* les dépendances entre rubriques ;
* les priorités d'exécution ;
* les règles d'éligibilité ;
* les contrôles de cohérence.

Toutes les implémentations du moteur de paie devront être conformes à ce référentiel.

---

# 2. Hiérarchie des règles

Les règles sont classées en quatre catégories.

| Niveau | Catégorie              | Exemple                                 |
| ------ | ---------------------- | --------------------------------------- |
| R1     | Règles réglementaires  | Barème IRG, taux CNAS, point indiciaire |
| R2     | Règles administratives | Avancement, nomination, carrière        |
| R3     | Règles fonctionnelles  | Éligibilité à une rubrique              |
| R4     | Règles techniques      | Ordonnancement des calculs              |

En cas de conflit, une règle de niveau supérieur prévaut.

---

# 3. Cycle de calcul de la paie

Le moteur suit une séquence fixe.

```text
Chargement du contexte
        │
        ▼
Validation des données
        │
        ▼
Détermination des droits
        │
        ▼
Calcul des gains
        │
        ▼
Calcul des retenues
        │
        ▼
Calcul des cotisations
        │
        ▼
Calcul de l'IRG
        │
        ▼
Calcul du net
        │
        ▼
Contrôles
        │
        ▼
Validation
```

Cette séquence est déterministe et reproductible.

---

# 4. Principes fondamentaux

Le moteur de calcul doit respecter les principes suivants :

* **idempotence** : deux calculs avec les mêmes données produisent le même résultat ;
* **traçabilité** : chaque décision est enregistrée ;
* **explicabilité** : chaque montant peut être justifié ;
* **historisation** : les paramètres utilisés sont ceux en vigueur à la date d'effet ;
* **non-régression** : une évolution réglementaire ne doit pas modifier rétroactivement les résultats validés.

---

# 5. Structure normalisée d'une règle métier

Chaque règle est documentée selon le modèle suivant.

| Élément              | Description                      |
| -------------------- | -------------------------------- |
| Identifiant          | RM-XXXX                          |
| Nom                  | Libellé de la règle              |
| Domaine              | RH, Carrière, Paie…              |
| Source réglementaire | Référence juridique              |
| Description          | Objet de la règle                |
| Conditions           | Prérequis                        |
| Algorithme           | Description du traitement        |
| Données d'entrée     | Entités utilisées                |
| Données de sortie    | Résultat produit                 |
| Exceptions           | Cas particuliers                 |
| Tests associés       | Références des scénarios de test |

---

# 6. Règles d'éligibilité

Les droits aux rubriques sont déterminés par un moteur de règles.

Critères possibles :

* corps ;
* grade ;
* échelon ;
* fonction ;
* type de contrat ;
* statut ;
* ancienneté ;
* établissement ;
* période d'effet.

Une rubrique ne peut être calculée que si toutes ses conditions d'éligibilité sont satisfaites.

---

# 7. Dépendances entre rubriques

Les rubriques peuvent dépendre d'autres rubriques.

Exemples :

* une indemnité calculée à partir du salaire de base ;
* une retenue calculée sur le salaire brut ;
* l'IRG calculé après les cotisations.

Le moteur doit construire un graphe de dépendances afin de garantir l'ordre d'exécution.

---

# 8. Paramètres réglementaires

Les paramètres réglementaires ne sont jamais codés en dur.

Ils sont stockés dans les référentiels et versionnés.

Exemples :

* valeur du point indiciaire ;
* taux CNAS ;
* taux retraite ;
* barème IRG ;
* plafonds réglementaires.

Toutes les valeurs monétaires sont exprimées en **dinar algérien (DZD)**.

---

# 9. Règles de validation

Avant tout calcul, le moteur vérifie notamment :

* existence d'un contrat actif ;
* grade valide ;
* échelon valide ;
* période ouverte ;
* paramètres réglementaires disponibles ;
* absence de doublon de bulletin pour la même période (sauf procédure de régularisation).

---

# 10. Journal de calcul

Chaque calcul produit un journal détaillé comprenant :

* date et heure ;
* version du moteur ;
* paramètres utilisés ;
* règles appliquées ;
* rubriques calculées ;
* rubriques rejetées et motifs ;
* montants intermédiaires ;
* résultat final.

Ce journal facilite les audits et les analyses d'écarts.

---

# 11. Gestion des exceptions

Les cas particuliers (absence de paramètre, incohérence de carrière, données manquantes, etc.) sont identifiés et traités selon une politique documentée. Les erreurs critiques empêchent la validation du bulletin ; les avertissements sont consignés sans bloquer le processus si la réglementation le permet.

---

# 12. Historisation des règles

Chaque modification d'une règle métier ou d'un paramètre réglementaire est :

* datée ;
* versionnée ;
* justifiée ;
* conservée.

Le moteur sélectionne automatiquement la version applicable à la période de paie.

---

# 13. Stratégie de tests

Chaque règle métier doit disposer de tests :

* cas nominal ;
* valeurs limites ;
* cas d'erreur ;
* cas historiques (avant/après changement réglementaire).

Ces tests constituent la base des tests unitaires du domaine.

---

# 14. Traçabilité

Chaque règle est reliée à :

* une ou plusieurs rubriques ;
* les entités métier concernées ;
* les services de domaine qui l'implémentent ;
* les cas d'utilisation (Volume 8) ;
* les scénarios de test (Volume 18).

---

# 15. Critères d'acceptation

Le référentiel des règles métier est conforme lorsque :

* toutes les règles sont identifiées et versionnées ;
* aucun paramètre réglementaire n'est codé en dur ;
* l'ordre d'exécution des calculs est maîtrisé ;
* chaque résultat est explicable et traçable ;
* les tests couvrent les cas nominaux et les cas limites.

---

# Évolution majeure proposée pour les prochains volumes

À partir de cette **Version 2.0**, je recommande d'aller au-delà d'un simple cahier des charges et de produire une documentation de niveau **ERP industriel** inspirée des pratiques de SAP, Oracle ERP et Microsoft Dynamics.

Les volumes suivants pourront notamment couvrir :

* **Volume 10** : Architecture UI/UX WPF (navigation, Shell, vues, ViewModels, design system, ergonomie).
* **Volume 11** : Architecture des services applicatifs (Use Cases, CQRS léger, orchestration).
* **Volume 12** : Modèle SQLite complet (dictionnaire de données, schéma physique, index, contraintes).
* **Volume 13** : Architecture du moteur de calcul (pipeline, stratégie, calculateurs, règles composables).
* **Volume 14** : Reporting QuestPDF (gabarits, styles, documents réglementaires).
* **Volume 15** : Sécurité, journalisation, sauvegarde et reprise.
* **Volume 16** : Stratégie de migration VB.NET → .NET 10.
* **Volume 17** : Performance, optimisation et exploitation.
* **Volume 18** : Stratégie de tests (unitaires, intégration, fonctionnels, non-régression).
* **Volume 19** : Déploiement, maintenance et évolutions.
* **Volume 20** : Annexes (glossaire, conventions, matrices de traçabilité, nomenclatures).

Cette extension transformerait le cahier des charges en un **véritable référentiel d'architecture ERP**, couvrant l'intégralité du cycle de vie du projet, depuis la conception jusqu'à l'exploitation et aux évolutions réglementaires futures.


