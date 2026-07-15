# J3.e — Proposition de modèle de paramétrage générique

> **Statut :** v1.0 — Proposition soumise à validation. Objectif : couvrir **toutes** les rubriques
> du catalogue J3C par la donnée, de sorte qu'une évolution réglementaire = mise à jour de
> paramètres, sans modification du code métier (principe cardinal du PLAN_ACTION).

## 1. Le socle existant couvre déjà l'essentiel

Le schéma V001–V007 offre 5 mécanismes composables, tous versionnés par `DateEffet`/`DateFin` :

```
Rubriques (identité, nature, base, ordre, flags)
   ├── RubriqueFormules      → expression évaluée par le FormulaEngine
   ├── RubriqueParametres    → (clé, valeur) : taux, seuils, plafonds
   ├── RubriqueDependances   → DAG d'ordre de calcul
   └── ReglesEligibilite     → matrice profil agent × rubrique
Cotisations + CotisationAssietteRubriques → assiettes paramétrables
BaremeIRG/Tranches + IRGReglesPeriode      → fiscalité par période
Parametres                                  → transverses (point, arrondi)
```

**Patron de résolution unique** (à implémenter une fois dans le Domaine, Phase 3) :
`Résoudre(clé, datePériode) = version telle que DateEffet ≤ datePériode < DateFin` — appliqué
uniformément aux grilles, formules, paramètres, éligibilités, barèmes et périodes IRG.
C'est ce résolveur qui matérialise RM-100/101/104.

## 2. Analyse de couverture : 4 lacunes identifiées

| # | Besoin réglementaire | Exemples | Lacune |
|---|----------------------|----------|--------|
| L1 | Montant/taux fonction d'une **tranche de critère** | DOC_PEDAG (catégorie), IFC (catégorie), DIR_ETAB (type d'établissement), QUALIF (catégorie), SOUT_PARAMED (catégorie) | Exprimable en `RubriqueParametres` uniquement par convention de nommage (`FORFAIT_CAT_1`…) : fragile, non requêtable, bornes implicites |
| L2 | Éligibilité par **origine statutaire** | ISSRP 45 % : « grades de promotion d'origine enseignante » | Le critère `CORPS` ne suffit pas (RM-044) |
| L3 | **Périodicité de versement** ≠ périodicité de calcul | PAPP/PAPG/rendement : mensuel/trimestriel | Colonne unique `Periodicite` |
| L4 | **Bonification indiciaire** postes supérieurs | D.p. 07-307 + révisions | Aucune table (si Q-07 = périmètre V1) |

## 3. L1 — Table générique `RubriqueBaremes` (V008)

```sql
CREATE TABLE RubriqueBaremes (
    Id          TEXT NOT NULL PRIMARY KEY,   -- ex. "RB-IFC-2015-CAT7"
    RubriqueId  TEXT NOT NULL REFERENCES Rubriques(Id),
    Dimension   TEXT NOT NULL CHECK (Dimension IN
                  ('CATEGORIE', 'ECHELON', 'ANCIENNETE', 'TYPE_ETABLISSEMENT', 'CORPS')),
    BorneInf    TEXT NOT NULL,               -- "7" ou "PRIMAIRE" (dimension discrète : BorneInf = BorneSup)
    BorneSup    TEXT,                        -- NULL = +infini
    TypeValeur  TEXT NOT NULL CHECK (TypeValeur IN ('TAUX', 'MONTANT')),
    Valeur      TEXT NOT NULL,               -- fraction/décimal canonique, parsé par le moteur
    DateEffet   TEXT NOT NULL,
    DateFin     TEXT,
    Source      TEXT, Hash TEXT NOT NULL, CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_RubriqueBaremes_Rub_Dim_Borne_Date
    ON RubriqueBaremes (RubriqueId, Dimension, BorneInf, DateEffet);
```

- Le FormulaEngine expose une fonction `bareme(RUBRIQUE, dimension)` résolue à la date de paie.
- Couvre IFC, DOC_PEDAG, DIR_ETAB, QUALIF, SOUT_PARAMED, TECHNICITE (seuil), et tout futur barème
  par tranches sans nouvelle table.
- `RubriqueParametres` reste réservé aux **scalaires** (un taux, un plafond).

## 4. L2 — Critère d'origine statutaire

Deux compléments, cumulables :

1. **`GradeAttributs`** (référentiel) : `(GradeId, Attribut, Valeur, DateEffet, DateFin)` —
   ex. `("Conseiller de l'Education", "ORIGINE_ENSEIGNANTE_POSSIBLE", "1")`. Sert au paramétrage
   par défaut de la matrice ISSRP (Q-03).
2. **Attribut d'agent** `OrigineStatutaire` (ENSEIGNANT / AUTRE / INCONNU), saisi au dossier :
   nouveau critère `ORIGINE_CORPS` dans `ReglesEligibilite.Critere`. Quand deux règles ISSRP
   sont candidates pour le même agent (45 vs 30), l'origine tranche ; à défaut, STOP & ASK à la saisie.

## 5. L3 — Périodicité de versement

```sql
ALTER TABLE Rubriques ADD COLUMN PeriodiciteVersement TEXT
    CHECK (PeriodiciteVersement IN ('MENSUELLE','TRIMESTRIELLE','ANNUELLE','PONCTUELLE'));
-- NULL = identique à Periodicite (calcul)
```
Le comportement V1 (versement mensuel du prorata vs cumul trimestriel) reste une décision
fonctionnelle → **Q-09** ; le schéma supporte les deux.

## 6. L4 — Bonifications indiciaires (si Q-07 = oui)

Table `BonificationsIndiciaires` : `(Regime ['ETAT'|'ETABLISSEMENT'], Niveau, Categorie, Section,
NiveauHierarchique, Indice, DateEffet, DateFin)` — 4 versions (2008, 2022-03, 2023, 2024).
La rubrique `BONIF` la consomme via `bareme()`-like ; exclusivité RM-067 = règle d'éligibilité
(`FONCTION NOT_IN …`) ou contrôle de validation.

## 7. Cinématique cible d'une évolution réglementaire (exemple : décret 2026 modifiant l'ISSRP)

1. L'utilisateur (écran de paramétrage, Phase 6) ou un script de seed clôt les versions courantes
   (`DateFin = 2025-12-31`) et insère les nouvelles (`DateEffet = 2026-01-01`, `Source` = décret).
2. Aucun code modifié ; le résolveur sélectionne la bonne version par période.
3. Si l'effet est rétroactif : le moteur de rappels (Phase 4) détecte les périodes payées ≥ date
   d'effet et génère les deltas (RM-102), sans toucher aux bulletins validés (RM-103).

## 8. Ce qui reste légitimement dans le code

- L'**algorithme** IRG (séquence exonération/abattement/lissages) — ses **valeurs** sont en base.
- Le pipeline de calcul, le résolveur de versions, le service d'arrondi, l'évaluateur de formules.
- Les invariants du domaine (RM-080…085) — ce sont des règles de structure, pas des valeurs.

Tout le reste (taux, montants, seuils, tranches, bornes, dates, bénéficiaires, dépendances,
formules, ordre de calcul, flags fiscaux/sociaux, assiettes) est en base, versionné et éditable.
