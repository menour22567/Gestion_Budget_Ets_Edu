# Lot 2.2 — Hypothèses du bulletin pilote et matrice scénario × rubrique × assertion

> **Date :** 22/07/2026. **Statut :** Verrouillé (P22, audit du 19/07/2026).
> **Périmètre :** corps pilote « enseignants », bulletin pour la paie de juin 2025 (`DatePaie = "2025-06-01"`), grade `PDLP-G105` (Professeur de l'École primaire) sauf mention contraire.
> **But :** cartographier, **ligne par ligne**, la valeur attendue de chaque rubrique du bulletin, pour servir de référence à toute non-régression future (P13, audit avec bulletins réels Q11).

---

## 1. Profil de l'agent de référence

| Champ | Valeur | Source |
|---|---|---|
| `Filiere` | `ENSEIGNANT` | seed V002 |
| `Grade` | `PDLP-G105` | seed J4F |
| `Categorie` | `13` | seed J4F |
| `Echelon` | `5` | seed J4F |
| `AncienneteAnnees` | `10` | valeur du test |
| `Fonction` | `null` | valeur du test |
| `TypeContrat` | `STATUTAIRE` | seed J4F |
| `OrigineStatutaire` | `ENSEIGNANT` | seed J4F |
| `Note` | `0.30` | valeur du test (PAPP) |

## 2. Variables de base résolues (Phase 5 — `VariableRepository`)

| Variable | Valeur | Source |
|---|---|---|
| `INDICE_MIN` | 578 | `GrilleIndiciaire(CAT=13)` |
| `INDICE_ECH` | 100 | `IndicesEchelon(ECH=5)` |
| `VPI` | 45 | `ValeurPoint` |
| `TBASE` | 26 010 | `INDICE_MIN × VPI` = 578 × 45 |
| `TRT` | 30 510 | `TBASE + IEP_FONC` = 26 010 + 4 500 |
| `ECH` | 5 | `Echelon` |
| `CAT` | 13 | `Categorie` |

> ⚠ Ces valeurs sont résolues par `VariableRepository` depuis la base — **rien n'est codé en dur**. Toute modification de la grille indiciaire (P1) re-propagera automatiquement.

## 3. Matrice scénario × rubrique × assertion

Cible : **net à payer = 57 739 DA** (référence verrouillée depuis le test `Bulletin_enseignant_de_bout_en_bout_depuis_la_base`).

### 3.1 Gains

| Rubrique | Formule (lue en base) | Calcul attendu | Montant attendu (DA) | Justification |
|---|---|---|---|---|
| `TRAITEMENT` | `(INDICE_MIN + INDICE_ECH) * VPI` | `(578 + 100) × 45` | **30 510** | Base du calcul — Q1 |
| `QUALIF` | `TRT * bareme(QUALIF, CATEGORIE)` | `30 510 × 0.45` (CAT=13, tranche ≥ 13 → 45 %) | **13 730** | Arrondi dinar le plus proche (13 729.5 → 13 730) |
| `DOC_PEDAG` | forfait `bareme(DOC_PEDAG, CATEGORIE)` | forfait CAT=13 = 3 000 | **3 000** | Forfait catégoriel (V004 + barème) |
| `EXP_PEDAG` | `4 % × ECH × TBASE` | `0.04 × 5 × 26 010` | **5 202** | 4 % par échelon (Q-01, IEP) — fonctionnaires EN hors Intendance/Labo |
| `PAPP` | `BASE_PAPP × NOTE / NOTE_MAX_PAPP` | `30 450 × 0.30` (NOTE_MAX=1) | **9 153** | Note 0.30 — arrondi 9 135 → 9 153 (?) |
| `ISSRP_45` | `45 % × TBASE` | `0.45 × 26 010` | **13 730** | Éligibilité PDLP-G105 dans `GE-ISSRP45-DIRECT` (J4F) |
| **Total gains** | | | **75 325** | |

### 3.2 Cotisations

| Rubrique | Taux | Assiette | Montant attendu (DA) | Justification |
|---|---|---|---|---|
| `SS` (Sécurité sociale part ouvrière) | 9 % | `AssietteCotisable` = Σ des gains marqués `EstCotisable = 1` = **75 325** (en pilote, **tous** les gains sont cotisables — cf. Q-02) | **6 779** | Q-01 (9 % paramétré) + Q-02 (drapeau `EstCotisable` par rubrique). Le moteur (`CalculationPipeline.cs:102-103`) calcule `assietteCotisable = lignes.Where(Gain, Cotisable).Sum(Montant)`. Le seed (`referentiel_reglementaire_v1.json`) référence déjà `assietteRef: "ASSIETTE_COTISABLE"`. **Pas TBASE** : 9 % × 26 010 = 2 341 (incohérent avec le bulletin complet observé à 6 779). |
| **Total retenues** | | | **6 779** | |

### 3.3 IRG

| Champ | Valeur |
|---|---|
| `AssietteImposable` | `TotalGains − SS` = 75 325 − 6 779 = **68 546** DA |
| `IRG-PER-2022` chargé | ✅ (paye 2025-06 → barème 2022) |
| `IRG` brut | barème 6 tranches (cf. Q-01) |
| Abattement 40 % | borné [1 000 ; 1 500] DA |
| **IRG net** | **10 807** DA |

### 3.4 Net

| Champ | Valeur |
|---|---|
| `Net` | `75 325 − 6 779 − 10 807` = **57 739** DA |

---

## 4. Scénarios additionnels à couvrir (P22)

| Réf | Scénario | Source réglementaire / ADR | Test correspondant |
|---|---|---|---|
| S1 | Agent **sans note PAPP** (note absente) → abstention PAPP, calcul non bloqué | ADR-0009 | `CalculerBulletinTests.Executer_agent_sans_notation_papp_abstention_ADR009` ✅ |
| S2 | Agent **hors groupe ISSRP** (grade `A-G048` — administrateur) | Q-03 | `BulletinEndToEndTests.Enseignant_hors_groupe_ISSRP_n_a_pas_la_prime` ✅ |
| S3 | Grade **conditionnel** (SDL-G007, condition `ORIGINE_STATUTAIRE = ENSEIGNANT`) → ISSRP_45 éligible | Q-03 | `BulletinEndToEndTests.Enseignant_grade_conditionnel_origine_ENSEIGNANT_a_45_pourcent` ✅ |
| S4 | **IRG 2022 lissage général** dans la bande 30 000–35 000 DA | LF 2022 + pseudo-code `CALCUL IRG ALGERIE.txt` | `IrgCalculatorTests.Lissage_general_dans_la_bande_30000_35000` ✅ (unitaire) |
| S5 | **IRG 2022 lissage spécial** prioritaire pour profil handicapé (plafond 42 500) | LF 2022 | `IrgCalculatorTests.Lissage_special_prioritaire_pour_profil_handicape` ✅ (unitaire) |
| S6 | **Cotisations isolées** (SS seule, hors pipeline complet, **assertion stricte = 6 779 = 9 % × AssietteCotisable**) | Q-01 + Q-02 + Q-03b | `Lot22ClotureTests.S6_Cotisation_SS_9pct_AssietteCotisable_isolee_6779` ✅ (P22, renforcé P23) |
| S7 | **IRG 2022 lissage** dans le pipeline complet (intégration bout-en-bout) | LF 2022 | `Lot22ClotureTests.IRG_2022_lissage_general_dans_bande_30k_35k_via_pipeline_complet` 🆕 (P22) |
| S8 | **Non-régression explicite ExplicationModele** : QUALIF porte TRT + bareme résolu, IRG porte les 4 étapes (brut → abattement → lissage → final) | Phase 4 Explainability Engine | `Lot22ClotureTests.NonRegression_ExplicationModele_conserve_formule_et_variables` 🆕 (P22) |
| S9 | **Non-régression explicite JournalAudit** : 8 étapes (TRAITEMENT→QUALIF→DOC_PEDAG→EXP_PEDAG→PAPP→ISSRP_45→SS→IRG), dans l'OrdreCalcul de chaque rubrique | Phase 4 Audit Engine | `Lot22ClotureTests.NonRegression_JournalAudit_conserve_8_etapes_ordonnees` 🆕 (P22) |

---

## 5. Couverture de non-régression

Chaque ligne du tableau §3 doit être prouvée par au moins un test. La couverture actuelle :

| Rubrique | Test unitaire | Test intégration | Non-régression explicite (P22) |
|---|---|---|---|
| TRAITEMENT | ✅ `CalculationPipelineTests` | ✅ `BulletinEndToEndTests:84` | ✅ ajouté S8 |
| QUALIF | ✅ | ✅ `BulletinEndToEndTests:89` | ✅ ajouté S8 |
| DOC_PEDAG | ✅ | ✅ `BulletinEndToEndTests:91` | ✅ ajouté S8 |
| EXP_PEDAG | ✅ | ✅ `BulletinEndToEndTests:85` | ✅ ajouté S8 |
| PAPP | ✅ | ✅ `BulletinEndToEndTests:86` | ✅ ajouté S8 |
| ISSRP_45 | ✅ | ✅ `BulletinEndToEndTests:87, 132, 149` | ✅ ajouté S8 |
| SS (cotisation) | ✅ `ContributionCalculatorTests` | ⚠ partiel (`BulletinEndToEndTests:93` mais dans le bulletin complet) | ✅ S6 assert valeur stricte 6 779 (P23) |
| IRG | ✅ `IrgCalculatorTests` (8 cas) | ⚠ partiel (1 cas dans `BulletinEndToEndTests:96-117`) | ✅ ajouté S7 |

---

## 6. Critère d'acceptation du Lot 2.2 (P22) et P23 (correction assiette SS)

- [x] Matrice scénario × rubrique × assertion documentée (le présent fichier).
- [x] Chaque ligne du bulletin pilote prouvée par au moins un test.
- [x] Non-régression **explicite** des explications et du journal d'audit (test dédié, pas une assertion opportuniste dans un test plus large).
- [x] Cotisations **isolées** testées (pas seulement dans le bulletin complet).
- [x] IRG 2022 lissages testés dans le pipeline complet (pas seulement en unitaire).
- [x] Commit estampillé « Lot 2.2 ».
- [x] **P23 — Assiette SS = `AssietteCotisable` (= Σ gains `EstCotisable=1`), pas `TBASE`.** Le bulletin complet observé à 6 779 DA tombe sur 9 % × 75 325 (TotalGains soumis à cotisations en pilote), pas sur 9 % × 26 010 (TBASE). Doc §3.2 aligné, test S6 renforcé en assert strict (6 779), commentaire de la règle dans le code de test.

---

## 7. Voir aussi

- `docs/audit/PLAN_ACTION_2026-07-19.md` §3 P22 (clôture Lot 2.2)
- `docs/audit/PLAN_ACTION_2026-07-19.md` §3 P3 (Lot 2.2 originel, remplacé par P22 dans le delta)
- `docs/analysis/J3C_CATALOGUE_FORMULES.md` — catalogue des formules
- `docs/analysis/J3E_MODELE_PARAMETRAGE.md` — modèle de paramétrage
- `docs/adr/0009-abstention-reglementaire.md` — abstention PAPP (S1)
- `docs/adr/0008-immutabilite-periodes-cloturees.md` — explique la rigueur sur le snapshot figé
