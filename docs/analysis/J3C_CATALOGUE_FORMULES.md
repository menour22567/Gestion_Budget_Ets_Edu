# J3.c — Catalogue exhaustif des formules de calcul

> **Statut :** v1.0 — Inventaire cible complet. Aucune formule ne sera codée sans figurer ici.
> **Conventions :** `TBASE` = traitement de base (RM-001) ; `TRT` = traitement (RM-002) ;
> `ECH` = n° d'échelon détenu ; `CAT` = catégorie du grade ; toutes les valeurs numériques citées
> sont des **paramètres versionnés en base**, jamais des constantes de code (cf. J3D).
> Arrondi : service centralisé unique (RM-120), non répété par formule.
> Les dates indiquées sont des **dates d'effet réglementaires** (la rétroactivité suit RM-102).

---

## 1. Socle (toutes filières)

| Code | Libellé | Formule | Validité | Bénéficiaires | Dépendances | Source |
|------|---------|---------|----------|---------------|-------------|--------|
| TBASE | Traitement de base | `IndiceMin(CAT, datePaie) × ValeurPoint(datePaie)` | 2008-01-01 → … (grilles versionnées 2008/2022-03/2023/2024) | Tous | grille indiciaire | Art. 5 & 8 D.p. 07-304 ; 22-138 ; 23-54 |
| TRT | Traitement | `(IndiceMin + IndiceEchelon(CAT, ECH, datePaie)) × ValeurPoint` | idem | Tous | TBASE, grille échelons | Art. 5 D.p. 07-304 |
| TRT_CONTR | Traitement (contractuels) | idem sur la grille des **emplois** | idem | Agents contractuels | grille emplois | Art. 45 D.p. 07-308 mod. |
| BONIF | Bonification indiciaire poste supérieur ⛳Q-07 | `IndiceBonification(niveau/section, datePaie) × ValeurPoint` | 2008-01-01 (tables 2008/2022-03/2023/2024) | Titulaires de postes supérieurs | référentiel bonifications | D.p. 07-307 ; 22-139 ; 23-55 |

## 1 bis. Indemnités d'expérience professionnelle (IEP) — toutes filières

> ⚠ **Clarification fonctionnelle du 14/07/2026 (révision de Q2)** : l'IEP est servie à **tous**
> les employés mais existe en **deux variantes** selon le statut. Elle est distincte de
> l'indemnité d'expérience **pédagogique** (EXP_PEDAG, §2), réservée aux corps EN hors
> Intendance et Laboratoire.

| Code | Libellé | Formule | Validité | Bénéficiaires | Base | Source |
|------|---------|---------|----------|---------------|------|--------|
| IEP_FONC | Ind. d'expérience professionnelle (fonctionnaires) | `IE × VPI` — `IE` = indice d'échelon détenu (grille en vigueur), `VPI` = valeur du point. Rétribution liée à la progression de carrière : `TRT = TBASE + IEP_FONC`. | 2008-01-01 → … (suit les grilles 2008/2022-03/2023/2024) | Tous les fonctionnaires | Indice d'échelon | Art. 5 D.p. 07-304 (composante échelon du traitement) |
| IEP_CONT | Ind. d'expérience professionnelle (contractuels) | `TBASE × min(ANC_PUB × 1,4 % + ANC_PRIV × 0,7 % ; 60 %)` — prime d'ancienneté **composite** ; le plafond de 60 % s'applique au **taux composite**, jamais aux années. Ex. : ANC_PUB=30, ANC_PRIV=30 → 63 % → retenu 60 % → TB 30 000 → 18 000 DA. | 2008-01-01 → … | Tous les agents contractuels | TBASE (emploi occupé) | Art. 16 D.p. 07-304 (taux 1,4 %/0,7 %) ; clarification fonctionnelle 14/07/2026 |

Paramètres réglementaires associés (clés `Parametres`, versionnées) :
`IEP_TAUX_PUBLIC_PCT = 1,4` · `IEP_TAUX_PRIVE_PCT = 0,7` · `IEP_PLAFOND_PCT = 60`.
Variables d'entrée par agent : `ANC_PUB` (années d'ancienneté de service public),
`ANC_PRIV` (années de service privé) — données du dossier agent.

## 2. Corps spécifiques de l'Éducation nationale (D.ex. 10-78 → 11-373 → 12-403 → 15-271 → 25-55)

| Code | Libellé | Formule | Validité / versions | Bénéficiaires (versionnés) | Base | Source |
|------|---------|---------|---------------------|----------------------------|------|--------|
| PAPP | Prime d'amélioration des performances pédagogiques | `TRT × tauxNotation` avec `0 ≤ tauxNotation ≤ 40 %` ; calcul mensuel, service trimestriel | 2008-01-01 → … (inchangée 2025) | 2008 : enseignants, éducation, orientation/guidance, alimentation ; **+ direction, inspection au 29/05/2012** | TRT | Art. 3 D.ex. 10-78 ; 12-403 ; 25-55 |
| PAPG | Prime d'amélioration des performances de gestion | `TRT × tauxNotation`, plafond 40 % ; trimestrielle | 2008-01-01 → … | Intendance | TRT | Art. 4 D.ex. 10-78 ; 25-55 |
| REND_LABO | Prime de rendement (laboratoire) | `TRT × tauxNotation`, plafond 30 % ; trimestrielle | 2008-01-01 → … | Laboratoire | TRT | Art. 5 D.ex. 10-78 ; 25-55 |
| QUALIF | Indemnité de qualification | `TRT × (40 % si CAT ≤ 12 ; 45 % si CAT ≥ 13)` | 2008-01-01 → … (taux 25/30 % du 10-78 **remplacés rétroactivement**, jamais calculés) | 2008 : enseignants, éducation, orientation, alimentation, intendance ; + direction, inspection au 29/05/2012 | TRT | Art. 7 D.ex. 11-373 ; 12-403 ; Art. 7 D.ex. 25-55 |
| DOC_PEDAG | Indemnité de documentation pédagogique | Forfait : `2 000 si CAT ≤ 10 ; 2 500 si 11 ≤ CAT ≤ 12 ; 3 000 si CAT ≥ 13` (DA/mois) | 2008-01-01 → … (montants inchangés) | 2008 : enseignants, éducation, orientation, alimentation ; + intendance (11-373, rétro. 2008) ; + direction, inspection (29/05/2012) | Forfait | Art. 8 D.ex. 10-78 ; Art. 5 D.ex. 11-373 ; Art. 8 D.ex. 25-55 |
| EXP_PEDAG | Indemnité d'expérience pédagogique | `TBASE × 4 % × ECH` — **distincte de l'IEP (§1 bis)** ; réservée aux corps EN **hors Intendance et Laboratoire** | v1 [2008-01-01 ; 2012-05-28] ; v2 [2012-05-29 ; 2024-12-31] ; v3 [2025-01-01 ; …] (formule inchangée) | v1 : enseignants, éducation, orientation/guidance, alimentation scolaire ; v2/v3 : + direction, + inspection | TBASE × échelon | Art. 9 D.ex. 10-78 ; Art. 3 D.ex. 12-403 ; Art. 9 D.ex. 25-55 ; clarification 14/07/2026 |
| SERV_TECH_LABO | Indemnité des services techniques (labo) | `TRT × 25 %` | 2008-01-01 → … (créée par 11-373, rétro. 2008) | Laboratoire | TRT | Art. 3 D.ex. 11-373 ; Art. 5 D.ex. 25-55 |
| NUIS_LABO | Indemnité de nuisance (labo) | `TRT × taux` : **10 %** [2008-01-01 ; 2024-12-31] → **25 %** [2025-01-01 ; …] | 2 versions | Laboratoire | TRT | Art. 3 D.ex. 11-373 ; Art. 5 D.ex. 25-55 |
| ISSRP | Ind. de soutien scolaire et remédiation pédagogique | `TRT × taux` — v1 : **15 %** unique [2008-01-01 ; 2024-12-31] (créée 26/10/2011, rétro. 2008 → rappels) ; v2 [2025-01-01 ; …] : **45 %** / **30 %** / **15 %** selon groupe (RM-043) | 2 versions | Tous corps EN (v1) ; 3 groupes (v2) | TRT | Art. 9 bis D.ex. 11-373 ; Art. 10 D.ex. 25-55 |
| DIR_ETAB | Indemnité de direction d'établissement | Forfait : `3 000 (primaire) ; 4 000 (collège) ; 5 000 (lycée)` DA/mois | 2015-09-01 → … (inchangée 2025) | Directeurs en exercice effectif | Forfait | Art. 9 bis 1 D.ex. 15-271 ; Art. 11 D.ex. 25-55 |
| GEST_FIN | Indemnité de gestion financière et matérielle | `TBASE × 4 % × ECH` | 2015-09-01 → … | Intendance | TBASE × échelon | Art. 9 bis 2 D.ex. 15-271 ; Art. 12 D.ex. 25-55 |

## 3. Corps communs (D.ex. 10-134 ; 13-188)

| Code | Libellé | Formule | Validité | Bénéficiaires | Source |
|------|---------|---------|----------|---------------|--------|
| REND_CC | Prime de rendement | `TRT × tauxNotation`, plafond 30 % ; trimestrielle | 2008-01-01 → … | Tous corps communs | Art. 3 D.ex. 10-134 |
| SERV_ADM | Ind. des services administratifs communs | `TRT × 25 %` (secrétaires, agents/attachés d'admin., comptables, agents tech. doc/archives, assistants doc.) ou `TRT × 40 %` (administrateurs, traducteurs-interprètes, documentalistes-archivistes, analystes) | 2008-01-01 → … | Corps communs admin. | Art. 4 D.ex. 10-134 |
| SERV_TECH_CC | Ind. des services techniques communs | `TRT × 25 %` (agents labo, agents/adjoints techniques, techniciens) ou `TRT × 40 %` (ingénieurs) | 2008-01-01 → … | Corps communs techniques | Art. 5 D.ex. 10-134 |
| SOUT_ADM_CC | Ind. de soutien aux activités de l'administration | `TRT × 10 %` | **2012-01-01** → … (créée 09/05/2013, rétro. 01/01/2012 → rappels) | Tous corps communs | Art. 5 bis D.ex. 13-188 |

## 4. Ouvriers professionnels, conducteurs, appariteurs (D.ex. 10-135 ; 13-189)

| Code | Libellé | Formule | Validité | Bénéficiaires | Source |
|------|---------|---------|----------|---------------|--------|
| REND_OP | Prime de rendement | `TRT × tauxNotation`, plafond 30 % ; trimestrielle | 2008-01-01 → … | OP, conducteurs, appariteurs | Art. 3 D.ex. 10-135 |
| NUIS_OP | Indemnité de nuisance | `TRT × 25 %` | 2008-01-01 → … | Ouvriers professionnels uniquement | Art. 4 D.ex. 10-135 |
| FORF_SERV | Indemnité forfaitaire de service | `TRT × 25 %` | 2008-01-01 → … | Conducteurs et appariteurs | Art. 5 D.ex. 10-135 |
| SOUT_ADM_OP | Ind. de soutien aux activités de l'administration | `TRT × 10 %` | 2012-01-01 → … | OP, conducteurs, appariteurs | Art. 5 bis D.ex. 13-189 |

## 5. Agents contractuels (D.ex. 10-136 ; 13-190) — assiette = traitement de l'emploi occupé

| Code | Libellé | Formule | Validité | Bénéficiaires | Source |
|------|---------|---------|----------|---------------|--------|
| REND_CONTR | Prime de rendement | `TRT_CONTR × tauxNotation`, plafond 30 % ; trimestrielle | 2008-01-01 → … | Tous emplois contractuels | Art. 3 D.ex. 10-136 |
| NUIS_CONTR | Indemnité de nuisance | `TRT_CONTR × 25 %` | 2008-01-01 → … | Emploi ouvrier professionnel | Art. 4 D.ex. 10-136 |
| FORF_SERV_CONTR | Indemnité forfaitaire de service | `TRT_CONTR × 25 %` | 2008-01-01 → … | Agents de service, conducteurs, chefs de parc, gardiens | Art. 5 D.ex. 10-136 |
| RISQ_ASTR | Indemnité de risque et d'astreinte | `TRT_CONTR × 25 %` | 2008-01-01 → … | Agents de prévention niv. 1 et 2 | Art. 6 D.ex. 10-136 |
| SOUT_ADM_CONTR | Ind. de soutien aux activités de l'administration | `TRT_CONTR × 10 %` | 2012-01-01 → … | Agents contractuels (art. 19 ord. 06-03) | Art. 6 bis D.ex. 13-190 |

## 6. Paramédicaux de santé publique (D.ex. 11-200, effet 2008 ; modifié 24-425, effet 2025)

| Code | Libellé | Formule (v2011 → v2025) | Bénéficiaires | Source |
|------|---------|--------------------------|---------------|--------|
| PAP | Prime d'amélioration des performances | `TRT × tauxNotation` — plafond **30 %** [2008 ; 2024] → **35 %** [2025 ; …] ; trimestrielle | Filière soins/rééducation/médico-technique | Art. 3 D.ex. 11-200 ; 24-425 |
| ASTR_PARAMED | Indemnité d'astreinte paramédicale | `TRT × 25 %` [2008 ; 2024] → `TRT × 40 %` [2025 ; …] | idem | Art. 4 |
| SOUT_PARAMED | Ind. de soutien aux activités paramédicales | v2011 : `TRT × (30 % si CAT ≤ 10 ; 25 % si CAT ≥ 11)` → v2025 : `TRT × (55 % si CAT ≤ 10 ; 50 % si CAT ≥ 12)` ⛳ cat. 11 non couverte 2025 (**Q-05**) | idem | Art. 5 |
| TECHNICITE | Indemnité de technicité | `TRT × 10 %` — v2011 : CAT ≥ 11 → v2025 : CAT ≥ 12 ⛳ Q-05 | idem | Art. 6 |
| PAP_ENS_PARAMED | Prime d'amélioration des performances (filière enseignement) | `TRT × tauxNotation`, plafond 40 % ; trimestrielle | Filière enseignement & inspection pédagogique paraméd. | Art. 8 D.ex. 11-200 |
| QUALIF_PARAMED | Indemnité de qualification | `TBASE × 30 %` (⚠ assiette = traitement **de base**) | idem | Art. 9 D.ex. 11-200 |
| EXP_PEDAG_PARAMED | Indemnité d'expérience pédagogique | `TBASE × 4 % × ECH` | idem | Art. 10 D.ex. 11-200 |
| DOC_PEDAG_PARAMED | Indemnité de documentation pédagogique | Forfait `3 000` DA/mois | idem | Art. 11 D.ex. 11-200 |

Toutes les primes/indemnités paramédicales sont **cotisables** (Art. 12 D.ex. 11-200).

## 7. IFC — Indemnité forfaitaire compensatrice (D.ex. 08-70 ; 15-176)

Forfait mensuel par catégorie (fonctionnaires **et** contractuels) :

| Catégorie | 2008-01-01 → 2014-12-31 | 2015-01-01 → … |
|-----------|------------------------:|----------------:|
| 1 | 3 200 | 7 700 |
| 2 | 3 200 | 7 400 |
| 3 | 3 200 | 6 900 |
| 4 | 3 200 | 6 400 |
| 5 | 3 200 | 5 700 |
| 6 | 3 200 | 5 000 |
| 7–8 | 2 500 | 3 800 |
| 9–10 | 2 000 | 3 100 |
| 11–17 | 1 500 | 1 500 (« le reste sans changement ») |

## 8. Cotisations et retenues

| Code | Libellé | Formule | Source |
|------|---------|---------|--------|
| SS | Sécurité sociale (part ouvrière) | `AssietteCotisable × 9 %` (taux éditable ; assiette = Σ rubriques cotisables via `CotisationAssietteRubriques`) | Q3/Q3b |
| MUT | Mutuelle (facultative) | Montant fixe choisi par l'agent | Q3b |
| OEUV_SOC | Œuvres sociales (facultative) | Montant fixe temporaire choisi par l'agent | Q3b |

## 9. IRG — Impôt sur le revenu global (retenue à la source)

**Algorithme de référence** : `IRG_Algerie_2008_2026_PseudoCode.txt` (arbitrages INC-09).
Entrées : `datePaie`, `SI` (revenu mensuel imposable = Σ imposables − cotisations salariales), `profil`.

1. **Sélection de la règle de période** (versionnée en base, `IRGReglesPeriode`) :

| Période | Barème | Exonération | Lissage général (30 001 < SI < 35 000) | Lissage spécial (30 000 < SI < plafond) |
|---|---|---|---|---|
| [1000-01-01 ; 2020-05-31] | 2008 (4 tranches : ≤10 000→0 % ; →30 000→20 % ; →120 000→30 % ; >120 000→35 %) | — | — | Abattement handicapés 2010 (RM-066) sur [2010-01-01 ; 2020-05-31] |
| [2020-06-01 ; 2020-12-31] | 2008 | SI ≤ 30 000 → IRG = 0 | `× 8/3 − 20000/3` | `× 5/3 − 12500/3`, plafond 40 000 |
| [2021-01-01 ; 2021-12-31] | 2008 | idem | `× 8/3 − 20000/3` | `× 5/3 − 12500/3`, plafond 42 500 |
| [2022-01-01 ; …] | **2022 (6 tranches** : ≤20 000→0 % ; →40 000→23 % ; →80 000→27 % ; →160 000→30 % ; →320 000→33 % ; >320 000→35 %**)** ⚠ INC-01/Q-01 | idem | `× 137/51 − 27925/8` | `× 93/61 − 81213/41`, plafond 42 500 |

2. **IRG brut** progressif sur le barème mensuel de la période.
3. **Abattement 40 % sur l'IRG brut**, borné `[1 000 ; 1 500]` DA/mois → `irgApresAbattement = max(0, brut − abattement)`.
4. **Exonération** : si activée et `SI ≤ 30 000` → IRG = 0.
5. **Lissage spécial** (profil `HANDICAPE_OU_RETRAITE_RG`, non cumulable avec le général) puis **lissage général**, chacun `max(0, …)`.
6. Sinon `max(0, irgApresAbattement)`.

Coefficients stockés en **fractions exactes TEXT** (V007) : `8/3`, `20000/3`, `5/3`, `12500/3`, `137/51`, `27925/8`, `93/61`, `81213/41`.

## 10. Totaux et net

| Code | Formule |
|------|---------|
| TOT_GAINS | `Σ rubriques Nature=GAIN` |
| ASS_COT | `Σ gains cotisables` (paramétré par cotisation) |
| ASS_IMP | `Σ gains imposables − cotisations salariales déductibles` |
| TOT_RET | `Σ retenues + cotisations salariales + IRG` |
| NET | `TOT_GAINS − TOT_RET` (arrondi centralisé RM-120) |

## 11. Rappels rétroactifs (RM-102)

`Rappel(rubrique, mois M) = Montant(nouvelle version, M) − Montant(version payée, M)` pour chaque mois M
de la plage [date d'effet ; dernière période payée], versé sur la première période ouverte.
Le rappel hérite des flags imposable/cotisable de la rubrique d'origine (traitement fiscal du rappel
à confirmer au fil de l'eau — STOP & ASK si un cas réel diverge).
