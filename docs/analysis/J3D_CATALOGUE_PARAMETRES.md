# J3.d — Catalogue des paramètres réglementaires

> **Statut :** v1.0 — Recense **tout ce qui doit être piloté par la donnée** (jamais par le code),
> avec la table SQLite d'accueil. Les tables marquées 🆕 sont proposées en J3E (schéma V008).
> Chaque paramètre est versionné (`DateEffet`/`DateFin`) et traçable (`Source`, `Hash`).

## 1. Paramètres transverses (table `Parametres`)

| Clé | Type | Valeur initiale | Source |
|-----|------|-----------------|--------|
| `VALEUR_POINT` | INT (DA) | 45 (versionné : 2008-01-01 → …) | Art. 8 D.p. 07-304 ; Q1 |
| `ARRONDI_MODE` | TEXT | `DINAR` (alternatives : `DIZAINE`) | Q9b |
| `ARRONDI_PORTEE` | TEXT | `UNIFORME` (rubriques + net) | Q9b |
| `DEVISE` | TEXT | `DZD` | RM-121 |
| `SS_TAUX_DEFAUT` | REAL | 0,09 (redondance de contrôle avec `Cotisations`) | Q3b |
| `IEP_TAUX_PUBLIC_PCT` | REAL | 1,4 (% d'IEP_CONT par année d'ancienneté publique) | Art. 16 D.p. 07-304 ; clarification 14/07/2026 |
| `IEP_TAUX_PRIVE_PCT` | REAL | 0,7 (% d'IEP_CONT par année d'ancienneté privée) | idem |
| `IEP_PLAFOND_PCT` | REAL | 60 (plafond du taux composite IEP_CONT, en % du TB) | idem |

## 2. Nomenclature & classification (tables V002 — `TypeContrats`, `Filieres`, `Secteurs`, `TypePersonnels`, `Corps`, `Grades`)

Paramétré depuis `Cascade_Corps_Grades_30526.csv` (source faisant foi, Q6) :
type de contrat (statutaire/contractuel), filière (ADMIN, ENSEIGNANT, INSPECTION, SANTE_PUBLIQUE,
OUVRIERS_AGENTS), secteur, type de personnel, corps (~48), grades (185) avec catégorie/subdivision.
**Extension nécessaire** : attribut de grade « origine statutaire enseignante » (Oui/Non) pour le
groupe ISSRP 45 % (🆕 `GradeAttributs`, cf. J3E §4 et Q-03).

## 3. Grille indiciaire (tables V003)

| Paramètre | Contenu | Versions |
|-----------|---------|----------|
| Indices minimaux | par catégorie 1–17 + subdivisions HC 1–7 | 2008-01-01 ; 2022-03-01 ; 2023-01-01 ; 2024-01-01 |
| Indices d'échelon | par (catégorie/subdivision, échelon 1–12) | idem |
| Grille des emplois contractuels | par catégorie d'emploi 1–7 | idem (07-308 mod. 22-140, 23-56) |
| 🆕 Bonifications indiciaires postes supérieurs ⛳Q-07 | 14 niveaux (services de l'État) + matrice catégories A/B/C × sections × N/N'/N-1/N-2/N-3 (établissements publics) | 2008-01-01 ; 2022-03-01 ; 2023-01-01 ; 2024-01-01 (07-307, 22-139, 23-55) |

## 4. Rubriques (tables V004)

Par rubrique du catalogue J3C : nature (GAIN/RETENUE/COTISATION/IMPOT), base de calcul, périodicité
de calcul **et 🆕 de versement** (INC-04), ordre/priorité, flags imposable/cotisable, actif.

### `RubriqueParametres` (clé/valeur versionnés) — inventaire cible

| Rubrique | Clés de paramètres | Versions notables |
|----------|--------------------|-------------------|
| PAPP, PAPG, REND_*, PAP, PAP_ENS_PARAMED | `TAUX_MAX` (0,40 / 0,30 / 0,35…) | PAP : 0,30 → 0,35 au 2025-01-01 |
| EXP_PEDAG, EXP_PEDAG_PARAMED, GEST_FIN | `TAUX_PAR_ECHELON` = 0,04 | stable |
| QUALIF | `TAUX_CAT_MAX12` = 0,40 ; `TAUX_CAT_MIN13` = 0,45 | rétroactif 2008 (11-373) |
| ISSRP_45 / _30 / _15 | `TAUX` = 0,45 / 0,30 / 0,15 | v1 15 % [2008 ; 2024], v2 [2025 ; …] |
| SERV_TECH_LABO, NUIS_OP, FORF_SERV, RISQ_ASTR, ASTR_PARAMED… | `TAUX` = 0,25 (ASTR_PARAMED → 0,40 au 2025-01-01) | cf. J3C |
| NUIS_LABO | `TAUX` : 0,10 → 0,25 au 2025-01-01 | 25-55 |
| SERV_ADM, SERV_TECH_CC | `TAUX_GROUPE_A` = 0,25 ; `TAUX_GROUPE_B` = 0,40 (groupes = éligibilité par corps) | 10-134 |
| SOUT_ADM_* | `TAUX` = 0,10 | effet 2012-01-01 |
| TECHNICITE | `TAUX` = 0,10 ; seuil catégorie : ≥ 11 → ≥ 12 (2025) ⛳Q-05 | 11-200 ; 24-425 |
| IEP_CONT | `IEP_TAUX_PUBLIC_PCT` = 1,4 ; `IEP_TAUX_PRIVE_PCT` = 0,7 ; `IEP_PLAFOND_PCT` = 60 (plafond du **taux composite**, pas des années) | 07-304 art. 16 ; clarification 14/07/2026 |
| IEP_FONC | aucun paramètre propre (consomme `VALEUR_POINT` + grille des indices d'échelon) | 07-304 art. 5 |

### 🆕 `RubriqueBaremes` (barèmes par tranche de critère — J3E §3)

| Rubrique | Dimension | Tranches (valeur) | Versions |
|----------|-----------|-------------------|----------|
| DOC_PEDAG | CATEGORIE | ≤10 → 2 000 ; 11–12 → 2 500 ; ≥13 → 3 000 | stable 2008 → … |
| DOC_PEDAG_PARAMED | — | forfait 3 000 | stable |
| DIR_ETAB | TYPE_ETABLISSEMENT | primaire → 3 000 ; collège → 4 000 ; lycée → 5 000 | 2015-09-01 → … |
| IFC | CATEGORIE | barème 2008 par groupes ; barème 2015 par catégorie (J3C §7) | 2008-01-01 ; 2015-01-01 |
| SOUT_PARAMED | CATEGORIE | v2011 : ≤10 → 30 % ; ≥11 → 25 % — v2025 : ≤10 → 55 % ; ≥12 → 50 % ⛳Q-05 | 2008 ; 2025 |
| QUALIF | CATEGORIE | ≤12 → 40 % ; ≥13 → 45 % (alternative à RubriqueParametres) | 2008 → … |

## 5. Éligibilité (table `ReglesEligibilite` V005, critères étendus 🆕)

- Matrice rubrique × (filière / corps / grade / catégorie / fonction / type de contrat / échelon /
  ancienneté / 🆕 origine statutaire / 🆕 type d'établissement), versionnée.
- Versions d'éligibilité notables : PAPP/QUALIF/DOC_PEDAG/EXP_PEDAG étendues à direction+inspection
  au **2012-05-29** ; DOC_PEDAG étendue à l'intendance rétroactivement au 2008-01-01 ;
  matrice ISSRP v2 au 2025-01-01 (185 grades → 45/30/15, à valider Q-03).

## 6. Cotisations (tables V005)

| Paramètre | Contenu |
|-----------|---------|
| `Cotisations` | SS 9 % (obligatoire salariale, éditable), retraite si distincte, mutuelle & œuvres sociales (facultatives, montant fixe) |
| `CotisationAssietteRubriques` | inclusion/exclusion de chaque rubrique dans l'assiette de chaque cotisation, versionnée |

## 7. IRG (tables V006/V007)

| Paramètre | Contenu | État |
|-----------|---------|------|
| `BaremeIRG` + `BaremeIRGTranches` | Barème 2008 (4 tranches) **+ 🆕 barème 2022 (6 tranches)** | 2008 seedé ; 2022 à seeder (INC-01/Q-01) |
| `IRGReglesPeriode` | 4 périodes : exonération, abattement 40 % [1000 ; 1500], coefficients/constantes en fractions exactes, plafond spécial | seedé (repointage 2022+ requis) |
| 🆕 Abattement handicapés 2010 | 80/60/30/10 % plafonné 1 000 DA, [2010-01-01 ; 2020-05-31] (RM-066) | à modéliser si les périodes < 2020 sont dans le périmètre de recalcul |
| Profils | `STANDARD`, `HANDICAPE_OU_RETRAITE_RG` (attribut d'agent) | à porter au dossier agent |

## 8. Variables mensuelles (pilotées par saisie, pas par décret)

Notation individuelle (PAPP/PAPG/rendement/PAP), heures supplémentaires, absences, rappels manuels,
retenues diverses (avances, oppositions), montants des retenues facultatives par agent,
nombre d'enfants, situation familiale, **anciennetés `ANC_PUB` / `ANC_PRIV`** (dossier agent,
entrées d'IEP_CONT). → Tables de gestion (Phase 5), hors référentiel réglementaire.

## 9. Couverture du principe « zéro hardcoding »

| Famille | Table d'accueil | Statut |
|---------|-----------------|--------|
| Point indiciaire, arrondi, devise | `Parametres` | ✅ existant |
| Grilles indiciaires (4 versions) | V003 | ✅ seedé |
| Nomenclature corps/grades | V002 | ✅ seedé |
| Rubriques + taux + formules + dépendances | V004 | ✅ schéma ; seed partiel (6/≈30) |
| Éligibilité | V005 | ⚠ critère origine + matrice complète manquants |
| Cotisations + assiettes | V005 | ✅ schéma ; seed SS |
| IRG (barèmes, périodes, fractions) | V006/V007 | ⚠ barème 2022 manquant |
| Barèmes par tranches (IFC, forfaits, taux par catégorie) | 🆕 `RubriqueBaremes` | à créer (V008) |
| Bonifications postes supérieurs | 🆕 table dédiée | à créer si Q-07 = oui |
| Périodicité de versement | 🆕 colonne `Rubriques` | à créer (V008) |
