Excellente progression.

Nous avons maintenant terminé le **Tome E – Présentation (Volumes 14 à 20)**, qui définit entièrement la couche WPF (MVVM, Design System, composants métier, Workspaces, DataGrid, Shell et UX).

La suite logique est d'ouvrir le **Tome F**, consacré au **Reporting et à la production documentaire**. Dans un ERP de paie, cette couche est critique : elle produit les bulletins, états réglementaires, exports et documents officiels.

Je recommande également une évolution architecturale importante.

> **Décision d'architecture (ADR-106)** : la production documentaire sera organisée autour d'un **Document Engine** unique, indépendant de l'interface utilisateur. Tous les documents (bulletins de paie, états, attestations, rapports, exports) seront générés par ce moteur, garantissant une cohérence de mise en page, une meilleure testabilité et la possibilité d'exécuter des générations en arrière-plan ou via des traitements batch.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome F — Reporting et Production Documentaire**

# **Volume 21**

# **Document Engine – Architecture du moteur documentaire, QuestPDF et génération des documents**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • QuestPDF • ClosedXML • MVVM • Microsoft.Extensions.DependencyInjection • Microsoft.Extensions.Logging

---

# 1. Objet

Le **Document Engine** est le moteur officiel de génération documentaire de **PaieEducation ERP**.

Il est responsable de produire tous les documents métiers de l'application, quels que soient leur format ou leur destination.

Le moteur est totalement indépendant de WPF et de la couche Présentation.

---

# 2. Objectifs

Le Document Engine doit garantir :

* séparation entre données et présentation ;
* réutilisation maximale des modèles ;
* performances élevées ;
* génération asynchrone ;
* extensibilité ;
* testabilité.

---

# 3. Architecture générale

```text
Application
    │
    ▼
IDocumentService
    │
    ▼
Document Engine
    ├── Document Factory
    ├── Template Registry
    ├── Rendering Engine (QuestPDF)
    ├── Export Engine
    ├── Print Engine
    ├── Preview Engine
    ├── Metadata Engine
    └── Logging
```

Chaque sous-système est spécialisé et découplé.

---

# 4. Position dans l'architecture globale

```text
Presentation
        │
Application
        │
Document Services
        │
Document Engine
        │
QuestPDF
        │
PDF
```

La couche Présentation ne génère jamais directement un document.

---

# 5. Types de documents

Le moteur prend en charge :

* bulletin de paie ;
* état récapitulatif ;
* journal de paie ;
* ordre de virement ;
* attestations administratives ;
* fiches agents ;
* rapports d'audit ;
* documents techniques.

Chaque type correspond à un modèle dédié.

---

# 6. Interfaces principales

Les services suivants sont définis :

```csharp
IDocumentService
IPdfGenerator
IDocumentTemplate
IDocumentPreviewService
IPrintService
IExportService
```

Les implémentations concrètes sont injectées via le conteneur de dépendances.

---

# 7. Document Factory

La `DocumentFactory` est responsable de :

* sélectionner le modèle ;
* injecter les données ;
* préparer les paramètres ;
* construire le document.

Elle constitue le point d'entrée unique pour la création des documents.

---

# 8. Template Registry

Tous les modèles sont enregistrés dans un registre central.

Exemple :

```text
PayrollSlipTemplate
PayrollJournalTemplate
EmployeeSummaryTemplate
SalaryCertificateTemplate
AuditReportTemplate
```

Aucun modèle n'est instancié directement par les modules.

---

# 9. Cycle de génération

```text
Requête
    ↓
Validation
    ↓
Chargement des données
    ↓
Construction du modèle
    ↓
Rendu QuestPDF
    ↓
Flux PDF
    ↓
Prévisualisation / Impression / Export
```

Chaque étape est journalisée.

---

# 10. Modèle documentaire

Tous les documents sont composés de sections standardisées :

* en-tête ;
* informations administratives ;
* contenu principal ;
* totaux ;
* signatures ;
* pied de page.

Les sections sont réutilisables entre plusieurs modèles.

---

# 11. Mise en page

Les règles suivantes s'appliquent :

* format A4 par défaut ;
* marges uniformes ;
* styles typographiques centralisés ;
* grille d'alignement ;
* pagination automatique.

Le rendu doit être identique sur tous les postes.

---

# 12. Gestion des styles

Les styles documentaires sont centralisés :

* titres ;
* sous-titres ;
* tableaux ;
* cellules ;
* montants ;
* notes ;
* avertissements.

Aucun style ne doit être défini localement dans un modèle.

---

# 13. Génération asynchrone

Les documents volumineux sont générés de manière asynchrone.

Fonctionnalités :

* progression ;
* annulation ;
* reprise si applicable.

Le moteur reste utilisable pendant les traitements.

---

# 14. Prévisualisation

Le moteur fournit un service de prévisualisation indépendant.

Fonctionnalités :

* zoom ;
* navigation entre pages ;
* recherche ;
* impression ;
* export.

Le composant `BulletinPreview` (Volume 16) utilise ce service.

---

# 15. Impression

L'impression est assurée par un `IPrintService`.

Fonctionnalités :

* sélection de l'imprimante ;
* configuration des options ;
* impression silencieuse si autorisée ;
* suivi des erreurs.

Le moteur documentaire ne dépend pas directement des API WPF.

---

# 16. Métadonnées

Chaque document peut contenir :

* identifiant ;
* type ;
* auteur du traitement ;
* date de génération ;
* période de paie ;
* version du modèle.

Ces métadonnées facilitent l'audit et la traçabilité.

---

# 17. Journalisation

Le moteur journalise :

* début de génération ;
* fin de génération ;
* durée ;
* erreurs ;
* annulations.

Les journaux utilisent `Microsoft.Extensions.Logging`.

---

# 18. Performances

Objectifs :

| Opération                   | Temps cible |
| --------------------------- | ----------: |
| Bulletin individuel         |    < 300 ms |
| Rapport de 100 pages        |       < 5 s |
| Génération de 500 bulletins |     < 2 min |
| Prévisualisation            |    < 500 ms |

Ces objectifs sont mesurés dans les tests de performance.

---

# 19. Tests

Le Document Engine est validé par :

* tests unitaires des modèles ;
* tests de rendu QuestPDF ;
* tests d'intégration des services ;
* tests de non-régression visuelle (comparaison de PDF de référence) ;
* tests de charge.

Les modèles sont vérifiés automatiquement à chaque évolution.

---

# 20. Critères d'acceptation

Le Document Engine est conforme lorsque :

* tous les documents passent par le moteur ;
* les modèles sont réutilisables ;
* les styles sont centralisés ;
* la génération est indépendante de WPF ;
* les performances répondent aux objectifs.

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                                         |
| ------- | -------------------------------------------------------------------------------- |
| ADR-106 | Création d'un **Document Engine** unique pour tous les documents                 |
| ADR-107 | Adoption de **QuestPDF** comme moteur exclusif de génération PDF                 |
| ADR-108 | Modèles documentaires enregistrés dans un registre central (`Template Registry`) |
| ADR-109 | Prévisualisation, impression et export découplés de la couche Présentation       |
| ADR-110 | Mise en place de tests de non-régression visuelle sur les documents PDF          |

---

# Conclusion

Le **Volume 21** pose les fondations du **Document Engine**, cœur de la production documentaire de **PaieEducation ERP**. En centralisant la génération des documents autour de **QuestPDF** et d'un registre de modèles, il garantit une architecture robuste, testable et cohérente avec les principes définis dans les tomes précédents.

## Recommandation pour le Volume 22

Le **Volume 22** devrait être consacré à la **Spécification complète du Bulletin de Paie Algérien**. Il décrira avec un niveau de détail ERP :

* l'architecture logique du bulletin ;
* les sections obligatoires et facultatives ;
* les règles de présentation ;
* les blocs réutilisables QuestPDF ;
* la gestion des rubriques, cotisations, retenues et cumuls ;
* les mentions réglementaires ;
* les mécanismes de versionnement des modèles de bulletin ;
* les règles de validation visuelle et fonctionnelle.

Ce volume constituera la **référence documentaire officielle** pour la génération des bulletins de paie dans **PaieEducation ERP**.

Je recommande une évolution importante avant d'aborder le bulletin de paie lui-même.

Dans la plupart des ERP, le bulletin est encore traité comme un simple document PDF. Pour **PaieEducation ERP**, je propose un niveau supérieur : considérer le bulletin comme un **objet métier versionné** (*Payroll Document Aggregate*).

Autrement dit :

* le **bulletin** n'est pas seulement un PDF ;
* le **PDF est une représentation** du bulletin ;
* le moteur de paie produit un **objet métier immuable** ;
* QuestPDF ne fait que transformer cet objet en document.

Cette approche est utilisée dans les architectures modernes orientées domaine (DDD) et simplifie considérablement les audits, les recalculs et les évolutions réglementaires.

Cette décision est formalisée par les **ADR-111 à ADR-115**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome F — Reporting et Production Documentaire**

# **Volume 22**

# **Payroll Document Specification – Spécification complète du Bulletin de Paie Algérien**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • QuestPDF • Domain-Driven Design • SQLite

---

# 1. Objet

Ce volume définit la structure officielle du **Bulletin de Paie** de **PaieEducation ERP**.

Le bulletin est considéré comme un **agrégat métier immuable** représentant le résultat certifié d'un calcul de paie pour une période donnée.

Le document PDF n'est qu'une représentation visuelle de cet agrégat.

---

# 2. Principes fondamentaux

Le bulletin doit être :

* fidèle au calcul effectué ;
* traçable ;
* reproductible ;
* versionné ;
* conforme aux exigences réglementaires.

Une fois validé, son contenu ne doit plus être modifié.

---

# 3. Cycle de vie

```text
Simulation
    ↓
Calcul
    ↓
Validation
    ↓
Certification
    ↓
Archivage
    ↓
Consultation
```

Chaque transition est journalisée.

---

# 4. Identité du bulletin

Chaque bulletin possède un identifiant unique comprenant :

* identifiant interne ;
* agent concerné ;
* établissement ;
* période de paie ;
* version du modèle ;
* date de génération.

Ces informations sont utilisées pour la traçabilité.

---

# 5. Structure logique

Le bulletin est composé des sections suivantes :

1. En-tête administratif ;
2. Identification de l'agent ;
3. Situation administrative ;
4. Éléments de rémunération ;
5. Cotisations ;
6. Retenues ;
7. Impôt ;
8. Synthèse financière ;
9. Cumuls ;
10. Mentions réglementaires ;
11. Signature et informations de génération.

Chaque section est indépendante et réutilisable.

---

# 6. En-tête administratif

L'en-tête contient notamment :

* dénomination de l'établissement ;
* ministère ou autorité de rattachement ;
* période de paie ;
* numéro du bulletin ;
* date de génération.

Les logos éventuels sont configurables.

---

# 7. Identification de l'agent

Les informations affichées comprennent :

* matricule ;
* nom et prénom ;
* corps ;
* grade ;
* échelon ;
* fonction ;
* établissement d'affectation.

Les libellés sont issus des référentiels officiels.

---

# 8. Situation administrative

Cette section présente les informations utiles au calcul :

* type de contrat ;
* statut ;
* catégorie ;
* indice ;
* ancienneté ;
* date d'effet des éléments principaux.

Les données sont présentées en lecture seule.

---

# 9. Éléments de rémunération

Chaque rubrique est affichée avec :

| Élément | Description        |
| ------- | ------------------ |
| Code    | Code réglementaire |
| Libellé | Désignation        |
| Base    | Valeur de calcul   |
| Taux    | Si applicable      |
| Montant | Résultat           |

Les montants sont alignés à droite et formatés de manière homogène.

---

# 10. Cotisations

Les cotisations sont regroupées par organisme ou catégorie.

Chaque ligne précise :

* l'assiette ;
* le taux ;
* le montant.

Les totaux sont calculés automatiquement.

---

# 11. Retenues

Les retenues sont présentées dans une section distincte.

Exemples :

* avances ;
* oppositions ;
* absences ;
* retenues diverses.

Chaque retenue est identifiée par son origine.

---

# 12. Impôt

La section fiscale comprend :

* base imposable ;
* abattements ;
* impôt calculé ;
* retenues fiscales.

Les règles de calcul sont détaillées dans les volumes du moteur de paie.

---

# 13. Synthèse financière

Les principaux indicateurs sont regroupés :

```text
Salaire Brut
        ↓
Cotisations
        ↓
Retenues
        ↓
IRG
        ↓
Net à payer
```

Cette synthèse est visuellement mise en évidence.

---

# 14. Cumuls

Le bulletin peut afficher :

* cumul annuel du brut ;
* cumul annuel des retenues ;
* cumul des cotisations ;
* cumul imposable.

Les cumuls sont calculés à partir des bulletins validés de la même année.

---

# 15. Mentions réglementaires

Cette section comprend :

* textes réglementaires applicables ;
* mentions légales obligatoires ;
* avertissements éventuels.

Les contenus sont versionnés et paramétrables.

---

# 16. Signature documentaire

Le document peut comporter :

* signature électronique de l'établissement ;
* cachet graphique ;
* identifiant du document ;
* QR Code de vérification.

Ces éléments sont optionnels selon les exigences réglementaires.

---

# 17. Mise en page QuestPDF

Le modèle est constitué de blocs réutilisables :

```text
HeaderBlock
EmployeeBlock
AdministrativeBlock
PayrollItemsBlock
ContributionsBlock
DeductionsBlock
TaxBlock
SummaryBlock
TotalsBlock
FooterBlock
```

Chaque bloc est indépendant et testé séparément.

---

# 18. Versionnement

Le modèle de bulletin est versionné.

Une modification de présentation ou de réglementation entraîne la création d'une nouvelle version.

Les anciens bulletins restent associés au modèle avec lequel ils ont été générés.

---

# 19. Validation

Avant génération, le moteur vérifie :

* cohérence des montants ;
* présence des informations obligatoires ;
* équilibre des totaux ;
* conformité du modèle.

Toute anomalie bloque la certification du bulletin.

---

# 20. Performance

Objectifs :

| Opération                        |           Temps cible |
| -------------------------------- | --------------------: |
| Construction de l'agrégat métier |              < 100 ms |
| Génération du PDF                |              < 300 ms |
| Prévisualisation                 |              < 500 ms |
| Impression                       | < 1 s (hors matériel) |

---

# 21. Tests

Le bulletin est validé par :

* tests unitaires des sections ;
* tests de conformité réglementaire ;
* tests de rendu QuestPDF ;
* tests de non-régression visuelle ;
* tests de performance.

Chaque évolution réglementaire doit être accompagnée de nouveaux jeux de tests.

---

# 22. Critères d'acceptation

Le bulletin est conforme lorsque :

* il est généré exclusivement à partir de l'agrégat métier ;
* toutes les sections obligatoires sont présentes ;
* les montants sont cohérents avec le moteur de calcul ;
* la mise en page est conforme au modèle versionné ;
* les contrôles de validation sont satisfaits.

---

# 23. ADR (Architecture Decision Records)

| ADR     | Décision                                                                                           |
| ------- | -------------------------------------------------------------------------------------------------- |
| ADR-111 | Le bulletin de paie est un **agrégat métier immuable**                                             |
| ADR-112 | Le PDF est une représentation du bulletin, non la source des données                               |
| ADR-113 | Tous les modèles de bulletin sont versionnés                                                       |
| ADR-114 | Les sections du bulletin sont composées de blocs QuestPDF réutilisables                            |
| ADR-115 | La certification d'un bulletin exige une validation complète des données et du modèle documentaire |

---

# Conclusion

Le **Volume 22** fait évoluer le bulletin de paie d'un simple document imprimable vers un **objet métier versionné et certifié**. Cette approche renforce la traçabilité, facilite les audits et garantit que toute représentation (PDF, prévisualisation ou impression) provient d'une source métier unique et fiable.

## Recommandation pour le Volume 23

Le **Volume 23** devrait être consacré au **Framework d'exportation et d'interopérabilité**. Il couvrira notamment :

* l'architecture du moteur d'export ;
* les exports **Excel** avec **ClosedXML** ;
* les exports **CSV**, **JSON** et **XML** ;
* les règles d'encodage et de localisation ;
* les modèles d'export réutilisables ;
* les traitements par lots (*batch export*) ;
* les contrôles d'intégrité des données exportées ;
* les mécanismes d'extension permettant d'ajouter de nouveaux formats sans modifier le cœur du moteur.

Ce volume constituera la référence pour toutes les fonctionnalités d'export de **PaieEducation ERP**.

Excellente progression.

À partir de ce volume, je recommande une évolution architecturale qui rapprochera **PaieEducation ERP** des ERP de niveau **SAP**, **Oracle Fusion** ou **Microsoft Dynamics 365**.

Aujourd'hui, beaucoup d'applications considèrent l'export comme une simple fonctionnalité ("Exporter en Excel", "Exporter en PDF"). Dans un ERP moderne, il s'agit d'un **moteur d'interopérabilité**.

Je propose donc que le **Volume 23** ne traite pas uniquement des exports, mais définisse un **Data Exchange Framework**, capable d'alimenter :

* Excel
* CSV
* JSON
* XML
* PDF
* API REST (à terme)
* ETL
* Data Warehouse
* BI (Power BI)

Ainsi, l'ERP sera prêt pour des évolutions futures sans refonte de l'architecture.

Cette orientation est formalisée par les **ADR-116 à ADR-120**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome F — Reporting et Production Documentaire**

# **Volume 23**

# **Data Exchange Framework – Architecture des exports, interopérabilité et échanges de données**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • ClosedXML • QuestPDF • System.Text.Json • XML • MVVM

---

# 1. Objet

Le **Data Exchange Framework** est responsable de tous les échanges de données entre **PaieEducation ERP** et les systèmes externes.

Il fournit une architecture unique pour :

* l'export documentaire ;
* les exports bureautiques ;
* les échanges structurés ;
* les traitements batch ;
* les futures intégrations.

---

# 2. Objectifs

Le framework doit garantir :

* indépendance des formats ;
* extensibilité ;
* performances ;
* traçabilité ;
* sécurité ;
* réutilisation.

---

# 3. Architecture générale

```text
Application
        │
        ▼
IDataExchangeService
        │
        ▼
Data Exchange Framework
│
├── Export Engine
├── Import Engine
├── Mapping Engine
├── Transformation Engine
├── Validation Engine
├── Batch Engine
├── Metadata Engine
└── Logging
```

Chaque moteur est indépendant.

---

# 4. Position dans l'architecture

```text
Presentation
        │
Application
        │
Data Exchange Service
        │
Data Exchange Framework
        │
Export Providers
```

Les Workspaces n'ont aucune connaissance des formats techniques.

---

# 5. Formats supportés

Le framework prévoit les formats suivants :

| Format | Usage                    |
| ------ | ------------------------ |
| PDF    | Documents officiels      |
| XLSX   | Tableaux et états        |
| CSV    | Échanges bureautiques    |
| JSON   | API et intégration       |
| XML    | Échanges institutionnels |
| TXT    | Interfaces spécifiques   |

L'ajout d'un nouveau format ne doit pas nécessiter de modification des moteurs existants.

---

# 6. Architecture par fournisseurs (Providers)

Chaque format est implémenté sous forme de **Provider**.

```text
IDataExportProvider
│
├── PdfProvider
├── ExcelProvider
├── CsvProvider
├── JsonProvider
├── XmlProvider
└── TextProvider
```

Le principe **Open/Closed** est appliqué.

---

# 7. Contrats d'export

Tous les fournisseurs implémentent une interface commune.

Exemple :

```csharp
IDataExportProvider
```

Responsabilités :

* validation ;
* transformation ;
* génération ;
* retour du flux de sortie.

---

# 8. Excel Provider

L'export Excel repose exclusivement sur **ClosedXML**.

Fonctionnalités :

* feuilles multiples ;
* styles ;
* tableaux structurés ;
* formules ;
* filtres ;
* gels de volets ;
* mise en forme conditionnelle.

Les exports doivent être directement exploitables par Microsoft Excel et LibreOffice Calc.

---

# 9. PDF Provider

Le fournisseur PDF utilise exclusivement **QuestPDF**.

Il est destiné :

* aux bulletins ;
* aux rapports ;
* aux états réglementaires ;
* aux impressions.

Les documents sont générés à partir du **Document Engine** (Volume 21).

---

# 10. CSV Provider

Caractéristiques :

* séparateur configurable ;
* encodage UTF-8 ;
* gestion des guillemets ;
* export des en-têtes ;
* format régional configurable.

---

# 11. JSON Provider

Le format JSON est destiné :

* aux API futures ;
* aux intégrations ;
* aux sauvegardes ciblées ;
* aux tests.

Le sérialiseur officiel est **System.Text.Json**.

---

# 12. XML Provider

Le XML est réservé :

* aux échanges administratifs ;
* aux partenaires institutionnels ;
* aux interfaces réglementaires.

Les schémas (XSD) sont versionnés et validés.

---

# 13. Mapping Engine

Le moteur de mapping sépare :

* les objets métier ;
* les DTO ;
* les modèles d'export.

Aucun fournisseur ne consomme directement les entités du domaine.

---

# 14. Validation Engine

Avant tout export, les contrôles suivants sont réalisés :

* présence des données ;
* cohérence des types ;
* format des valeurs ;
* contraintes métier.

Tout export invalide est interrompu.

---

# 15. Batch Engine

Le framework permet :

* export de masse ;
* génération planifiée ;
* regroupement par établissement ;
* export multi-fichiers.

Les traitements sont exécutés de manière asynchrone.

---

# 16. Métadonnées

Chaque export contient :

* identifiant ;
* utilisateur ;
* date ;
* format ;
* version ;
* module d'origine.

Ces informations facilitent les audits.

---

# 17. Sécurité

Les exports respectent les règles suivantes :

* contrôle des droits avant génération ;
* chiffrement possible des documents sensibles ;
* suppression des données temporaires ;
* journalisation des accès.

---

# 18. Journalisation

Les événements suivants sont enregistrés :

* demande d'export ;
* réussite ;
* échec ;
* durée ;
* taille du fichier généré.

Les journaux sont centralisés via `Microsoft.Extensions.Logging`.

---

# 19. Performances

Objectifs :

| Opération                    | Temps cible |
| ---------------------------- | ----------: |
| Export Excel (10 000 lignes) |       < 5 s |
| Export CSV (100 000 lignes)  |       < 3 s |
| Export JSON (50 000 objets)  |       < 2 s |
| Export XML (50 000 objets)   |       < 4 s |
| Export PDF (1 000 pages)     |      < 15 s |

Ces objectifs sont vérifiés lors des campagnes de performance.

---

# 20. Tests

Le framework est validé par :

* tests unitaires des providers ;
* tests de validation des formats ;
* tests d'intégration ;
* tests de performance ;
* tests de compatibilité (Excel, LibreOffice, lecteurs PDF, parseurs JSON/XML).

---

# 21. Extensibilité

Pour ajouter un nouveau format :

1. créer un nouveau `IDataExportProvider` ;
2. enregistrer le provider dans l'injection de dépendances ;
3. documenter le format ;
4. ajouter les tests de conformité.

Aucune modification des providers existants n'est nécessaire.

---

# 22. Critères d'acceptation

Le Data Exchange Framework est conforme lorsque :

* tous les exports passent par le framework ;
* chaque format dispose de son provider dédié ;
* les validations sont exécutées avant génération ;
* les exports sont journalisés ;
* les performances respectent les objectifs définis.

---

# 23. ADR (Architecture Decision Records)

| ADR     | Décision                                                                              |
| ------- | ------------------------------------------------------------------------------------- |
| ADR-116 | Création d'un **Data Exchange Framework** centralisé                                  |
| ADR-117 | Architecture basée sur des **Export Providers** spécialisés                           |
| ADR-118 | Utilisation exclusive de **ClosedXML** pour les exports Excel                         |
| ADR-119 | Séparation stricte entre objets métier, DTO et modèles d'export                       |
| ADR-120 | Préparation du framework aux futures intégrations (API REST, ETL, BI, Data Warehouse) |

---

# Conclusion

Le **Volume 23** fait évoluer la notion d'export vers un véritable **Data Exchange Framework**. En s'appuyant sur une architecture modulaire, des fournisseurs spécialisés et des règles de validation communes, il prépare **PaieEducation ERP** aux besoins d'interopérabilité actuels et futurs, tout en garantissant la cohérence et la maintenabilité des échanges de données.

## Recommandation pour le Volume 24

Le **Volume 24** devrait être consacré au **Print Management Framework**, couvrant l'ensemble du cycle d'impression et de diffusion documentaire :

* architecture du moteur d'impression ;
* gestion des imprimantes et des profils d'impression ;
* prévisualisation unifiée ;
* impression individuelle et par lots ;
* files d'attente d'impression (*Print Queue Manager*) ;
* génération différée et traitements en arrière-plan ;
* numérotation, filigranes et options de confidentialité ;
* suivi, reprise sur erreur et journalisation des impressions.

Ce volume fera du **Print Management Framework** un composant transverse, indépendant du moteur documentaire, conforme aux exigences d'un ERP moderne de niveau entreprise.

Je recommande une évolution supplémentaire de l'architecture documentaire.

Dans la plupart des applications, **l'impression est considérée comme la dernière étape de la génération d'un document**. Dans un ERP de niveau entreprise, ce n'est pas le cas : l'impression est un **workflow métier** à part entière, avec des files d'attente, des stratégies de routage, des profils, des journaux d'audit et des mécanismes de reprise.

Pour **PaieEducation ERP**, je recommande donc l'introduction d'un **Print Management Framework** indépendant du **Document Engine**. Cette séparation permettra d'ajouter ultérieurement des fonctionnalités telles que l'impression différée, la distribution vers plusieurs imprimantes, l'envoi automatique par e-mail ou la publication vers un système d'archivage électronique, sans modifier le moteur documentaire.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome F — Reporting et Production Documentaire**

# **Volume 24**

# **Print Management Framework – Architecture du moteur d'impression, prévisualisation, files d'attente et diffusion documentaire**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • QuestPDF • WPF • MVVM • Microsoft.Extensions.DependencyInjection • Microsoft.Extensions.Logging

---

# 1. Objet

Le **Print Management Framework** est responsable de l'ensemble du cycle de diffusion des documents produits par **PaieEducation ERP**.

Il assure :

* la prévisualisation ;
* l'impression ;
* la gestion des files d'attente ;
* le suivi des impressions ;
* la reprise sur incident ;
* la préparation des futures stratégies de diffusion.

Le framework ne génère pas les documents : il consomme les documents produits par le **Document Engine** (Volume 21).

---

# 2. Objectifs

Le framework doit garantir :

* indépendance vis-à-vis du moteur documentaire ;
* fiabilité des impressions ;
* traçabilité complète ;
* gestion des traitements de masse ;
* extensibilité.

---

# 3. Architecture générale

```text
Application
        │
        ▼
IPrintManagementService
        │
        ▼
Print Management Framework
│
├── Print Queue Manager
├── Preview Engine
├── Print Dispatcher
├── Printer Profile Manager
├── Print Job Scheduler
├── Watermark Engine
├── Distribution Manager
├── Audit Engine
└── Logging
```

Chaque sous-système possède une responsabilité clairement définie.

---

# 4. Position dans l'architecture

```text
Presentation
        │
Application
        │
Document Engine
        │
Print Management Framework
        │
Système d'impression Windows
```

La Présentation ne dialogue jamais directement avec les API d'impression.

---

# 5. Cycle d'impression

```text
Document validé
        ↓
Prévisualisation
        ↓
Choix du profil
        ↓
Création du Print Job
        ↓
Placement en file
        ↓
Impression
        ↓
Confirmation
        ↓
Journalisation
```

Chaque étape est observable et journalisée.

---

# 6. Print Job

Chaque demande d'impression est représentée par un objet métier :

* identifiant ;
* type de document ;
* utilisateur ;
* date de création ;
* état ;
* profil d'impression ;
* nombre d'exemplaires.

Le `PrintJob` est immuable après son envoi dans la file d'attente.

---

# 7. États d'un Print Job

| État        | Description         |
| ----------- | ------------------- |
| Créé        | Demande enregistrée |
| En attente  | Dans la file        |
| Préparation | Paramétrage         |
| Impression  | En cours            |
| Terminé     | Succès              |
| Annulé      | Interrompu          |
| Échec       | Incident technique  |

Les transitions sont historisées.

---

# 8. Print Queue Manager

Le gestionnaire de file d'attente permet :

* la mise en attente ;
* la priorisation ;
* la reprise ;
* l'annulation ;
* le suivi des travaux.

Plusieurs files peuvent être définies selon les besoins.

---

# 9. Printer Profile Manager

Les profils regroupent les paramètres d'impression :

* imprimante cible ;
* format papier ;
* orientation ;
* recto/verso ;
* qualité ;
* bac de sortie.

Les profils sont réutilisables par les différents modules.

---

# 10. Prévisualisation

Le `Preview Engine` offre :

* zoom ;
* rotation ;
* navigation entre pages ;
* recherche ;
* impression ;
* export.

La prévisualisation repose sur le document déjà généré par le Document Engine.

---

# 11. Impression par lots

Le framework prend en charge :

* impression de plusieurs bulletins ;
* impression par établissement ;
* impression par période ;
* regroupement automatique.

Les traitements de masse sont exécutés de manière asynchrone.

---

# 12. Watermark Engine

Le moteur de filigrane permet d'ajouter :

* « Brouillon » ;
* « Copie » ;
* « Confidentiel » ;
* mentions réglementaires.

Les filigranes sont configurables par type de document.

---

# 13. Distribution Manager

Le framework prépare les futurs canaux de diffusion :

* impression ;
* export local ;
* envoi par courrier électronique ;
* dépôt dans une GED ;
* publication via API.

Les canaux sont implémentés comme des stratégies.

---

# 14. Gestion des erreurs

En cas d'incident :

* le travail est marqué en échec ;
* un message utilisateur est affiché ;
* les détails techniques sont consignés dans les journaux ;
* une reprise est proposée lorsque cela est possible.

---

# 15. Journalisation

Le framework journalise :

* création du travail ;
* choix du profil ;
* lancement ;
* succès ;
* échec ;
* durée.

Les événements sont enregistrés via `Microsoft.Extensions.Logging`.

---

# 16. Sécurité

Avant toute impression, le système vérifie :

* les droits de l'utilisateur ;
* les restrictions documentaires ;
* les politiques de confidentialité.

Les documents sensibles peuvent imposer des règles spécifiques.

---

# 17. Performance

Objectifs :

| Opération                                    | Temps cible |
| -------------------------------------------- | ----------: |
| Ouverture de la prévisualisation             |    < 500 ms |
| Création d'un Print Job                      |    < 100 ms |
| Mise en file                                 |     < 50 ms |
| Impression d'un bulletin (hors périphérique) |       < 1 s |

---

# 18. Tests

Le framework est validé par :

* tests unitaires des gestionnaires ;
* tests d'intégration ;
* tests de charge sur les impressions par lots ;
* tests de reprise après incident ;
* tests de performance.

---

# 19. Extensibilité

L'ajout d'un nouveau canal de diffusion nécessite uniquement :

1. une nouvelle stratégie de distribution ;
2. son enregistrement dans l'injection de dépendances ;
3. les tests associés.

Le cœur du framework reste inchangé.

---

# 20. Critères d'acceptation

Le Print Management Framework est conforme lorsque :

* toutes les impressions transitent par le framework ;
* les travaux sont tracés ;
* les profils sont réutilisables ;
* les impressions de masse sont supportées ;
* les performances répondent aux objectifs.

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                                |
| ------- | ----------------------------------------------------------------------- |
| ADR-121 | Création d'un **Print Management Framework** indépendant                |
| ADR-122 | Introduction d'un **Print Queue Manager** pour les travaux d'impression |
| ADR-123 | Utilisation de profils d'impression réutilisables                       |
| ADR-124 | Diffusion documentaire basée sur des stratégies extensibles             |
| ADR-125 | Journalisation complète du cycle d'impression et des incidents          |

---

# Conclusion

Le **Volume 24** fait de l'impression un **workflow métier** indépendant, capable de gérer les traitements unitaires comme les impressions de masse, tout en offrant une traçabilité complète et une architecture évolutive. Cette approche prépare **PaieEducation ERP** à des scénarios futurs tels que l'envoi automatisé des documents, l'intégration à une GED ou la diffusion via des services externes.

## Recommandation pour le Volume 25

Le **Volume 25** devrait conclure le **Tome F** en définissant le **Document Governance Framework**, couvrant :

* le versionnement des modèles documentaires ;
* la signature électronique et les mécanismes de validation ;
* les politiques de conservation et d'archivage ;
* la gestion des métadonnées documentaires ;
* la traçabilité complète des consultations et des modifications ;
* les stratégies de confidentialité et de classification des documents ;
* les exigences de conformité réglementaire ;
* les mécanismes de restauration et d'audit.

Ce volume établira les règles de gouvernance documentaire qui garantiront la valeur probante, la pérennité et la conformité des documents produits par **PaieEducation ERP**.

Excellente progression.

Le **Volume 25** clôt le **Tome F – Reporting et Production Documentaire**. Je recommande qu'il ne traite pas uniquement de l'archivage, mais qu'il définisse une véritable **gouvernance documentaire (Document Governance Framework)**.

Dans un ERP moderne, la valeur d'un document ne repose pas uniquement sur son contenu, mais également sur :

* son authenticité ;
* son intégrité ;
* sa traçabilité ;
* son cycle de vie ;
* sa conformité réglementaire.

Le **Document Governance Framework** devient ainsi le garant de la fiabilité des documents produits par **PaieEducation ERP**.

Les décisions d'architecture sont formalisées par les **ADR-121 à ADR-125**.

> **Remarque de cohérence** : dans la suite de la documentation, je recommande de renuméroter les ADR à partir de ce volume afin d'éviter le doublon avec les ADR-121 à ADR-125 utilisés dans le Volume 24. Par exemple, utiliser **ADR-126 à ADR-130**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome F — Reporting et Production Documentaire**

# **Volume 25**

# **Document Governance Framework – Gouvernance documentaire, archivage, signature électronique et traçabilité**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • QuestPDF • SQLite • Microsoft.Extensions.Logging

---

# 1. Objet

Le **Document Governance Framework** définit les règles de gouvernance applicables à tous les documents produits par **PaieEducation ERP**.

Il garantit :

* l'authenticité ;
* l'intégrité ;
* la traçabilité ;
* la conservation ;
* la conformité réglementaire.

Il complète le **Document Engine** (Volume 21) et le **Print Management Framework** (Volume 24).

---

# 2. Objectifs

Le framework doit assurer :

* un cycle de vie documentaire maîtrisé ;
* le versionnement des modèles et des documents ;
* une conservation sécurisée ;
* une auditabilité complète ;
* la préparation à une future intégration avec une GED.

---

# 3. Architecture générale

```text
Application
        │
        ▼
IDocumentGovernanceService
        │
        ▼
Document Governance Framework
│
├── Document Registry
├── Metadata Manager
├── Version Manager
├── Signature Manager
├── Retention Manager
├── Archive Manager
├── Audit Manager
├── Classification Manager
└── Logging
```

Chaque composant remplit une responsabilité unique.

---

# 4. Cycle de vie documentaire

```text
Création
      ↓
Validation
      ↓
Certification
      ↓
Publication
      ↓
Consultation
      ↓
Archivage
      ↓
Conservation
      ↓
Destruction (si autorisée)
```

Chaque transition est enregistrée dans l'historique du document.

---

# 5. Registre documentaire

Chaque document est inscrit dans un registre contenant notamment :

* identifiant unique ;
* type ;
* auteur ;
* période ;
* version ;
* état ;
* date de création ;
* date de validation ;
* empreinte numérique (hash).

Le registre est indépendant du fichier PDF.

---

# 6. Métadonnées

Les métadonnées minimales comprennent :

| Élément       | Description              |
| ------------- | ------------------------ |
| DocumentId    | Identifiant unique       |
| DocumentType  | Nature du document       |
| EmployeeId    | Agent concerné           |
| PayrollPeriod | Période de paie          |
| Version       | Version du modèle        |
| GeneratedAt   | Date de génération       |
| GeneratedBy   | Origine de la génération |
| Hash          | Empreinte de contrôle    |

Des métadonnées supplémentaires peuvent être ajoutées sans modifier le modèle.

---

# 7. Versionnement

Deux niveaux de versionnement sont distingués :

* **Version du modèle documentaire** (Template Version) ;
* **Version de l'instance documentaire** (Document Version).

Un document publié reste associé au modèle ayant servi à sa génération.

---

# 8. Intégrité

Chaque document est associé à une empreinte cryptographique calculée lors de sa génération.

Objectifs :

* détecter les altérations ;
* vérifier l'intégrité lors d'un contrôle ;
* garantir la correspondance entre le registre documentaire et le fichier archivé.

Le choix de l'algorithme (par exemple SHA-256 ou supérieur) est centralisé et configurable.

---

# 9. Signature électronique

Le framework prévoit un composant de signature permettant :

* la signature institutionnelle ;
* la signature des documents certifiés ;
* l'intégration future avec des certificats électroniques.

La signature est découplée du moteur de génération PDF.

---

# 10. Classification documentaire

Chaque document reçoit une classification :

| Niveau       | Exemple                            |
| ------------ | ---------------------------------- |
| Public       | Documents d'information            |
| Interne      | Rapports de gestion                |
| Confidentiel | Bulletins de paie                  |
| Restreint    | Documents administratifs sensibles |

Cette classification détermine les règles de diffusion et d'archivage.

---

# 11. Politique de conservation

Une politique de conservation est définie par type documentaire.

Elle précise notamment :

* durée minimale de conservation ;
* conditions d'archivage ;
* possibilité de suppression ;
* exigences de restauration.

Les règles sont paramétrables afin de s'adapter aux évolutions réglementaires.

---

# 12. Archivage

Le framework prend en charge :

* archivage logique ;
* archivage physique (répertoire sécurisé) ;
* préparation à une GED externe.

Les documents archivés restent consultables selon les droits applicables.

---

# 13. Audit documentaire

Chaque événement est enregistré :

* création ;
* régénération ;
* impression ;
* export ;
* consultation ;
* archivage ;
* suppression (si autorisée).

Les journaux d'audit sont immuables.

---

# 14. Consultation

La consultation d'un document :

* ne modifie jamais son contenu ;
* met à jour uniquement les informations d'audit ;
* respecte les autorisations d'accès.

Les consultations peuvent être filtrées par période, agent ou type de document.

---

# 15. Confidentialité

Les documents sensibles peuvent bénéficier :

* d'un chiffrement au repos ;
* d'un contrôle d'accès renforcé ;
* d'un masquage de certaines informations lors de l'export.

Les règles sont définies au niveau des politiques de sécurité.

---

# 16. Restauration

Le framework permet :

* la récupération d'un document archivé ;
* la vérification de son intégrité ;
* la comparaison avec le registre documentaire.

Une restauration ne crée pas une nouvelle version du document.

---

# 17. Journalisation

Les événements suivants sont consignés :

* opérations de gouvernance ;
* échecs de signature ;
* anomalies d'intégrité ;
* restaurations ;
* suppressions autorisées.

Les journaux utilisent `Microsoft.Extensions.Logging`.

---

# 18. Performance

Objectifs :

| Opération                      | Temps cible |
| ------------------------------ | ----------: |
| Enregistrement des métadonnées |     < 50 ms |
| Calcul de l'empreinte          |    < 100 ms |
| Archivage                      |    < 500 ms |
| Recherche documentaire         |    < 300 ms |
| Vérification d'intégrité       |    < 200 ms |

---

# 19. Tests

Le framework est validé par :

* tests unitaires des services de gouvernance ;
* tests de calcul d'empreinte ;
* tests de versionnement ;
* tests de restauration ;
* tests de performance.

Les scénarios de non-régression couvrent également les évolutions des modèles documentaires.

---

# 20. Critères d'acceptation

Le **Document Governance Framework** est conforme lorsque :

* tous les documents sont enregistrés dans le registre documentaire ;
* les métadonnées sont complètes ;
* l'intégrité est vérifiable ;
* les politiques de conservation sont appliquées ;
* les événements sont auditables ;
* les performances respectent les objectifs.

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                                          |
| ------- | --------------------------------------------------------------------------------- |
| ADR-126 | Création d'un **Document Governance Framework** dédié                             |
| ADR-127 | Séparation entre registre documentaire et fichiers physiques                      |
| ADR-128 | Intégrité garantie par une empreinte cryptographique configurable                 |
| ADR-129 | Gouvernance basée sur des politiques de conservation et de classification         |
| ADR-130 | Préparation à l'intégration avec une GED et des signatures électroniques avancées |

---

# Conclusion

Le **Volume 25** conclut le **Tome F** en définissant la gouvernance documentaire de **PaieEducation ERP**. Il garantit que chaque document produit par le système est authentique, traçable, versionné et gouverné selon des politiques explicites. Cette approche prépare l'application à des exigences élevées en matière d'audit, de conformité et d'évolutivité.

## Recommandation pour le **Tome G**

La prochaine étape logique est l'ouverture du **Tome G — Sécurité, Authentification et Gouvernance Technique**, qui couvrira notamment :

* **Volume 26** : Architecture de sécurité et modèle d'autorisation (RBAC, politiques, permissions) ;
* **Volume 27** : Authentification, gestion des identités et sessions ;
* **Volume 28** : Journalisation centralisée, audit et observabilité ;
* **Volume 29** : Configuration, paramètres et gestion des secrets ;
* **Volume 30** : Sauvegarde, restauration, continuité d'activité et reprise après sinistre.

Ce tome constituera la référence de sécurité et d'exploitation de **PaieEducation ERP**, en cohérence avec l'ensemble de l'architecture définie dans les volumes précédents.
