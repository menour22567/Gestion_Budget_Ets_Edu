# Dictionnaire de Données — PaieEducation ERP

> **Source de vérité** : les scripts `V*.sql` de `src/PaieEducation.Persistence.Migrations/`.
> **Version courante :** V001 → V008 (déjà en place) + **V009 (Workbench réglementaire, ADR-0007 / J3I)** — 7 migrations de données (V001-V008) + 1 migration de référentiel (V009), **~30 tables** (2 système + ~28 métier), **0 valeur réglementaire en dur**.
> **Principe cardinal** : aucune règle ou valeur n'est codée en dur. Tout vit en base, versionné par date d'effet, éditable par l'utilisateur via le Workbench réglementaire (D7).

Ce document est **vivant** : il est régénéré/mis à jour à chaque ajout de migration en Phase 1, Phase 3bis (V009) et chaque évolution de schéma en Phase 5 (Persistence & Infrastructure).

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
| Workbench — barèmes indexés (J3E L1) + `PeriodiciteVersement` (J3E L3) | 1 (+2 colonnes) | V008 |
| **Workbench réglementaire (ADR-0007, J3I)** : sources de valeur, groupes d'éligibilité (DNF), dictionnaire de critères, messages, amorces gestion | 5 nouvelles + 2 amendées | **V009** |
| **Total** | **~30** (2 système + ~28 métier) | |

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

## 8bis. V008 — Workbench socle : barèmes indexés & périodicité de versement (J3E)

> Introduit par `docs/analysis/J3E_MODELE_PARAMETRAGE.md`. Étend V004 pour supporter
> les patterns P4 (forfait par catégorie), P5 (forfait par type d'établissement),
> P6 (% par grade) et P12 (IFC — barème catégorie par catégorie), ainsi que P13
> (périodicité de versement ≠ de calcul).

### 8bis.1. `RubriqueBaremes` (versionnée)

Une même rubrique peut prendre une **valeur différente selon une dimension** (catégorie,
grade, échelon, ancienneté, type d'établissement, corps). Le FormulaEngine expose
`bareme(RUBRIQUE, dimension)` qui résout la bonne ligne à la date de paie.

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | ex. `RB-IFC-2015-CAT7`, `RB-DOC-CAT-10-2008` |
| `RubriqueId` | TEXT | FK → `Rubriques(Id)` | Index `IX_RubriqueBaremes_RubriqueId` |
| `Dimension` | TEXT | NOT NULL `CHECK IN ('CATEGORIE','ECHELON','ANCIENNETE','TYPE_ETABLISSEMENT','CORPS','GRADE')` | Axe d'indexation |
| `BorneInf` | TEXT | NOT NULL | `"7"` (cat), `"PRIMAIRE"` (type etab), `"PROFECOLPRIM"` (corps) |
| `BorneSup` | TEXT | | `NULL` = +infini ; sinon, valeur_textuelle (discrète) |
| `TypeValeur` | TEXT | NOT NULL `CHECK IN ('TAUX','MONTANT')` | Nature de la valeur (fraction ou DA) |
| `Valeur` | TEXT | NOT NULL | Fraction canonique ou entier DA, parsé par le moteur |
| `DateEffet` | TEXT | NOT NULL | `UNIQUE(RubriqueId, Dimension, BorneInf, DateEffet)` |
| `DateFin` | TEXT | | `NULL` = « toujours en vigueur » |
| `Source` | TEXT | | ex. `"Décret 08-70 art. 1"`, `"D.ex. 10-78 art. 8"` |
| `Hash` | TEXT | NOT NULL | SHA-256 du payload utile |
| `CreatedAt` | TEXT | NOT NULL | ISO 8601 UTC |
| `CreatedBy` | TEXT | NOT NULL | Acteur |
| `UpdatedAt` | TEXT | | Dernière MAJ (cf. V009 §9bis.6 pour les colonnes ajoutées) |
| `UpdatedBy` | TEXT | | Acteur de la dernière MAJ |

**Cas d'usage (issu de `Reglementation/`) :**
- `IFC` (P12) — dimension `CATEGORIE`, 4 lignes (cat. 1-6 / 7-8 / 9-10 / 11-17) en 2008, 11 lignes (cat. 1 à 10 + 11-17) en 2015
- `DOC_PEDAG` (P4) — dimension `CATEGORIE`, 3 lignes (≤10 / 11-12 / ≥13), 3 versions (2008, 2011, 2025)
- `DIR_ETAB` (P5) — dimension `TYPE_ETABLISSEMENT`, 3 lignes (PRIMAIRE / CEM / LYCEE)
- `QUALIF` (P6) — dimension `CATEGORIE`, 2 lignes (≤12 / ≥13)
- `SERV_ADM`, `SERV_TECH_CC` (P6) — dimension `GRADE`, 2 lignes (25 % / 40 %)

### 8bis.2. `Rubriques.PeriodiciteVersement` (colonne ajoutée)

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `PeriodiciteVersement` | TEXT | `CHECK IN ('MENSUELLE','TRIMESTRIELLE','ANNUELLE','PONCTUELLE')` | Périodicité de **versement** (≠ `Periodicite` qui est la périodicité de **calcul**) |

`NULL` = identique à `Periodicite` (calcul). Pour PAPP/PAPG/rendement (P13) :
`Periodicite = 'MENSUELLE'` (calcul mensuel), `PeriodiciteVersement = 'TRIMESTRIELLE'`
(versement trimestriel — le moteur provisionne).

---

## 8ter. V009 — Workbench réglementaire (ADR-0007, J3I, J3J v1.0)

> Migration unique qui regroupe tout le bloc Workbench (D10) : sources de valeur,
> groupes d'éligibilité (DNF), dictionnaire de critères, messages, audit barème.
>
> **Version finale (15/07/2026)** — refactorée selon R1-R5 du
> `J3J_REFACTORING_AVANT_V009.md` (validé utilisateur 15/07/2026). Distinction
> explicite **audit technique** (catalogues) vs **traçabilité réglementaire**
> (règles, valeurs, textes). Aucune table de gestion agent n'est créée en V009 —
> design preview documenté en J3J § 8.3-8.5 pour la Phase 5.

### 8ter.1. `SourcesValeur` (catalogue technique — R2 + R4 révisé)

Catalogue extensible des **sources de valeur** — d'où une rubrique tire sa « matière
première ». Remplace les calculateurs typés VB pour le pattern P3 (% indexé sur
une source externe). D6. **Nature : catalogue technique — audit minimal.**

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK (R2) | ex. `NOTATION_AGENT`, `ANCIENNETE_PUBLIQUE`, `ANCIENNETE_PRIVEE`, `INDICE_ECHELON`, `POINT_INDICIAIRE`, `BASE_ASSIETTE`, `CONSTANTE_REGLEMENTAIRE` |
| `Libelle` | TEXT | NOT NULL | Nom humain |
| `Description` | TEXT | | Sémantique de la source |
| `Actif` | INTEGER | NOT NULL `CHECK IN (0,1)` DEFAULT 1 | |
| `CreatedAt` | TEXT | NOT NULL | |
| `CreatedBy` | TEXT | NOT NULL | |

**Seed V1** : 7 codes (cf. tableau ci-dessus), tous actifs. Une nouvelle source =
`INSERT` + un `SourceValeurCalculator` enregistré en DI (pattern Open/Closed) —
**pas de migration**.

### 8ter.2. `Rubriques.SourceValeurId` (colonne ajoutée — R2)

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `SourceValeurId` | TEXT | FK → `SourcesValeur(Id)` (R2) | `NULL` = la formule `RubriqueFormules.Expression` est self-contained (P1, P2, P4-P7) ; non `NULL` = la valeur est tirée d'une source paramétrable (P3) |

Le FormulaEngine expose `valeurSource(RUBRIQUE)` qui résout `SourceValeurId` puis
délègue au `SourceValeurCalculator` enregistré. Pour PAPP, `SourceValeurId =
'NOTATION_AGENT'` ; le moteur résout le taux via le calculateur de notation
(l'implémentation concrète est un adapter de `DALNotation`).

### 8ter.3. `CriteresEligibilite` (catalogue technique — R2 + R3 + R4 révisé)

Dictionnaire des critères d'éligibilité. **Remplace** (R3) le `CHECK IN (...)` en dur
de `ReglesEligibilite.Critere` — un nouveau critère = une ligne dans cette table,
pas une migration. **Nature : catalogue technique — audit minimal.**

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK (R2) | ex. `FILIERE`, `CORPS`, `GRADE`, `CATEGORIE`, `FONCTION`, `TYPE_CONTRAT`, `ECHELON`, `ANCIENNETE`, `TYPE_ETABLISSEMENT`, `ORIGINE_STATUTAIRE` (D3) |
| `Libelle` | TEXT | NOT NULL | |
| `TypeValeur` | TEXT | NOT NULL `CHECK IN ('TEXT','INT','DATE','ENUM')` | Sémantique de la valeur |
| `SourceResolution` | TEXT | NOT NULL `CHECK IN ('ATTRIBUT_AGENT','ATTRIBUT_GRADE','CARRIERE','CALCULE')` | Comment l'évaluateur résout la valeur (cf. J3H D3) |
| `Actif` | INTEGER | NOT NULL `CHECK IN (0,1)` DEFAULT 1 | |
| `CreatedAt` | TEXT | NOT NULL | |
| `CreatedBy` | TEXT | NOT NULL | |

### 8ter.4. `MessagesRegles` (texte réglementaire — R2 + R4 révisé)

Messages paramétrables (multilingues) pour les règles d'éligibilité et les
avertissements. Sépare la **logique** (groupe, sévérité) de la **présentation**
(message affiché à l'utilisateur). **Nature : texte réglementaire — audit complet
préservé** (R4 révisé, validation utilisateur 15/07/2026).

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK (R2) | ex. `MSG-ISSRP-45-INCONNU-ORIGINE` |
| `Categorie` | TEXT | NOT NULL `CHECK IN ('ELIGIBILITE','AVERTISSEMENT','SUGGESTION')` | |
| `TexteFr` | TEXT | NOT NULL | Message en français (V1) |
| `TexteAr` | TEXT | | Message en arabe (post-V1) |
| `Source` | TEXT | NOT NULL | Référence réglementaire (décret/arrêté) — R4 révisé |
| `DateEffet` | TEXT | NOT NULL | R4 révisé — versioning du wording |
| `DateFin` | TEXT | | R4 révisé |
| `Actif` | INTEGER | NOT NULL `CHECK IN (0,1)` DEFAULT 1 | |
| `CreatedAt` | TEXT | NOT NULL | |
| `CreatedBy` | TEXT | NOT NULL | |

### 8ter.5. `GroupesEligibilite` (règle réglementaire — D5)

D5 — abandon de la limite V1 « pas de conditions composées ». DNF : conditions d'un
groupe ETées, groupes OUés. Repris de J3H §2. **Nature : règle réglementaire —
audit complet.**

| Colonne | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `Id` | TEXT | PK | ex. `GE-ISSRP45-ORIGINE` |
| `RubriqueId` | TEXT | FK → `Rubriques(Id)` | |
| `Severite` | TEXT | NOT NULL DEFAULT `'INFO'` `CHECK IN ('INFO','RECOMMANDEE','OBLIGATOIRE_REGLEMENTAIRE')` | D2 |
| `MessageId` | TEXT | FK → `MessagesRegles(Id)` (R2) | |
| `Priorite` | INTEGER | NOT NULL DEFAULT 100 | Ordre d'affichage des suggestions (≠ ordre d'application) |
| `DateEffet` | TEXT | NOT NULL | |
| `DateFin` | TEXT | | |
| `Source` | TEXT | | |
| `Hash` | TEXT | NOT NULL | |
| `CreatedAt` | TEXT | NOT NULL | |
| `CreatedBy` | TEXT | NOT NULL | |

### 8ter.6. `ReglesEligibilite` — amendements (R3 + L-M2)

`ReglesEligibilite` est amendée en V009 par :

- **R3** : suppression du `CHECK IN (...)` sur `Critere` ; nouvelle colonne `CritereId`
  (FK vers `CriteresEligibilite.Id`).
- **L-M2** : nouvelle colonne `GroupeId` (FK vers `GroupesEligibilite.Id`, NULL = condition commune).

| Colonne ajoutée | Type | Contrainte | Description |
|---------|------|-----------|-------------|
| `CritereId` | TEXT | FK → `CriteresEligibilite(Id)` | **Remplace** `Critere` (TEXT avec CHECK) — source unique de vérité (R3) |
| `GroupeId` | TEXT | FK → `GroupesEligibilite(Id)` | `NULL` = condition **commune** à la rubrique (ET plat V008 inchangé) ; non `NULL` = condition **membre** du groupe (ET dans le groupe, OU entre groupes) |

**Migration de données (R3)** : pour chaque règle V008 existante, on résout le code
métier du critère (`FILIERE`, `CORPS`, `GRADE`, …) et on remplit `CritereId` par
jointure sur `CriteresEligibilite.Id`. La colonne `Critere` (TEXT) est **supprimée**
— la sémantique est portée par `CritereId` + le dictionnaire. `GroupeId` est laissé
`NULL` pour toutes les règles V008 (les conditions communes restent communes ; les
groupes sont un concept V009+).

### 8ter.7. `RubriqueBaremes` — colonnes d'audit (L-M3)

Ajoutées en V009 (alignement `AuditLog` V001) :

| Colonne | Type | Description |
|---------|------|-------------|
| `CreatedBy` | TEXT | Acteur de la création (déjà en V008) |
| `UpdatedAt` | TEXT | ISO 8601 UTC, NULL si jamais modifiée |
| `UpdatedBy` | TEXT | Acteur de la dernière modification |

**Trigger applicatif** : toute modification d'une ligne de `RubriqueBaremes` écrit
dans `AuditLog` (qui, quand, valeur avant/après, source). C'est le **Pattern Editor**
du Workbench (§5.2 J3I) qui en est l'auteur.

### 8ter.8. Tables NON créées en V009 (R1 — reportées à Phase 5)

Conformément à R1 (validé utilisateur, J3J § 6) : ces tables de gestion dépendent
directement du modèle `Agents` et ne sont **pas créées en avance**. Leur conception
intentionnelle est documentée dans `J3J_REFACTORING_AVANT_V009.md` § 8.3-8.5 pour
préserver la cohérence avec les phases suivantes.

- ~~`AgentAttributs`~~ — design preview J3J § 8.3
- ~~`AgentRubriques`~~ — design preview J3J § 8.4
- ~~`AvertissementsHistorique`~~ — design preview J3J § 8.5

> **Cohérence J3I / J3H** : J3H § 1 dit déjà « les tables de gestion sont créées
> avec `Agents` en Phase 5 ». J3I § 9 v1.0 (initial) divergeait par erreur. La
> version refactorée **rétablit la cohérence** : J3H § 1 est la référence, J3I
> § 9 / DICTIONNAIRE § 8ter ne créent plus de tables vides en avance.

### 8ter.9. Test d'upgrade V008 → V009

Référencé par `PLAN_ACTION.md` Phase 3bis :

1. Appliquer V008 sur une base vide, seeder les barèmes IFC/DOC (P4/P12).
2. Appliquer V009 — vérification :
   - `SourcesValeur` (7 lignes seed, R2 = PK `Id`)
   - `CriteresEligibilite` (10 codes seed, R2 = PK `Id`)
   - `MessagesRegles` (vide, R2 = PK `Id`, audit complet R4 révisé)
   - `GroupesEligibilite` (vide)
   - `Rubriques.SourceValeurId` (NULL partout par défaut)
   - `ReglesEligibilite.CritereId` (rempli par jointure sur le seed `CriteresEligibilite`)
   - `ReglesEligibilite.GroupeId` (NULL partout — V008 préservé)
   - `ReglesEligibilite.Critere` (TEXT avec CHECK) **supprimé**
   - `RubriqueBaremes.UpdatedAt`/`UpdatedBy` (NULL partout)
3. Suite de tests existante (117 tests) reste verte.
4. Test DNF : créer un groupe `GE-TEST-A` avec 2 conditions (l'une ETée, l'autre
   ETée dans un 2e groupe), vérifier l'évaluation OU/ET conformément au pseudo-code
   J3H §2.
5. Test source de valeur : créer une rubrique bidon avec `SourceValeurId =
   'CONSTANTE_REGLEMENTAIRE'`, vérifier que `valeurSource(RUB)` retourne la valeur
   du `Calculateur` enregistré.
6. Test audit allégé (R4 révisé) : insérer une nouvelle source dans `SourcesValeur`
   (technique) et un nouveau message dans `MessagesRegles` (réglementaire) ; vérifier
   que la première a un audit minimal (CreatedAt/By) et le second un audit complet
   (Source, DateEffet/Fin).

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

### 9.8. Barème indexé (V008, J3E L1) — `bareme(RUBRIQUE, DIMENSION, clé, date)`
```sql
SELECT TypeValeur, Valeur FROM RubriqueBaremes
WHERE RubriqueId = $r AND Dimension = $dim AND BorneInf = $cle
  AND DateEffet <= $d AND (DateFin IS NULL OR DateFin >= $d)
ORDER BY DateEffet DESC LIMIT 1;
```

### 9.9. Éligibilité DNF (V009, J3H §2) — groupe satisfait si toutes ses conditions le sont
```sql
-- 1. Récupérer les groupes actifs de la rubrique
SELECT g.* FROM GroupesEligibilite g
WHERE g.RubriqueId = $r
  AND g.DateEffet <= $d AND (g.DateFin IS NULL OR g.DateFin >= $d);

-- 2. Pour chaque groupe, évaluer ses conditions (les `ReglesEligibilite` dont
--    `GroupeId = g.Id` doivent toutes être satisfaites). Si oui, le groupe est
--    satisfait. Si au moins un groupe est satisfait, la rubrique est éligible.
--    (Logique portée par le `RegleEligibiliteEvaluator` du domaine, ADR-0005.)
```

### 9.10. Source de valeur d'une rubrique (V009 refactoré, D6, R2)
```sql
SELECT sv.Id, sv.Libelle
FROM Rubriques r
JOIN SourcesValeur sv ON sv.Id = r.SourceValeurId
WHERE r.Id = $r;
-- Le calculateur typé est résolu côté DI (pas en SQL).
```

---

## 10. Reconstruction depuis zéro

1. **Créer une base vide** : `Data Source=paie.db` (fichier) — le `SqliteMigrator` crée le fichier.
2. **Appliquer les migrations** : `var migrator = new SqliteMigrator(options, MigrationLoader.LoadFromAssembly(...)); migrator.Apply();` — applique **V001 → V009** (V009 = Workbench réglementaire, ADR-0007).
3. **Seeder** : Phase 2 — lecture des fichiers `Reglementation/` et `Documentation de Référence du Projet/`, insertion idempotente. Un **rapport d'import** (lignes lues/insérées/rejetées) est produit. Seed complémentaire V009 : `SourcesValeur` (7 codes), `CriteresEligibilite` (≥10 codes).
4. **Vérifier** : `SELECT COUNT(*) FROM SchemaVersions` doit retourner **8** (V001-V008) après application complète des migrations jusqu'à V008, ou **9** si V009 est appliqué. La CI enforce la cohérence.
5. **Auditer** : la colonne `Hash` permet de rejouer le seed et de détecter les dérives (comparaison SHA-256). Pour les modifications en cours d'exploitation, `AuditLog` (V001) + colonnes `RubriqueBaremes.UpdatedAt`/`UpdatedBy` (V009) tracent l'auteur et la date.

---

## 11. Hors périmètre (rappel)

- **Calcul de paie** : la logique métier (assemblage des rubriques, calcul IRG détaillé, ...) est dans la couche `Application` / `Domain`, **pas** dans la base. Aucune procédure stockée, aucun trigger. La base stocke les règles, le moteur les interprète.
- **Bulletins** : pas dans cette phase (V010+, J5+).
- **Agents** : tables `AgentAttributs` / `AgentRubriques` / `AvertissementsHistorique` **non créées** en V009 (R1, J3J § 6 et 8.3-8.5) — leur création effective attend la Phase 5, avec `Agents` et le Workbench UI.
- **Historique des valeurs** : chaque changement = nouvelle version (ligne avec nouvelle `DateEffet`). `AuditLog` (V001) trace les actions ; les colonnes `UpdatedAt`/`UpdatedBy` ajoutées en V009 sur `RubriqueBaremes` complètent l'audit au niveau des barèmes.
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
1. Créer une nouvelle migration `V010__xxx.sql` (jamais modifier une migration déjà appliquée, y compris V009).
2. Compléter ce dictionnaire dans le même commit.
3. Tester l'upgrade depuis la version N-1 (le `SqliteMigrator` est conçu pour) — le test d'upgrade V008→V009 est documenté en §8ter.9 et sert de référence pour les futures migrations.
4. **Aucun hotfix en prod** : la mise à jour passe toujours par une migration versionnée.

**Règle d'or** : si une valeur est dans le code, c'est un bug.

**Règle d'or n°2 (post-V009, ADR-0007)** : toute nouvelle rubrique, tout nouveau paramètre, toute nouvelle règle d'éligibilité **passe par le Workbench** (UI Phase 6 + use cases Phase 5), pas par une migration. La migration est réservée aux **changements de structure** (nouvelles tables, nouvelles colonnes). Les valeurs vivent en base, saisies par l'utilisateur, historisées.
