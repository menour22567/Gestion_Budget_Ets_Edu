Excellente progression. Nous arrivons maintenant au **cœur technique de l'ERP**.

Si les **Volumes 7 à 12** définissent les données, les fonctionnalités et l'architecture, le **Volume 13** décrit **comment la paie est réellement calculée**. Ce document est celui qui guidera directement l'implémentation de la couche **Domain** en .NET 10. Il doit être suffisamment précis pour permettre le développement sans ambiguïté.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome D — Architecture Technique**

# **Volume 13**

# **Architecture du Moteur de Calcul de la Paie (Domain Engine)**

**Version :** 2.0
**Statut :** Référentiel officiel du moteur de calcul

---

# 1. Objet du document

Le moteur de calcul constitue le **cœur fonctionnel** de **PaieEducation ERP**.

Il est responsable de :

* déterminer les droits d'un agent ;
* calculer chaque rubrique de paie ;
* appliquer les textes réglementaires ;
* produire un bulletin reproductible ;
* expliquer chaque montant calculé ;
* garantir la conformité réglementaire.

Toutes les règles décrites dans le **Volume 9** sont implémentées exclusivement dans cette couche.

---

# 2. Position dans l'architecture

```text
Presentation (WPF)

        │

Application Layer

        │

───────────────

Domain Layer

│

├── Payroll Engine
├── Rule Engine
├── Eligibility Engine
├── Formula Engine
├── Calculation Pipeline
├── Validation Engine
└── Audit Engine

        │

Persistence
```

Le moteur est **indépendant** de WPF, de SQLite et de QuestPDF.

---

# 3. Principes fondamentaux

Le moteur respecte les principes suivants :

* déterministe ;
* sans effet de bord ;
* testable ;
* extensible ;
* historisable ;
* explicable ;
* modulaire ;
* compatible avec les évolutions réglementaires.

---

# 4. Architecture interne

```text
PayrollEngine

│

├── ContextBuilder
├── EligibilityResolver
├── RuleResolver
├── FormulaResolver
├── DependencyResolver
├── CalculationPipeline
├── ValidationEngine
├── ResultAssembler
└── AuditWriter
```

Chaque composant possède une responsabilité unique.

---

# 5. Construction du contexte

Avant tout calcul, le moteur construit un **PayrollContext**.

Il regroupe :

* agent ;
* contrat ;
* carrière ;
* affectation ;
* établissement ;
* période ;
* variables mensuelles ;
* paramètres réglementaires ;
* référentiels.

Le contexte est **immuable** pendant toute l'exécution.

---

# 6. Cycle de calcul

```text
Chargement

↓

Construction du contexte

↓

Validation

↓

Détermination des droits

↓

Tri des rubriques

↓

Calcul des gains

↓

Calcul des retenues

↓

Calcul des cotisations

↓

Calcul IRG

↓

Calcul Net

↓

Contrôles

↓

Production du résultat
```

Chaque étape produit des traces exploitables pour l'audit.

---

# 7. Moteur d'éligibilité

Le moteur d'éligibilité décide quelles rubriques sont applicables.

Critères possibles :

* type de contrat ;
* type de personnel ;
* statut ;
* corps ;
* grade ;
* échelon ;
* fonction ;
* ancienneté ;
* établissement ;
* période de validité.

Aucune rubrique n'est calculée sans validation préalable.

---

# 8. Résolution des dépendances

Certaines rubriques dépendent d'autres rubriques.

Exemple :

```text
Salaire de base

↓

Indemnité A

↓

Indemnité B

↓

Salaire brut

↓

Cotisations

↓

IRG

↓

Net
```

Le moteur construit un **graphe orienté acyclique (DAG)** et applique un tri topologique afin de garantir un ordre de calcul valide. Toute dépendance circulaire est détectée avant l'exécution et bloque le calcul avec un diagnostic explicite.

---

# 9. Calculateurs spécialisés

Chaque famille de calcul est isolée.

| Calculateur              | Responsabilité  |
| ------------------------ | --------------- |
| SalaryCalculator         | Salaire de base |
| AllowanceCalculator      | Indemnités      |
| BonusCalculator          | Primes          |
| DeductionCalculator      | Retenues        |
| ContributionCalculator   | Cotisations     |
| TaxCalculator            | IRG             |
| NetCalculator            | Salaire net     |
| ReminderCalculator       | Rappels         |
| RegularizationCalculator | Régularisations |

---

# 10. Pipeline de calcul

```text
Pipeline

↓

Step 1

↓

Step 2

↓

Step 3

↓

…

↓

Step N
```

Chaque étape :

* reçoit un contexte ;
* produit un résultat ;
* enrichit le journal d'exécution ;
* ne modifie pas les données d'entrée.

---

# 11. Gestion des formules

Les formules ne sont jamais codées directement dans les ViewModels ou les Use Cases.

Elles sont :

* identifiées par un code ;
* documentées ;
* versionnées ;
* exécutées par le moteur.

Une formule peut utiliser :

* constantes réglementaires ;
* paramètres ;
* résultats intermédiaires ;
* variables mensuelles.

---

# 12. Paramètres réglementaires

Le moteur charge automatiquement les paramètres applicables à la période :

* valeur du point indiciaire ;
* barème IRG ;
* taux CNAS ;
* taux retraite ;
* plafonds ;
* seuils.

Tous les montants sont exprimés en **dinar algérien (DZD)**.

---

# 13. Gestion des périodes

Le moteur doit pouvoir calculer :

* une période courante ;
* une période antérieure ;
* un rappel ;
* une régularisation.

Le contexte réglementaire est reconstitué en fonction de la date d'effet.

---

# 14. Validation métier

Avant validation d'un bulletin, les contrôles suivants sont exécutés :

* agent actif ;
* contrat valide ;
* période ouverte ;
* référentiels disponibles ;
* paramètres présents ;
* cohérence des données de carrière ;
* absence d'anomalies bloquantes.

---

# 15. Explicabilité

Chaque montant produit est accompagné d'une justification.

Exemple de journal logique :

```text
Rubrique : Salaire de base

Source :
Grade : Professeur
Échelon : 5

Point indiciaire : version 2026-01

Indice majoré : XXX

Montant calculé : XXXX DZD
```

Ce mécanisme permettra, à terme, une fonctionnalité **"Pourquoi ce montant ?"** directement accessible depuis le bulletin.

---

# 16. Journal d'exécution

Le moteur produit un journal structuré contenant :

* identifiant du calcul ;
* version du moteur ;
* durée d'exécution ;
* règles appliquées ;
* paramètres utilisés ;
* résultats intermédiaires ;
* anomalies détectées.

Ce journal est exploitable pour les audits et les diagnostics.

---

# 17. Gestion des erreurs

Les erreurs sont classées selon leur gravité.

| Niveau          | Conséquence         |
| --------------- | ------------------- |
| Information     | Journal uniquement  |
| Avertissement   | Calcul poursuivi    |
| Erreur métier   | Bulletin non validé |
| Erreur critique | Arrêt du calcul     |

Les messages doivent être explicites et orientés métier.

---

# 18. Performances

Objectifs de référence :

| Traitement               | Objectif |
| ------------------------ | -------: |
| Construction du contexte | < 100 ms |
| Calcul individuel        |    < 3 s |
| Calcul de 500 agents     |  < 2 min |
| Génération du journal    |    < 1 s |

Ces objectifs seront validés lors des campagnes de tests.

---

# 19. Extensibilité

L'ajout d'une nouvelle rubrique doit nécessiter uniquement :

1. la création de la rubrique dans les référentiels ;
2. la définition de ses règles d'éligibilité ;
3. la déclaration de sa formule ;
4. son intégration dans le pipeline si nécessaire.

Le moteur doit éviter toute modification de code existant lorsque les évolutions peuvent être pilotées par les données (*Open/Closed Principle*).

---

# 20. Tests

Chaque composant du moteur dispose de :

* tests unitaires ;
* tests d'intégration ;
* jeux de données réglementaires ;
* scénarios de non-régression.

Un même jeu de données doit produire exactement les mêmes résultats, garantissant la reproductibilité.

---

# 21. Traçabilité

Chaque calcul est lié à :

* l'agent ;
* la période ;
* la version du moteur ;
* les paramètres réglementaires utilisés ;
* les règles métier appliquées ;
* le bulletin généré.

Cette traçabilité est essentielle pour les audits et les contrôles réglementaires.

---

# 22. Critères d'acceptation

Le moteur de calcul est conforme lorsque :

* les règles du **Volume 9** sont intégralement implémentées ;
* les dépendances entre rubriques sont résolues sans ambiguïté ;
* chaque résultat est explicable ;
* les paramètres sont historisés ;
* les performances respectent les objectifs ;
* les tests de non-régression sont validés.

---

# Conclusion

Le **Volume 13** constitue le **socle algorithmique** de **PaieEducation ERP**. Il définit l'architecture du moteur de calcul, la résolution des règles métier et les mécanismes garantissant la conformité réglementaire et la reproductibilité des résultats.

## Évolution proposée pour le Volume 14

Pour atteindre un niveau de documentation comparable aux ERP de référence, le **Volume 14** sera consacré à **l'architecture documentaire et au moteur de génération de rapports avec QuestPDF**. Il couvrira :

* l'architecture du module **Reporting** ;
* les gabarits de documents (bulletin de paie, attestations, états réglementaires) ;
* le système de styles et de thèmes ;
* la pagination, les en-têtes et pieds de page ;
* l'export PDF et Excel (avec **ClosedXML**) ;
* la gestion des impressions et des aperçus ;
* la traçabilité des documents générés.

Ce volume fera de **QuestPDF** un composant pleinement intégré à l'architecture modulaire de l'ERP, en remplacement définitif des solutions RDLC.


Excellente progression. À partir de ce volume, nous quittons l'architecture métier pour entrer dans la **chaîne documentaire**, indispensable à un ERP de paie. Le module de reporting n'est pas un simple export PDF : il devient un **moteur documentaire** capable de produire des documents réglementaires, administratifs et décisionnels.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome D — Architecture Technique**

# **Volume 14**

# **Architecture du Reporting et du Moteur Documentaire (QuestPDF & ClosedXML)**

**Version :** 2.0
**Statut :** Référentiel officiel du module Reporting

---

# 1. Objet du document

Le module **Reporting** est chargé de la génération de tous les documents produits par **PaieEducation ERP**.

Il couvre :

* les bulletins de paie ;
* les états réglementaires ;
* les attestations administratives ;
* les rapports de contrôle ;
* les exports Excel ;
* les impressions.

Les objectifs sont :

* conformité réglementaire ;
* qualité typographique ;
* performances ;
* reproductibilité ;
* séparation stricte entre données et présentation.

---

# 2. Position dans l'architecture

```text
Presentation (WPF)
        │
        ▼
Application Layer
        │
        ▼
Reporting Layer
        │
 ┌──────┴────────┐
 ▼               ▼
QuestPDF     ClosedXML
        │
        ▼
PDF / XLSX
```

Le module Reporting ne calcule jamais les données ; il consomme exclusivement des DTO préparés par la couche **Application**.

---

# 3. Organisation du projet

```text
PaieEducation.Reporting

├── Documents
├── Templates
├── Components
├── Styles
├── Fonts
├── Images
├── Pdf
├── Excel
├── Printers
├── Preview
└── Utilities
```

Chaque dossier correspond à une responsabilité précise.

---

# 4. Technologies retenues

| Fonction              | Technologie                            |
| --------------------- | -------------------------------------- |
| PDF                   | QuestPDF                               |
| Excel                 | ClosedXML                              |
| Impression            | PrintDialog WPF                        |
| Aperçu                | Visionneuse PDF intégrée               |
| Images                | PNG / SVG                              |
| Codes-barres (option) | Bibliothèque dédiée compatible .NET 10 |
| QR Codes (option)     | Bibliothèque dédiée compatible .NET 10 |

---

# 5. Catalogue documentaire

Le système prend en charge les catégories suivantes.

## Documents de paie

* Bulletin individuel
* Bulletin réédité
* Détail des rubriques
* Rappels
* Régularisations

## Documents RH

* Attestation de travail
* Attestation de salaire
* Décision administrative
* Certificat de présence

## États réglementaires

* Livre de paie
* Journal de paie
* États de cotisations
* Synthèse par établissement
* Synthèse par corps
* Synthèse budgétaire

## Exports

* Excel
* PDF
* CSV (si nécessaire)

---

# 6. Architecture documentaire

Chaque document est construit selon une structure commune.

```text
Document

├── Header
├── Body
├── Footer
├── Signature
├── Pagination
└── Métadonnées
```

Cette homogénéité facilite la maintenance et garantit une identité visuelle cohérente.

---

# 7. Pipeline de génération

```text
Application Service
        │
        ▼
DTO
        │
        ▼
Document Builder
        │
        ▼
QuestPDF Template
        │
        ▼
PDF
```

Les données sont figées avant la génération afin d'assurer la reproductibilité.

---

# 8. Modèle de document

Chaque document est composé de sections réutilisables.

| Section                    | Réutilisable |
| -------------------------- | ------------ |
| En-tête                    | Oui          |
| Informations établissement | Oui          |
| Informations agent         | Oui          |
| Tableau principal          | Oui          |
| Totaux                     | Oui          |
| Signatures                 | Oui          |
| Pied de page               | Oui          |

Les composants sont mutualisés afin d'éviter les duplications.

---

# 9. Système de styles

Tous les styles sont centralisés.

## Typographie

* Police principale : Inter ou Segoe UI
* Police de secours : Aptos

## Couleurs

* Bleu institutionnel
* Gris neutre
* Noir
* Rouge (alertes)

## Éléments

* Titres
* Sous-titres
* Tableaux
* Bordures
* Totaux
* Signatures

Aucun style ne doit être défini directement dans un document.

---

# 10. Bulletin de paie

Le bulletin comprend notamment :

* informations de l'établissement ;
* informations de l'agent ;
* période ;
* rubriques de gains ;
* rubriques de retenues ;
* cotisations ;
* IRG ;
* salaire net ;
* observations ;
* signatures.

Les montants sont exprimés exclusivement en **dinar algérien (DZD)**.

---

# 11. Attestations

Toutes les attestations suivent une structure uniforme.

```text
Logo

↓

Établissement

↓

Titre

↓

Corps du document

↓

Signature

↓

Cachet
```

Le contenu est alimenté à partir des données validées de l'ERP.

---

# 12. États tabulaires

Les états volumineux utilisent :

* pagination automatique ;
* répétition des en-têtes ;
* totaux intermédiaires ;
* totaux généraux ;
* regroupements hiérarchiques ;
* orientation portrait ou paysage selon le cas.

---

# 13. Export Excel (ClosedXML)

Les exports Excel sont générés sans dépendre de Microsoft Office.

Fonctionnalités :

* feuilles multiples ;
* styles ;
* formats numériques ;
* filtres ;
* tris ;
* gel des volets ;
* ajustement automatique des colonnes.

---

# 14. Aperçu avant impression

L'application fournit :

* zoom ;
* rotation ;
* navigation par pages ;
* recherche textuelle (si prise en charge par le visualiseur) ;
* impression directe.

---

# 15. Impression

L'impression est pilotée par un service dédié.

Fonctions :

* choix de l'imprimante ;
* nombre de copies ;
* sélection des pages ;
* orientation ;
* format papier (A4 principalement).

---

# 16. Métadonnées des documents

Chaque document embarque des informations techniques :

* identifiant ;
* type ;
* version du modèle ;
* date de génération ;
* auteur (ou processus) ;
* version de l'application.

Ces métadonnées facilitent les audits.

---

# 17. Archivage

Les documents générés peuvent être :

* enregistrés localement ;
* réédités à partir des données validées ;
* archivés dans une arborescence configurable.

Le moteur documentaire ne modifie jamais les données métier.

---

# 18. Performances

Objectifs :

| Document                   | Temps cible |
| -------------------------- | ----------: |
| Bulletin individuel        |       < 1 s |
| Attestation                |       < 1 s |
| Livre de paie (500 agents) |      < 30 s |
| Export Excel (500 agents)  |      < 15 s |

Les performances sont mesurées hors temps d'impression.

---

# 19. Extensibilité

L'ajout d'un nouveau document doit nécessiter :

1. la création d'un DTO spécifique ;
2. un modèle QuestPDF ;
3. son enregistrement dans le catalogue documentaire ;
4. son exposition via un service applicatif.

Aucune modification des documents existants ne doit être requise.

---

# 20. Traçabilité

Chaque document est lié à :

* un agent ou une période ;
* un modèle de document ;
* une version ;
* une date de génération ;
* un journal d'exécution.

Cette traçabilité garantit la reproductibilité des éditions.

---

# 21. Critères d'acceptation

Le module Reporting est conforme lorsque :

* tous les documents réglementaires sont générés via QuestPDF ;
* tous les exports Excel utilisent ClosedXML ;
* les styles sont centralisés ;
* les modèles sont réutilisables ;
* les documents sont reproductibles ;
* les performances respectent les objectifs définis.

---

# 22. Gouvernance documentaire

Toute évolution d'un modèle de document doit respecter les règles suivantes :

* versionnement du modèle ;
* validation fonctionnelle préalable ;
* conservation des anciennes versions si elles concernent des documents réglementaires ;
* documentation des changements.

Cette gouvernance évite les divergences entre les documents générés au fil des évolutions.

---

# Conclusion

Le **Volume 14** formalise l'architecture complète du moteur documentaire de **PaieEducation ERP**. Il garantit une production homogène, performante et conforme des bulletins, attestations, états réglementaires et exports, tout en exploitant pleinement **QuestPDF** et **ClosedXML**.

## Orientation pour le Volume 15

Le **Volume 15** sera consacré à **la sécurité, à la journalisation, à la sauvegarde et à la résilience de l'application**. Il définira notamment :

* la politique de sécurité de l'ERP ;
* la gestion des utilisateurs et des rôles (activable ou désactivable selon le déploiement) ;
* la journalisation technique et fonctionnelle (`Microsoft.Extensions.Logging`) ;
* les mécanismes de sauvegarde et de restauration de la base SQLite ;
* la reprise après incident ;
* les stratégies de continuité d'activité et de protection des données.

Ce volume établira le cadre d'exploitation sécurisé de l'application en environnement **100 % local et hors ligne**.


Excellente progression. Le **Volume 15** est stratégique : il définit les mécanismes qui garantissent la **fiabilité**, la **sécurité**, la **traçabilité** et la **résilience** de l'ERP. Pour un logiciel de paie, ces aspects sont aussi importants que le moteur de calcul lui-même.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome D — Architecture Technique**

# **Volume 15**

# **Sécurité, Journalisation, Sauvegarde et Résilience**

**Version :** 2.0
**Statut :** Référentiel officiel des services transverses

---

# 1. Objet du document

Ce volume définit les mécanismes transverses qui assurent :

* la sécurité de l'application ;
* la protection des données ;
* la journalisation technique et fonctionnelle ;
* la sauvegarde de la base SQLite ;
* la restauration ;
* la reprise après incident ;
* la continuité de fonctionnement.

Ces mécanismes sont applicables à tous les modules de **PaieEducation ERP**.

---

# 2. Architecture des services transverses

```text
                 WPF

                  │

          Application Layer

                  │

        ┌─────────┼──────────┐
        │         │          │
        ▼         ▼          ▼

 Logging   Security   Backup Service

        │         │          │

        └─────────┼──────────┘

                  ▼

             Infrastructure

                  ▼

               SQLite
```

Les services transverses sont indépendants du moteur de calcul.

---

# 3. Principes de sécurité

Le logiciel doit respecter les principes suivants :

* moindre privilège ;
* séparation des responsabilités ;
* traçabilité des actions ;
* intégrité des données ;
* confidentialité des informations ;
* disponibilité des données.

---

# 4. Gestion des utilisateurs

L'ERP doit pouvoir fonctionner dans deux modes.

## Mode autonome

* aucun utilisateur
* ouverture immédiate
* adapté aux petits établissements

## Mode sécurisé

* authentification
* rôles
* permissions
* historique des connexions

Le mode est activé par configuration.

---

# 5. Gestion des rôles

Exemple de rôles.

| Rôle              | Responsabilités     |
| ----------------- | ------------------- |
| Administrateur    | Paramétrage complet |
| Gestionnaire RH   | Dossiers agents     |
| Gestionnaire Paie | Calcul et bulletins |
| Consultation      | Lecture seule       |
| Audit             | Accès aux journaux  |

L'architecture doit permettre l'ajout de nouveaux rôles sans modification du code métier.

---

# 6. Modèle d'autorisation

Les droits sont attribués :

```text
Utilisateur

↓

Rôle

↓

Permission

↓

Fonction
```

Une permission correspond à une action métier.

Exemples :

* Calculer une paie
* Modifier un agent
* Supprimer une rubrique
* Clôturer une période
* Restaurer une sauvegarde

---

# 7. Journalisation technique

La journalisation est assurée via :

**Microsoft.Extensions.Logging**

Les fournisseurs de journalisation doivent être remplaçables.

---

# 8. Niveaux de journalisation

| Niveau      | Utilisation                |
| ----------- | -------------------------- |
| Trace       | Diagnostic détaillé        |
| Debug       | Développement              |
| Information | Activité normale           |
| Warning     | Anomalie non bloquante     |
| Error       | Erreur métier ou technique |
| Critical    | Arrêt du système           |

En production, le niveau minimal recommandé est **Information**.

---

# 9. Journalisation métier

En complément des logs techniques, l'ERP conserve un journal fonctionnel.

Exemples :

* création d'un agent ;
* modification d'un contrat ;
* lancement d'un calcul ;
* validation d'un bulletin ;
* clôture d'une période ;
* génération d'un document.

Chaque événement est horodaté et associé à son contexte.

---

# 10. Format des journaux

Chaque entrée contient :

* identifiant ;
* date/heure ;
* niveau ;
* module ;
* utilisateur (si activé) ;
* opération ;
* résultat ;
* message ;
* détails techniques (si nécessaire).

Les journaux sont exploitables pour les audits et le support.

---

# 11. Gestion des exceptions

Les exceptions sont centralisées.

Flux de traitement :

```text
Exception

↓

Exception Handler

↓

Logging

↓

Notification utilisateur

↓

Journal d'audit
```

Les messages affichés à l'utilisateur restent compréhensibles ; les détails techniques sont consignés dans les journaux.

---

# 12. Sauvegarde de la base SQLite

Le service de sauvegarde doit permettre :

* sauvegarde manuelle ;
* sauvegarde programmée (optionnelle) ;
* sauvegarde avant migration ;
* sauvegarde avant clôture de période.

Les sauvegardes sont réalisées sur une copie cohérente de la base.

---

# 13. Stratégie de sauvegarde

Format recommandé :

```text
PaieEducation_YYYYMMDD_HHMMSS.db
```

Exemple :

```text
PaieEducation_20260712_183000.db
```

Chaque sauvegarde est accompagnée de métadonnées :

* version du schéma ;
* version de l'application ;
* taille ;
* somme de contrôle (checksum).

---

# 14. Restauration

Avant toute restauration :

1. vérification du fichier ;
2. contrôle de la version du schéma ;
3. validation de l'intégrité SQLite ;
4. confirmation utilisateur.

Après restauration :

* rechargement des paramètres ;
* relecture des référentiels ;
* reprise des services.

---

# 15. Vérification d'intégrité

Avant l'ouverture de la base :

* contrôle d'existence ;
* contrôle des migrations ;
* vérification des clés étrangères ;
* contrôle de l'intégrité (`PRAGMA integrity_check`) ;
* vérification de l'espace disque disponible.

Toute anomalie bloque l'ouverture si elle compromet la cohérence des données.

---

# 16. Reprise après incident

Le système doit être capable de reprendre après :

* arrêt brutal ;
* coupure électrique ;
* fermeture inattendue ;
* erreur système.

Grâce au mode **WAL** de SQLite et aux transactions atomiques, la cohérence des données est préservée.

---

# 17. Politique de récupération

Les traitements interrompus sont classés comme :

| Cas                        | Action                 |
| -------------------------- | ---------------------- |
| Calcul non validé          | Annulation automatique |
| Bulletin validé            | Conservation           |
| Génération PDF interrompue | Reprise à la demande   |
| Export Excel interrompu    | Recréation             |

---

# 18. Protection des fichiers

Les fichiers suivants sont protégés :

* base SQLite ;
* sauvegardes ;
* journaux ;
* paramètres ;
* modèles documentaires.

Les chemins d'accès sont configurables et vérifiés.

---

# 19. Audit des opérations sensibles

Les actions suivantes sont systématiquement auditées :

* suppression logique ;
* restauration de sauvegarde ;
* modification d'un paramètre réglementaire ;
* clôture ou réouverture de période ;
* recalcul d'un bulletin validé.

L'audit inclut l'identité de l'utilisateur si le mode sécurisé est activé.

---

# 20. Supervision

Le système expose un tableau de bord technique comprenant :

* état de la base ;
* nombre de sauvegardes ;
* espace disque disponible ;
* version de l'application ;
* version du schéma ;
* derniers événements critiques.

---

# 21. Performances des services transverses

Objectifs :

| Service                    | Temps cible |
| -------------------------- | ----------: |
| Sauvegarde (base ≤ 500 Mo) |      < 15 s |
| Restauration               |      < 20 s |
| Vérification d'intégrité   |       < 5 s |
| Écriture d'un journal      |     < 10 ms |

Ces valeurs sont indicatives et devront être validées sur la plateforme cible.

---

# 22. Continuité d'exploitation

Les services critiques doivent rester disponibles même en cas de défaillance d'un module non essentiel.

Exemples :

* une erreur d'impression ne bloque pas le calcul de la paie ;
* un échec d'export Excel n'affecte pas la validation d'un bulletin.

Cette isolation limite les impacts des incidents.

---

# 23. Conformité et confidentialité

Le système doit permettre :

* la conservation des données conformément aux exigences réglementaires applicables ;
* la limitation des accès aux informations sensibles ;
* la traçabilité des modifications ;
* la suppression logique lorsque l'historique doit être préservé.

---

# 24. Critères d'acceptation

Les services transverses sont conformes lorsque :

* tous les événements critiques sont journalisés ;
* les sauvegardes sont fiables et vérifiables ;
* la restauration est reproductible ;
* les contrôles d'intégrité sont automatisés ;
* les erreurs sont centralisées ;
* les performances respectent les objectifs.

---

# Conclusion

Le **Volume 15** définit le cadre de sécurité et de résilience de **PaieEducation ERP**. Il garantit la protection des données, la traçabilité des opérations et la continuité de fonctionnement dans un environnement **100 % local**, conformément aux choix technologiques du projet (.NET 10 LTS, WPF, SQLite).

## Orientation pour le Volume 16

Le **Volume 16** sera consacré à la **stratégie complète de migration de l'application VB.NET vers .NET 10**. Il couvrira :

* la cartographie des modules existants ;
* les règles de migration du code VB.NET vers C# ;
* la reprise des règles métier ;
* la migration des données Access vers SQLite ;
* les critères de validation fonctionnelle ;
* la stratégie de coexistence et de bascule ;
* les risques, les mesures d'atténuation et le plan de déploiement progressif.

Ce volume constituera le guide de référence pour conduire la modernisation de l'application existante vers la nouvelle architecture.

Excellente progression. Nous entrons maintenant dans un volume qui n'existe généralement pas dans les petits projets, mais qui est indispensable pour une **migration ERP** de cette ampleur.

Le **Volume 16** constitue le **plan directeur de migration**. Son objectif n'est pas seulement de convertir du code VB.NET vers C#, mais de garantir une transition maîtrisée, traçable et sans régression fonctionnelle.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 2.0**

**Tome E — Migration et Modernisation**

# **Volume 16**

# **Stratégie de Migration de VB.NET vers .NET 10 LTS**

**Version :** 2.0
**Statut :** Référentiel officiel de migration

---

# 1. Objet du document

Ce volume définit la stratégie de migration de l'application **PaieEducation_VB.NET** vers **PaieEducation ERP** reposant sur la pile technologique suivante :

* .NET 10 LTS
* C#
* WPF
* MVVM
* SQLite
* CommunityToolkit.Mvvm
* Microsoft.Extensions.DependencyInjection
* Microsoft.Extensions.Logging
* QuestPDF
* ClosedXML

L'objectif est de garantir une modernisation sans perte fonctionnelle, tout en améliorant l'architecture, la maintenabilité et les performances.

---

# 2. Principes de migration

La migration respecte les principes suivants :

* continuité fonctionnelle ;
* migration incrémentale ;
* absence de réécriture "big bang" ;
* validation à chaque étape ;
* conservation des règles métier validées ;
* amélioration de l'architecture sans modifier le comportement attendu.

---

# 3. Inventaire du patrimoine applicatif

Les éléments de l'application existante sont classés en catégories.

| Catégorie              | Action                              |
| ---------------------- | ----------------------------------- |
| Règles métier validées | Migrer et moderniser                |
| Interfaces WinForms    | Remplacer par WPF                   |
| Requêtes Access        | Adapter à SQLite                    |
| Rapports RDLC          | Remplacer par QuestPDF              |
| Exports Excel          | Remplacer par ClosedXML             |
| Utilitaires            | Réévaluer puis migrer si pertinents |
| Code obsolète          | Supprimer après validation          |

Chaque composant fait l'objet d'une fiche de migration.

---

# 4. Cartographie des modules

| Application d'origine | Nouveau module          |
| --------------------- | ----------------------- |
| Gestion des agents    | RH                      |
| Carrière              | Carrière                |
| Paramètres            | Référentiels            |
| Calcul de la paie     | Domain / Payroll Engine |
| Bulletins             | Reporting               |
| Impression            | Reporting               |
| Sauvegarde            | Infrastructure          |
| Outils                | Maintenance             |

Cette cartographie permet de suivre l'avancement de la migration.

---

# 5. Migration des interfaces

Toutes les interfaces WinForms sont remplacées par des vues WPF selon les principes MVVM.

Règles :

* aucune réutilisation directe des formulaires WinForms ;
* séparation stricte Vue / ViewModel ;
* utilisation de contrôles WPF réutilisables ;
* navigation centralisée.

L'ergonomie peut être améliorée sans altérer les fonctionnalités.

---

# 6. Migration du code métier

Le code métier est analysé selon trois cas.

### Cas 1 : Code conforme

Migration quasi directe vers la couche **Domain**.

### Cas 2 : Code mêlant UI et métier

Extraction des règles métier avant migration.

### Cas 3 : Code obsolète ou redondant

Suppression après validation fonctionnelle.

Aucune logique métier ne doit rester dans la couche Presentation.

---

# 7. Migration de la base de données

La migration Access → SQLite comprend :

1. analyse du schéma existant ;
2. création du schéma SQLite ;
3. conversion des données ;
4. contrôle de cohérence ;
5. validation fonctionnelle.

Les identifiants fonctionnels (codes métier) sont conservés.

---

# 8. Migration des données

Les données sont migrées dans l'ordre suivant :

1. référentiels ;
2. établissements ;
3. agents ;
4. carrière ;
5. contrats ;
6. variables ;
7. paramètres réglementaires ;
8. bulletins ;
9. historiques.

Chaque étape fait l'objet d'un rapport de migration.

---

# 9. Migration des rapports

Tous les rapports RDLC sont remplacés par des modèles QuestPDF.

Correspondances :

| Ancien           | Nouveau              |
| ---------------- | -------------------- |
| Bulletin RDLC    | Bulletin QuestPDF    |
| Attestation RDLC | Attestation QuestPDF |
| États RDLC       | Rapports QuestPDF    |

Les mises en page sont modernisées tout en conservant les informations réglementaires.

---

# 10. Migration des exports

Les exports existants sont remplacés par **ClosedXML**.

Fonctionnalités conservées :

* format XLSX ;
* styles ;
* filtres ;
* formules si nécessaire.

---

# 11. Validation de la migration

Chaque module migré est validé selon trois niveaux.

### Validation technique

* compilation ;
* dépendances ;
* performances.

### Validation fonctionnelle

* conformité des traitements ;
* comparaison avec l'application d'origine.

### Validation réglementaire

* vérification des calculs ;
* contrôle des états produits.

---

# 12. Comparaison des résultats

Pour chaque scénario de test :

* même agent ;
* même période ;
* mêmes paramètres.

Les résultats attendus sont :

* mêmes montants ;
* mêmes rubriques ;
* mêmes retenues ;
* mêmes totaux.

Toute divergence est analysée et documentée.

---

# 13. Gestion des écarts

Les écarts sont classés selon leur origine.

| Origine                 | Traitement                        |
| ----------------------- | --------------------------------- |
| Bug historique          | Corriger dans la nouvelle version |
| Différence d'arrondi    | Documenter et valider             |
| Évolution réglementaire | Adapter le moteur                 |
| Régression              | Corriger avant mise en production |

---

# 14. Coexistence

Pendant la migration, les deux applications peuvent coexister.

```text id="y9k7m1"
VB.NET (production)
        │
        ├── Calcul de référence
        │
        ▼
.NET 10 ERP (validation)
```

Cette coexistence permet des comparaisons sécurisées avant la bascule.

---

# 15. Plan de bascule

La mise en production suit les étapes suivantes :

1. sauvegarde complète de l'application existante ;
2. export des données ;
3. migration vers SQLite ;
4. validation technique ;
5. validation fonctionnelle ;
6. formation des utilisateurs ;
7. mise en service ;
8. période de surveillance renforcée.

---

# 16. Gestion des risques

| Risque                     | Mesure d'atténuation                                 |
| -------------------------- | ---------------------------------------------------- |
| Perte de données           | Sauvegardes multiples et tests de restauration       |
| Régression métier          | Tests de non-régression automatisés                  |
| Écart de calcul            | Comparaison systématique avec l'ancienne application |
| Performances insuffisantes | Campagnes de tests et optimisation                   |
| Résistance au changement   | Formation et documentation                           |

---

# 17. Critères de réussite

La migration est considérée comme réussie lorsque :

* toutes les fonctionnalités prévues sont disponibles ;
* les calculs sont conformes aux règles réglementaires ;
* les données sont intégralement reprises ;
* les performances répondent aux objectifs ;
* les utilisateurs valident la nouvelle application.

---

# 18. Gouvernance de la migration

La migration est pilotée selon les rôles suivants :

| Rôle                 | Responsabilité             |
| -------------------- | -------------------------- |
| Chef de projet       | Planification et suivi     |
| Architecte logiciel  | Validation technique       |
| Expert métier        | Validation réglementaire   |
| Développeur          | Implémentation             |
| Testeur              | Vérification fonctionnelle |
| Utilisateur référent | Recette métier             |

---

# 19. Indicateurs de suivi

Les principaux indicateurs sont :

* pourcentage de modules migrés ;
* taux de couverture des tests ;
* nombre d'écarts détectés ;
* nombre d'écarts corrigés ;
* temps moyen de migration d'un module ;
* taux de conformité des calculs.

Ces indicateurs alimentent le tableau de bord du projet.

---

# 20. Critères d'acceptation

Le processus de migration est conforme lorsque :

* chaque module dispose d'une fiche de migration ;
* les données sont transférées sans perte ;
* les résultats sont comparables à ceux de l'application d'origine ;
* les écarts sont documentés et validés ;
* la bascule est réversible jusqu'à la validation finale.

---

# Conclusion

Le **Volume 16** fournit une stratégie complète de migration vers **.NET 10 LTS**. Il transforme la modernisation de **PaieEducation_VB.NET** en un projet maîtrisé, progressif et mesurable, limitant les risques tout en préservant le patrimoine métier.

## Orientation pour le Volume 17

Le **Volume 17** sera consacré à la **stratégie de qualité logicielle et de tests**. Il décrira :

* la pyramide de tests (unitaires, intégration, fonctionnels) ;
* les jeux de données de référence ;
* les scénarios de non-régression de la paie ;
* les tests de performance et de charge ;
* les critères d'acceptation par module ;
* la stratégie de recette utilisateur.

Ce volume complétera la documentation en garantissant que chaque évolution de l'ERP puisse être validée de manière reproductible et conforme aux exigences réglementaires.



