# Dictionnaire de Données — PaieEducation ERP

> **Source de vérité** : les scripts `V*.sql` de `src/PaieEducation.Persistence.Migrations/`.
> **Version courante** : V001 → V006 — 6 migrations, **22 tables** (2 système + 20 métier), **0 valeur réglementaire en dur**.
> **Principe cardinal** : aucune règle ou valeur n'est codée en dur. Tout vit en base, versionné par date d'effet, éditable par l'utilisateur.

Ce document est **vivant** : il est régénéré/mis à jour à chaque ajout de migration en Phase 1 et chaque évolution de schéma en Phase 5 (Persistence & Infrastructure).

---

## 1. Vue d'ensemble

| Catégorie | Tables | Migration |
|-----------|:------:|-----------|
| Système (méta) | 2 | bootstrap + V001 |
| Structure & carrière (nomenclature) | 9 | V002 |
| Grille indiciaire (données réglementaires) | 3 | V003 |
| Rubriques (éléments de paie) | 4 | V004 |
| Éligibilité + cotisations | 3 | V005 |
| IRG + paramètres système | 4 | V006 |
| **Total** | **22** | |

**Moteur de résolution par date** : toutes les tables « réglementaires » (V003, V004 sauf `Rubriques`, V005 sauf `Cotisations`, V006 sauf `BaremeIRG`/`Parametres`) partagent la même requête de résolution : on cherche la ligne dont la `DateEffet` est la plus récente **≤ date_demandée**, et dont la `DateFin` est **≥ date_demandée** (ou `NULL` pour « toujours en vigueur »).

---

## 2. Conventions globales

### 2.1. Identifiants
- `Id` = `TEXT NOT NULL PRIMARY KEY` — code métier stable et lisible (ex. `PEM` pour un corps, `ISSRP_45` pour la rubrique de soutien à 45 %, `VP-2007-01-01` pour une version de valeur du point).
- Pas d'auto-incrément. Les Ids sont fournis par le seed (Phase 2) ou par l'application.

### 2.2. Versionnement par date d'effet
Les tables qui portent des données sujettes à évolution réglementaire ont les colonnes :
- `DateEffet TEXT NOT NULL` — début de validité, ISO 8601 `YYYY-MM-DD`.
- `DateFin TEXT NULL` — fin de validité ; `NULL` = « toujours en vigueur ».
- `Version TEXT NOT NULL` — libellé humain de la version (ex. `"2007"`, `"2022-03"`, `"2024"`).
- `Source TEXT` — référence réglementaire (ex. `"Décret 07-308"`, `"Loi de finances 2022"`).

**Requête de résolution par date** (modèle partagé) :
```sql
SELECT * FROM TableVersionnee
WHERE XxxId = $id
  AND DateEffet <= $date
  AND (DateFin IS NULL OR DateFin >= $date)
ORDER BY DateEffet DESC
LIMIT 1;
```

### 2.3. Audit
- `CreatedAt TEXT NOT NULL` — horodatage ISO 8601 UTC de la création de la ligne.
- `UpdatedAt TEXT NULL` — dernière MAJ (tables statiques seulement).
- `CreatedBy TEXT NOT NULL` / `UpdatedBy TEXT` — acteur (utilisateur OS, script, `« test »`...).
- `Hash TEXT NOT NULL` — SHA-256 du payload utile (calculé au seed pour détecter les drifts).
- `Source TEXT` — référence d'origine (décret, fichier CSV source, etc.).

### 2.4. Booléens
SQLite n'a pas de booléen : on utilise `INTEGER NOT NULL CHECK (col IN (0, 1))`.
- `1` = vrai, `0` = faux.
- Nom de colonne en français : `Actif`, `EstImposable`, `EstCotisable`, `EstRetenue`, `Incluse`, `HorsCategorie`.

### 2.5. Énumérations
Toutes les colonnes à valeurs finies sont protégées par un `CHECK (col IN (...))` côté SQL.
L'application lit/valide via le même enum (cf. `Domain/Enums` quand il sera créé en Phase 3).

### 2.6. FK et intégrité
- `PRAGMA foreign_keys = ON` est positionné à **chaque** connexion ouverte par `SqliteMigrator` (SQLite perd ce réglage entre deux ouvertures).
- `PRAGMA journal_mode = WAL` activé sur les bases fichier (ignoré en `:memory:`).
- `ON DELETE` par défaut (`NO ACTION` — la suppression d'un parent référencé est rejetée).
- `CHECK` d'auto-dépendance interdit dans `RubriqueDependances` (`RubriqueId <> DependDeId`).

---

## 3. Tables système (hors modèle métier)

### 3.1. `SchemaVersions` (bootstrap du migrateur)
> Crée par le `SqliteMigrator` à chaque `Apply()` (`CREATE TABLE IF NOT EXISTS`). N'est pas versionnée elle-même.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Version` | INTEGER | PK | Numéro de la migration (1, 2, 3, ...) |
| `Name` | TEXT | NOT NULL | Nom court extrait du nom de fichier (`init`, `nomenclature`, ...) |
| `AppliedAt` | TEXT | NOT NULL | ISO 8601 UTC de l'application |
| `AppliedBy` | TEXT | NOT NULL | Acteur (utilisateur, script, `« test »`) |
| `DurationMs` | INTEGER | NOT NULL | Durée d'application en ms |
| `Checksum` | TEXT | NOT NULL | SHA-256 hex (64 chars) du SQL appliqué — détection de drift |

### 3.2. `AuditLog` (V001)
> Journal de toute action métier sensible. Écrit par le moteur applicatif (pas par trigger).

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | INTEGER | PK AUTOINCREMENT | Compteur |
| `OccurredAt` | TEXT | NOT NULL | ISO 8601 UTC |
| `Actor` | TEXT | NOT NULL | Utilisateur / job / service |
| `Action` | TEXT | NOT NULL | `INSERT` / `UPDATE` / `DELETE` / `CALCUL` / `VALIDATE` / ... |
| `EntityType` | TEXT | NOT NULL | ex. `'Agent'`, `'Bulletin'`, `'Rubrique'`, `'Cotisation'` |
| `EntityId` | TEXT | | Identifiant métier (`Matricule`, `IdBulletin`, `CodeRubrique`...) |
| `Payload` | TEXT | | JSON optionnel (diff, snapshot, contexte) |
| `Comment` | TEXT | | Note libre |

Index : `IX_AuditLog_OccurredAt`, `IX_AuditLog_EntityType_EntityId`.

---

## 4. V002 — Nomenclature (structure & carrière)

Tables statiques (pas de `DateEffet`/`DateFin` : l'identité est stable ; ce qui change se gère par `Actif = 0` + nouvelle ligne).

### 4.1. `Filieres`
Grande famille professionnelle. Une seule occurrence par filière, jamais versionnée.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | ex. `"ENSEIGNANT"`, `"ADMIN"`, `"INSPECTION"`, `"SANTE_PUBLIQUE"`, `"OUVRIERS_AGENTS"` |
| `Libelle` | TEXT | NOT NULL | Nom humain |
| `Actif` | INTEGER | `CHECK IN (0,1)` | |
| `CreatedAt` | TEXT | NOT NULL | |
| `UpdatedAt` | TEXT | | |
| `Source` | TEXT | | |
| `Hash` | TEXT | NOT NULL | |

### 4.2. `TypesContrat`
Type de contrat de l'agent. Deux valeurs V1 : `STATUTAIRE` (fonctionnaire) et `CONTRACTUEL`.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"STATUTAIRE"` / `"CONTRACTUEL"` |
| `Libelle` | TEXT | NOT NULL | |
| (mêmes colonnes d'audit) |

### 4.3. `TypesPersonnel`
Classification fonctionnelle large (Enseignant, Administratif, ...).

### 4.4. `Fonctions`
Poste occupé : `DIRECTEUR`, `CENSEUR`, `INTENDANT`, etc.

### 4.5. `Echelons`
1..12, avec `CHECK Numero BETWEEN 1 AND 12` et `UNIQUE(Numero)`. L'`Id` est l'entier en texte (`"1"`, `"2"`, ...).

### 4.6. `Categories`
Niveaux 1..17 + `HC-S1` + `HC-S2`. La colonne `Niveau` est unique et bornée 1..19.
La colonne `HorsCategorie` (0/1) distingue les 2 niveaux HC.

| Id | Niveau | HorsCategorie | Exemple |
|----|-------:|:-------------:|---------|
| `"1"` | 1 | 0 | 1ère catégorie |
| ... | ... | 0 | ... |
| `"17"` | 17 | 0 | 17ème catégorie |
| `"HC-S1"` | 18 | 1 | Hors Catégorie Spécial 1 |
| `"HC-S2"` | 19 | 1 | Hors Catégorie Spécial 2 |

### 4.7. `Corps`
Ensemble de grades d'une même filière (ex. `PEM` = Professeurs de l'Enseignement Moyen).

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"PEM"`, `"PES"`, `"PELP"`, `"AT"`, ... |
| `Libelle` | TEXT | NOT NULL | |
| `FiliereId` | TEXT | FK → `Filieres(Id)` | Index `IX_Corps_FiliereId` |
| (audit) | | | |

### 4.8. `Grades`
Rang au sein d'un corps. `UNIQUE(CorpsId, Ordre)` empêche les doublons d'ordre dans un même corps.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | |
| `Libelle` | TEXT | NOT NULL | |
| `CorpsId` | TEXT | FK → `Corps(Id)` | Index `IX_Grades_CorpsId` |
| `Ordre` | INTEGER | NOT NULL `CHECK >= 1` | Rang hiérarchique (1 = plus bas) |

### 4.9. `Etablissements`
Etablissement public (Lycée, CEM, Primaire, ...). Pas de FK vers une table de commune/wilaya (pas dans le périmètre V1).

| Colonne | Type | Description |
|---------|------|-------------|
| `Id` | TEXT PK | Code interne (ex. `"LYC001"`) |
| `Nom` | TEXT NOT NULL | |
| `Type` | TEXT NOT NULL | `Lycée` / `CEM` / `Primaire` / ... |
| `Adresse` | TEXT | |
| `Telephone` | TEXT | |

---

## 5. V003 — Grille indiciaire (données réglementaires versionnées)

Toutes les tables de cette section sont versionnées (DateEffet/DateFin/Version/Source/Hash).

### 5.1. `ValeurPoint`
Valeur du point indiciaire (DZD) à une date d'effet. **Clé du moteur de paie** : `traitement_base = indice_min × valeur_point`.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | ex. `"VP-2007-01-01"` |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE` |
| `DateFin` | TEXT | | |
| `Valeur` | REAL | NOT NULL `CHECK > 0` | DZD (défaut 45) |
| `Version` | TEXT | NOT NULL | `"2007"`, `"2022-03"`, ... |
| `Source` | TEXT | | ex. `"Décret 07-308"` |

**Test cas concret** : catégorie 7 = 348/398/473/548 sur 4 périodes (2007, 2022-03, 2023, 2024).

### 5.2. `GrilleIndiciaire`
Indice minimal par catégorie, versionné.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"GI-<catId>-<date>"` |
| `CategorieId` | TEXT | FK → `Categories(Id)` | |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE(CategorieId, DateEffet)` |
| `DateFin` | TEXT | | |
| `IndiceMin` | INTEGER | NOT NULL `CHECK > 0` | ex. 348, 398, 473, 548 pour cat. 7 |

### 5.3. `IndicesEchelon`
Points additionnels par échelon, versionnés.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"IE-<echId>-<date>"` |
| `EchelonId` | TEXT | FK → `Echelons(Id)` | |
| `Indice` | INTEGER | NOT NULL `CHECK >= 0` | |

> **Note** : si l'indice d'échelon doit varier par corps (à confirmer), ajouter une colonne `CorpsId` ou dénormaliser en `Echelon × Corps` lors de la phase d'extension (Phase 10).

---

## 6. V004 — Rubriques (éléments de paie)

### 6.1. `Rubriques` (statique)
Identité stable d'un élément de paie. Ce qui change (taux, seuil, formule) vit dans les tables filles versionnées.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"IEP"`, `"PAPP"`, `"IRG"`, `"ISSRP_45"`, ... |
| `Libelle` | TEXT | NOT NULL | |
| `Nature` | TEXT | NOT NULL `CHECK IN ('GAIN','RETENUE','COTISATION','IMPOT')` | |
| `BaseCalcul` | TEXT | NOT NULL `CHECK IN ('TRAITEMENT','TBASE','TBASE_ECHELON','FORFAIT','ASSIETTE_COTISABLE','ASSIETTE_IMPOSABLE')` | Base sur laquelle la rubrique se calcule |
| `Periodicite` | TEXT | NOT NULL `CHECK IN ('MENSUELLE','TRIMESTRIELLE','ANNUELLE','PONCTUELLE')` | |
| `OrdreCalcul` | INTEGER | NOT NULL `CHECK >= 0` | Rang dans le pipeline (plus petit = plus tôt) |
| `EstImposable` | INTEGER | NOT NULL `CHECK IN (0,1)` | DEFAULT 0 |
| `EstCotisable` | INTEGER | NOT NULL `CHECK IN (0,1)` | DEFAULT 0 |
| `Description` | TEXT | | Explication humaine (utilisée par l'UI) |
| `Actif` | INTEGER | `CHECK IN (0,1)` | DEFAULT 1 |

### 6.2. `RubriqueFormules` (versionnée)
Expression de calcul **en texte**, évaluée par le `FormulaEngine` (Phase 4). Aucune formule n'est codée en dur.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"RF-IEP-2007-01-01"` |
| `RubriqueId` | TEXT | FK → `Rubriques(Id)` | |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE(RubriqueId, DateEffet)` |
| `DateFin` | TEXT | | |
| `Expression` | TEXT | NOT NULL | ex. `"round(TBASE * TAUX_IEP * ECH, 2)"` |
| `Ordre` | INTEGER | NOT NULL DEFAULT 0 | (cas multi-formules, par défaut 0) |

### 6.3. `RubriqueParametres` (versionnée)
Couples (clé, valeur) versionnés. **Tout paramètre réglementaire** (taux, seuil, forfait, ...) vit ici, pas en code.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"RP-IEP-TAUX-2007-01-01"` |
| `RubriqueId` | TEXT | FK → `Rubriques(Id)` | |
| `Cle` | TEXT | NOT NULL | ex. `"TAUX"`, `"SEUIL_MIN"`, `"FORFAIT_CAT_1"` |
| `Valeur` | TEXT | NOT NULL | Texte pour porter n'importe quel type (parsing applicatif) |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE(RubriqueId, Cle, DateEffet)` |
| `DateFin` | TEXT | | |

### 6.4. `RubriqueDependances` (versionnée)
Arêtes du graphe DAG de calcul. La rubrique A dépend de B si A a besoin du résultat de B.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | |
| `RubriqueId` | TEXT | FK → `Rubriques(Id)` | |
| `DependDeId` | TEXT | FK → `Rubriques(Id)` | |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE(RubriqueId, DependDeId, DateEffet)` |
| `DateFin` | TEXT | | |
| `CHECK(RubriqueId <> DependDeId)` | | | Auto-dépendance interdite |

Index : `IX_RubriqueDependances_DependDeId` pour la résolution inverse.

---

## 7. V005 — Éligibilité & cotisations

### 7.1. `ReglesEligibilite` (versionnée)
Une même rubrique peut être éligible ou non selon le profil de l'agent.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | |
| `RubriqueId` | TEXT | FK → `Rubriques(Id)` | |
| `Critere` | TEXT | NOT NULL `CHECK IN ('FILIERE','CORPS','GRADE','CATEGORIE','FONCTION','TYPE_CONTRAT','ECHELON','ANCIENNETE')` | |
| `Operateur` | TEXT | NOT NULL `CHECK IN ('=','IN','NOT_IN','>=','<=','>','<')` | |
| `Valeur` | TEXT | NOT NULL | `"PEM"` pour `=`, `"PEM,PES,INSPECTION"` pour `IN` (CSV sans espace) |
| `DateEffet` | TEXT | NOT NULL | |
| `DateFin` | TEXT | | |

Index : `IX_ReglesEligibilite_RubriqueId`, `IX_ReglesEligibilite_Critere_Valeur`.

**Cas d'usage (issu de la matrice Q6)** : les 3 taux d'ISSRP (45 / 30 / 15) sont modélisés par **3 rubriques distinctes** (`ISSRP_45`, `ISSRP_30`, `ISSRP_15`), chacune avec sa règle d'éligibilité pointant sur son groupe de corps. La requête de résolution du moteur : « pour un agent en corps `PEM`, quelle est la rubrique `ISSRP_*` applicable ? »

### 7.2. `Cotisations` (versionnée)
Paramétrage fin des retenues (taux + assiette), conformément à Q3.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"SS"`, `"RETRAITE"`, `"MUTUELLE"`, `"OEUVRES_SOCIALES"`, ... |
| `Code` | TEXT | NOT NULL | ex. `"SS"` |
| `Libelle` | TEXT | NOT NULL | |
| `TypeCotisation` | TEXT | NOT NULL `CHECK IN ('OBLIGATOIRE_SALARIALE','OBLIGATOIRE_PATRONALE','FACULTATIVE')` | |
| `Taux` | REAL | `CHECK IS NULL OR BETWEEN 0 AND 1` | NULL pour les FACULTATIVE à montant fixe |
| `AssietteRef` | TEXT | NOT NULL `CHECK IN ('ASSIETTE_COTISABLE','ASSIETTE_IMPOSABLE','TRAITEMENT_BASE','TRAITEMENT_BRUT','MONTANT_FIXE')` | |
| `EstRetenue` | INTEGER | NOT NULL `CHECK IN (0,1)` | 0 = part patronale seule, 1 = retenue sur le bulletin de l'agent |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE(Code, DateEffet)` |
| `DateFin` | TEXT | | |

**Cas Q3b** :
- **SS part ouvrière 9 %** : `Code='SS'`, `Type='OBLIGATOIRE_SALARIALE'`, `Taux=0.09`, `AssietteRef='ASSIETTE_COTISABLE'`.
- **Mutuelle (facultative)** : `Code='MUTUELLE'`, `Type='FACULTATIVE'`, `Taux=NULL`, `AssietteRef='MONTANT_FIXE'`. Le montant fixe est stocké par agent (hors V1.c — table à venir Phase 3).
- **Œuvres sociales (facultative)** : idem, `Code='OEUVRES_SOCIALES'`.

### 7.3. `CotisationAssietteRubriques` (versionnée)
Composition de l'assiette d'une cotisation : quelles rubriques la composent ?

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | |
| `CotisationId` | TEXT | FK → `Cotisations(Id)` | |
| `RubriqueId` | TEXT | FK → `Rubriques(Id)` | |
| `Incluse` | INTEGER | NOT NULL `CHECK IN (0,1)` | DEFAULT 1 — permet d'exclure une rubrique d'une assiette donnée |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE(CotisationId, RubriqueId, DateEffet)` |
| `DateFin` | TEXT | | |

---

## 8. V006 — IRG & paramètres système

> **Patch V007 (J2.a)** : `IRGReglesPeriode` voit ses colonnes `CoefGeneral`, `ConstGeneral`, `CoefSpecial`, `ConstSpecial` passer de `REAL`/`INTEGER` à `TEXT` pour stocker la **fraction réglementaire exacte** (ex. `8/3`, `20000/3`, `137/51`, `27925/8`, `93/61`, `81213/41`). Une fraction comme `8/3` ne se représente pas exactement en `REAL` ; la stocker en `TEXT` garantit la précision du référentiel. CHECK interdit `Coef*='0'` (division par zéro dans la formule).
>
> Le seed canonique (4 règles de période + barème 2008) est appliqué par la CLI `tools/PaieEducation.Tools` (`seed irg`) — pas dans une migration.

### 8.1. `BaremeIRG` (en-tête)

### 8.1. `BaremeIRG` (en-tête)
En-tête d'un barème. **Une seule instance V1** : le barème 2008 (les « règles de période » 2020/2021/2022+ ne sont pas des barèmes distincts — cf. Q4b).

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | `"IRG-2008"` |
| `Code` | TEXT | NOT NULL UNIQUE | |
| `Libelle` | TEXT | NOT NULL | |
| `DateEffet` | TEXT | NOT NULL | |
| `DateFin` | TEXT | | |

### 8.2. `BaremeIRGTranches`
Tranches du barème 2008. `BorneSup = NULL` signifie « +infini ».

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | |
| `BaremeId` | TEXT | FK → `BaremeIRG(Id)` | |
| `BorneInf` | INTEGER | NOT NULL `CHECK >= 0` | DA |
| `BorneSup` | INTEGER | | DA, NULL = +infini |
| `Taux` | REAL | NOT NULL `CHECK BETWEEN 0 AND 1` | 0.00, 0.20, 0.30, 0.35 |
| `Ordre` | INTEGER | NOT NULL `CHECK >= 1` | `UNIQUE(BaremeId, Ordre)` et `UNIQUE(BaremeId, BorneInf)` |

**Tranches du barème 2008** (à seed) :

| Ordre | BorneInf | BorneSup | Taux |
|------:|---------:|---------:|-----:|
| 1 | 0 | 10 000 | 0.00 |
| 2 | 10 001 | 30 000 | 0.20 |
| 3 | 30 001 | 120 000 | 0.30 |
| 4 | 120 001 | NULL | 0.35 |

### 8.3. `IRGReglesPeriode` (versionnée — cœur de Q4/Q4b)
Une ligne par période (2020, 2021, 2022+). Toutes les colonnes `Default 0`/`1` permettent de porter une règle vide (cas 2020/2021 où il n'y a pas de lissage).

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | |
| `Code` | TEXT | NOT NULL UNIQUE | `"PERIODE-2020"`, `"PERIODE-2021"`, `"PERIODE-2022+"` |
| `Libelle` | TEXT | NOT NULL | |
| `DateDebut` | TEXT | NOT NULL | `IX_IRGReglesPeriode_DateDebut` |
| `DateFin` | TEXT | | NULL = période ouverte |
| `BaremeId` | TEXT | FK → `BaremeIRG(Id)` | |
| `ExonerationSeuil` | INTEGER | NOT NULL DEFAULT 0 | IRG = 0 si imposable ≤ seuil (ex. 30 000) |
| `AbattementTaux` | REAL | NOT NULL DEFAULT 0 | ex. 0.40 (40 % sur IRG brut) |
| `AbattementMin` | INTEGER | NOT NULL DEFAULT 0 | borne basse (ex. 1 000) |
| `AbattementMax` | INTEGER | NOT NULL DEFAULT 0 | borne haute (ex. 1 500), `CHECK >= AbattementMin` |
| `CoefGeneral` | TEXT | NOT NULL DEFAULT '1' | Fraction canonique (« 8/3 », « 137/51 », « 1 »). Cf. V007. |
| `ConstGeneral` | TEXT | NOT NULL DEFAULT '0' | Fraction ou entier (« 20000/3 », « 0 »). |
| `CoefSpecial` | TEXT | NOT NULL DEFAULT '1' | Fraction canonique (« 5/3 », « 93/61 »). Cf. V007. |
| `ConstSpecial` | TEXT | NOT NULL DEFAULT '0' | Fraction ou entier. |
| `PlafondSpecial` | INTEGER | NOT NULL DEFAULT 0 | Plafond IRG pour profil spécial |
| `Source` | TEXT | | |

**Valeurs V1 (à confirmer en Phase 8 contre les bulletins réels)** :

| Période | DateDebut | DateFin | Exon. | Abatt. (taux / min / max) | CoefG | CoefS |
|---------|-----------|---------|------:|:--------------------------|------:|------:|
| 2020 | 2020-01-01 | 2020-12-31 | 30 000 | 0.40 / 1 000 / 1 500 | 1.0 | 1.0 |
| 2021 | 2021-01-01 | 2021-12-31 | 30 000 | 0.40 / 1 000 / 1 500 | 1.0 | 1.0 |
| 2022+ | 2022-01-01 | NULL | 30 000 | 0.40 / 1 000 / 1 500 | 0.80 | 0.80 (plafond 15 000) |

### 8.4. `Parametres` (versionnée — clés/valeurs typées)
Paramètres système transverses (arrondi, défauts, seuils globaux). `Valeur` est en TEXT ; `Type` indique comment l'application doit l'interpréter.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | |
| `Cle` | TEXT | NOT NULL UNIQUE | ex. `"ARRONDI_MODE"`, `"VALEUR_POINT_DEFAUT"`, `"SEUIL_ANCIENNETE_IEP"` |
| `Valeur` | TEXT | NOT NULL | Sérialisation (string) — interprétation selon `Type` |
| `Type` | TEXT | NOT NULL `CHECK IN ('INT','REAL','BOOL','TEXT','DATE')` | |
| `Description` | TEXT | | |
| `DateEffet` | TEXT | NOT NULL | |
| `DateFin` | TEXT | | |

**Paramètres V1 seedés** :

| Clé | Type | Valeur | Réf |
|-----|------|--------|-----|
| `ARRONDI_MODE` | TEXT | `DINAR_PLUS_PROCHE` | Q9b — défaut retenu |
| `ARRONDI_PRECISION` | INT | `1` | Précision en DA |
| `VALEUR_POINT_DEFAUT` | REAL | `45` | Avant seed de `ValeurPoint` |
| `SEUIL_EXONERATION_IRG_DEFAUT` | INT | `30000` | Avant seed de `IRGReglesPeriode` |

---

## 9. Requêtes de résolution — les plus utilisées

### 9.1. Valeur du point à une date
```sql
SELECT Valeur FROM ValeurPoint
WHERE DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d)
ORDER BY DateEffet DESC LIMIT 1;
```

### 9.2. Indice minimal d'une catégorie à une date
```sql
SELECT IndiceMin FROM GrilleIndiciaire
WHERE CategorieId = $cat AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d)
ORDER BY DateEffet DESC LIMIT 1;
```

### 9.3. Règle IRG applicable à une date
```sql
SELECT * FROM IRGReglesPeriode
WHERE DateDebut <= $d AND (DateFin IS NULL OR DateFin >= $d)
ORDER BY DateDebut DESC LIMIT 1;
```

### 9.4. Tranche IRG applicable à un imposable
```sql
SELECT Taux FROM BaremeIRGTranches
WHERE BaremeId = $bareme AND BorneInf <= $imposable
  AND (BorneSup IS NULL OR BorneSup >= $imposable);
```

### 9.5. Règle d'éligibilité d'une rubrique par corps
```sql
SELECT * FROM ReglesEligibilite
WHERE RubriqueId = $r
  AND Critere = 'CORPS'
  AND (
        (Operateur = '=' AND Valeur = $corps)
     OR (Operateur = 'IN' AND (',' || Valeur || ',') LIKE ('%,' || $corps || ',%'))
  )
  AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d);
```

### 9.6. Formule d'une rubrique à une date
```sql
SELECT Expression FROM RubriqueFormules
WHERE RubriqueId = $r AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d)
ORDER BY DateEffet DESC LIMIT 1;
```

### 9.7. Paramètre d'une rubrique à une date
```sql
SELECT Valeur FROM RubriqueParametres
WHERE RubriqueId = $r AND Cle = $cle
  AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d)
ORDER BY DateEffet DESC LIMIT 1;
```

---

## 10. Reconstruction depuis zéro

1. **Créer une base vide** : `Data Source=paie.db` (fichier) — le `SqliteMigrator` crée le fichier.
2. **Appliquer les migrations** : `var migrator = new SqliteMigrator(options, MigrationLoader.LoadFromAssembly(...)); migrator.Apply();`
3. **Seeder** : Phase 2 — lecture des fichiers `Reglementation/` et `Documentation de Référence du Projet/`, insertion idempotente. Un **rapport d'import** (lignes lues/insérées/rejetées) est produit.
4. **Vérifier** : `SELECT COUNT(*) FROM SchemaVersions` doit retourner 6.
5. **Auditer** : la colonne `Hash` permet de rejouer le seed et de détecter les dérives (comparaison SHA-256).

---

## 11. Hors périmètre (rappel)

- **Calcul de paie** : la logique métier (assemblage des rubriques, calcul IRG détaillé, ...) est dans la couche `Application` / `Domain`, **pas** dans la base. Aucune procédure stockée, aucun trigger. La base stocke les règles, le moteur les interprète.
- **Bulletins** : pas dans cette phase (V007+, J5+).
- **Agents** : pas dans cette phase (V007+).
- **Historique des valeurs** : chaque changement = nouvelle version (ligne avec nouvelle `DateEffet`). Pas de table d'audit des modifications (`AuditLog` en V1 trace les actions, pas les valeurs).
- **Triggers** : aucune contrainte `RAISE` / trigger. Tout le contrôle est dans l'app.

---

## 12. Conventions de nommage (rappel)

Voir `CONVENTIONS.md` §4. En résumé :
- Tables : `PascalCase` au singulier (`Agent`, `Bulletin`, `Rubrique`).
- Colonnes : `PascalCase` (`DateEffet`, `Id`, `Libelle`).
- Ids : code métier stable, en majuscules pour les nomenclatures (`PEM`, `IRG-2008`), en kebab-case pour les lignes versionnées (`VP-2007-01-01`).
- FK : `<TableParent>Id` (`FiliereId`, `CategorieId`, `BaremeId`).
- Index : `IX_<Table>_<Colonne(s)>`.
- Contraintes : nommées par SQLite par défaut (les noms explicites seront ajoutés en Phase 5 si besoin).

---

## 13. Évolution du schéma

Toute nouvelle table ou colonne suit la procédure :
1. Créer une nouvelle migration `V007__xxx.sql` (jamais modifier une migration déjà appliquée).
2. Compléter ce dictionnaire dans le même commit.
3. Tester l'upgrade depuis la version N-1 (le `SqliteMigrator` est conçu pour).
4. **Aucun hotfix en prod** : la mise à jour passe toujours par une migration versionnée.

**Règle d'or** : si une valeur est dans le code, c'est un bug.
