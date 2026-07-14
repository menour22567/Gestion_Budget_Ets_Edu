# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome C — Moteur de Calcul de Paie**

# **Volume 9**

# **Architecture du Payroll Engine, Pipeline de Calcul et Moteur de Résolution Métier**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Document Fondamental**)
**Technologies :** .NET 10 LTS • C# 14 • SQLite • Clean Architecture • DDD

---

# Préambule

Le présent volume constitue **le cœur technique et fonctionnel** de **PaieEducation ERP**.

Il décrit de manière exhaustive l'architecture du **Payroll Engine**, c'est-à-dire le moteur chargé de produire le bulletin de paie conformément à la réglementation algérienne.

Contrairement aux ERP classiques où les calculs sont souvent dispersés dans le code, **PaieEducation ERP adopte une architecture orientée moteur (Engine-Oriented Architecture)**.

Chaque étape du calcul est isolée, traçable, testable et explicable.

---

# 1. Objectifs du Payroll Engine

Le moteur doit garantir :

* exactitude réglementaire ;
* reproductibilité des calculs ;
* traçabilité complète ;
* performances élevées ;
* possibilité de recalcul intégral ;
* évolutivité réglementaire ;
* indépendance vis-à-vis de l'interface graphique.

Le moteur ne connaît ni WPF, ni QuestPDF, ni SQLite directement.

---

# 2. Vision générale

```text
                Payroll Engine

                      │
────────────────────────────────────────────

Context Builder

Eligibility Engine

Variable Engine

Formula Engine

Calculation Engine

Dependency Resolver

Totals Engine

Validation Engine

Audit Engine

Explainability Engine

Snapshot Engine

────────────────────────────────────────────
```

Chaque sous-moteur possède une responsabilité unique.

---

# 3. Architecture générale

```text
Demande de calcul

↓

Construction du contexte

↓

Validation

↓

Éligibilité

↓

Variables

↓

Résolution des dépendances

↓

Calcul des rubriques

↓

Calcul des retenues

↓

Calcul IRG

↓

Calcul Net

↓

Contrôles

↓

Production du bulletin

↓

Journal d'explication
```

Aucune étape ne peut être ignorée.

---

# 4. Contrat principal

Le moteur expose une interface unique.

```csharp
public interface IPayrollEngine
{
    PayrollResult Calculate(PayrollContext context);
}
```

Toutes les opérations de calcul passent par cette interface.

---

# 5. Construction du contexte

Le **PayrollContext** rassemble toutes les données nécessaires avant le lancement des calculs.

Il comprend notamment :

* agent ;
* période ;
* contrat actif ;
* grade ;
* échelon ;
* corps ;
* catégorie ;
* ancienneté ;
* affectation ;
* situation familiale ;
* variables de paie ;
* paramètres réglementaires.

Le contexte est **immuable** pendant toute l'exécution.

---

# 6. Architecture interne du moteur

```text
PayrollContext

↓

Eligibility Engine

↓

Variable Engine

↓

Formula Engine

↓

Calculation Engine

↓

Totals Engine

↓

Validation Engine

↓

PayrollResult
```

Chaque moteur consomme uniquement le résultat du précédent.

---

# 7. Eligibility Engine

Le moteur d'éligibilité détermine quelles rubriques sont applicables.

Entrées :

* caractéristiques de l'agent ;
* règles d'éligibilité ;
* période ;
* référentiels.

Sortie :

```text
Rubrique 1001 : applicable

Rubrique 1002 : exclue

Rubrique 1003 : applicable
```

Toutes les décisions sont justifiées.

---

# 8. Variable Engine

Ce moteur calcule les variables nécessaires aux formules.

Exemples :

* salaire de base ;
* indice majoré ;
* ancienneté ;
* nombre d'enfants ;
* heures supplémentaires ;
* zone géographique ;
* taux réglementaires.

Les variables sont mises à disposition des moteurs suivants.

---

# 9. Formula Engine

Le moteur des formules transforme les expressions réglementaires en résultats numériques.

Exemple conceptuel :

```text
SalaireBase × Taux
```

Le moteur :

* valide les opérandes ;
* applique les règles d'arrondi ;
* détecte les divisions interdites ;
* contrôle les dépassements.

Les formules sont paramétrables et versionnées.

---

# 10. Dependency Resolver

Certaines rubriques dépendent d'autres rubriques.

Exemple :

```text
Rubrique A

↓

Rubrique B

↓

Rubrique C
```

Le moteur construit un graphe orienté des dépendances et détermine un ordre d'évaluation sans cycle.

En cas de dépendance circulaire, le calcul est interrompu avec une erreur explicite.

---

# 11. Calculation Engine

Le moteur principal exécute les calculateurs.

Exemples de calculateurs :

* Salaire de base ;
* IEP ;
* Indemnités ;
* Retenues ;
* Cotisations ;
* IRG ;
* Net à payer.

Chaque calculateur est isolé et implémente une interface commune.

```csharp
public interface IPayrollCalculator
{
    CalculationResult Calculate(PayrollContext context);
}
```

---

# 12. Orchestrateur des calculateurs

Le `PayrollEngine` agit comme orchestrateur.

```text
Payroll Engine
│
├── BaseSalaryCalculator
├── SeniorityCalculator
├── IndemnityCalculator
├── AllowanceCalculator
├── DeductionCalculator
├── IRGCalculator
├── NetSalaryCalculator
└── TotalsCalculator
```

Chaque calculateur est enregistré dans le conteneur DI.

---

# 13. Pipeline de calcul

Le pipeline suit un ordre strict :

| Étape | Action                 |
| ----- | ---------------------- |
| 1     | Validation du contexte |
| 2     | Éligibilité            |
| 3     | Variables              |
| 4     | Dépendances            |
| 5     | Calcul des gains       |
| 6     | Calcul des retenues    |
| 7     | Cotisations            |
| 8     | Impôts (IRG)           |
| 9     | Totaux                 |
| 10    | Contrôles finaux       |
| 11    | Génération du résultat |

Cet ordre est invariant.

---

# 14. Totals Engine

Le moteur calcule :

* total gains ;
* total retenues ;
* brut imposable ;
* brut cotisable ;
* net imposable ;
* net à payer.

Les montants sont exprimés exclusivement en **Dinar algérien (DZD)**.

---

# 15. Validation Engine

Après le calcul, le moteur vérifie :

* cohérence des montants ;
* équilibre des totaux ;
* présence des rubriques obligatoires ;
* conformité des règles réglementaires.

Toute anomalie est remontée avant la validation du bulletin.

---

# 16. Explainability Engine

Chaque montant produit doit être explicable.

Exemple :

```text
Rubrique : IEP

Résultat :

12 500 DZD

Explication :

Base = 50 000

Taux = 25 %

Résultat = 12 500
```

Chaque étape du calcul est historisée.

---

# 17. Audit Engine

Le moteur d'audit enregistre :

* l'ordre d'exécution ;
* les calculateurs appelés ;
* les variables utilisées ;
* les règles appliquées ;
* les exceptions rencontrées ;
* la durée d'exécution.

Ces informations sont essentielles pour les diagnostics et les contrôles.

---

# 18. Snapshot Engine

À chaque calcul, un instantané est produit.

Le snapshot contient :

* contexte de calcul ;
* version des paramètres ;
* version réglementaire ;
* résultats ;
* journal d'explication.

Il permet de reproduire un calcul à l'identique.

---

# 19. Gestion des versions réglementaires

Le moteur est conçu pour supporter plusieurs versions des règles de paie.

Chaque règle est associée à :

* une date d'effet ;
* une date de fin de validité (optionnelle) ;
* un identifiant de version.

Le moteur sélectionne automatiquement la version applicable à la période calculée.

---

# 20. Recalcul incrémental

Le moteur doit être capable de recalculer uniquement les éléments impactés par une modification.

Exemple :

* changement d'échelon ;
* ajout d'une indemnité ;
* correction d'une variable.

Le graphe des dépendances permet d'identifier les rubriques à recalculer sans relancer l'intégralité du pipeline.

---

# 21. Performances

Objectifs :

| Opération                           | Temps cible |
| ----------------------------------- | ----------: |
| Construction du contexte            |     < 50 ms |
| Éligibilité                         |    < 100 ms |
| Calcul complet d'un bulletin        |    < 300 ms |
| Recalcul incrémental                |    < 100 ms |
| Génération du journal d'explication |     < 50 ms |

Ces objectifs sont définis pour un poste de travail standard.

---

# 22. Tests

Le moteur est couvert par plusieurs niveaux de tests :

* tests unitaires de chaque calculateur ;
* tests d'intégration du pipeline ;
* tests de non-régression réglementaire ;
* tests de performance ;
* tests de reproductibilité.

Toute évolution réglementaire doit être accompagnée de nouveaux jeux d'essais.

---

# 23. Critères d'acceptation

Le Payroll Engine est validé lorsque :

* le pipeline respecte l'ordre défini ;
* chaque calculateur est indépendant ;
* les dépendances entre rubriques sont résolues sans ambiguïté ;
* chaque montant est explicable ;
* les versions réglementaires sont correctement prises en compte ;
* le recalcul incrémental produit les mêmes résultats qu'un recalcul complet.

---

# 24. ADR (Architecture Decision Records)

| ADR     | Décision                                                             |
| ------- | -------------------------------------------------------------------- |
| ADR-046 | Architecture orientée moteur (Engine-Oriented Architecture)          |
| ADR-047 | Pipeline de calcul déterministe et immuable                          |
| ADR-048 | Calculateurs spécialisés implémentant `IPayrollCalculator`           |
| ADR-049 | Moteur d'explicabilité intégré (`Explainability Engine`)             |
| ADR-050 | Support natif des versions réglementaires et du recalcul incrémental |

---

# Conclusion

Le **Volume 9** définit le **Payroll Engine**, véritable cœur algorithmique de **PaieEducation ERP**. Il décrit l'ensemble du pipeline de calcul, l'orchestration des calculateurs, la gestion des dépendances, l'audit et l'explicabilité, afin de garantir des traitements fiables, reproductibles et conformes à la réglementation.

## Recommandation pour le Volume 10

Pour compléter ce tome consacré au moteur de paie, je recommande que le **Volume 10** soit entièrement dédié à la **bibliothèque des calculateurs métier**.

Ce volume décrira, pour chaque calculateur :

* son rôle fonctionnel ;
* son contrat (`IPayrollCalculator`) ;
* ses entrées et sorties ;
* ses dépendances ;
* les variables consommées et produites ;
* les règles de calcul applicables (sans figer les valeurs réglementaires dans le code) ;
* les stratégies d'arrondi et de validation ;
* les jeux de tests unitaires attendus.

Il constituera le guide de développement de tous les calculateurs spécialisés (`BaseSalaryCalculator`, `IRGCalculator`, `IEPCalculator`, `IndemnityCalculator`, etc.) qui composeront le moteur de paie.

# Documentation de Référence du Projet

# **PaieEducation ERP**

## **Version 4.0**

# **Documentation de Spécification d'Implémentation**

---

# **Tome C — Moteur de Calcul de Paie**

# **Volume 10**

# **Bibliothèque des Calculateurs Métier (Payroll Calculators Library) et Contrats d'Implémentation**

**Version :** 4.0
**Statut :** DDS – Detailed Design Specification (**Niveau ERP**)
**Technologies :** .NET 10 LTS • C# 14 • Clean Architecture • DDD • SQLite

---

# Préambule

Le **Payroll Engine** (Volume 9) décrit l'orchestration globale du calcul.

Le présent volume décrit **les calculateurs eux-mêmes**.

Chaque calcul réglementaire est encapsulé dans un composant indépendant.

L'objectif est de disposer d'une **bibliothèque de calculateurs réutilisables**, testables et facilement maintenables.

Cette approche est similaire à celle employée dans les grands ERP de paie, où chaque règle métier est isolée dans un composant spécialisé.

---

# 1. Principes de conception

Un calculateur possède une responsabilité unique.

Il :

* reçoit un contexte de calcul ;
* vérifie ses préconditions ;
* effectue un calcul ;
* retourne un résultat structuré ;
* ne modifie jamais directement le bulletin.

Il ne connaît ni SQLite, ni WPF, ni QuestPDF.

---

# 2. Contrat commun

Tous les calculateurs implémentent un contrat unique.

```csharp
public interface IPayrollCalculator
{
    string Code { get; }

    int Priority { get; }

    bool CanExecute(PayrollContext context);

    Task<CalculationResult> CalculateAsync(
        PayrollContext context,
        CancellationToken cancellationToken);
}
```

---

# 3. Structure du projet

```text
Application

Payroll

Calculators

├── BaseSalary
├── Seniority
├── Indemnities
├── Allowances
├── Deductions
├── Contributions
├── Taxes
├── Totals
├── Validation
└── Helpers
```

Chaque dossier contient :

* le calculateur ;
* ses tests ;
* sa documentation ;
* ses dépendances.

---

# 4. Cycle d'exécution

```text
CanExecute()

↓

Validation

↓

Chargement Variables

↓

Calcul

↓

Contrôles

↓

Résultat

↓

Journal
```

---

# 5. Structure d'un résultat

Le résultat d'un calcul est normalisé.

```csharp
public sealed record CalculationResult
{
    string RubricCode;

    decimal Amount;

    decimal Base;

    decimal Rate;

    bool Success;

    IReadOnlyCollection<CalculationMessage> Messages;

    IReadOnlyCollection<CalculationExplanation> Explanations;
}
```

---

# 6. Classification des calculateurs

La bibliothèque est organisée par domaines fonctionnels.

| Famille       | Responsabilité        |
| ------------- | --------------------- |
| BaseSalary    | Salaire de base       |
| Seniority     | Ancienneté            |
| Indemnities   | Indemnités            |
| Allowances    | Primes                |
| Deductions    | Retenues              |
| Contributions | Cotisations           |
| Taxes         | Fiscalité (IRG, etc.) |
| Totals        | Totaux                |
| Validation    | Contrôles             |

---

# 7. BaseSalaryCalculator

## Mission

Calcul du salaire de base.

## Entrées

* grade ;
* échelon ;
* indice ;
* grille indiciaire applicable.

## Sorties

* montant de base en **DZD**.

## Dépendances

Aucune rubrique préalable.

---

# 8. SeniorityCalculator

Mission :

calculer l'ancienneté.

Entrées :

* date de recrutement ;
* interruptions ;
* décisions administratives.

Sorties :

* ancienneté retenue ;
* taux applicable.

---

# 9. IndexCalculator

Responsable de :

* l'indice ;
* l'indice majoré ;
* les conversions.

Consomme :

* grade ;
* échelon ;
* référentiel indiciaire.

---

# 10. IndemnityCalculator

Calcule toutes les indemnités.

Il ne contient aucune règle codée en dur.

Les règles proviennent exclusivement :

* des tables réglementaires ;
* des paramètres métier.

---

# 11. AllowanceCalculator

Responsable des primes.

Exemples :

* rendement ;
* responsabilité ;
* zone ;
* documentation.

Chaque prime possède sa propre stratégie de calcul.

---

# 12. DeductionCalculator

Calcule les retenues.

Exemples :

* absence ;
* grève ;
* retenue disciplinaire ;
* acomptes.

---

# 13. ContributionCalculator

Responsable :

* sécurité sociale ;
* retraite ;
* cotisations réglementaires.

Les taux sont versionnés.

---

# 14. IRGCalculator

Le calculateur IRG est isolé.

Il utilise :

* le brut imposable ;
* les abattements ;
* le barème réglementaire.

Il ne connaît jamais le détail des autres calculateurs.

---

# 15. NetSalaryCalculator

Mission :

calculer le net.

Entrées :

```text
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

---

# 16. TotalsCalculator

Produit :

* total gains ;
* total retenues ;
* total cotisations ;
* net imposable ;
* net à payer.

---

# 17. ValidationCalculator

Dernier calculateur du pipeline.

Il vérifie :

* montants négatifs ;
* doublons ;
* rubriques obligatoires ;
* cohérence.

---

# 18. Calculateurs spécialisés

Le framework permet d'ajouter :

```text
PrimeExceptionnelleCalculator

PrimeCovidCalculator

HeuresSupCalculator

VacationCalculator

ZoneCalculator

ResponsibilityCalculator
```

sans modifier les calculateurs existants.

---

# 19. Priorités

Chaque calculateur possède une priorité.

Exemple :

| Priorité | Calculateur |
| -------- | ----------- |
| 100      | BaseSalary  |
| 200      | Indice      |
| 300      | Ancienneté  |
| 400      | Indemnités  |
| 500      | Primes      |
| 600      | Retenues    |
| 700      | Cotisations |
| 800      | IRG         |
| 900      | Net         |
| 1000     | Validation  |

L'orchestrateur exécute les calculateurs dans cet ordre.

---

# 20. Dépendances

Le moteur construit un graphe :

```text
SalaireBase
      │
      ▼
Indice
      │
      ▼
IEP
      │
      ▼
IRG
      │
      ▼
Net
```

Les cycles sont interdits.

---

# 21. Explainability

Chaque calculateur doit produire une justification.

Exemple :

```text
Calculateur

↓

Variables utilisées

↓

Formule

↓

Résultat

↓

Arrondi

↓

Montant final
```

Cette justification est intégrée au journal d'audit.

---

# 22. Arrondis

Tous les calculateurs utilisent un **service centralisé d'arrondi**.

Aucun calculateur ne doit implémenter sa propre logique d'arrondi.

Cela garantit l'uniformité des résultats.

---

# 23. Journalisation

Chaque exécution enregistre :

* heure ;
* durée ;
* variables utilisées ;
* résultat ;
* éventuelles anomalies.

Les journaux facilitent les audits et les investigations.

---

# 24. Gestion des erreurs

Les erreurs sont classées :

| Niveau      | Effet                 |
| ----------- | --------------------- |
| Information | poursuite             |
| Warning     | poursuite avec alerte |
| Error       | arrêt du calculateur  |
| Critical    | arrêt du pipeline     |

---

# 25. Tests unitaires

Chaque calculateur doit disposer de jeux de tests couvrant :

* cas nominal ;
* valeurs limites ;
* cas d'erreur ;
* régression réglementaire ;
* performance.

Les tests sont indépendants de SQLite et de l'interface utilisateur.

---

# 26. Performances

Objectifs :

| Élément              | Temps maximal |
| -------------------- | ------------: |
| Calculateur simple   |        < 5 ms |
| Calculateur complexe |       < 20 ms |
| Pipeline complet     |      < 300 ms |
| Journalisation       |       < 20 ms |

---

# 27. Évolutivité

L'ajout d'un nouveau calculateur suit le processus suivant :

1. créer une classe implémentant `IPayrollCalculator` ;
2. enregistrer le calculateur dans l'injection de dépendances ;
3. définir sa priorité ;
4. ajouter ses tests unitaires ;
5. documenter ses règles métier.

Aucune modification du moteur principal n'est requise.

---

# 28. Critères d'acceptation

La bibliothèque de calculateurs est validée lorsque :

* chaque calcul est isolé dans un composant dédié ;
* les dépendances sont explicitement déclarées ;
* les résultats sont entièrement explicables ;
* les règles réglementaires sont paramétrables ;
* les tests unitaires couvrent les comportements critiques ;
* l'ajout d'un nouveau calculateur ne nécessite pas de modifier les calculateurs existants.

---

# 29. ADR (Architecture Decision Records)

| ADR     | Décision                                                             |
| ------- | -------------------------------------------------------------------- |
| ADR-051 | Un calculateur = une responsabilité métier                           |
| ADR-052 | Contrat unique `IPayrollCalculator`                                  |
| ADR-053 | Priorisation déterministe des calculateurs                           |
| ADR-054 | Service d'arrondi centralisé                                         |
| ADR-055 | Journalisation et explicabilité obligatoires pour chaque calculateur |

---

# Conclusion

Le **Volume 10** complète la spécification du **moteur de calcul** en définissant la **bibliothèque des calculateurs métier**. Il normalise leurs contrats, leur cycle d'exécution, leurs dépendances, leurs règles de validation et leurs exigences de test. Cette architecture garantit un moteur de paie modulaire, extensible et conforme aux exigences d'un ERP moderne.

## Recommandation pour la suite

À ce stade, la documentation a couvert :

* l'architecture applicative ;
* le modèle de domaine ;
* le moteur de calcul.

Pour atteindre un niveau comparable à celui des grands ERP, je recommande d'ouvrir un **Tome D — Persistance et Infrastructure**, qui détaillera notamment :

* l'architecture SQLite et le schéma physique de la base ;
* les migrations et la gestion des versions du schéma ;
* les repositories et l'Unité de Travail (*Unit of Work*) ;
* les stratégies de sauvegarde et de restauration ;
* les performances des accès aux données ;
* la journalisation technique et la résilience.

Ce tome constituera le socle de l'implémentation de la couche `Persistence` et de l'infrastructure de **PaieEducation ERP**.

