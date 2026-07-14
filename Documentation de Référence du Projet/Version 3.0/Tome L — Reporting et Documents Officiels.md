Excellent. Nous arrivons maintenant à un volume qui constitue un véritable **sous-système** dans un ERP moderne. Le reporting n'est pas seulement un module d'impression : c'est une architecture complète, indépendante du moteur de paie.

Pour **PaieEducation ERP**, je recommande de considérer le reporting comme un projet à part entière (`PaieEducation.Reporting`), afin qu'il puisse évoluer indépendamment du reste de l'application.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 3.0**

**Tome L — Reporting et Documents Officiels**

# **Volume 27**

# **Architecture du Sous-système de Reporting (QuestPDF, ClosedXML et Génération Documentaire)**

**Version :** 3.0
**Statut :** Référentiel officiel du module **Reporting**

---

# 1. Objet

Le sous-système **Reporting** est responsable de la production de tous les documents générés par **PaieEducation ERP**.

Il prend en charge :

* les bulletins de paie individuels ;
* les états récapitulatifs ;
* les états comptables ;
* les attestations administratives ;
* les exports Excel ;
* les documents de contrôle ;
* les journaux d'exécution.

Il est totalement indépendant du moteur de calcul.

---

# 2. Principes d'architecture

Le reporting repose sur les principes suivants :

* séparation entre données et présentation ;
* documents générés à partir de modèles ;
* mise en page cohérente ;
* réutilisation des composants graphiques ;
* indépendance vis-à-vis de la couche WPF.

Les documents ne contiennent aucune logique métier.

---

# 3. Position dans l'architecture

```text
Presentation (WPF)
        │
Application
        │
Reporting Services
        │
QuestPDF / ClosedXML
        │
PDF / XLSX
```

La couche **Reporting** consomme uniquement des **DTO** fournis par la couche **Application**.

---

# 4. Organisation du projet

```text
PaieEducation.Reporting

│
├── Documents
├── Templates
├── Components
├── Layouts
├── Styles
├── Pdf
├── Excel
├── Printing
├── Preview
├── Export
├── Assets
└── DependencyInjection
```

Chaque dossier répond à une responsabilité précise.

---

# 5. Technologies retenues

| Technologie                              | Usage                          |
| ---------------------------------------- | ------------------------------ |
| QuestPDF                                 | Génération des documents PDF   |
| ClosedXML                                | Génération des classeurs Excel |
| WPF                                      | Prévisualisation               |
| Microsoft.Extensions.DependencyInjection | Injection des services         |

Aucune technologie de reporting héritée (RDLC, Crystal Reports, etc.) n'est utilisée.

---

# 6. Types de documents

Le module prend en charge :

* bulletin de paie ;
* état mensuel des salaires ;
* état des cotisations ;
* état des retenues ;
* liste des agents ;
* attestations diverses ;
* bordereaux ;
* journaux d'audit.

Chaque type de document est implémenté indépendamment.

---

# 7. Architecture des documents

Tous les documents suivent une structure commune :

```text
Document
│
├── En-tête
├── Informations administratives
├── Corps
├── Totaux
├── Mentions légales
└── Pied de page
```

Cette structure garantit une présentation homogène.

---

# 8. Composants réutilisables

Les éléments graphiques communs sont factorisés.

Exemples :

* en-tête institutionnel ;
* bloc identité de l'agent ;
* tableau des rubriques ;
* tableau des retenues ;
* signature ;
* pied de page.

Ces composants sont partagés entre plusieurs documents.

---

# 9. Styles graphiques

Un référentiel de styles unique est défini :

| Élément          | Style           |
| ---------------- | --------------- |
| Titres           | Institutionnel  |
| Sous-titres      | Standard        |
| Tableaux         | Uniformes       |
| Totaux           | Mis en évidence |
| Mentions légales | Discrètes       |

Les couleurs, polices et espacements sont centralisés.

---

# 10. Bulletin de paie

Le bulletin comprend notamment :

* identification de l'établissement ;
* identification de l'agent ;
* période de paie ;
* rubriques détaillées ;
* gains ;
* retenues ;
* cotisations ;
* salaire brut ;
* salaire imposable ;
* IRG ;
* net à payer (en **DZD**) ;
* mentions réglementaires.

Le contenu est alimenté exclusivement par les DTO.

---

# 11. États récapitulatifs

Les états permettent de consolider les données :

* par établissement ;
* par corps ;
* par grade ;
* par période ;
* par rubrique.

Ils sont optimisés pour les impressions grand format.

---

# 12. Attestations administratives

Le sous-système produit différents modèles :

* attestation de travail ;
* attestation de salaire ;
* certificat administratif ;
* autres documents prévus par les procédures internes.

Chaque modèle est indépendant et versionné.

---

# 13. Exports Excel

Les exports sont réalisés avec **ClosedXML**.

Fonctionnalités :

* feuilles multiples ;
* mise en forme ;
* filtres ;
* gels de volets ;
* formules lorsque pertinent.

Les fichiers générés sont compatibles avec les principaux tableurs.

---

# 14. Prévisualisation

Avant impression, chaque document peut être prévisualisé dans WPF.

La prévisualisation respecte :

* le format papier ;
* les marges ;
* les sauts de page ;
* l'échelle réelle.

---

# 15. Impression

Le service d'impression permet :

* le choix de l'imprimante ;
* la sélection des pages ;
* le nombre de copies ;
* l'impression recto/verso selon les capacités du matériel.

---

# 16. Internationalisation

Le module est conçu pour supporter :

* plusieurs langues d'interface ;
* différents formats de date ;
* différents formats numériques.

Toutefois, les montants monétaires restent exprimés en **DZD**.

---

# 17. Performance

Objectifs :

| Document            | Temps cible |
| ------------------- | ----------: |
| Bulletin individuel |       < 1 s |
| Attestation         |    < 500 ms |
| État de 500 agents  |      < 10 s |
| Export Excel        |      < 15 s |

Les documents volumineux doivent être générés sans consommation excessive de mémoire.

---

# 18. Gestion des modèles

Les modèles de documents sont versionnés.

Chaque évolution de mise en page est documentée afin de garantir la reproductibilité des impressions historiques.

---

# 19. Journalisation

Chaque génération de document est journalisée avec :

* type de document ;
* identifiant de l'utilisateur ;
* période concernée ;
* date et heure ;
* durée de génération ;
* résultat (succès ou échec).

Ces informations facilitent les audits.

---

# 20. Critères d'acceptation

Le sous-système **Reporting** est conforme lorsque :

* tous les documents sont générés par QuestPDF ou ClosedXML ;
* les modèles sont réutilisables ;
* les styles sont centralisés ;
* les documents utilisent exclusivement des DTO ;
* les montants sont affichés en **DZD** ;
* les performances répondent aux objectifs définis.

---

# 21. Architecture interne recommandée

Pour assurer l'évolutivité du module, il est recommandé de structurer le projet autour des composants suivants :

| Composant               | Rôle                               |
| ----------------------- | ---------------------------------- |
| `IReportGenerator`      | Contrat de génération de documents |
| `BulletinDocument`      | Composition du bulletin de paie    |
| `AttestationDocument`   | Génération des attestations        |
| `SummaryReportDocument` | États récapitulatifs               |
| `ExcelExportService`    | Génération des fichiers XLSX       |
| `DocumentStyleProvider` | Styles graphiques partagés         |
| `ReportPreviewService`  | Prévisualisation WPF               |
| `PrintService`          | Impression                         |

---

# 22. Gouvernance documentaire

Chaque modèle de document suit un cycle de vie :

1. conception ;
2. validation fonctionnelle ;
3. validation réglementaire ;
4. publication ;
5. versionnement ;
6. archivage des anciennes versions.

Cette gouvernance garantit la cohérence des documents produits dans le temps.

---

# Conclusion

Le **Volume 27** définit le sous-système **Reporting** comme un module autonome, modulaire et entièrement découplé du moteur de paie. Grâce à **QuestPDF** et **ClosedXML**, il permet de produire des documents professionnels, performants et conformes aux exigences réglementaires, tout en restant facilement extensible.

## Orientation pour le Volume 28

Le **Volume 28** sera consacré à la **Sécurité, l'Audit et la Traçabilité**. Il décrira notamment :

* le journal d'audit des opérations ;
* la traçabilité des calculs et des modifications ;
* les profils et habilitations (si activés ultérieurement) ;
* l'intégrité des données ;
* la gestion des sauvegardes sécurisées ;
* les mécanismes de contrôle et de conformité.

Ce volume fera entrer **PaieEducation ERP** dans une logique de **gouvernance des données**, indispensable pour un ERP de niveau professionnel.
