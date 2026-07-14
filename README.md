# PaieEducation ERP

ERP de paie **desktop, 100 % hors ligne** pour les établissements publics de l'Éducation
nationale algérienne et corps assimilés.

**Stack :** .NET 10 LTS · C# 14 · WPF · MVVM (CommunityToolkit.Mvvm) · SQLite · Dapper ·
QuestPDF · ClosedXML · xUnit. Architecture **Clean Architecture + DDD**.

> Principe cardinal : **zéro hardcoding** des règles et valeurs réglementaires
> (grilles, indices, point, barèmes IRG, taux de cotisation, rubriques, formules, éligibilité) —
> tout est **paramétré en base et versionné par date d'effet**.

## Structure

```
src/    Domain · Application · Infrastructure · Persistence · Reporting · Shared · Presentation · Bootstrapper
tests/  Tests.Unit · Tests.Integration · Tests.Architecture
tools/  Tools (import/seed des données de référence)
docs/   PLAN_ACTION.md · GLOSSAIRE.md · CONVENTIONS.md · adr/
```

## Prérequis

- SDK **.NET 10.0.301** (épinglé par `global.json`).
- Windows 10/11 (WPF).

## Commandes

```bash
dotnet restore PaieEducation.slnx
dotnet build   PaieEducation.slnx -c Debug
dotnet test    PaieEducation.slnx -c Debug
```

## Documentation

- Plan d'action et jalons : [`docs/PLAN_ACTION.md`](docs/PLAN_ACTION.md)
- Glossaire métier : [`docs/GLOSSAIRE.md`](docs/GLOSSAIRE.md)
- Conventions : [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md)
- Décisions d'architecture : [`docs/adr/`](docs/adr/)
- Référentiels métier fournis : `Reglementation/`, `Documentation de Référence du Projet/`
