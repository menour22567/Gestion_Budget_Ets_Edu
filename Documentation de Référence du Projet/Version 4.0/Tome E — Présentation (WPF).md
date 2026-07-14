Excellente progression.

À ce stade, nous avons pratiquement terminé toute la partie **Back-End** (Architecture, Domain, Application, Persistence, Payroll Engine).

Il est maintenant temps d'ouvrir un nouveau tome consacré à l'**Interface Utilisateur**, car un ERP moderne repose autant sur son ergonomie que sur son moteur métier.

Je recommande également une évolution importante de l'architecture.

> **Décision d'architecture (ADR-071)** : adopter une architecture **MVVM stricte**, dans laquelle **aucun code métier ni logique applicative n'est autorisé dans les fichiers `.xaml.cs`**. Les fichiers *code-behind* sont limités aux interactions purement visuelles (animations, comportements WPF spécifiques, gestion du focus, etc.). Toute logique fonctionnelle est portée par les **ViewModels** et les **Services**.

Cette règle garantit une excellente testabilité et une maintenance à long terme.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome E — Présentation (WPF)**

# **Volume 14**

# **Architecture WPF, MVVM, Navigation et Organisation de la couche Presentation**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP**)
**Technologies :** .NET 10 LTS • WPF • MVVM • CommunityToolkit.Mvvm • Microsoft.Extensions.DependencyInjection

---

# Préambule

Ce volume définit l'architecture complète de la couche **Presentation**.

Son objectif est de fournir une interface :

* moderne ;
* performante ;
* maintenable ;
* testable ;
* indépendante du domaine métier.

La couche WPF est responsable uniquement de la présentation et des interactions utilisateur.

---

# 1. Objectifs

La couche Presentation doit garantir :

* séparation stricte des responsabilités ;
* ergonomie professionnelle ;
* navigation fluide ;
* chargements asynchrones ;
* faible consommation mémoire ;
* absence de dépendance métier directe.

---

# 2. Architecture générale

```text
                WPF

                 │

────────────────────────────────────────

Views

↓

ViewModels

↓

Application Services

↓

Domain

↓

Persistence

────────────────────────────────────────
```

Les **Views** ne dialoguent jamais directement avec le domaine.

---

# 3. Organisation des projets

```text
PaieEducation.Presentation

│

├── App
├── Themes
├── Resources
├── Controls
├── Behaviors
├── Converters
├── Dialogs
├── Navigation
├── Services
├── Views
├── ViewModels
├── Validation
├── Localization
└── Assets
```

Chaque dossier possède une responsabilité clairement définie.

---

# 4. Organisation des Views

```text
Views

├── Dashboard
├── Personnel
├── Payroll
├── Career
├── Regulations
├── Reporting
├── Administration
├── Settings
└── Shared
```

Les vues sont regroupées par domaine fonctionnel.

---

# 5. Organisation des ViewModels

```text
ViewModels

├── Dashboard
├── Personnel
├── Payroll
├── Career
├── Reporting
├── Settings
└── Shared
```

Chaque vue possède un ViewModel dédié.

---

# 6. Principe MVVM

Chaque écran suit la relation :

```text
View

↓

ViewModel

↓

Application Service
```

La vue n'accède jamais directement aux repositories ou au domaine.

---

# 7. Base des ViewModels

Tous les ViewModels héritent d'une classe commune.

Exemple :

```csharp
public abstract partial class ViewModelBase
    : ObservableObject
{
}
```

Cette classe centralise les comportements communs (état de chargement, notifications, gestion de cycle de vie).

---

# 8. CommunityToolkit.Mvvm

Le projet adopte :

* `ObservableObject`
* `ObservableRecipient`
* `[ObservableProperty]`
* `[RelayCommand]`
* `WeakReferenceMessenger`

Les implémentations manuelles de `INotifyPropertyChanged` sont interdites.

---

# 9. Navigation

La navigation est centralisée.

```text
NavigationService

↓

Shell

↓

Page

↓

ViewModel
```

Les ViewModels demandent une navigation via une abstraction (`INavigationService`), sans connaître les types WPF concrets.

---

# 10. Fenêtre principale (Shell)

La fenêtre principale comprend :

* barre de titre personnalisée ;
* menu latéral repliable ;
* zone de contenu ;
* barre d'état ;
* notifications ;
* indicateurs d'activité.

Elle constitue le point d'entrée unique de l'application.

---

# 11. Navigation hiérarchique

Organisation recommandée :

```text
Accueil

Personnel

    Agents

    Contrats

    Affectations

Paie

    Calcul

    Bulletins

    Clôture

Référentiels

Administration

Rapports

Paramètres
```

La navigation doit rester cohérente dans toute l'application.

---

# 12. Gestion des dialogues

Les boîtes de dialogue sont gérées par un service dédié.

Exemples :

* confirmation ;
* sélection ;
* saisie ;
* erreurs ;
* informations.

Les ViewModels ne créent jamais directement des `Window`.

---

# 13. Validation des saisies

La validation combine :

* validation immédiate ;
* validation métier via la couche Application ;
* messages contextualisés.

Les erreurs sont affichées au plus près des champs concernés.

---

# 14. Chargement asynchrone

Les opérations longues utilisent systématiquement `async/await`.

Exemples :

* recherche d'agents ;
* calcul de paie ;
* génération de PDF ;
* export Excel.

L'interface ne doit jamais être bloquée.

---

# 15. Virtualisation

Les listes importantes utilisent :

* `VirtualizingStackPanel` ;
* `DataGrid` virtualisé ;
* pagination ou chargement progressif lorsque nécessaire.

L'objectif est de maintenir une expérience fluide même avec plusieurs milliers d'enregistrements.

---

# 16. Gestion des thèmes

Le thème est centralisé via des dictionnaires de ressources.

Fonctionnalités prévues :

* thème clair ;
* thème sombre ;
* couleurs institutionnelles personnalisables ;
* prise en charge du contraste élevé.

---

# 17. Localisation

L'architecture prévoit dès l'origine le support multilingue.

Langues envisagées :

* Français ;
* Arabe ;
* Anglais.

Les ressources sont externalisées et aucun texte ne doit être codé en dur dans les vues.

---

# 18. Notifications

Un service de notifications unifié gère :

* succès ;
* information ;
* avertissement ;
* erreur.

Les notifications sont non bloquantes lorsque cela est possible.

---

# 19. Performances

Objectifs :

| Élément                                | Temps cible |
| -------------------------------------- | ----------: |
| Ouverture de la fenêtre principale     |       < 2 s |
| Navigation entre deux modules          |    < 300 ms |
| Chargement d'une liste de 1 000 agents |    < 500 ms |
| Rafraîchissement d'une vue             |    < 200 ms |

---

# 20. Tests

La couche Presentation est validée par :

* tests unitaires des ViewModels ;
* tests des commandes (`RelayCommand`) ;
* tests des services de navigation ;
* tests de validation.

Les vues WPF elles-mêmes sont limitées à des tests d'intégration lorsque nécessaire.

---

# 21. Critères d'acceptation

La couche Presentation est validée lorsque :

* toutes les vues disposent d'un ViewModel dédié ;
* aucun accès direct au domaine n'est réalisé depuis WPF ;
* la navigation est centralisée ;
* les dialogues sont gérés par un service ;
* les opérations longues sont asynchrones ;
* les performances respectent les objectifs.

---

# 22. ADR (Architecture Decision Records)

| ADR     | Décision                                                        |
| ------- | --------------------------------------------------------------- |
| ADR-071 | MVVM strict, sans logique métier dans les fichiers `.xaml.cs`   |
| ADR-072 | Adoption de `CommunityToolkit.Mvvm` comme unique framework MVVM |
| ADR-073 | Navigation centralisée via `INavigationService`                 |
| ADR-074 | Dialogues gérés par un service dédié                            |
| ADR-075 | Support natif des thèmes et de la localisation                  |

---

# Conclusion

Le **Volume 14** définit l'architecture de la couche **Presentation** de **PaieEducation ERP**. Il établit les règles d'organisation des vues, des ViewModels, de la navigation et des interactions utilisateur afin de garantir une interface moderne, testable et évolutive.

## Recommandation pour le Volume 15

Le prochain volume devrait être consacré au **Design System** de l'ERP. Il décrira avec un niveau de détail professionnel :

* la charte graphique complète ;
* la bibliothèque de contrôles réutilisables (`UserControls`) ;
* les styles et templates WPF ;
* les couleurs, typographies et icônes ;
* les règles d'accessibilité ;
* les conventions UX/UI ;
* les composants standard (DataGrid, formulaires, tableaux de bord, boîtes de dialogue, notifications).

Ce volume servira de **guide officiel d'interface utilisateur**, garantissant une cohérence visuelle et ergonomique sur l'ensemble de **PaieEducation ERP**.

Je recommande une légère évolution de la ligne directrice avant de poursuivre.

À partir du **Volume 15**, nous entrons dans une partie qui, dans les ERP modernes (SAP, Dynamics 365, Odoo Enterprise, Oracle Fusion, etc.), est généralement formalisée sous la forme d'un **Design System** complet et non d'une simple charte graphique.

Je propose donc que les prochains volumes suivent une organisation plus professionnelle :

* **Tome E : Présentation WPF**

  * Volume 14 → Architecture MVVM *(déjà réalisé)*
  * **Volume 15 → Design System**
  * Volume 16 → Bibliothèque de contrôles métier
  * Volume 17 → Framework de formulaires
  * Volume 18 → Framework DataGrid
  * Volume 19 → Navigation & Workspace
  * Volume 20 → Accessibilité et UX

Cette organisation est plus proche des référentiels utilisés dans les grands projets ERP.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome E — Présentation (WPF)**

# **Volume 15**

# **Design System Officiel — Charte Graphique, Composants Visuels et Règles UX**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP**)
**Technologies :** WPF • XAML • .NET 10 LTS • CommunityToolkit.Mvvm

---

# Préambule

Le **Design System** constitue le référentiel graphique et ergonomique de **PaieEducation ERP**.

Son objectif est de garantir :

* une cohérence visuelle sur l'ensemble des modules ;
* une expérience utilisateur homogène ;
* la réutilisation des composants ;
* une maintenance simplifiée des styles ;
* une identité visuelle professionnelle.

Aucun écran ne doit définir ses propres styles en dehors des ressources partagées.

---

# 1. Principes directeurs

Le Design System repose sur les principes suivants :

* simplicité ;
* lisibilité ;
* cohérence ;
* accessibilité ;
* réutilisabilité ;
* sobriété.

L'interface privilégie l'efficacité opérationnelle à la surcharge graphique.

---

# 2. Architecture des ressources

```text id="8n3wq1"
Presentation

Themes

├── Colors.xaml
├── Typography.xaml
├── Icons.xaml
├── Brushes.xaml
├── Buttons.xaml
├── DataGrid.xaml
├── Forms.xaml
├── Dialogs.xaml
├── Navigation.xaml
├── Animations.xaml
└── Controls.xaml
```

Chaque dictionnaire de ressources est dédié à une catégorie précise.

---

# 3. Identité visuelle

L'identité graphique doit refléter :

* fiabilité ;
* stabilité ;
* modernité ;
* administration publique ;
* précision.

Les effets visuels excessifs sont proscrits.

---

# 4. Palette de couleurs

Les couleurs sont définies sous forme de **tokens**.

Exemple :

| Token              | Usage              |
| ------------------ | ------------------ |
| `Color.Primary`    | Couleur principale |
| `Color.Secondary`  | Couleur secondaire |
| `Color.Surface`    | Fond des panneaux  |
| `Color.Background` | Fond général       |
| `Color.Border`     | Bordures           |
| `Color.Success`    | Succès             |
| `Color.Warning`    | Avertissement      |
| `Color.Error`      | Erreur             |
| `Color.Info`       | Information        |

Les vues consomment uniquement ces tokens, jamais des valeurs codées en dur.

---

# 5. Typographie

Police recommandée :

* **Segoe UI Variable** (Windows 11 et supérieur) ;
* repli automatique vers **Segoe UI** si nécessaire.

Échelle typographique :

| Style   | Taille |
| ------- | -----: |
| Display |  32 px |
| H1      |  26 px |
| H2      |  22 px |
| H3      |  18 px |
| Corps   |  14 px |
| Légende |  12 px |

Les tailles sont centralisées dans les ressources.

---

# 6. Espacement

Le système utilise une grille de **8 pixels**.

Exemples :

| Valeur | Utilisation        |
| ------ | ------------------ |
| 4 px   | Ajustements fins   |
| 8 px   | Espacement minimal |
| 16 px  | Entre contrôles    |
| 24 px  | Entre sections     |
| 32 px  | Entre panneaux     |

Cette grille assure une cohérence visuelle.

---

# 7. Icônes

Les icônes sont vectorielles.

Bibliothèque recommandée :

* **Segoe Fluent Icons** ;
* ou une bibliothèque SVG unifiée.

Les images bitmap sont réservées aux illustrations.

---

# 8. Boutons

Catégories :

| Type      | Usage                 |
| --------- | --------------------- |
| Primary   | Action principale     |
| Secondary | Action complémentaire |
| Outline   | Action peu fréquente  |
| Icon      | Action compacte       |
| Danger    | Suppression           |

Chaque type possède un style unique.

---

# 9. Champs de saisie

Tous les contrôles de saisie partagent :

* hauteur uniforme ;
* marges homogènes ;
* indication de champ obligatoire ;
* message d'erreur intégré ;
* comportement identique du focus.

---

# 10. DataGrid

Le `DataGrid` est standardisé.

Règles :

* en-têtes homogènes ;
* tri visuel explicite ;
* virtualisation activée ;
* lignes alternées ;
* sélection clairement visible.

Aucune vue ne redéfinit localement son apparence.

---

# 11. Cartes (Cards)

Les cartes servent à présenter des informations synthétiques :

* indicateurs ;
* statistiques ;
* résumés.

Elles utilisent :

* fond uniforme ;
* bordure discrète ;
* espacement constant ;
* ombre légère (si retenue par la charte).

---

# 12. Barre de navigation

La navigation principale comprend :

* icône ;
* libellé ;
* état actif ;
* état survol ;
* état désactivé.

Elle reste cohérente dans tous les modules.

---

# 13. Barre d'outils

Les actions les plus fréquentes sont accessibles via une barre d'outils commune.

Exemples :

* Nouveau ;
* Modifier ;
* Supprimer ;
* Enregistrer ;
* Annuler ;
* Actualiser ;
* Exporter ;
* Imprimer.

Les icônes et libellés sont uniformisés.

---

# 14. Messages utilisateur

Les messages sont classés en quatre catégories :

| Type          | Couleur |
| ------------- | ------- |
| Information   | Info    |
| Succès        | Success |
| Avertissement | Warning |
| Erreur        | Error   |

Le vocabulaire est clair, sans jargon technique.

---

# 15. États des contrôles

Chaque contrôle définit explicitement les états suivants :

* normal ;
* survol ;
* focus ;
* sélection ;
* désactivé ;
* erreur ;
* chargement.

Les transitions restent discrètes et cohérentes.

---

# 16. Responsive interne

Bien que WPF cible principalement le poste de travail, l'interface doit :

* s'adapter aux différentes résolutions ;
* supporter le redimensionnement ;
* éviter les tailles fixes lorsque possible.

Les panneaux privilégient les conteneurs flexibles (`Grid`, `DockPanel`, etc.).

---

# 17. Accessibilité

Le Design System intègre dès l'origine :

* contraste suffisant ;
* navigation clavier complète ;
* focus visible ;
* taille de police configurable ;
* compatibilité avec les lecteurs d'écran.

L'accessibilité n'est pas une option mais une exigence de conception.

---

# 18. Performance visuelle

Les ressources graphiques sont mutualisées.

Les styles sont définis une seule fois.

Les animations sont limitées aux interactions utiles et ne doivent jamais dégrader la fluidité.

---

# 19. Gouvernance

Toute création ou modification d'un composant visuel suit ce processus :

1. proposition ;
2. validation ;
3. documentation ;
4. ajout au Design System ;
5. réutilisation.

Aucun style local ne doit devenir une référence de fait.

---

# 20. Critères d'acceptation

Le Design System est validé lorsque :

* toutes les vues utilisent les ressources communes ;
* les couleurs et typographies proviennent des tokens officiels ;
* les composants visuels sont homogènes ;
* les règles d'accessibilité sont respectées ;
* les styles locaux sont limités à des cas exceptionnels dûment justifiés.

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                                    |
| ------- | --------------------------------------------------------------------------- |
| ADR-076 | Adoption d'un Design System centralisé                                      |
| ADR-077 | Utilisation de tokens pour les couleurs et ressources graphiques            |
| ADR-078 | Grille d'espacement basée sur des multiples de 8 px                         |
| ADR-079 | Standardisation des styles de contrôles WPF                                 |
| ADR-080 | Les composants visuels sont réutilisables et documentés avant leur adoption |

---

# Conclusion

Le **Volume 15** établit le **Design System officiel** de **PaieEducation ERP**. Il définit les règles de présentation, les ressources partagées et les conventions UX qui garantiront une interface cohérente, accessible et pérenne sur l'ensemble de l'application.

## Recommandation pour le Volume 16

Le **Volume 16** devrait constituer une **bibliothèque de composants métier WPF**. Il décrira chaque contrôle réutilisable sous forme de spécifications d'implémentation :

* `AgentSelector`
* `PayrollPeriodPicker`
* `RubriqueSelector`
* `GradeSelector`
* `CorpsSelector`
* `EchelonSelector`
* `MoneyTextBox`
* `PercentageTextBox`
* `FormulaEditor`
* `PayrollSummaryCard`
* `ValidationBanner`
* `ExplainabilityPanel`
* `AuditTimeline`
* `BulletinPreview`
* `SearchPanel`
* `FilterPanel`

Ce volume formera le **catalogue officiel des composants métier** de **PaieEducation ERP**, garantissant une interface uniforme et une forte réutilisation dans tous les modules.

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome E — Présentation (WPF)**

# **Volume 16**

# **Bibliothèque Officielle des Composants Métier WPF (Business Controls Library)**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Architecture**)
**Technologies :** .NET 10 LTS • WPF • MVVM • CommunityToolkit.Mvvm • XAML

---

# Préambule

L'un des principaux facteurs de réussite d'un ERP est la **standardisation de son interface utilisateur**.

Dans les ERP professionnels (SAP, Oracle Fusion, Microsoft Dynamics 365, Sage X3, Odoo Enterprise…), les écrans ne sont jamais construits à partir de contrôles WPF natifs uniquement.

Ils reposent sur une **bibliothèque interne de composants métier**.

Cette approche présente plusieurs avantages :

* homogénéité visuelle ;
* homogénéité fonctionnelle ;
* réduction importante du code XAML ;
* maintenance simplifiée ;
* évolution centralisée.

Le présent volume définit la **Business Controls Library** de **PaieEducation ERP**.

---

# 1. Objectifs

La bibliothèque doit garantir :

* réutilisation maximale ;
* encapsulation de la logique d'interface ;
* cohérence graphique ;
* conformité MVVM ;
* personnalisation par styles ;
* testabilité.

---

# 2. Architecture générale

```text
Presentation

Controls

│

├── Inputs
├── Selectors
├── Payroll
├── Dashboard
├── Dialogs
├── Search
├── Grids
├── Cards
├── Timeline
├── Validation
├── Reporting
└── Shared
```

Chaque composant possède :

* son XAML ;
* son code-behind minimal ;
* son ViewModel si nécessaire ;
* sa documentation.

---

# 3. Hiérarchie

```text
Control

↓

ERP Control

↓

Business Control

↓

Module Control
```

Exemple :

```text
Control

↓

ERPComboBox

↓

AgentSelector

↓

PayrollAgentSelector
```

---

# 4. Convention de nommage

Tous les composants suivent :

```
ERPxxxxx
```

pour les composants génériques.

Exemples :

```
ERPButton

ERPTextBox

ERPDataGrid

ERPDialog

ERPCard

ERPWindow
```

Les composants métier suivent :

```
AgentSelector

PayrollSummaryCard

RubriqueSelector

PeriodPicker
```

---

# 5. Famille Input Controls

Composants :

```
ERPTextBox

ERPNumberBox

ERPMoneyBox

ERPPercentageBox

ERPDatePicker

ERPDateRangePicker

ERPFormulaBox

ERPSearchBox
```

Tous utilisent les mêmes styles.

---

# 6. Famille Selector Controls

Composants spécialisés :

```
AgentSelector

GradeSelector

CorpsSelector

EchelonSelector

CategorieSelector

ContratSelector

PeriodeSelector

RubriqueSelector

EtablissementSelector
```

Ils encapsulent :

* recherche ;
* filtrage ;
* validation ;
* sélection.

---

# 7. MoneyTextBox

Contrôle indispensable.

Fonctionnalités :

* format DZD ;
* séparateur configurable ;
* validation décimale ;
* négatifs interdits si besoin ;
* culture configurable.

Il remplace totalement un TextBox classique pour les montants.

---

# 8. PercentageTextBox

Fonctionnalités :

* pourcentage
* validation
* limites
* affichage %

---

# 9. FormulaEditor

Contrôle dédié aux formules réglementaires.

Fonctionnalités :

* coloration syntaxique
* validation
* autocomplétion
* aide contextuelle
* aperçu

Il sera utilisé par le moteur de formules.

---

# 10. SearchPanel

Composant commun.

Comprend :

* recherche libre
* filtres
* réinitialisation
* sauvegarde des critères
* historique

Tous les modules utilisent ce composant.

---

# 11. FilterPanel

Responsabilités :

* filtres dynamiques
* groupes
* favoris
* filtres rapides

---

# 12. ERPDataGrid

Le DataGrid ERP est un composant personnalisé.

Fonctionnalités :

* colonnes configurables
* virtualisation
* tri
* filtres
* export
* sélection multiple
* copier/coller
* menu contextuel

Toutes les listes utilisent ce composant.

---

# 13. PayrollSummaryCard

Carte de synthèse.

Affiche :

```
Brut

↓

Retenues

↓

Cotisations

↓

IRG

↓

Net
```

Elle est utilisée :

* aperçu
* validation
* impression

---

# 14. ValidationBanner

Affiche :

* erreurs
* warnings
* validations
* informations

Il remplace les MessageBox classiques.

---

# 15. ExplainabilityPanel

Composant majeur.

Permet d'afficher :

```
Rubrique

↓

Variables

↓

Formule

↓

Résultat

↓

Explication
```

Il est directement connecté au **Explainability Engine** décrit dans le Tome C.

---

# 16. AuditTimeline

Affiche :

```
Calcul

↓

Validation

↓

Correction

↓

Recalcul

↓

Impression
```

Chaque étape est horodatée.

---

# 17. BulletinPreview

Contrôle complexe.

Il affiche :

* bulletin complet
* zoom
* pagination
* impression
* export PDF

Il utilise QuestPDF comme source de rendu.

---

# 18. Dashboard Cards

Bibliothèque :

```
EmployeeCard

PayrollCard

AlertCard

StatisticCard

ChartCard

InfoCard
```

Toutes héritent d'une base commune.

---

# 19. Dialog Framework

Bibliothèque :

```
ConfirmationDialog

SelectionDialog

ErrorDialog

InformationDialog

ProgressDialog

InputDialog
```

Tous les dialogues utilisent le même moteur.

---

# 20. Navigation Controls

Composants :

```
NavigationDrawer

NavigationGroup

Breadcrumb

WorkspaceTabs

ModuleHeader
```

Ils constituent le Shell de l'application.

---

# 21. Indicateurs de chargement

Bibliothèque :

```
BusyOverlay

LoadingSpinner

ProgressRing

ProgressBar

SkeletonLoader
```

L'utilisateur reçoit toujours un retour visuel pendant les traitements.

---

# 22. Composants de validation

```
ValidationSummary

ValidationBadge

ValidationIcon

RequiredIndicator

ValidationTooltip
```

Ils assurent une présentation uniforme des erreurs.

---

# 23. Structure interne d'un composant

Chaque composant suit l'organisation suivante :

```text
AgentSelector

├── AgentSelector.xaml
├── AgentSelector.xaml.cs
├── AgentSelectorViewModel.cs (si nécessaire)
├── AgentSelectorTheme.xaml
├── AgentSelectorTests.cs
└── README.md
```

Cette structure facilite la maintenance et les tests.

---

# 24. Cycle de vie

Un composant suit les étapes :

```text
Construction

↓

Injection des dépendances

↓

Initialisation

↓

Chargement des données

↓

Interaction utilisateur

↓

Libération des ressources
```

Les composants implémentant `IDisposable` doivent libérer explicitement leurs ressources.

---

# 25. Intégration MVVM

Les composants n'accèdent jamais directement :

* aux repositories ;
* au domaine ;
* à SQLite.

Ils communiquent uniquement via :

* leurs propriétés de dépendance ;
* les commandes (`RelayCommand`) ;
* les services injectés.

---

# 26. Performances

Objectifs :

| Élément                        | Temps cible |
| ------------------------------ | ----------: |
| Création d'un contrôle simple  |     < 10 ms |
| Chargement d'un sélecteur      |    < 100 ms |
| Ouverture d'un DataGrid        |    < 200 ms |
| Rafraîchissement d'une carte   |     < 50 ms |
| Affichage d'un BulletinPreview |    < 500 ms |

---

# 27. Tests

Chaque composant doit disposer de :

* tests unitaires ;
* tests d'intégration UI lorsque pertinent ;
* validation des styles ;
* tests d'accessibilité ;
* tests de performance.

---

# 28. Catalogue officiel

La version 1 du catalogue comprend environ **60 à 80 composants réutilisables**, répartis entre :

| Famille    | Nombre estimé |
| ---------- | ------------: |
| Inputs     |            15 |
| Selectors  |            12 |
| Grids      |             8 |
| Cards      |            10 |
| Dialogs    |             8 |
| Navigation |             8 |
| Validation |             6 |
| Reporting  |             6 |
| Divers     |            10 |

L'objectif est que **plus de 90 % de l'interface utilisateur** soit construite à partir de ces composants plutôt qu'avec des contrôles WPF natifs.

---

# 29. Critères d'acceptation

La Business Controls Library est validée lorsque :

* tous les composants sont documentés ;
* chaque composant possède une responsabilité clairement définie ;
* les styles sont centralisés dans le Design System (Volume 15) ;
* les composants sont compatibles avec le MVVM strict ;
* aucun composant ne contient de logique métier ;
* les performances et l'accessibilité sont conformes aux objectifs du projet.

---

# 30. ADR (Architecture Decision Records)

| ADR     | Décision                                                                              |
| ------- | ------------------------------------------------------------------------------------- |
| ADR-081 | Création d'une **Business Controls Library** propriétaire pour l'ERP                  |
| ADR-082 | Préfixe `ERP` réservé aux composants génériques de la plateforme                      |
| ADR-083 | Composants métier spécialisés pour les objets de paie et de gestion du personnel      |
| ADR-084 | Réutilisation systématique des composants dans tous les modules                       |
| ADR-085 | Les composants sont indépendants du domaine métier et des technologies de persistance |

---

# Conclusion

Le **Volume 16** établit la **Business Controls Library** de **PaieEducation ERP**. En définissant une bibliothèque de composants réutilisables, normalisés et compatibles avec le **Design System** (Volume 15) et l'architecture **MVVM** (Volume 14), il fournit une base solide pour développer une interface cohérente, performante et maintenable.

## Recommandation pour le Volume 17

Le **Volume 17** devrait être consacré au **Framework de formulaires ERP (ERP Form Framework)**. Il spécifiera :

* l'architecture des formulaires de création, consultation et modification ;
* les modèles de disposition (*layout templates*) ;
* les règles de validation en temps réel ;
* la gestion des états (création, édition, lecture seule, archivage) ;
* les barres d'actions normalisées ;
* les mécanismes de détection des modifications non enregistrées ;
* les conventions de navigation entre formulaires.

Ce volume deviendra la référence pour la conception de tous les écrans de saisie de **PaieEducation ERP**, garantissant une expérience utilisateur uniforme dans l'ensemble des modules.

Je recommande une évolution supplémentaire de l'architecture documentaire avant de poursuivre.

À partir du **Volume 17**, nous pouvons dépasser le simple concept de "formulaire WPF". Les ERP modernes (SAP Fiori, Dynamics 365, Oracle Fusion, IFS Cloud…) utilisent un véritable **Framework de Workspace**, où chaque écran est considéré comme un **espace de travail métier** plutôt qu'une simple fenêtre.

Pour **PaieEducation ERP**, je recommande donc que le **Framework de formulaires** soit conçu comme un **Workspace Framework**, capable de gérer :

* les formulaires de saisie ;
* les écrans de consultation ;
* les assistants (wizards) ;
* les tableaux de bord ;
* les écrans de validation ;
* les écrans de calcul ;
* les aperçus (Preview).

Cette approche est plus évolutive et mieux adaptée à un ERP.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome E — Présentation (WPF)**

# **Volume 17**

# **ERP Workspace Framework – Architecture des Espaces de Travail, Formulaires et Interactions Utilisateur**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Architecture**)
**Technologies :** .NET 10 LTS • WPF • MVVM • CommunityToolkit.Mvvm

---

# 1. Objet

Ce volume définit le **Workspace Framework** de **PaieEducation ERP**.

Un *Workspace* représente un espace de travail métier autonome. Il remplace la notion classique de « formulaire » et constitue l'unité fonctionnelle de l'interface utilisateur.

---

# 2. Principes

Chaque Workspace doit être :

* centré sur une tâche métier ;
* indépendant des autres Workspaces ;
* réutilisable ;
* testable ;
* compatible MVVM ;
* intégrable dans la navigation principale.

---

# 3. Types de Workspaces

Le framework distingue les catégories suivantes :

| Type               | Usage                            |
| ------------------ | -------------------------------- |
| Consultation       | Recherche et affichage           |
| Édition            | Création ou modification         |
| Assistant (Wizard) | Processus guidé                  |
| Tableau de bord    | Indicateurs et synthèses         |
| Validation         | Contrôle et approbation          |
| Calcul             | Traitements métier (ex. paie)    |
| Aperçu             | Prévisualisation PDF, impression |

Chaque type possède un comportement standardisé.

---

# 4. Architecture d'un Workspace

```text
Workspace
│
├── View
├── ViewModel
├── Commands
├── Validation
├── Navigation
├── Toolbar
├── StatusBar
└── Services
```

Les responsabilités sont clairement séparées.

---

# 5. Cycle de vie

Chaque Workspace suit le cycle suivant :

```text
Création
    ↓
Injection des dépendances
    ↓
Initialisation
    ↓
Chargement des données
    ↓
Interaction utilisateur
    ↓
Sauvegarde éventuelle
    ↓
Fermeture
```

Chaque étape peut être interceptée pour exécuter des traitements spécifiques (journalisation, validation, confirmation de fermeture, etc.).

---

# 6. États d'un Workspace

Un Workspace peut être dans l'un des états suivants :

| État           | Description               |
| -------------- | ------------------------- |
| Initialisation | Construction de l'écran   |
| Chargement     | Lecture des données       |
| Prêt           | Interaction normale       |
| Modification   | Données modifiées         |
| Validation     | Vérifications en cours    |
| Sauvegarde     | Enregistrement            |
| Erreur         | Incident bloquant         |
| Fermeture      | Libération des ressources |

Les transitions d'état sont pilotées par le ViewModel.

---

# 7. Barre d'actions standard

Tous les Workspaces disposent d'une barre d'actions homogène.

Actions courantes :

* Nouveau
* Ouvrir
* Enregistrer
* Enregistrer sous
* Annuler
* Actualiser
* Imprimer
* Exporter
* Historique
* Aide

Chaque action est reliée à une `RelayCommand`.

---

# 8. Détection des modifications

Le framework doit détecter automatiquement :

* les champs modifiés ;
* les suppressions ;
* les ajouts ;
* les changements dans les collections.

Avant la fermeture d'un Workspace contenant des modifications non enregistrées, une confirmation est demandée à l'utilisateur.

---

# 9. Validation

La validation est organisée en trois niveaux :

1. **Validation de présentation** (format, champs obligatoires).
2. **Validation applicative** (règles fonctionnelles via la couche Application).
3. **Validation métier** (invariants du domaine).

Les messages d'erreur sont regroupés dans un `ValidationSummary` tout en restant affichés au niveau des champs concernés.

---

# 10. Gestion des erreurs

Les erreurs sont classées selon leur nature :

| Niveau           | Exemple            |
| ---------------- | ------------------ |
| Information      | Opération terminée |
| Avertissement    | Donnée incomplète  |
| Erreur métier    | Contrat invalide   |
| Erreur technique | Base indisponible  |

Les erreurs techniques sont journalisées et présentées avec un message compréhensible, sans exposer de détails internes.

---

# 11. Navigation entre Workspaces

La navigation est assurée par `INavigationService`.

Fonctionnalités :

* ouverture ;
* fermeture ;
* remplacement ;
* navigation avec paramètres ;
* retour arrière si pertinent.

Les Workspaces ne se créent jamais directement entre eux.

---

# 12. Gestion des onglets

Le Shell peut héberger plusieurs Workspaces simultanément sous forme d'onglets.

Règles :

* un titre explicite ;
* indication des modifications non enregistrées ;
* fermeture individuelle ;
* possibilité d'épingler certains Workspaces.

---

# 13. Chargement asynchrone

Les opérations longues sont exécutées en arrière-plan.

Pendant le chargement :

* affichage d'un `BusyOverlay` ;
* désactivation des actions incompatibles ;
* possibilité d'annulation lorsque cela est pertinent.

---

# 14. Modèles de disposition (Layout Templates)

Le framework fournit plusieurs modèles de mise en page :

* **FormLayout** : formulaire classique ;
* **MasterDetailLayout** : liste + détail ;
* **DashboardLayout** : cartes et graphiques ;
* **WizardLayout** : étapes successives ;
* **PreviewLayout** : aperçu de document.

Les nouveaux Workspaces doivent s'appuyer sur ces modèles avant de créer un layout spécifique.

---

# 15. Historique et audit

Chaque Workspace peut afficher un historique des opérations effectuées sur l'entité en cours (création, modification, validation, recalcul, impression).

Cet historique est fourni par les services applicatifs et affiché via le composant `AuditTimeline`.

---

# 16. Intégration avec les services

Les ViewModels interagissent uniquement avec :

* les services applicatifs ;
* les services de navigation ;
* les services de dialogue ;
* les services de notification.

Ils ne connaissent ni SQLite, ni Dapper, ni les repositories.

---

# 17. Performance

Objectifs :

| Opération                           | Temps cible |
| ----------------------------------- | ----------: |
| Ouverture d'un Workspace            |    < 300 ms |
| Chargement d'un formulaire standard |    < 500 ms |
| Passage d'un onglet à un autre      |    < 150 ms |
| Fermeture d'un Workspace            |    < 100 ms |

---

# 18. Tests

Le Workspace Framework est validé par :

* tests unitaires des ViewModels ;
* tests des commandes ;
* tests de navigation ;
* tests de validation ;
* tests de cycle de vie.

Les comportements communs sont testés une seule fois dans les classes de base.

---

# 19. Critères d'acceptation

Le framework est considéré comme conforme lorsque :

* tous les écrans reposent sur un Workspace ;
* les états sont gérés de manière homogène ;
* la navigation est centralisée ;
* les validations sont cohérentes ;
* les modifications non enregistrées sont détectées ;
* les performances respectent les objectifs définis.

---

# 20. ADR (Architecture Decision Records)

| ADR     | Décision                                                                     |
| ------- | ---------------------------------------------------------------------------- |
| ADR-086 | Adoption d'un **Workspace Framework** en remplacement des formulaires isolés |
| ADR-087 | Cycle de vie standardisé pour tous les Workspaces                            |
| ADR-088 | Barre d'actions commune à l'ensemble des modules                             |
| ADR-089 | Détection automatique des modifications non enregistrées                     |
| ADR-090 | Utilisation de modèles de disposition réutilisables pour tous les Workspaces |

---

# Conclusion

Le **Volume 17** remplace la notion traditionnelle de formulaire par celle de **Workspace métier**, offrant une architecture plus proche des ERP modernes. Cette standardisation améliore la cohérence fonctionnelle, la réutilisation des composants et la maintenabilité de l'application.

## Recommandation pour le Volume 18

Le **Volume 18** devrait être consacré au **Framework DataGrid ERP**, avec un niveau de détail couvrant :

* l'architecture du composant `ERPDataGrid` ;
* les colonnes configurables et typées ;
* les filtres avancés ;
* le tri multi-colonnes ;
* la recherche instantanée ;
* la virtualisation ;
* les regroupements ;
* les exports (Excel via **ClosedXML**, PDF via **QuestPDF**) ;
* la personnalisation des vues utilisateur ;
* les performances sur de grands volumes de données.

Ce document fera du `ERPDataGrid` un composant central de **PaieEducation ERP**, réutilisé dans tous les modules manipulant des listes de données.

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome E — Présentation (WPF)**

# **Volume 18**

# **ERP DataGrid Framework – Architecture, Virtualisation, Filtrage, Personnalisation et Gestion des Données**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • WPF • MVVM • ClosedXML • QuestPDF • CommunityToolkit.Mvvm

---

# Préambule

Le **DataGrid** est le composant le plus utilisé dans un ERP.

Dans **PaieEducation ERP**, il ne s'agit pas d'un simple contrôle `DataGrid` WPF mais d'un véritable **Framework de gestion des données tabulaires**, conçu pour :

* manipuler plusieurs dizaines de milliers de lignes ;
* offrir une expérience utilisateur homogène ;
* être entièrement configurable ;
* servir de base à tous les modules de l'application.

Le composant officiel est nommé :

```text
ERPDataGrid
```

Il remplace systématiquement le `DataGrid` WPF natif dans les écrans métiers.

---

# 1. Objectifs

Le framework doit garantir :

* performances élevées ;
* virtualisation native ;
* personnalisation utilisateur ;
* export multi-format ;
* accessibilité ;
* réutilisation complète.

---

# 2. Architecture

```text
ERPDataGrid

│

├── Data Source
├── Columns Engine
├── Filtering Engine
├── Sorting Engine
├── Grouping Engine
├── Selection Engine
├── Export Engine
├── Personalization Engine
├── Virtualization Engine
├── Clipboard Engine
└── Context Menu Engine
```

Chaque moteur est indépendant et extensible.

---

# 3. Principe MVVM

Le contrôle est piloté exclusivement par le ViewModel.

```text
ViewModel

↓

ObservableCollection

↓

CollectionView

↓

ERPDataGrid
```

Le code-behind du contrôle ne contient aucune logique métier.

---

# 4. Sources de données

Le composant accepte :

* `ObservableCollection<T>`
* `IReadOnlyList<T>`
* `ICollectionView`
* collections paginées
* collections virtuelles

La liaison est réalisée via le binding WPF.

---

# 5. Colonnes

Chaque colonne est décrite par une définition déclarative.

Exemple de propriétés :

| Propriété  | Description                         |
| ---------- | ----------------------------------- |
| Nom        | Identifiant                         |
| Titre      | Libellé affiché                     |
| Largeur    | Fixe, Auto ou Étoile                |
| Alignement | Gauche, Centre, Droite              |
| Format     | Texte, Date, Monétaire, Pourcentage |
| Triable    | Oui/Non                             |
| Filtrable  | Oui/Non                             |
| Visible    | Oui/Non                             |
| Exportable | Oui/Non                             |

Les colonnes sont configurables sans modifier le code métier.

---

# 6. Types de colonnes

Le framework fournit notamment :

* `TextColumn`
* `NumberColumn`
* `MoneyColumn`
* `PercentageColumn`
* `DateColumn`
* `BooleanColumn`
* `StatusColumn`
* `IconColumn`
* `ActionColumn`
* `HyperlinkColumn`
* `ProgressColumn`

Chaque type applique automatiquement le style approprié.

---

# 7. Tri

Fonctionnalités :

* tri simple ;
* tri multi-colonnes ;
* ordre ascendant ou descendant ;
* mémorisation des préférences utilisateur.

Le tri est stable et déterministe.

---

# 8. Filtrage

Le moteur de filtrage prend en charge :

* texte ;
* valeurs numériques ;
* dates ;
* booléens ;
* listes de valeurs ;
* plages de montants ;
* plages de dates.

Les filtres peuvent être combinés.

---

# 9. Recherche instantanée

Le composant intègre une recherche rapide.

Caractéristiques :

* recherche sur plusieurs colonnes ;
* mise en évidence des résultats ;
* actualisation en temps réel ;
* possibilité de limiter les colonnes concernées.

---

# 10. Regroupement

Les utilisateurs peuvent regrouper les données par :

* établissement ;
* corps ;
* grade ;
* catégorie ;
* période ;
* statut.

Les groupes affichent des synthèses (nombre d'éléments, totaux, etc.).

---

# 11. Virtualisation

La virtualisation est activée par défaut.

Objectifs :

* affichage fluide de grands volumes ;
* chargement progressif ;
* consommation mémoire maîtrisée.

Aucun écran ne doit désactiver cette fonctionnalité sans justification technique.

---

# 12. Sélection

Modes pris en charge :

* ligne unique ;
* sélection multiple ;
* sélection par plage ;
* sélection par cases à cocher.

Les sélections sont conservées lors des rafraîchissements lorsque cela est possible.

---

# 13. Menu contextuel

Le menu contextuel standard propose notamment :

* Ouvrir ;
* Modifier ;
* Dupliquer ;
* Supprimer ;
* Exporter ;
* Imprimer ;
* Copier ;
* Historique.

Les modules peuvent y ajouter des actions spécifiques.

---

# 14. Export

Formats pris en charge :

| Format         | Bibliothèque |
| -------------- | ------------ |
| Excel          | ClosedXML    |
| PDF            | QuestPDF     |
| CSV            | Natif        |
| Presse-papiers | WPF          |

L'export respecte :

* l'ordre des colonnes ;
* les formats d'affichage ;
* les filtres actifs ;
* les regroupements lorsque cela est pertinent.

---

# 15. Personnalisation utilisateur

Chaque utilisateur peut personnaliser :

* l'ordre des colonnes ;
* leur largeur ;
* leur visibilité ;
* les tris ;
* les filtres favoris.

Les préférences sont persistées dans la couche Infrastructure.

---

# 16. Calculs et synthèses

Le DataGrid peut afficher :

* nombre de lignes ;
* somme ;
* moyenne ;
* minimum ;
* maximum.

Ces indicateurs sont calculés automatiquement selon la configuration des colonnes.

---

# 17. Rafraîchissement

Le framework distingue :

* rafraîchissement complet ;
* rafraîchissement partiel ;
* mise à jour d'une ligne ;
* mise à jour d'une cellule.

Cette granularité limite les opérations coûteuses.

---

# 18. Accessibilité

Le composant est compatible avec :

* navigation clavier ;
* lecteurs d'écran ;
* contraste élevé ;
* raccourcis clavier ;
* focus visible.

Toutes les fonctionnalités sont accessibles sans souris.

---

# 19. Performances

Objectifs :

| Opération                     | Temps cible |
| ----------------------------- | ----------: |
| Chargement de 10 000 lignes   |       < 1 s |
| Tri d'une colonne indexée     |    < 300 ms |
| Application d'un filtre       |    < 500 ms |
| Export Excel de 10 000 lignes |       < 5 s |
| Export PDF de 1 000 lignes    |      < 10 s |

---

# 20. Structure interne

```text
ERPDataGrid

├── ERPDataGrid.xaml
├── ERPDataGrid.xaml.cs
├── ERPDataGridViewModel.cs
├── Columns
├── Filters
├── Export
├── Personalization
├── Virtualization
├── Themes
└── Tests
```

Les moteurs sont organisés par responsabilité.

---

# 21. Journalisation

Les opérations suivantes peuvent être journalisées :

* ouverture d'une vue ;
* export ;
* impression ;
* changement de configuration ;
* erreurs d'affichage.

La journalisation est assurée via `Microsoft.Extensions.Logging`.

---

# 22. Tests

Le composant est validé par :

* tests unitaires des moteurs (tri, filtrage, export) ;
* tests d'intégration WPF ;
* tests de performance ;
* tests d'accessibilité ;
* tests de charge.

---

# 23. Critères d'acceptation

Le `ERPDataGrid` est conforme lorsque :

* il remplace le `DataGrid` natif dans tous les modules ;
* la virtualisation est active ;
* les exports fonctionnent dans tous les formats prévus ;
* les personnalisations utilisateur sont persistées ;
* les performances répondent aux objectifs fixés.

---

# 24. ADR (Architecture Decision Records)

| ADR     | Décision                                               |
| ------- | ------------------------------------------------------ |
| ADR-091 | Création du composant propriétaire `ERPDataGrid`       |
| ADR-092 | Virtualisation activée par défaut                      |
| ADR-093 | Moteurs de tri, filtrage et regroupement indépendants  |
| ADR-094 | Personnalisation persistante des vues utilisateur      |
| ADR-095 | Exports standardisés via **ClosedXML** et **QuestPDF** |

---

# Conclusion

Le **Volume 18** définit le **ERP DataGrid Framework**, composant central de l'interface de **PaieEducation ERP**. En standardisant la gestion des listes, des filtres, des exports et de la personnalisation, il garantit une expérience utilisateur homogène et des performances adaptées à un ERP moderne.

## Recommandation pour le Volume 19

Le **Volume 19** devrait être consacré au **Shell, à la Navigation et au Workspace Manager**, avec un niveau de détail couvrant :

* l'architecture de la fenêtre principale (`MainShell`) ;
* le gestionnaire d'espaces de travail (`WorkspaceManager`) ;
* les onglets dynamiques ;
* les panneaux ancrables (*Docking Layout*) ;
* les raccourcis clavier globaux ;
* la recherche universelle (*Global Search*) ;
* les favoris et l'historique de navigation ;
* les notifications centralisées ;
* les tableaux de bord personnalisables.

Ce volume fera du **Shell** le véritable **centre de commande** de **PaieEducation ERP**, comparable à celui des ERP de niveau entreprise tels que SAP ou Microsoft Dynamics 365.

Je pense que nous pouvons encore faire évoluer la qualité de cette documentation.

À partir du **Volume 19**, je recommande de ne plus considérer le **Shell** comme une simple fenêtre principale WPF. Dans les ERP de niveau entreprise, le Shell est un **cadre d'exécution (Application Framework)** qui orchestre les Workspaces, les services transverses, les notifications, la sécurité, les préférences utilisateur et la navigation.

Je propose donc que **MainWindow** soit renommée conceptuellement en **Application Shell**, composée de sous-systèmes spécialisés.

Cette approche rapproche **PaieEducation ERP** des architectures utilisées par SAP, Oracle Fusion ou Microsoft Dynamics.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome E — Présentation (WPF)**

# **Volume 19**

# **Application Shell Framework – Architecture du Shell, Navigation, Workspace Manager et Services Transverses**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • WPF • MVVM • CommunityToolkit.Mvvm • Microsoft.Extensions.DependencyInjection • Microsoft.Extensions.Logging

---

# 1. Objet

Le **Application Shell** constitue le point d'entrée unique de **PaieEducation ERP**.

Il est responsable de :

* l'initialisation de l'application ;
* l'hébergement des Workspaces ;
* la navigation ;
* les services transverses ;
* l'expérience utilisateur globale.

Le Shell n'implémente aucune logique métier.

---

# 2. Architecture générale

```text
Application Shell
│
├── Workspace Manager
├── Navigation Manager
├── Module Manager
├── Command Manager
├── Notification Center
├── Status Bar
├── Search Service
├── Favorites Service
├── Recent Items Service
├── Theme Manager
├── Localization Manager
├── User Preferences
├── Help Center
└── Window Manager
```

Chaque sous-système est indépendant et injectable.

---

# 3. Structure du projet

```text
Presentation
│
├── Shell
│   ├── MainShell.xaml
│   ├── MainShellViewModel.cs
│   ├── ShellBootstrapper.cs
│   ├── ShellServices.cs
│   └── ShellResources.xaml
│
├── Navigation
├── Workspaces
├── Controls
├── Dialogs
├── Themes
└── Services
```

Le Shell est organisé comme un module à part entière.

---

# 4. Cycle de démarrage

Le démarrage suit les étapes suivantes :

```text
Application
    ↓
Initialisation du conteneur DI
    ↓
Configuration des services
    ↓
Initialisation du Logger
    ↓
Chargement des paramètres utilisateur
    ↓
Initialisation des modules
    ↓
Création du Shell
    ↓
Ouverture du Workspace d'accueil
```

Chaque étape est journalisée.

---

# 5. Workspace Manager

Le `WorkspaceManager` est responsable de :

* ouvrir un Workspace ;
* fermer un Workspace ;
* réactiver un Workspace existant ;
* gérer les onglets ;
* conserver le contexte utilisateur.

Aucun Workspace ne manipule directement les autres.

---

# 6. Navigation Manager

Le `NavigationManager` fournit :

* navigation hiérarchique ;
* navigation contextuelle ;
* historique ;
* retour arrière ;
* raccourcis de navigation.

Les ViewModels ne manipulent jamais directement les vues WPF.

---

# 7. Module Manager

Chaque domaine fonctionnel est enregistré comme un module.

Exemples :

```text
Personnel

Carrière

Paie

Référentiels

Administration

Paramètres

Reporting
```

Les modules peuvent être activés ou désactivés selon la configuration.

---

# 8. Workspace Tabs

Le Shell prend en charge des onglets dynamiques.

Fonctionnalités :

* ouverture multiple ;
* fermeture individuelle ;
* réorganisation par glisser-déposer ;
* indicateur de modifications ;
* restauration à la prochaine ouverture de l'application.

---

# 9. Barre latérale (Navigation Drawer)

La barre latérale comprend :

* accès aux modules ;
* favoris ;
* éléments récents ;
* recherche ;
* paramètres.

Elle peut être réduite pour optimiser l'espace.

---

# 10. Recherche globale

Le `GlobalSearchService` permet de rechercher rapidement :

* un agent ;
* un bulletin ;
* une rubrique ;
* un établissement ;
* un écran ;
* une commande.

Les résultats sont regroupés par catégorie et accessibles au clavier.

---

# 11. Centre de notifications

Le `NotificationCenter` centralise :

* informations ;
* avertissements ;
* erreurs ;
* succès ;
* événements système.

Les notifications importantes peuvent être historisées.

---

# 12. Barre d'état

La barre d'état affiche notamment :

* utilisateur connecté (si une authentification est introduite ultérieurement) ;
* période de paie active ;
* établissement actif ;
* état des traitements ;
* progression des tâches ;
* date et heure.

Les informations affichées sont configurables.

---

# 13. Gestion des favoris

L'utilisateur peut ajouter en favoris :

* un écran ;
* une recherche ;
* un rapport ;
* un agent ;
* un bulletin.

Les favoris sont synchronisés avec les préférences utilisateur.

---

# 14. Historique

Le `RecentItemsService` conserve l'historique des éléments récemment consultés.

Exemples :

* derniers agents ouverts ;
* derniers bulletins générés ;
* derniers rapports imprimés.

Le nombre d'éléments est configurable.

---

# 15. Gestion des thèmes

Le `ThemeManager` permet :

* le changement de thème à chaud ;
* le support clair/sombre ;
* la personnalisation des couleurs institutionnelles ;
* l'application des ressources WPF sans redémarrage.

---

# 16. Localisation

Le `LocalizationManager` centralise :

* les ressources textuelles ;
* les formats de dates ;
* les formats monétaires ;
* les langues supportées.

Le changement de langue peut être appliqué dynamiquement lorsque cela est possible.

---

# 17. Gestion des fenêtres

Le `WindowManager` supervise :

* les dialogues ;
* les fenêtres secondaires ;
* les aperçus ;
* les fenêtres modales et non modales.

Les Workspaces restent hébergés dans le Shell.

---

# 18. Commandes globales

Le `CommandManager` expose des commandes accessibles depuis n'importe quel Workspace.

Exemples :

* Enregistrer (`Ctrl+S`) ;
* Rechercher (`Ctrl+F`) ;
* Actualiser (`F5`) ;
* Imprimer (`Ctrl+P`) ;
* Exporter (`Ctrl+E`) ;
* Aide (`F1`).

Les raccourcis sont configurables.

---

# 19. Journalisation

Le Shell journalise :

* démarrage ;
* arrêt ;
* ouverture et fermeture des Workspaces ;
* changements de thème ;
* erreurs non gérées ;
* opérations longues.

La journalisation s'appuie sur `Microsoft.Extensions.Logging`.

---

# 20. Performance

Objectifs :

| Opération                | Temps cible |
| ------------------------ | ----------: |
| Démarrage complet        |       < 3 s |
| Ouverture d'un Workspace |    < 300 ms |
| Changement d'onglet      |    < 150 ms |
| Recherche globale        |    < 500 ms |
| Changement de thème      |    < 300 ms |

Ces objectifs doivent être mesurés régulièrement.

---

# 21. Tests

Le Shell est validé par :

* tests unitaires des gestionnaires (Navigation, Workspaces, Notifications) ;
* tests d'intégration de l'initialisation ;
* tests de performance au démarrage ;
* tests des raccourcis clavier ;
* tests d'accessibilité.

---

# 22. Critères d'acceptation

Le **Application Shell** est conforme lorsque :

* tous les Workspaces sont hébergés par le Shell ;
* la navigation est centralisée ;
* les modules sont découplés ;
* les services transverses sont injectés ;
* le démarrage respecte les objectifs de performance ;
* l'expérience utilisateur est homogène.

---

# 23. ADR (Architecture Decision Records)

| ADR     | Décision                                                                                |
| ------- | --------------------------------------------------------------------------------------- |
| ADR-096 | Adoption d'un **Application Shell** comme cadre d'exécution unique                      |
| ADR-097 | Gestion centralisée des Workspaces via `WorkspaceManager`                               |
| ADR-098 | Navigation orchestrée par `NavigationManager`                                           |
| ADR-099 | Centralisation des services transverses (notifications, thèmes, préférences, recherche) |
| ADR-100 | Le Shell ne contient aucune logique métier et dépend uniquement de services injectés    |

---

# Conclusion

Le **Volume 19** transforme la fenêtre principale en un véritable **Application Shell Framework**, responsable de l'orchestration de l'ensemble de l'expérience utilisateur. Cette architecture fournit une base robuste, modulaire et évolutive pour tous les modules de **PaieEducation ERP**.

## Recommandation pour le Volume 20

Le **Volume 20** devrait être consacré à **l'Accessibilité, l'Ergonomie et l'Expérience Utilisateur (UX)** avec un niveau de détail couvrant :

* les règles d'accessibilité (WCAG adaptées à une application bureautique) ;
* la navigation clavier complète ;
* les conventions de raccourcis ;
* les aides contextuelles et infobulles intelligentes ;
* les messages d'erreur et leur rédaction ;
* les états de chargement et de progression ;
* les bonnes pratiques de densité d'information pour un ERP ;
* les principes de conception centrée utilisateur.

Ce volume complétera la couche **Présentation** en définissant les standards ergonomiques qui garantiront une application efficace, inclusive et agréable à utiliser au quotidien.

Je pense que nous pouvons encore faire évoluer le niveau de cette documentation.

À partir de ce volume, je recommande que **PaieEducation ERP** adopte officiellement les principes des **WCAG 2.2 AA** (adaptés à une application bureautique WPF) ainsi que les **Microsoft Windows Desktop UX Guidelines**. Peu d'ERP documentent ces aspects dès la phase de conception, alors qu'ils ont un impact direct sur la productivité, l'accessibilité et la pérennité de l'application.

Cette décision sera formalisée par les **ADR-101 à ADR-105**.

---

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome E — Présentation (WPF)**

# **Volume 20**

# **Accessibilité, Ergonomie et Expérience Utilisateur (UX) – Standards Officiels**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP Enterprise**)
**Technologies :** .NET 10 LTS • WPF • MVVM

---

# 1. Objet

Ce volume définit les règles d'ergonomie et d'accessibilité applicables à tous les modules de **PaieEducation ERP**.

L'objectif est de concevoir une application :

* efficace ;
* accessible ;
* cohérente ;
* confortable pour une utilisation quotidienne intensive.

---

# 2. Principes UX

Les principes fondamentaux sont :

* simplicité ;
* cohérence ;
* prévisibilité ;
* retour immédiat à l'utilisateur ;
* réduction des actions inutiles ;
* prévention des erreurs.

Chaque écran doit être conçu pour minimiser la charge cognitive.

---

# 3. Public cible

L'application est destinée à :

* gestionnaires de paie ;
* responsables des ressources humaines ;
* chefs d'établissement ;
* administrateurs fonctionnels.

Les utilisateurs ne sont pas supposés être des experts techniques.

---

# 4. Densité d'information

Les écrans doivent afficher une forte densité d'information sans nuire à la lisibilité.

Règles :

* hiérarchiser les informations ;
* limiter les espaces perdus ;
* privilégier les tableaux lorsque nécessaire ;
* réserver les cartes aux indicateurs clés.

---

# 5. Navigation clavier

Toutes les fonctionnalités doivent être accessibles au clavier.

Raccourcis recommandés :

| Action              | Raccourci  |
| ------------------- | ---------- |
| Enregistrer         | Ctrl + S   |
| Rechercher          | Ctrl + F   |
| Actualiser          | F5         |
| Imprimer            | Ctrl + P   |
| Exporter            | Ctrl + E   |
| Aide                | F1         |
| Fermer le Workspace | Ctrl + W   |
| Changer d'onglet    | Ctrl + Tab |

Les raccourcis sont documentés et personnalisables.

---

# 6. Gestion du focus

Le focus clavier doit :

* suivre un ordre logique ;
* être toujours visible ;
* être restauré après la fermeture d'un dialogue ;
* ne jamais être perdu lors des changements de vue.

---

# 7. Contraste

Les couleurs respectent un contraste suffisant entre :

* texte et fond ;
* icônes et fond ;
* états sélectionnés ;
* éléments désactivés.

Les combinaisons de couleurs ne doivent jamais être le seul moyen de transmettre une information.

---

# 8. Taille des éléments

Les contrôles interactifs doivent présenter une taille suffisante pour une utilisation confortable, y compris sur des écrans haute résolution.

Les zones cliquables sont homogènes sur l'ensemble de l'application.

---

# 9. Messages utilisateur

Les messages doivent être :

* courts ;
* explicites ;
* orientés solution.

Exemple :

❌ « Erreur 0x80004005 »

✔ « Impossible d'enregistrer le bulletin. Vérifiez que la période de paie est ouverte. »

Les détails techniques sont réservés aux journaux.

---

# 10. États de chargement

Toute opération dépassant environ **300 ms** affiche un indicateur visuel :

* barre de progression ;
* animation discrète ;
* message de contexte.

Les traitements annulables proposent un bouton d'annulation.

---

# 11. Prévention des erreurs

Le système privilégie :

* listes déroulantes plutôt que saisie libre lorsque possible ;
* validation en temps réel ;
* confirmations ciblées pour les actions irréversibles ;
* désactivation des commandes non disponibles.

---

# 12. Confirmation des actions

Une confirmation est demandée uniquement pour les opérations à impact important :

* suppression définitive ;
* clôture de période ;
* recalcul global ;
* restauration d'une sauvegarde.

Les confirmations excessives sont à éviter.

---

# 13. Aides contextuelles

Chaque module peut fournir :

* infobulles ;
* aide contextuelle (`F1`) ;
* explication des règles de calcul ;
* liens vers la documentation interne.

L'utilisateur obtient l'information sans quitter son contexte de travail.

---

# 14. États visuels

Chaque composant doit définir les états suivants :

| État       | Usage                    |
| ---------- | ------------------------ |
| Normal     | Utilisation courante     |
| Survol     | Interaction de la souris |
| Focus      | Navigation clavier       |
| Sélection  | Élément actif            |
| Désactivé  | Action indisponible      |
| Chargement | Traitement en cours      |
| Erreur     | Validation échouée       |
| Succès     | Action réussie           |

Les transitions restent sobres et cohérentes.

---

# 15. Accessibilité

Le système est compatible avec :

* navigation clavier complète ;
* lecteurs d'écran Windows ;
* contrastes élevés ;
* redimensionnement des textes ;
* préférences d'accessibilité du système d'exploitation.

---

# 16. Internationalisation

L'interface prend en charge :

* français ;
* arabe ;
* anglais.

Les formats de date, de nombre et de monnaie sont adaptés à la culture sélectionnée.

Le changement de langue est prévu sans modification du code métier.

---

# 17. Performance perçue

Objectifs :

| Opération                   | Temps cible |
| --------------------------- | ----------: |
| Retour visuel après un clic |    < 100 ms |
| Ouverture d'un dialogue     |    < 200 ms |
| Changement de Workspace     |    < 300 ms |
| Recherche instantanée       |    < 500 ms |

Lorsque le traitement réel est plus long, un indicateur d'activité est affiché.

---

# 18. Journalisation des incidents UX

Les événements suivants peuvent être suivis :

* erreurs d'interface ;
* commandes annulées ;
* échecs de validation ;
* exceptions non gérées ;
* temps de réponse anormalement élevés.

Ces informations alimentent les analyses de qualité.

---

# 19. Évaluation ergonomique

Avant chaque version majeure, une revue UX vérifie notamment :

* cohérence visuelle ;
* simplicité des parcours ;
* accessibilité ;
* performances perçues ;
* compréhension des messages.

Les écarts sont consignés et priorisés.

---

# 20. Critères d'acceptation

La couche Présentation est conforme lorsque :

* tous les écrans sont utilisables au clavier ;
* les contrastes et tailles respectent les standards définis ;
* les messages sont compréhensibles ;
* les temps de réponse respectent les objectifs ;
* les composants sont cohérents avec le Design System (Volume 15).

---

# 21. ADR (Architecture Decision Records)

| ADR     | Décision                                                            |
| ------- | ------------------------------------------------------------------- |
| ADR-101 | Adoption des principes WCAG 2.2 AA adaptés à WPF                    |
| ADR-102 | Navigation clavier complète sur tous les Workspaces                 |
| ADR-103 | Messages utilisateurs orientés métier et non techniques             |
| ADR-104 | Indicateurs de progression obligatoires pour les opérations longues |
| ADR-105 | Revue ergonomique systématique avant chaque version majeure         |

---

# Conclusion

Le **Volume 20** établit les standards d'**accessibilité**, d'**ergonomie** et d'**expérience utilisateur** de **PaieEducation ERP**. Il complète les volumes précédents consacrés au **Design System**, à la **Business Controls Library**, au **Workspace Framework**, au **ERPDataGrid** et au **Application Shell**, en garantissant une interface homogène, performante et adaptée à un usage professionnel intensif.

## Recommandation pour le Tome suivant

La couche **Présentation (Tome E)** est désormais suffisamment définie. La suite logique est d'ouvrir un nouveau **Tome F — Reporting et Documents**, qui couvrira l'ensemble du cycle documentaire de l'ERP :

* **Volume 21** : Architecture du moteur de reporting avec **QuestPDF** ;
* **Volume 22** : Conception des modèles de bulletins de paie et documents administratifs ;
* **Volume 23** : Framework d'export (PDF, Excel avec **ClosedXML**, CSV) ;
* **Volume 24** : Impression, prévisualisation et gestion des files d'impression ;
* **Volume 25** : Signature électronique, archivage documentaire et traçabilité.

Ce nouveau tome constituera la référence complète pour la production documentaire de **PaieEducation ERP**, en cohérence avec la pile technologique retenue (.NET 10 LTS, WPF, MVVM, SQLite, QuestPDF et ClosedXML).

