# ADR-0004 — Codes métier comme clés primaires des tables de référentiel (dérogation ADR-062 V4)

**Statut :** Accepté — J3 (14/07/2026). Formalise une pratique en place depuis V002.

## Contexte

L'ADR-062 de la documentation V4 (Tome D, vol. 12) impose « GUID comme clé primaire pour toutes
les tables ». Or le schéma V002+ utilise des **codes métier lisibles** comme PK des référentiels :
`Rubriques.Id = "IEP_FONC"`, `Categories.Id = "HC-S1"`, `Filieres.Id = "ENSEIGNANT"`,
`BaremeIRG.Id = "IRG-2022"`… L'analyse J3 (INC-14, `docs/analysis/J3A_RAPPORT_COHERENCE.md`)
a acté cette divergence.

## Décision

Dérogation ciblée à l'ADR-062 :

- **Tables de référentiel réglementaire** (nomenclature, grilles, rubriques, barèmes, paramètres,
  règles d'éligibilité) : PK = **code métier stable** (TEXT). Les versions temporelles d'une même
  notion portent un Id suffixé par la date d'effet (ex. `RP-IEP-TAUX-2007-01-01`).
- **Tables de gestion** (agents, contrats, bulletins, variables mensuelles — Phase 5) :
  PK = **GUID**, conformément à l'ADR-062.

## Justification

- L'identité d'une rubrique réglementaire est **fonctionnelle et publique** (« IEP », « IRG ») :
  un GUID n'ajouterait qu'une indirection sans protéger aucun invariant.
- Les seeds sont **idempotents et diffables** (`ON CONFLICT(Id) DO NOTHING`) car l'Id est
  déterministe — impossible avec des GUID générés.
- Les FK sont lisibles en base (`RubriqueId = 'ISSRP_45'`), ce qui simplifie l'audit réglementaire
  exigé par le Tome B (V2 vol. 9 §12).
- Les données de gestion, elles, n'ont pas d'identité fonctionnelle stable a priori
  (le matricule est un attribut UNIQUE, jamais une PK — conforme V4 Tome D vol. 12 §5).

## Conséquences

- La règle est vérifiable en revue de schéma : toute nouvelle table doit se classer explicitement
  « référentiel » (code) ou « gestion » (GUID).
- Les écrans de paramétrage (Phase 6) afficheront directement les codes, sans table de correspondance.
