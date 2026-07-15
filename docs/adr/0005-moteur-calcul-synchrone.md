# ADR-0005 — Cœur de calcul de paie synchrone et pur ; asynchronisme en couche Application

**Statut :** Accepté — J3 (14/07/2026). Tranche l'incohérence INC-20 de la V4.

## Contexte

La documentation V4 est contradictoire : le Tome C vol. 9 §4 définit
`IPayrollEngine.Calculate(PayrollContext) : PayrollResult` (synchrone), tandis que le
Tome C vol. 10 §2 définit `IPayrollCalculator.CalculateAsync(context, ct)` (asynchrone).
L'analyse J3 (INC-20) impose de figer le contrat avant d'implémenter la couche Domain (Phase 3)
et le moteur (Phase 4).

## Décision

- Le **cœur de calcul** (Domain + calculateurs) est **synchrone et pur** : il reçoit un
  `PayrollContext` **immuable et complet** (toutes les données déjà chargées) et retourne un
  résultat déterministe. Signature retenue :
  `IPayrollCalculator.Calculate(PayrollContext) : CalculationResult` et
  `IPayrollEngine.Calculate(PayrollContext) : PayrollResult`.
- L'**asynchronisme vit en couche Application** : construction du contexte (I/O SQLite),
  calcul de masse (parallélisation par agent), annulation (`CancellationToken`), progression.

## Justification

- **Déterminisme et reproductibilité** (Tome B V2 vol. 9 §4, Tome C vol. 9 §18 Snapshot) : un
  pipeline sans I/O ni point de suspension est trivialement rejouable et testable — aucun mock
  asynchrone dans les tests du domaine (objectif de couverture Tome B vol. 7 §19).
- **Interdictions du domaine** (Tome B vol. 7 §15) : pas d'accès données dans Domain ; il n'y a
  donc **rien à attendre** dans un calculateur — un contrat async serait du faux-asynchrone.
- Les objectifs de performance (< 300 ms par bulletin, Tome C vol. 9 §21) relèvent du chargement
  du contexte et du batch, pas du calcul unitaire.

## Conséquences

- Le `PayrollContext` doit être **complet avant l'appel** : la couche Application est responsable
  de résoudre toutes les versions de paramètres à la date de la période (résolveur J3E §1).
- Le calcul de masse parallélise des appels synchrones purs (un contexte par agent) — pas de
  contention SQLite pendant le calcul.
- L'annulation d'un traitement de masse s'applique **entre** deux bulletins, jamais au milieu
  d'un calcul (cohérent avec l'immutabilité du bulletin, Tome F vol. 22 §2).
