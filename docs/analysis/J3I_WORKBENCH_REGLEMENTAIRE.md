# J3.i — Workbench réglementaire : piloter **toutes** les rubriques par la donnée

> **Statut :** v1.0 — 14/07/2026 — Proposition globale, synthèse de J3A→J3H et extension.
> **Aucun code, aucune migration tant que ce document n'est pas validé.**
> **Principe cardinal réaffirmé par l'utilisateur (14/07/2026) :** *« toutes les rubriques
> doivent être paramétrables par l'utilisateur via UI, avec historisation complète, pour
> que toute évolution du régime indemnitaire — passée ou future — soit applicable sans
> recompilation. Le périmètre fonctionnel est dicté par le dossier `Reglementation/`. »*
> **Références :** J3A (cohérence), J3B (RM), J3C (formules), J3D (paramètres), J3E
> (modèle V008), J3F (questions ouvertes), J3G (matrice ISSRP), J3H (affectation assistée),
> ADR-0004 (PK), ADR-0005 (moteur pur), V001→V008.

---

## 1. Positionnement — Workbench, pas un second modèle

Le dossier `Reglementation/` (corps EN + corps communs + ouvriers + contractuels +
paramédicaux + IRG + IFC) impose **14 patterns de calcul** distincts. La grande majorité
est déjà couverte par le socle V001→V008 + extensions J3E/J3H ; **ce qui manque, c'est la
surface utilisateur** — un ensemble cohérent d'écrans qui rende **toute** cette donnée
éditable, versionnable, simulable et audit-able par un utilisateur non développeur.

```
       ┌───────────────────────┐
       │   RÈGLEMENTATION      │   (dossier source — figé, hors solution)
       │   (décrets, arrêtés)  │
       └─────────┬─────────────┘
                 │  ingestion (Phase 2, idempotente)
                 ▼
       ┌───────────────────────┐
       │  Modèle V001→V008     │   (catalogue, barèmes, paramètres, règles)
       │  + extensions J3E/J3H │   (RubriqueBaremes, GroupesEligibilite, …)
       └─────────┬─────────────┘
                 │  le Workbench l'expose
                 ▼
       ┌───────────────────────┐
       │  WORKBENCH J3I  ◀──── │   ← ce document
       │  (UI + workflow)      │
       └─────────┬─────────────┘
                 │
                 ▼
       ┌───────────────────────┐
       │ Moteur de calcul (P4) │   (EligibilityEngine, FormulaEngine, …)
       └───────────────────────┘
```

**Conséquence :** J3I **n'ajoute pas de table « règles »** nouvelle en doublon. Il
s'appuie sur le modèle existant et lui ajoute (a) les quelques tables strictement
nécessaires pour boucher les trous restants (V009+), (b) la **totalité de la surface UI**,
(c) le **workflow d'évolution réglementaire**. C'est un document de **conception UI +
orchestration**, pas de conception de données — sauf pour les manques avérés.

---

## 2. Cartographie exhaustive des 14 patterns de calcul

Source : `Reglementation/elements_paie_historique_14726/elements_paie_historique_14726.txt`
(secteur EN), `Reglementation/IRG_Algerie_2008_2026/CALCUL IRG ALGERIE.txt` (fiscalité),
`l'IFC 2008 + 2015.txt` (IFC), `elements_paie_corps_commun_ouvriers_prof/*` (corps
communs), `regimes-indemnitaires_Paramedicaux 2011_2025/*` (paramédicaux — couverture
légère, à compléter), `ISSRP_Regles_Metier.md`, `IEP_Regles_Metier.md`, `PAPP_Regles_Metier.md`.

| #  | Pattern | Exemples concrets (avec texte source) | Périodes historiques |
|----|---------|---------------------------------------|----------------------|
| P1 | **% × base fixe** | `4 % × TBASE × ECH` (EXP_PEDAG, D.ex. 10-78 art. 9) ; `15 %` (ISSRP_2008) ; `25 %` (NUIS_LABO 2008-2024) ; `40 %` (SERV_ADM groupe B, D.ex. 10-134 art. 4) | 2008→ / 2025+ |
| P2 | **% composite plafonné** | `IEP_CONT = TBASE × min(ANC_PUB×1,4% + ANC_PRIV×0,7% ; 60%)` (D.p. 07-304 art. 16) | 2008→ |
| P3 | **% indexé par source externe (notation)** | `PAPP = TP × taux_rendement(0..40%)` (D.ex. 10-78 art. 3 → 12-403 → 25-55) ; `PAPG` idem intendance ; `REND_LABO` 0-30 % | 2008→ / 2012+ / 2025+ |
| P4 | **Forfait indexé par catégorie** | `DOC_PEDAG` : 2 000 / 2 500 / 3 000 DA selon `cat ≤ 10 / 11-12 / ≥ 13` (D.ex. 10-78 art. 8 → 11-373 art. 5) | 2008 / 2011 / 2025 |
| P5 | **Forfait indexé par type d'établissement** | `DIR_ETAB` : 3 000 / 4 000 / 5 000 DA selon Primaire / CEM / Lycée (D.ex. 15-271 art. 9 bis 1 → 25-55 art. 11) | 2015-09→ / 2025+ |
| P6 | **% indexé par grade** | `SERV_ADM` : `25 %` (secrétaires/attachés) ou `40 %` (admins/documentalistes) (D.ex. 10-134 art. 4) ; `SERV_TECH_CC` idem | 2008→ |
| P7 | **% à 3 taux par groupe de corps** | `ISSRP_15` / `ISSRP_30` / `ISSRP_45` sur 3 rubriques distinctes (D.ex. 11-373 art. 9 bis → 25-55 art. 10) | 2008-2024 / 2025+ |
| P8 | **Condition « en exercice effectif »** | `DIR_ETAB`, `GEST_FIN` (D.ex. 15-271 art. 9 bis 1 & 2) | 2015→ |
| P9 | **Cotisation paramétrable** (taux × assiette) | `SS` 9 % × ASSIETTE_COTISABLE (Q3) ; `MUNATEC` 1 % × ASSIETTE_COTISABLE (D.ex. 10-78 art. 11, Q3b-rev) ; `RETRAITE` part ouvrière | 2008→ |
| P10 | **Forfait à montant fixe (option agent)** | `OEUVRES_SOCIALES` retenue au choix de l'agent (Q3b) | 2008→ |
| P11 | **IRG barème à tranches + lissages** | barème 2008 (4 tranches) + barème 2022+ (6 tranches) + lissages 2020/2021/2022+ (`137/51`, `93/61`) + abattements (40 %, borné [1 000 ; 1 500]) + profils `HANDICAPE`/`RETRAITE_RG` | 2008→ / 2020 / 2021 / 2022+ |
| P12 | **IFC — barème catégorie par catégorie** | barème 2008 (par groupe de cat. 1-6/7-8/9-10/11-17) puis barème 2015 cat. par cat. (D.ex. 08-70 → 15-176) | 2008 / 2015 |
| P13 | **Périodicité de versement ≠ de calcul** | PAPP/PAPG/REND : calcul mensuel, **versement trimestriel** (D.ex. 10-78 art. 3) | 2008→ |
| P14 | **Effet rétroactif** (rappels) | D.ex. 11-373 publié 26/10/2011, **effet 01/01/2008** ; D.ex. 25-55 publié 21/01/2025, effet 01/01/2025 | continu |

> **Couverture paramédicaux :** *partielle* — seul `Regim indem des paramédicaux 2011-2024.pdf` est
> dans le périmètre lu. Le détail `regimes-indemnitaires_Paramedicaux 2011_2025` (deux fichiers)
> n'a pas été parsé ligne à ligne ; le modèle doit néanmoins le supporter via P1, P3, P4, P6.

---

## 3. Couverture par le modèle actuel (V001→V008 + J3E + J3H)

| # | Pattern | Mécanisme utilisé | Couverture | Source dans le modèle |
|---|---------|-------------------|:---:|---|
| P1 | % × base | `RubriqueFormules` (expression texte) | ✅ | V004 + J3E |
| P2 | % composite plafonné | `RubriqueFormules` + `RubriqueParametres` (clés taux/plafond) | ✅ | V004 + J3D §4 |
| P3 | % indexé notation | `RubriqueFormules` + **source externe non générique** (PAPP a son `PAPPCalculator` typé) | ⚠️ | **Manque un mécanisme générique « source de valeur »** |
| P4 | Forfait par catégorie | `RubriqueBaremes` (J3E §3, dimension `CATEGORIE`) | ✅ | J3E L1 / V008 |
| P5 | Forfait par type établissement | `RubriqueBaremes` (dimension `TYPE_ETABLISSEMENT`) | ✅ | J3E L1 / V008 |
| P6 | % par grade | `RubriqueBaremes` (dimension `GRADE`) + `RubriqueParametres` (2 clés `TAUX_GROUPE_A/B`) | ✅ (verbeux — voir §5.3) | J3E L1 / V008 |
| P7 | 3 taux par groupe de corps | 3 rubriques distinctes + `ReglesEligibilite` par groupe | ✅ | V005 + J3G |
| P8 | « exercice effectif » | `AgentAttributs` (`EXERCICE_EFFECTIF`, D3 J3H) | ✅ | J3H / V009 (avec `Agents`) |
| P9 | Cotisation paramétrable | `Cotisations` + `CotisationAssietteRubriques` + `RubriqueParametres` (TAUX) | ✅ | V005 |
| P10 | Forfait option agent | `Cotisations` (`Type='FACULTATIVE'`, `Taux=NULL`) + table de gestion par agent (Phase 5) | ✅ | V005 + Phase 5 |
| P11 | IRG barème + lissages | `BaremeIRG` + `BaremeIRGTranches` + `IRGReglesPeriode` (fractions exactes TEXT) | ✅ | V006 + V007 |
| P12 | IFC barème | `RubriqueBaremes` (dimension `CATEGORIE`) | ✅ | J3E L1 / V008 |
| P13 | Périodicité versement | `Rubriques.PeriodiciteVersement` (J3E §5, V008) | ✅ | V008 |
| P14 | Effet rétroactif | `DateEffet`/`DateFin` partout + moteur de rappels (Phase 4) | ✅ | V003-V007 + Q7 |

**Bilan : 13/14 patterns couverts par le modèle.** Le seul manquant au niveau **données**
est P3 (source de valeur externe). Tout le reste est une question **d'interface et de
workflow**, pas de schéma.

---

## 4. Lacunes restantes — ce qu'il faut encore ajouter

### 4.1 Lacunes modèle (V009+)

#### L-M1 — Source de valeur externe paramétrable (P3)
PAPP/PAPG/REND lisent aujourd'hui leur taux dans `DALNotation` via un calculateur typé
VB. C'est la bonne réponse pour PAPP, mais **on ne peut pas généraliser sans exposer la
notion**. Besoin : une table `SourcesValeur` qui décrit, pour une rubrique, **où elle va
chercher sa valeur** parmi un enum extensible.

```sql
CREATE TABLE SourcesValeur (
    Code        TEXT PRIMARY KEY,    -- ex. 'NOTATION_AGENT', 'ANCIENNETE_PUBLIQUE',
                                       --      'ANCIENNETE_PRIVEE', 'INDICE_ECHELON',
                                       --      'POINT_INDICIAIRE', 'BASE_ASSIETTE',
                                       --      'CONSTANTE_REGLEMENTAIRE'
    Libelle     TEXT NOT NULL,
    Description TEXT,
    Actif       INTEGER NOT NULL DEFAULT 1
);

ALTER TABLE Rubriques ADD COLUMN SourceValeurCode TEXT REFERENCES SourcesValeur(Code);
```
Le FormulaEngine expose `valeurSource(RUBRIQUE)`. La résolution effective est enregistrée
dans `Rubriques` (et non dans `RubriqueFormules` : une rubrique = une source) — un
catalogue simple, extensible sans code pour les sources standards ; un nouvel
`Calculateur` enregistré (pattern Open/Closed) pour les cas exotiques.

#### L-M2 — Conditions composées explicites (P6, P7)
V008 = ET plat (J3E-RM-040). J3H §2 propose déjà `GroupesEligibilite` (DNF : ET dans le
groupe, OU entre groupes) — **on adopte J3H, pas une nouvelle approche**. Une décision
formelle est prise ici : **la limite V1 « pas de groupes » (prompt J3H § C.5) est
abandonnée** au profit de J3H, parce que P6 (`SERV_ADM 25 % pour secrétaires OU agents
d'admin. OU attachés OU…`) l'exige. (Voir §11 — décision formelle D5.)

#### L-M3 — Colonnes d'audit enrichies sur barèmes (P4, P5, P6, P12)
`RubriqueBaremes` (J3E §3) a `DateEffet`/`DateFin`/`Source`/`Hash` mais **pas** d'audit
« qui a modifié quoi quand ». Les barèmes sont la donnée la plus sensible
réglementairement — il faut `CreatedBy`/`UpdatedBy`/`UpdatedAt` (alignés sur
`AuditLog`, V001).

### 4.2 Lacunes UI/Workflow (le cœur de J3I)

| Réf | Lacune | Conséquence |
|-----|--------|-------------|
| L-U1 | **Aucun écran d'édition des `RubriqueBaremes`** | L'utilisateur ne peut pas modifier les forfaits indexés (DOC, DIR, IFC) sans passer par SQL |
| L-U2 | **Aucun FormulaEditor** | `RubriqueFormules.Expression` est du TEXT brut — pas de coloration, pas de validation, pas d'aide à la saisie |
| L-U3 | **Aucun éditeur de règles d'éligibilité** | `ReglesEligibilite` n'est saisissable qu'en seed ; l'utilisateur ne peut pas ajouter une exception (« ISSRP_45 aussi pour les censeurs à partir de 2026 ») |
| L-U4 | **Aucun workflow d'évolution réglementaire** | Quand un décret sort, l'admin doit clore/créer manuellement N lignes, sans assistance ni simulation |
| L-U5 | **Aucun dry-run de simulation** | L'utilisateur ne peut pas voir « ce changement de taux va impacter 247 agents sur la période 03/2025 → 12/2025 » avant de valider |
| L-U6 | **Aucun audit visible côté UI** | `AuditLog` (V001) est en base, mais l'utilisateur ne peut pas voir « qui a changé le taux ISSRP de 0,15 à 0,30 le 14/07/2026 » dans l'écran de la rubrique |
| L-U7 | **Pas de typage fort des `RubriqueParametres`** | Pourcentage saisi comme texte libre « 0,15 » ou « 15% » — l'utilisateur peut se tromper ; pas d'aide à la saisie selon la clé |
| L-U8 | **Pas de garde-fou transversal** | Si l'utilisateur saisit une période qui chevauche une autre, c'est silencieusement permis — il faut une validation |
| L-U9 | **Pas de visualisation de la couverture réglementaire** | « Quels corps ne sont couverts par aucune ISSRP_45/30/15 ? » — impossible à voir sans requête SQL |
| L-U10 | **Pas d'écran de revue « matrice corps × rubrique »** | La matrice 14 patterns × ~50 corps × ~185 grades n'est pas visualisable |

### 4.3 Lacune mineure — nomenclature des sources (P11 IRG)

L'IRG a son propre jeu de règles (`IRGReglesPeriode`) déjà pleinement paramétrable
(V006). Il manque simplement **un écran d'édition WYSIWYG** aligné sur le reste — pas un
modèle nouveau. Voir §6.4.

---

## 5. Architecture du Workbench — une arborescence, un écran par pattern

### 5.1 Arborescence cible

```
Workbench réglementaire                    (accueil : tableau de bord)
├── Rubriques                              (catalogue, déjà en V004)
│   ├── [Fiche rubrique]
│   │   ├── Identité
│   │   ├── Formule          ← FormulaEditor (L-U2)
│   │   ├── Paramètres       ← éditeur clé/valeur typé (L-U7)
│   │   ├── Barème           ← éditeur de barème (L-U1) — visible si BaremeActif
│   │   ├── Éligibilité      ← éditeur de groupes de règles (L-U3) — J3H §2
│   │   ├── Dépendances
│   │   └── Historique       ← timeline + audit (L-U6)
│   └── [+ Nouvelle rubrique]
├── Cotisations
│   ├── Taux & assiettes    ← éditeur taux/assiette (L-U7)
│   └── Composition assiette
├── Fiscalité (IRG)
│   ├── Barèmes             ← éditeur barème (L-U1)
│   ├── Règles de période   ← éditeur fractions/lissages (L-U4)
│   └── Profils spéciaux
├── Carrière & grilles
│   ├── Valeur du point
│   ├── Grille indiciaire
│   └── Barèmes liés (IFC, etc.)
├── Simulation & dry-run    ← sandbox (L-U5)
├── Évolution réglementaire ← workflow assistant (L-U4) — voir §7
├── Audit & traçabilité
└── Matrice de couverture   ← visuel (L-U9, L-U10)
```

### 5.2 Écran-type « Fiche rubrique » — composants

Chaque fiche rubrique a un layout commun (cf. J3H-Q-J3H-4, repris et étendu) :

```
┌──────────────────────────────────────────────────────────────────┐
│ DOC_PEDAG  —  Indemnité de documentation pédagogique   [v 2025+]│
│ Nature: GAIN | Base: FORFAIT | Période: MENSUELLE | Affect.: ✓   │
├──────────────────────────────────────────────────────────────────┤
│ [ Identité │ Formule │ Paramètres │ Barème │ Éligibilité │ Audit ]│
├──────────────────────────────────────────────────────────────────┤
│  BARÈME — Montant indexé par catégorie                            │
│  ────────────────────────────────────                            │
│                                                                  │
│  Dimension : [CATÉGORIE ▼]                                       │
│  [+ Ajouter une tranche]                                         │
│                                                                  │
│  ┌────┬────────────┬────────────┬────────────┬────────────────┐  │
│  │ #  │ Borne inf  │ Borne sup  │ Montant DA │ Actions        │  │
│  ├────┼────────────┼────────────┼────────────┼────────────────┤  │
│  │ 1  │ 1          │ 10         │ 2 000      │ [Clore][Modif] │  │
│  │ 2  │ 11         │ 12         │ 2 500      │ [Clore][Modif] │  │
│  │ 3  │ 13         │ 17         │ 3 000      │ [Clore][Modif] │  │
│  │ 4  │ 18 (HC-S1) │ +∞         │ 3 000      │ [Clore][Modif] │  │
│  └────┴────────────┴────────────┴────────────┴────────────────┘  │
│                                                                  │
│  ⚠ Continuité : 3/4 tranches actives, 1 période ouverte (2025+) │
│  📊 Impact estimé : 1 240 agents enseignants concernés            │
└──────────────────────────────────────────────────────────────────┘
```

**Garde-fous embarqués (L-U8) :**
- Détection de **chevauchement** entre bornes — refus + motif
- Détection de **trou** entre BorneSup de la tranche n et BorneInf de la tranche n+1
- Une seule période **ouverte** (DateFin IS NULL) à la fois par (rubrique, dimension, clé)
- Si création d'une nouvelle tranche, proposition de **clôture automatique** de la précédente
- Si modification d'une tranche, **alerte ré-calcul** sur les bulletins de la période touchée
- **Source** obligatoire à toute création

### 5.3 Écran-type « Formule » — FormulaEditor (L-U2)

Édition de `RubriqueFormules.Expression` (TEXT). Le FormulaEngine expose un DSL simple :

```
# Variables
TBASE, TP, IE, VPI, ECH, ANC_PUB, ANC_PRIV, CAT, ...

# Fonctions
round(x, n)        # arrondi centralisé (paramètre ARRONDI_PORTEE)
min(a, b), max(a, b)
if(cond, a, b)
clamp(x, lo, hi)
bareme(RUB, dim)   # → RubriqueBaremes
valeurSource(RUB)  # → SourcesValeur

# Exemple
round(TBASE * 0.04 * ECH, 2)
```

**Fonctionnalités de l'éditeur :**
- Coloration syntaxique
- Auto-complétion sur les variables et fonctions
- Validation à la saisie (parenthèse, type)
- **Bouton « Simuler sur agent témoin »** — l'utilisateur choisit un agent, on affiche le résultat
- **Bouton « Voir l'explication »** — l'ExplainabilityEngine décompose le calcul
- **Bouton « Tester sur N agents »** — échantillon aléatoire de N agents, comparaison au calcul précédent

### 5.4 Écran-type « Éligibilité » — éditeur de groupes (L-U3 + L-M2)

Adoption directe de J3H §2 (`GroupesEligibilite` + `ReglesEligibilite` étendues).
Workbench :

```
ISSRP_45 — Conditions d'éligibilité
───────────────────────────────────
[+ Ajouter un groupe]    [+ Ajouter une condition au groupe sélectionné]

Groupe A : GRADES_PEDA_DIRECTS
  ─ (GRADE = 'PROFECOLPRIM' OR GRADE = 'PROFENSEFOND' OR …) [Sévérité: OBLIGATOIRE_REGLEMENTAIRE]

Groupe B : PROMOTION_ENSEIGNANT
  ─ (GRADE IN {7 grades conditionnels} AND ORIGINE_STATUTAIRE = 'ENSEIGNANT')  [Sévérité: RECOMMANDEE]

Période d'application : [2025-01-01] → [        ]   Source: [D.ex. 25-55 art. 10]
```

L'évaluateur J3H (forme normale disjonctive) résout ce genre d'expression nativement.

### 5.5 Écran « Matrice de couverture » (L-U9, L-U10)

Vue tabulaire :

```
                  │ DOC │ QUALIF │ ISSRP_45 │ ISSRP_30 │ ISSRP_15 │ ...
──────────────────┼─────┼────────┼──────────┼──────────┼──────────┼─────
PEM (enseignants) │  ✓  │   ✓    │    ✓     │    —     │    ✓     │
INTE (intendance) │  ✓  │   ✓    │    —     │    —     │    ✓     │
AGENLABO          │  —  │   —    │    —     │    —     │    ✓     │
...
```

Cellule cliquable → fiche condition / fiche rubrique. **Vert = règle active, Orange =
règle inactive (DateFin < aujourd'hui), Rouge = pas de règle, Gris = non applicable**.

---

## 6. Détail des écrans par pattern P1..P14

| Pattern | Écran principal | Tables touchées | Notes UI |
|---------|-----------------|-----------------|----------|
| P1 (% × base) | Fiche rubrique → Formule | `Rubriques`, `RubriqueFormules` | FormulaEditor + simulation |
| P2 (% composite plafonné) | Fiche rubrique → Formule + Paramètres | + `RubriqueParametres` | Édition des clés taux/plafond avec typage % |
| P3 (% indexé notation) | Fiche rubrique → Source de valeur | + `Rubriques.SourceValeurCode` | Catalogue `SourcesValeur` + mapping |
| P4 (forfait catégorie) | Fiche rubrique → Barème | `RubriqueBaremes` (dim CATÉGORIE) | Table des tranches |
| P5 (forfait type établissement) | Fiche rubrique → Barème | `RubriqueBaremes` (dim TYPE_ETABLISSEMENT) | idem |
| P6 (% par grade) | Fiche rubrique → Barème | `RubriqueBaremes` (dim GRADE) | idem + `RubriqueParametres` (clés TAUX_GROUPE) |
| P7 (3 taux par groupe) | 3 fiches rubriques (ISSRP_45/30/15) → Éligibilité | `ReglesEligibilite` (×3) | Éditeur de groupes |
| P8 (exercice effectif) | Fiche agent → Attributs | `AgentAttributs` | Saisie + historisation par agent |
| P9 (cotisation) | Écran Cotisations → Taux & assiettes | `Cotisations`, `CotisationAssietteRubriques` | Édition taux/assiette par période |
| P10 (forfait option agent) | Fiche agent → Cotisations optionnelles | + table de gestion par agent (Phase 5) | Montant au choix, périodicité |
| P11 (IRG) | Écran Fiscalité → Barèmes + Règles de période | `BaremeIRG`, `BaremeIRGTranches`, `IRGReglesPeriode` | Éditeur de tranches + lissages (fractions) |
| P12 (IFC) | Fiche rubrique → Barème | `RubriqueBaremes` (dim CATÉGORIE) | Identique à P4 |
| P13 (périodicité versement) | Fiche rubrique → Identité | `Rubriques.PeriodiciteVersement` | Sélecteur MENSUELLE/TRIMESTRIELLE/ANNUELLE/PONCTUELLE |
| P14 (effet rétroactif) | Workflow d'évolution réglementaire (§7) | toutes | Détection auto des bulletins à recalculer |

---

## 7. Workflow d'évolution réglementaire — le morceau stratégique

### 7.1 Cas d'usage principal

> *« Le 15/03/2026, le Journal Officiel publie un décret 26-XX qui modifie l'indemnité
> de qualification à compter du 01/01/2026. »*
>
> L'administrateur ouvre le Workbench, lance l'assistant d'évolution, et en 5 minutes
> la base est à jour, auditée, simulable.

### 7.2 Les 6 étapes de l'assistant

```
┌────────────────────────────────────────────────────────────────────┐
│ Assistant d'évolution réglementaire                                │
├────────────────────────────────────────────────────────────────────┤
│ 1. Identification de la rubrique                                  │
│    [●] QUALIF — Indemnité de qualification                        │
│                                                                    │
│ 2. Référence du texte                                              │
│    Décret n° [26-XX] du [15/03/2026]   Art. [ 7 ]                  │
│    Source externe : [https://www.joradp.dz/…/26-XX.pdf      ]      │
│                                                                    │
│ 3. Stratégie de versionning                                        │
│    ( ) Clôture de la période courante + nouvelle version          │
│    (●) Modification en place (rare,rétroactif)                     │
│    ( ) Duplication de la version « 2025 » → « 2026 »               │
│                                                                    │
│ 4. Paramètres modifiés (extrait auto depuis la version courante)   │
│    TAUX_CAT_MAX12   [ 0.40 ] → [ 0.45 ]                            │
│    TAUX_CAT_MIN13   [ 0.45 ] → [ 0.50 ]                            │
│                                                                    │
│ 5. Date d'effet                                                    │
│    [01/01/2026]   Date de publication : [15/03/2026]                │
│    ☑ Rétroactif → simulation des rappels (Phase 4)                  │
│                                                                    │
│ 6. Simulation dry-run                                              │
│    📊 Agents impactés : 1 240 (sur 1 875 EN — 66 %)                │
│    💰 Coût mensuel estimé delta : +3 100 000 DA                    │
│    ⚠ Bulletins validés à recalculer (rappels) : 312                │
│    [ ⏯ Voir le détail agent par agent ]                            │
│    [ ⏯ Exporter le rapport d'impact (PDF) ]                         │
│                                                                    │
│ [ ◀ Précédent ]    [ Annuler ]    [ Valider et appliquer ▶ ]      │
└────────────────────────────────────────────────────────────────────┘
```

### 7.3 Garanties du workflow

| Garantie | Mécanisme |
|----------|-----------|
| **Aucune perte d'historique** | Versionning strict (DateEffet/DateFin) ; les anciennes versions ne sont jamais supprimées, seulement cloturées |
| **Audit complet** | Chaque étape écrit dans `AuditLog` (qui/quand/avant/après/source) |
| **Dry-run obligatoire** | Aucune modification n'est commitée tant que la simulation n'a pas été vue (sauf bypass admin documenté) |
| **Rétroactif géré** | Si `DateEffet < aujourd'hui`, le moteur de rappels (Phase 4) génère les deltas pour les bulletins validés de la période touchée — **sans toucher aux bulletins eux-mêmes** (rappels = lignes additionnelles) |
| **Rollback possible** | Tant qu'aucun bulletin validé n'est impacté, on peut annuler la dernière version. Au-delà, c'est une nouvelle version (J3E §7) |
| **Source obligatoire** | Pas d'enregistrement sans référence textuelle (décret/arrêté) |
| **Continuité temporelle** | Validation automatique : pas de chevauchement, pas de trou, une seule période ouverte par clé |

### 7.4 Modes d'évolution

| Mode | Quand | Effet |
|------|-------|-------|
| **Modification courante** | Pas de DateFin / DateFin > aujourd'hui | Édition en place, recalcul des bulletins non validés, historique des valeurs conservé en `AuditLog` |
| **Clôture + nouvelle version** | DateFin < aujourd'hui, on veut une nouvelle valeur | La version courante est cloturée (`DateFin = hier`), une nouvelle ligne est créée avec la nouvelle `DateEffet` |
| **Duplication** | Nouvelle période réglementaire (ex. 2026) | Clone la dernière version avec nouvelle `DateEffet` ; l'utilisateur édite les paramètres |
| **Rétroactif** | `DateEffet < aujourd'hui` | Identique à « clôture + nouvelle » mais en plus, déclenche la génération de **rappels** pour les bulletins validés de la période rétroactive (Phase 4) |

---

## 8. Critères d'acceptation transverses

### 8.1 Fonctionnels

- **C-F1 — Couverture exhaustive** : les 14 patterns (P1-P14) sont éditables via UI, sans SQL
- **C-F2 — Toute modification passe par le workflow** (§7) — pas d'édition directe en base
- **C-F3 — Dry-run avant commit** : preview d'impact (agents × montant × période) systématique
- **C-F4 — Audit visible** : chaque modification est consultable depuis l'UI (L-U6)
- **C-F5 — Source obligatoire** : tout enregistrement porte une référence réglementaire
- **C-F6 — Continuité temporelle** : validations systématiques (pas de chevauchement, pas de trou, une seule ouverte)

### 8.2 Non-fonctionnels

- **C-NF1 — Déterminisme** : un même contexte (agent × période × version de la base) → un même résultat, garantie absolue
- **C-NF2 — Performance UI** : écran de rubrique ouvert en < 200 ms ; simulation sur 200 agents < 2 s
- **C-NF3 — Extensibilité** : un nouveau pattern = nouvelle entrée dans `SourcesValeur` ou nouveau type de bareme, **sans migration** de schéma
- **C-NF4 — Pas de hardcoding résiduel** : `grep` automatisé dans le code pour les noms de rubriques, taux, seuils (déjà en place V004)
- **C-NF5 — MVVM strict** : aucune logique métier dans le code-behind XAML (CONVENTIONS §5)

### 8.3 Tests

- **C-T1** — Pour chaque pattern P1..P14, au moins un test « édition UI → modification en base → recalcul paie correct »
- **C-T2** — Test de non-régression : la suite ISSRP existante (97 tests) + IEP (15) + PAPP (15) reste verte
- **C-T3** — Test de dry-run : modifier un taux et vérifier que le dry-run affiche le bon delta avant commit
- **C-T4** — Test de rétroactif : créer une version rétroactive, vérifier que les rappels sont générés (Phase 4)
- **C-T5** — Test de rollback : annuler une version non encore propagée, vérifier l'état de la base
- **C-T6** — Test de continuité : tenter un chevauchement, vérifier le refus

---

## 9. Extensions de modèle V009 — version refactorée (R1-R5, J3J)

> **Cette section est la version refactorée de la version initiale de J3I §9.**
> Les refactors R1-R5 sont documentés et argumentés dans
> `docs/analysis/J3J_REFACTORING_AVANT_V009.md` (15/07/2026, v1.0, validé utilisateur).
> **Statut : v1.0** — modèle final avant implémentation, en attente de dernière
> validation documentaire utilisateur.

### 9.1 Tables nouvelles

| Table | Nature (R4 révisé) | PK | Colonnes principales | Audit | Versionnée |
|---|:---:|:---:|---|:---:|:---:|
| **`SourcesValeur`** | **Catalogue technique** (R4 révisé) | `Id` (TEXT code, **R2**) | `Id`, `Libelle`, `Description`, `Actif` | CreatedAt/By | non |
| **`CriteresEligibilite`** | **Catalogue technique** (R4 révisé) | `Id` (TEXT code, **R2**) | `Id`, `Libelle`, `TypeValeur` (TEXT/INT/DATE/ENUM), `SourceResolution` (ATTRIBUT_AGENT/ATTRIBUT_GRADE/CARRIERE/CALCULE), `Actif` | CreatedAt/By | non |
| **`MessagesRegles`** | **Texte réglementaire** (R4 révisé — **audit complet préservé**) | `Id` (TEXT code, **R2**) | `Id`, `Categorie` (ELIGIBILITE/AVERTISSEMENT/SUGGESTION), `TexteFr`, `TexteAr` (nullable), `Source`, `DateEffet`, `DateFin`, `Actif` | CreatedAt/By | oui |
| **`GroupesEligibilite`** | **Règle réglementaire** | `Id` (TEXT code) | `Id`, `RubriqueId`, `Severite`, `MessageId`, `Priorite`, `DateEffet`, `DateFin`, `Source`, `Hash` | CreatedAt/By | oui |

### 9.2 Tables amendées

| Table | Amendement | Réf |
|---|---|---|
| `Rubriques` | + `SourceValeurId` (TEXT, FK → `SourcesValeur.Id`, NULL par défaut — P3 uniquement) | L-M1 (R2) |
| `ReglesEligibilite` | - `Critere` (TEXT) avec `CHECK IN (...)` → + `CritereId` (TEXT, FK → `CriteresEligibilite.Id`)<br>+ `GroupeId` (TEXT, FK → `GroupesEligibilite.Id`, NULL = condition commune) | L-M2 (R3) |
| `RubriqueBaremes` | + `UpdatedAt`, `UpdatedBy` (audit barème, alignement `AuditLog`) | L-M3 |

### 9.3 Tables NON créées en V009 (R1 — reportées à Phase 5)

Conformément à R1 (validé utilisateur, J3J § 6) : ces tables de gestion dépendent
directement du modèle `Agents` et ne sont **pas créées en avance**. Leur conception
intentionnelle est documentée dans `J3J_REFACTORING_AVANT_V009.md` § 8.3-8.5 pour
préserver la cohérence avec les phases suivantes.

- ~~`AgentAttributs`~~ — design preview J3J § 8.3
- ~~`AgentRubriques`~~ — design preview J3J § 8.4
- ~~`AvertissementsHistorique`~~ — design preview J3J § 8.5

> **Cohérence J3I / J3H** : J3H § 1 dit déjà « les tables de gestion sont créées
> avec `Agents` en Phase 5 ». J3I § 9 v1.0 divergeait par erreur. La version
> refactorée **rétablit la cohérence** : J3H § 1 est la référence, J3I § 9 ne crée
> plus de tables vides en avance. (CA-R7 du J3J)

### 9.4 Résumé des évolutions vs V008

**SQL synthétique V009 :**

```sql
-- 1. Nouveaux catalogues (R2 = Id partout ; R4 = audit allégé pour techniques)
CREATE TABLE SourcesValeur (
    Id TEXT NOT NULL PRIMARY KEY,         -- ex. 'NOTATION_AGENT', 'ANCIENNETE_PUBLIQUE'
    Libelle TEXT NOT NULL,
    Description TEXT,
    Actif INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0,1)),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL
);

CREATE TABLE CriteresEligibilite (
    Id TEXT NOT NULL PRIMARY KEY,         -- ex. 'FILIERE', 'CORPS', 'GRADE', 'CATEGORIE',
                                          --      'ORIGINE_STATUTAIRE' (D3)
    Libelle TEXT NOT NULL,
    TypeValeur TEXT NOT NULL CHECK (TypeValeur IN ('TEXT','INT','DATE','ENUM')),
    SourceResolution TEXT NOT NULL CHECK (SourceResolution IN
        ('ATTRIBUT_AGENT','ATTRIBUT_GRADE','CARRIERE','CALCULE')),
    Actif INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0,1)),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL
);

-- 2. Texte réglementaire (R4 révisé : audit complet conservé)
CREATE TABLE MessagesRegles (
    Id TEXT NOT NULL PRIMARY KEY,
    Categorie TEXT NOT NULL CHECK (Categorie IN ('ELIGIBILITE','AVERTISSEMENT','SUGGESTION')),
    TexteFr TEXT NOT NULL,
    TexteAr TEXT,
    Source TEXT,                          -- référence réglementaire (décret/arrêté)
    DateEffet TEXT NOT NULL,
    DateFin TEXT,
    Actif INTEGER NOT NULL DEFAULT 1 CHECK (Actif IN (0,1)),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL
);

-- 3. En-tête de règle (DNF, J3H §2, D5)
CREATE TABLE GroupesEligibilite (
    Id TEXT NOT NULL PRIMARY KEY,         -- ex. 'GE-ISSRP45-ORIGINE'
    RubriqueId TEXT NOT NULL REFERENCES Rubriques(Id),
    Severite TEXT NOT NULL DEFAULT 'INFO' CHECK (Severite IN
        ('INFO','RECOMMANDEE','OBLIGATOIRE_REGLEMENTAIRE')),
    MessageId TEXT REFERENCES MessagesRegles(Id),
    Priorite INTEGER NOT NULL DEFAULT 100,
    DateEffet TEXT NOT NULL,
    DateFin TEXT,
    Source TEXT,
    Hash TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL
);

-- 4. Amendements
ALTER TABLE Rubriques ADD COLUMN SourceValeurId TEXT REFERENCES SourcesValeur(Id);
                                       -- NULL par défaut (P3 uniquement)
ALTER TABLE ReglesEligibilite ADD COLUMN CritereId TEXT REFERENCES CriteresEligibilite(Id);
ALTER TABLE ReglesEligibilite ADD COLUMN GroupeId TEXT REFERENCES GroupesEligibilite(Id);
                                       -- NULL = condition commune
ALTER TABLE RubriqueBaremes ADD COLUMN UpdatedAt TEXT;
ALTER TABLE RubriqueBaremes ADD COLUMN UpdatedBy TEXT;

-- 5. Migration de données (R3) : CHECK supprimé, Critere -> CritereId via CriteresEligibilite
-- Les règles V008 existantes sont migrées : CritereId = Code du critère dans le dictionnaire.
-- Les conditions sans GroupeId restent communes (ET plat V008 inchangé).

-- 6. Seed du dictionnaire
INSERT INTO CriteresEligibilite (Id, Libelle, TypeValeur, SourceResolution, Actif, CreatedAt, CreatedBy) VALUES
    ('FILIERE', 'Filière', 'ENUM', 'CARRIERE', 1, $now, 'system'),
    ('CORPS', 'Corps', 'ENUM', 'CARRIERE', 1, $now, 'system'),
    ('GRADE', 'Grade', 'ENUM', 'CARRIERE', 1, $now, 'system'),
    ('CATEGORIE', 'Catégorie', 'INT', 'CARRIERE', 1, $now, 'system'),
    ('FONCTION', 'Fonction', 'ENUM', 'CARRIERE', 1, $now, 'system'),
    ('TYPE_CONTRAT', 'Type de contrat', 'ENUM', 'CARRIERE', 1, $now, 'system'),
    ('ECHELON', 'Échelon', 'INT', 'CARRIERE', 1, $now, 'system'),
    ('ANCIENNETE', 'Ancienneté (années)', 'INT', 'CALCULE', 1, $now, 'system'),
    ('TYPE_ETABLISSEMENT', "Type d'établissement", 'ENUM', 'CARRIERE', 1, $now, 'system'),
    ('ORIGINE_STATUTAIRE', 'Origine statutaire', 'ENUM', 'ATTRIBUT_AGENT', 1, $now, 'system');

INSERT INTO SourcesValeur (Id, Libelle, Description, Actif, CreatedAt, CreatedBy) VALUES
    ('NOTATION_AGENT', 'Note de l''agent', 'Résolu via DALNotation (PAPP, PAPG, REND)', 1, $now, 'system'),
    ('ANCIENNETE_PUBLIQUE', 'Ancienneté publique (années)', 'Service public antérieur', 1, $now, 'system'),
    ('ANCIENNETE_PRIVEE', 'Ancienneté privée (années)', 'Service privé antérieur (D3)', 1, $now, 'system'),
    ('INDICE_ECHELON', 'Indice d''échelon', 'Depuis T_Indices_Echelons à la date de paie', 1, $now, 'system'),
    ('POINT_INDICIAIRE', 'Valeur du point indiciaire', 'Depuis T_ValeurPoint à la date de paie', 1, $now, 'system'),
    ('BASE_ASSIETTE', 'Base d''assiette (cotisable/imposable)', 'Snapshot de l''assiette courante', 1, $now, 'system'),
    ('CONSTANTE_REGLEMENTAIRE', 'Constante réglementaire', 'Taux/plafond/borne lu en base', 1, $now, 'system');
```

### 9.5 Schéma V008 → V009 : récapitulatif

| Élément | V008 | V009 refactoré | Source |
|---|---|---|---|
| Tables de référentiel nomenclature | ~10 (Filieres, Corps, Grades…) | inchangé | V002 |
| Tables de barème | `RubriqueBaremes` | + `UpdatedAt`/`By` | L-M3 |
| Tables d'éligibilité | `ReglesEligibilite` (CHECK Critere) | + `GroupeId`, `CritereId` (FK) | L-M2, R3 |
| Sources de valeur | implicite (calculateurs typés) | explicite : `SourcesValeur` + `Rubriques.SourceValeurId` | L-M1 |
| Critères d'éligibilité | énumérés en dur (CHECK) | dictionnarisés : `CriteresEligibilite` | R3 |
| Messages de règles | absents | `MessagesRegles` (texte réglementaire, audit complet) | D7 |
| Groupes d'éligibilité (DNF) | absents | `GroupesEligibilite` + `ReglesEligibilite.GroupeId` | D5 |
| Tables de gestion agent | absentes | **différées Phase 5** (J3J § 8.3-8.5) | R1 |

**Une seule migration V009**, test d'upgrade V008 → V009 documenté en
`DICTIONNAIRE_DONNEES.md` § 8ter.9.

---

## 10. Plan d'implémentation révisé (inscription dans le PLAN_ACTION)

| Phase | Contenu J3I | Effort | Dépend de |
|-------|-------------|:------:|-----------|
| **3bis** (nouvelle) | Modèle V009 : `SourcesValeur`, `GroupesEligibilite`, audit barème ; migration depuis V008 ; tests de migration | M | 1, 3 |
| **4** (étendu) | Moteur étendu : `valeurSource()`, évaluation DNF (groupes), simulation dry-run rapide | L | 3bis |
| **5** (étendu) | Use cases Workbench : `AppliquerEvolution`, `Simuler`, `CloreVersion`, `DupliquerVersion`, `GenererRappels` | L | 4 |
| **6** (étendu) | Workbench UI complet : arborescence §5, FormulaEditor, éditeur de barème, éditeur de groupes, matrice de couverture, assistant d'évolution | XL | 5 |
| **8** (étendu) | Suite de tests Workbench (C-T1 → C-T6) + validation contre bulletins de référence (Q11) | L | 6 |

**Réordonnancement proposé :**

1. Le **Lot 1** du prompt J3H (modèle d'affectation) **reste valide** et alimente V009.
2. Le **Lot 2** (évaluateur) est étendu pour gérer `valeurSource()` et les groupes DNF.
3. Le **Lot 3** (use cases) absorbe les cas d'usage Workbench.
4. Le **Lot 4** (UI) devient le Workbench complet (et non plus seulement l'écran d'affectation).
5. Une **phase 3bis** est insérée pour la migration V008→V009 avant la phase 4.

Aucun lot n'est supprimé ; aucun existant n'est cassé. **J3I est une extension, pas une
réécriture.**

---

## 11. Décisions à valider (STOP & ASK)

| Réf | Question | Option recommandée | Bloque |
|-----|----------|--------------------|--------|
| **D5** | Abandon de la limite V1 « pas de conditions composées » (J3H §C.5) au profit de `GroupesEligibilite` (J3H §2, DNF) ? | ✅ Oui — exigé par P6 et P7 | Code V009 |
| **D6** | Ajouter `SourcesValeur` (catalogue extensible) plutôt qu'un calculateur typé par cas (P3) ? | ✅ Oui — généricité sans limite | Code V009 |
| **D7** | Workbench = arborescence unique (§5.1) avec écran-type par pattern, plutôt qu'un écran fourre-tout ? | ✅ Oui — clarté, testabilité, montée en charge | UI Phase 6 |
| **D8** | Dry-run obligatoire avant tout commit (L-U5) ? | ✅ Oui — sécurité, anticipation de coût | Workflow §7 |
| **D9** | Rétroactif = nouvelle version + génération de **rappels** (lignes additionnelles), pas modification des bulletins validés ? | ✅ Oui — RM-103 (Q7) | Moteur Phase 4 |
| **D10** | Une seule migration **V009** pour tout le bloc (L-M1 + L-M2 + L-M3 + J3H) ? | ✅ Oui — atomicité, test d'upgrade unique | Migration V009 |
| **D11** | La matrice de couverture (§5.5) est-elle un livrable de la V1 ou une vue « nice-to-have » post-V1 ? | Recommandation : **V1**, parce qu'elle sert d'outil de validation à l'administrateur | UI Phase 6 |

---

## 12. Risques et mitigations

| Risque | Impact | Mitigation |
|--------|--------|-----------|
| Complexité de l'UI Workbench | Phase 6 explose | Découpage en MVP (5 écrans prioritaires) puis enrichissement |
| Formule incorrecte saisie par l'utilisateur | Bulletins faux | FormulaEditor avec validation à la saisie + simulation obligatoire + audit |
| Rétroactif massif (effet 2008 sur nouveau paramètre) | Performance | Moteur de rappels batch (Phase 4) hors ligne, jamais bloquant |
| Duplication accidentelle de versions | Données corrompues | Verrouillage transactionnel sur la création + warning fort |
| Rollback impossible après impact bulletins | Audit trail perdu | Garde-fou : rollback libre tant que `DateEffet > max(DateEffet bulletin validé impacté)` ; au-delà, c'est une nouvelle version |
| Source de valeur non répertoriée | Code à modifier | Catalogue `SourcesValeur` + nouveau calculateur via DI (Open/Closed) |

---

## 13. Annexes

### 13.1 Index croisé Pattern → Tables → Écrans

Voir matrice §6.

### 13.2 Index croisé Rubrique → Pattern (extrait EN)

| Rubrique | Pattern | Périodes historiques | Tables spécifiques |
|----------|---------|----------------------|---------------------|
| `IEP_FONC` | P1 | 2008→ | `RubriqueFormules` |
| `IEP_CONT` | P2 | 2008→ | `RubriqueFormules` + `RubriqueParametres` (3 clés) |
| `PAPP` | P3 | 2008 / 2012 / 2025 | + `SourcesValeur('NOTATION_AGENT')` |
| `PAPG` | P3 | 2008 / 2025 | idem |
| `REND_LABO` | P3 | 2008→ (0-30 %) | idem |
| `QUALIF` | P6 (par catégorie) | 2008 (rétro 11-373) / 2025 | `RubriqueBaremes` (dim CATÉGORIE) |
| `DOC_PEDAG` | P4 | 2008 / 2011 / 2025 | `RubriqueBaremes` (dim CATÉGORIE) |
| `ISSRP_15/30/45` | P7 | 2008 (15 % unique) / 2025 (3 taux) | 3 rubriques + `ReglesEligibilite` (groupes) |
| `EXP_PEDAG` | P1 (% × base × ECH) | 2008→ | `RubriqueFormules` |
| `SERV_TECH_LABO` | P1 | 2008→ | idem |
| `NUIS_LABO` | P1 (taux variable) | 2008 (10 %) → 2025 (25 %) | idem + `RubriqueParametres('TAUX')` |
| `SERV_ADM` | P6 | 2008→ | `RubriqueBaremes` (dim GRADE) |
| `SERV_TECH_CC` | P6 | 2008→ | idem |
| `SOUT_ADM_*` | P1 | 2012→ | `RubriqueFormules` + `RubriqueParametres('TAUX')` |
| `DIR_ETAB` | P5 + P8 | 2015-09→ / 2025+ | `RubriqueBaremes` (dim TYPE_ETABLISSEMENT) + `AgentAttributs.EXERCICE_EFFECTIF` |
| `GEST_FIN` | P1 + P8 | 2015-09→ / 2025+ | idem + `AgentAttributs` |
| `IFC` | P12 | 2008 / 2015 | `RubriqueBaremes` (dim CATÉGORIE) |
| `SS` | P9 | 2008→ (taux 9 %) | `Cotisations` |
| `MUNATEC` | P9 | 2008→ (taux 1 %) | idem (Q-J3H-4) |
| `OEUVRES_SOCIALES` | P10 | 2008→ | `Cotisations` (FACULTATIVE) |
| `IRG` | P11 | 2008→ / 2020 / 2021 / 2022+ | `BaremeIRG` + `BaremeIRGTranches` + `IRGReglesPeriode` |

### 13.3 Glossaire des nouveaux concepts

- **Workbench réglementaire** : ensemble des écrans et workflows qui rendent la base
  réglementaire éditable par l'utilisateur final.
- **Source de valeur** : pour P3, point d'entrée externe (notation, ancienneté, indice)
  dont une rubrique tire sa valeur.
- **DNF (forme normale disjonctive)** : conditions d'un groupe ETées, groupes OUés.
- **Dry-run** : simulation d'une modification sans commit, retournant un rapport d'impact.
- **Rétroactif** : version dont la `DateEffet` est antérieure à la date du jour, déclenchant
  la génération de **rappels** (lignes additionnelles) pour les bulletins validés de la
  période rétroactive.

### 13.4 Documents à mettre à jour

- `PLAN_ACTION.md` — ajouter Phase 3bis, étendre Phase 4/5/6/8 (cf. §10)
- `DICTIONNAIRE_DONNEES.md` — ajouter V009 (SourcesValeur, GroupesEligibilite, audit barème)
- `CONVENTIONS.md` — ajouter la convention « toute modification réglementaire passe par
  le Workbench »
- `J3B_CATALOGUE_REGLES_METIER.md` — sourcer chaque RM à son écran Workbench (§13.2)
- `J3F_QUESTIONS_OUVERTES.md` — clôturer Q-04 (résolu par D5), Q-08 (résolu par P3), Q-09
  (résolu par P13)

---

*Dernière mise à jour : 14/07/2026 — v1.0 — soumis à validation (STOP & ASK, §11).*
