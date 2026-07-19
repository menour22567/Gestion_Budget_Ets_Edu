# Registre des décisions d'architecture (ADR)

> Chaque décision structurante est un fichier `NNNN-titre.md`. Statuts : Proposé, Accepté, Remplacé, Déprécié.

| N° | Titre | Statut |
|----|-------|--------|
| [0001](0001-clean-architecture-ddd.md) | Clean Architecture + DDD, 12 projets | Accepté |
| [0002](0002-slnx-central-package-management.md) | Solution `.slnx` + Central Package Management | Accepté |
| [0003](0003-nuget-audit-mode-direct.md) | Audit NuGet limité aux dépendances directes (NU1903) | Accepté |
| [0004](0004-cles-metier-referentiels.md) | Codes métier comme PK des référentiels (dérogation ADR-062 V4) | Accepté |
| [0005](0005-moteur-calcul-synchrone.md) | Cœur de calcul synchrone et pur ; asynchronisme en Application | Accepté |
| [0006](0006-affectation-assistee-rubriques.md) | Affectation assistée des rubriques : extension du socle d'éligibilité, un seul évaluateur | Accepté |
| [0007](0007-workbench-reglementaire.md) | Workbench réglementaire : toute rubrique, tout paramètre, toute règle d'éligibilité est saisissable et historisable par l'utilisateur | Accepté |
| [0008](0008-immutabilite-periodes-cloturees.md) | Immutabilité des périodes clôturées : toute correction rétroactive passe par un rappel, jamais par un recalcul | Accepté |
| [0009](0009-abstention-reglementaire.md) | Principe d'abstention réglementaire : jamais de droit inventé, extrapolé ou déduit | Accepté |
| [0010](0010-abstention-phase-paiement.md) | Abstention phase paiement : l'application gère la paie administrative, jamais son exécution bancaire | Proposé |
| [0011](0011-money-decision-decimal.md) | Maintien de `decimal` + arrondi centralisé (dérogation `CONVENTIONS.md §5`) | Accepté |

> Les ADR 001 à 130 issus de la documentation V4 restent la référence conceptuelle ;
> ce registre trace les décisions **prises pendant l'implémentation**.
