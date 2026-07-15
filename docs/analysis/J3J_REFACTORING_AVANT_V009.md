# J3.j — Refactoring du modèle avant V009 (Workbench réglementaire)

> **Statut :** v1.0 — 15/07/2026 — Proposition de refactoring, à valider **avant** toute
> implémentation de V009.
> **Principe directeur (utilisateur, 15/07/2026) :** le projet est en développement, la base
> peut être modifiée librement, aucune rétrocompatibilité n'est requise, aucune donnée
> existante n'est définitive. Refactor = on argumente + on valide, puis on fait.
> **Référence projet :** `docs/analysis/J3I_WORKBENCH_REGLEMENTAIRE.md` (V009 proposé),
> `docs/DICTIONNAIRE_DONNEES.md` (V001→V008 + V009 § 8ter), `docs/PLAN_ACTION.md`
> Phase 3bis, `docs/adr/0007-workbench-reglementaire.md`.

> **Note de nommage :** l'ancien « J3J » (stratégie de migration des bulletins historiques,
> lié à Q11) est **différé** par décision utilisateur du 15/07/2026 — pas de système en
> production, donc rien à migrer. La lettre J3J est recyclée ici pour ce document de
> refactoring, qui prend la priorité.

---

## 1. Cadrage — ce qui ne change pas, ce qu'on revoit

### 1.1 Ce qui ne change pas (validé 14/07/2026, ADR-0007)

- **D5** — `GroupesEligibilite` (DNF), abandon de la limite V1 « ET plat »
- **D6** — `SourcesValeur` (catalogue extensible) + `Rubriques.SourceValeurId`
- **D7** — Arborescence d'écrans spécialisés pour le Workbench
- **D8** — Dry-run obligatoire avant commit
- **D9** — Rétroactif = génération de rappels, pas de modif des bulletins validés
- **D10** — Migration unique V009 (un seul upgrade V008 → V009)
- **D11** — Matrice de couverture en V1

Ces sept décisions **demeurent le contrat fonctionnel** de V009. Aucun refactor ne les
remet en cause ; on nettoie la **forme** du modèle, pas le **fond**.

### 1.2 Ce qu'on revoit

La **forme** du modèle V009 tel qu'esquissé dans J3I § 9 + DICTIONNAIRE § 8ter. Axes :

1. **YAGNI** — pas de tables spéculatives créées en avance
2. **Cohérence de nommage** — convention `Id` pour tous les référentiels (ADR-0004)
3. **Source unique de vérité** — pas de validation dupliquée
4. **Expressivité minimale** — chaque colonne porte une sémantique non redondante

---

## 2. Diagnostic du modèle V008 + V009 proposé

### 2.1 Conventions observées dans V001-V008 (rappels)

| Type de table | PK | Audit complet | Versionnée | Source | Hash |
|---|:---:|:---:|:---:|:---:|:---:|
| **Référentiel pur** (nomenclature : `Filieres`, `Corps`, `Grades`, `Categories`, `Etablissements`, `Echelons`, `TypesContrat`, `Fonctions`, `TypesPersonnel`) | `Id` (TEXT code métier) | non | non | non | non |
| **Référentiel réglementaire** (`ValeurPoint`, `GrilleIndiciaire`, `IndicesEchelon`, `Rubriques` (statique), `BaremeIRG`) | `Id` | CreatedAt/By | DateEffet/Fin, Version | oui | oui |
| **Données versionnées** (`RubriqueFormules`, `RubriqueParametres`, `Cotisations`, `CotisationAssietteRubriques`, `IRGReglesPeriode`) | `Id` | CreatedAt/By | DateEffet/Fin | oui | oui |
| **Tables de gestion** (Phase 5 : `Agents`, bulletins) | GUID | oui | selon le cas | non | non |

### 2.2 V009 proposé (J3I § 9 + DICTIONNAIRE § 8ter) — grille de lecture

| Table | Type | PK proposé | Audit | DateEffet/Fin | Source | Hash | Actif |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `SourcesValeur` | Catalogue de code | **`Code`** (TEXT) | CreatedAt | — | — | — | oui |
| `CriteresEligibilite` | Catalogue de code | **`Code`** (TEXT) | CreatedAt | — | — | — | oui |
| `MessagesRegles` | Catalogue de code | **`Code`** (TEXT) | CreatedAt | — | — | — | oui |
| `GroupesEligibilite` | En-tête de règle | **`Id`** (TEXT) | CreatedAt | DateEffet/Fin | oui | oui | — |
| `ReglesEligibilite` (amendée) | Condition | `Id` (TEXT) | CreatedAt | DateEffet/Fin | oui | oui | — |
| `RubriqueBaremes` (amendée) | Versionnée | `Id` (TEXT) | CreatedAt/By, **UpdatedAt/By** | DateEffet/Fin | oui | oui | — |
| `AgentAttributs` | Gestion (vide) | **GUID** | CreatedAt/By | DateEffet/Fin | non | non | — |
| `AgentRubriques` | Gestion (vide) | **GUID** | CreatedAt/By | DateEffet/Fin | non | non | — |
| `AvertissementsHistorique` | Gestion append-only (vide) | **GUID** | CreatedAt | non | non | non | — |

### 2.3 Symptômes à corriger

1. **Hétérogénéité des PK** sur les catalogues de codes (`Code` vs `Id`) — incohérent avec V002.
2. **Tables de gestion créées vides** en V009 alors qu'aucun `Agents` n'existe encore — design spéculatif.
3. **Double source de vérité** sur les critères d'éligibilité : `CHECK IN (...)` sur `ReglesEligibilite.Critere` **et** dictionnaire `CriteresEligibilite`. L'ajout d'un nouveau critère oblige à toucher les deux.
4. **Audit excessif** sur les catalogues de code (`Source`, `Hash` inutiles car la valeur n'est pas réglementaire).
5. **Sémantique « valeur » ambigüe** — `RubriqueParametres.Valeur` (TEXT) et `RubriqueBaremes.Valeur` (TEXT) sont deux implémentations du même concept « valeur typée versionnée ». Différence justifiée (param scalaire vs param indexé) mais à expliciter.
6. **FK column naming** : `Rubriques.SourceValeurCode` est sémantiquement correct (Code est la PK) mais incohérent avec le reste du modèle où les FK utilisent `<Table>Id`.

---

## 3. Refactors proposés (4 majeurs + 1 mineur)

### 3.1 Refactor R1 — **Reporter les tables de gestion agent à V010+ (YAGNI)**

**Constat.** `AgentAttributs`, `AgentRubriques`, `AvertissementsHistorique` (J3I § 9) sont
créées **vides** en V009, en attendant l'arrivée de la table `Agents` (Phase 5). Aucun cas
d'usage, aucun écran, aucune donnée ne les alimente avant la Phase 5.

**Refactor.** **Ne pas créer ces tables en V009**. Elles seront conçues **avec** `Agents`
lorsque la couche gestion sera abordée, au moment où on connaîtra les vrais invariants
(combien d'attributs par agent, cardinalité AgentRubriques, etc.).

**Justification.**
- YAGNI strict : on n'écrit pas ce dont on n'a pas l'usage immédiat.
- Risque de sur-conception : créer `AgentAttributs` aujourd'hui nous force à deviner
  la cardinalité (combien d'attributs par agent ? un attribut par agent ou un attribut
  par (agent, code) ? quelle granularité de `DateEffet` ?). Si on se trompe, on
  migre dans 6 mois.
- Avec le contexte « dev libre, pas de prod », il n'y a **aucun coût** à attendre.

**Impact.**
- V009 — 3 tables en moins
- Le domaine (Phase 3) ne référence plus ces entités tant qu'elles n'existent pas
- J3H § 1 « table de gestion AgentAttributs / AgentRubriques / AvertissementsHistorique
  créées avec `Agents` en Phase 5 » devient la **référence unique** (J3H §1 dit
  d'ailleurs déjà cela — l'écart avec J3I § 9 est une erreur de J3I à corriger)

**Risque.** Aucun (pas de prod, pas de données).

**Effort.** ~0 (suppression de 3 CREATE TABLE dans le script V009).

---

### 3.2 Refactor R2 — **Harmoniser les PK : tous les référentiels en `Id` (ADR-0004)**

**Constat.** V009 propose `SourcesValeur.Code`, `CriteresEligibilite.Code`, `MessagesRegles.Code`
comme PK. V002 utilise `Filieres.Id`, `Corps.Id`, `Grades.Id`, etc. ADR-0004 dit : « tables
de *référentiel* → PK = code métier TEXT ». Le nom de la colonne devrait être `Id`
par cohérence.

**Refactor.** Renommer `Code` → `Id` partout (PK). Renommer les FK en conséquence :
- `SourcesValeur.Code` → `SourcesValeur.Id`
- `CriteresEligibilite.Code` → `CriteresEligibilite.Id`
- `MessagesRegles.Code` → `MessagesRegles.Id`
- FK `Rubriques.SourceValeurCode` → `Rubriques.SourceValeurId`
- FK `GroupesEligibilite.MessageCode` → `GroupesEligibilite.MessageId`
- FK `ReglesEligibilite.Critere` (TEXT) → `ReglesEligibilite.CritereId` (FK)

**Justification.**
- Cohérence avec V002 + ADR-0004
- Convention « une table = une PK = `Id` » devient universelle
- Code plus lisible (pas de `sv.Code`, juste `sv.Id`)
- Anti-confusion : un catalogue n'est pas « un code », c'est « un référentiel dont
  l'identifiant *est* un code métier »

**Impact.**
- Toutes les colonnes FK qui pointent vers ces tables changent de nom
- 3 CHECK contraintes sur `ReglesEligibilite` (et autres) disparaissent (cf. R3)
- Code de seed : remplacer `sv.Code` par `sv.Id` partout (3 fichiers, ~30 lignes)

**Risque.** Faible (dev, pas de prod). Effort modéré (renommage en série).

---

### 3.3 Refactor R3 — **Source unique de vérité sur les critères d'éligibilité**

**Constat.** `ReglesEligibilite.Critere` est `TEXT NOT NULL CHECK IN ('FILIERE','CORPS',
'GRADE',...)`. Le dictionnaire `CriteresEligibilite` (proposé en V009) **recense les
mêmes valeurs** + leur `SourceResolution` (ATTRIBUT_AGENT, ATTRIBUT_GRADE, etc.). Pour
ajouter un nouveau critère (ex. `TYPE_ETABLISSEMENT`, déjà listé dans J3E), il faut
toucher le CHECK SQL **et** insérer dans le dictionnaire — **deux endroits**,
désynchronisation possible.

**Refactor.**
- Supprimer le `CHECK IN (...)` de `ReglesEligibilite.Critere`
- Transformer `Critere` (TEXT) en `CritereId` (TEXT, FK → `CriteresEligibilite.Id`)
- Le dictionnaire devient l'**unique** source de validité : on insère un critère dans
  `CriteresEligibilite`, le FK enforce sa présence, l'évaluateur utilise `SourceResolution`
  pour savoir comment résoudre la valeur côté agent

**Justification.**
- Une seule source de vérité (cohérence J3I / Workbench)
- Pas de migration SQL pour ajouter un critère (DDL = `INSERT INTO CriteresEligibilite`)
- L'`ExplainabilityEngine` dispose de la `Libelle` + `SourceResolution` du critère
  pour ses justifications (« critère CORPS, source = corps de l'agent »)

**Impact.**
- `ReglesEligibilite.CritereId` au lieu de `Critere` (TEXT)
- CHECK supprimé
- Le type de valeur (TEXT/INT/DATE/ENUM) est porté par `CriteresEligibilite.TypeValeur`,
  l'évaluateur le lit pour parser `Valeur` correctement

**Risque.** Faible (dev). Compatible avec tous les cas J3B/J3G recensés.

---

### 3.4 Refactor R4 — **Audit au plus juste : distinguer audit technique, traçabilité réglementaire, gestion métier** (RÉVISÉ)

**Constat initial.** `SourcesValeur`, `CriteresEligibilite`, `MessagesRegles` ont
toutes trois été proposées avec les mêmes colonnes d'audit que les données
réglementaires (`DateEffet`/`DateFin`/`Version`/`Source`/`Hash`). Or toutes trois ne
sont **pas** de même nature — la révision R4 applique le discernement demandé.

**Classification par nature (RÉVISÉE).**

| Table | Nature | Audit requis | Justification |
|---|---|---|---|
| `SourcesValeur` | **Catalogue technique** (vocabulaire de mécanismes de calcul) | **Minimal** : `Id`, `Libelle`, `Description`, `Actif`, `CreatedAt`, `CreatedBy` | Une source de valeur (`NOTATION_AGENT`, `ANCIENNETE_PUBLIQUE`, …) est un mécanisme technique, pas une valeur réglementée. Pas de `DateEffet`/`DateFin`/`Source`/`Hash`. |
| `CriteresEligibilite` | **Catalogue technique** (vocabulaire d'expression des règles) | **Minimal** : `Id`, `Libelle`, `TypeValeur`, `SourceResolution`, `Actif`, `CreatedAt`, `CreatedBy` | Un critère (`CORPS`, `GRADE`, `CATEGORIE`, …) est un mot du vocabulaire. La règle **qui l'utilise** est réglementaire ; le critère lui-même ne l'est pas. |
| `MessagesRegles` | **Texte réglementaire** (libellés utilisateurs paraphrasant ou citant des décrets) | **Complet** : `Id`, `Categorie`, `TexteFr`, `TexteAr` (nullable), `Source` (référence du décret source), `DateEffet`, `DateFin` (versioning), `Actif`, `CreatedAt`, `CreatedBy` | Le wording d'un message peut être dicté par la réglementation (« en exercice effectif » est une expression verbatim du Décret 15-271). Si la formulation change, l'ancienne version reste consultable. `Source` obligatoire à la création (décret/arrêté). |

**Matrice de décision « cette donnée est-elle réglementaire ? » (RÉFÉRENCE PROJET) :**

```
Une donnée est « réglementaire » (donc audit complet DateEffet/DateFin/Source/Hash)
si et seulement si au moins l'un de ces critères est vrai :
  1. Sa valeur (ou sa liste de valeurs) est définie par un texte réglementaire
  2. Sa valeur change au gré des décrets/arrêtés
  3. Sa traçabilité est exigée pour l'audit comptable ou la preuve réglementaire

Sinon, c'est un catalogue de code (audit minimal : Actif + CreatedAt/By).
```

**Application à V009 :**
- `SourcesValeur` → **technique** → audit minimal
- `CriteresEligibilite` → **technique** → audit minimal
- `MessagesRegles` → **réglementaire** (texte) → audit complet
- `GroupesEligibilite` → **réglementaire** (règle) → audit complet (inchangé)
- `ReglesEligibilite` (amendée) → **réglementaire** (règle) → audit complet (inchangé)
- `RubriqueBaremes` (amendée) → **réglementaire** (valeur) → audit complet + `UpdatedAt`/`By` pour le Workbench (D7/D8)

**Justification (RÉVISÉE).**
- La nuance technique/réglementaire est ce qui justifie des audits différents —
  appliquer un audit identique partout est soit trop laxiste (manque de
  traçabilité sur des données réglementaires) soit trop lourd (colonnes
  inutilisées sur des catalogues techniques).
- L'audit complet sur `MessagesRegles` est **conservateur** : on ne peut pas
  exclure qu'un message doive un jour citer verbatim un décret. Mieux vaut
  garder la possibilité que découvrir le besoin en Phase 8.
- R5 (YAGNI) ne contredit pas R4 : R4 ne supprime pas l'audit des données
  réglementaires, il allège seulement les catalogues techniques. C'est exactement
  la lecture R5-compatible de la demande utilisateur.

**Impact.**
- Schéma V009 allégé pour `SourcesValeur` et `CriteresEligibilite` (5 colonnes en
  moins chacune par rapport au V009 initial)
- `MessagesRegles` garde son audit complet (identique à V009 initial)
- 10 colonnes en moins au total (au lieu de 15 initialement proposés)
- Le seed catalogue les codes techniques en un INSERT par code, versionne les
  messages comme des valeurs réglementaires

**Risque.** Aucun. La traçabilité réglementaire est **préservée** sur toutes les
données qui en ont besoin.

---

### 3.5 Refactor R5 (mineur) — **Nettoyer les amorces V007/V008 inutilisées**

**Constat.** Au cours des itérations J3A→J3I, certaines colonnes ou concepts ont
été évoqués puis abandonnés ou laissés en jachère :
- `BonificationsIndiciaires` (J3E § 6, Q-07 = non) — table proposée, jamais créée
- `Rubriques.RubriqueActif` (évoqué) — actuellement géré via `Actif`
- `Cotisations` pourrait bénéficier d'une clarification `EstRetenue` vs `Nature`
  (doublon sémantique)

**Refactor.** Ne **rien créer** qui n'a pas un cas d'usage immédiat. Si `Q-07` est
non, `BonificationsIndiciaires` n'existe pas. Si un concept a un usage partiel,
il est créé quand le cas d'usage émerge. La règle YAGNI s'applique déjà.

**Justification.** Discipline de conception : on ne pose pas de briques « au cas où ».

**Impact.** Aucun (déjà aligné). Le refactor est une **règle**, pas une action.

**Risque.** Aucun. Effort : zéro.

---

## 4. Schéma V009 final — après refactors

### 4.1 Tables nouvelles

| Table | Nature | PK | Colonnes principales | Audit | Versionnée |
|---|:---:|:---:|---|:---:|:---:|
| **`SourcesValeur`** | **Catalogue technique** | `Id` (TEXT code) | `Id`, `Libelle`, `Description`, `Actif` | CreatedAt/By | non |
| **`CriteresEligibilite`** | **Catalogue technique** | `Id` (TEXT code) | `Id`, `Libelle`, `TypeValeur` (TEXT/INT/DATE/ENUM), `SourceResolution` (ATTRIBUT_AGENT/ATTRIBUT_GRADE/CARRIERE/CALCULE), `Actif` | CreatedAt/By | non |
| **`MessagesRegles`** | **Texte réglementaire** | `Id` (TEXT code) | `Id`, `Categorie` (ELIGIBILITE/AVERTISSEMENT/SUGGESTION), `TexteFr`, `TexteAr` (nullable), `Source`, `DateEffet`, `DateFin`, `Actif` | CreatedAt/By | oui |
| **`GroupesEligibilite`** | **Règle réglementaire** | `Id` (TEXT code) | `Id`, `RubriqueId`, `Severite`, `MessageId`, `Priorite`, `DateEffet`, `DateFin`, `Source`, `Hash` | CreatedAt/By | oui |

### 4.2 Tables amendées

| Table | Amendement |
|---|---|
| `Rubriques` | + `SourceValeurId` (TEXT, FK → `SourcesValeur.Id`, NULL par défaut — P3 uniquement) |
| `ReglesEligibilite` | - `Critere` (TEXT) avec CHECK → + `CritereId` (TEXT, FK → `CriteresEligibilite.Id`)<br>+ `GroupeId` (TEXT, FK → `GroupesEligibilite.Id`, NULL = condition commune) |
| `RubriqueBaremes` | + `UpdatedAt`, `UpdatedBy` (audit barème) |

### 4.3 Tables NON créées en V009 (différées à Phase 5)

- ~~`AgentAttributs`~~ — créée avec `Agents` (Phase 5) — design preview § 8.3
- ~~`AgentRubriques`~~ — idem — design preview § 8.4
- ~~`AvertissementsHistorique`~~ — idem — design preview § 8.5

### 4.4 Comparaison avant/après

| Métrique | V009 initial (J3I) | V009 refactoré (R1-R5) | Gain |
|---|:---:|:---:|:---:|
| Tables nouvelles | 5 (+ 2 amendées) | 4 (+ 3 amendées) | -1 table |
| Tables de gestion vides créées en avance | 3 | 0 | -3 tables |
| Colonnes d'audit sur catalogues techniques | 10 (Sources + Criteres) | 0 (allégés) | **-10** |
| Colonnes d'audit sur `MessagesRegles` (réglementaire) | 0 (omis par erreur) | 5 (complet) | **+5** (récupéré) |
| CHECK + FK redondants | 1 (Critere) | 0 | -1 CHECK |
| Hétérogénéité de PK | `Code` et `Id` mélangés | `Id` partout | unifiée |
| Sémantique d'audit | Catalogue ≠ réglementaire indistincts | technique minimal / réglementaire complet | **clarifiée** |

---

## 5. Critères d'acceptation des refactors

- **CA-R1** — R1-R5 appliqués sans régression des 117 tests existants
- **CA-R2** — La table `SourcesValeur` est requêtable par `Id` (pas par `Code`)
- **CA-R3** — `ReglesEligibilite` n'a plus de `CHECK IN (...)` sur le critère ; l'insertion
  d'un nouveau critère passe uniquement par `CriteresEligibilite`
- **CA-R4** — Aucun fichier `.sql` ni `.cs` ne référence plus `SourcesValeur.Code`,
  `CriteresEligibilite.Code`, `MessagesRegles.Code`, ou `ReglesEligibilite.Critere`
- **CA-R5** — DICTIONNAIRE_DONNEES.md et J3I § 9 sont mis à jour pour refléter le schéma
  refactoré ; J3H § 1 (qui dit déjà « créées avec Agents en Phase 5 ») reste la référence
  unique — J3I § 9 n'a plus de contradiction avec J3H
- **CA-R6** — `MessagesRegles` conserve l'audit complet (`Source`, `DateEffet`/`DateFin`,
  `Hash`) — distinction explicite entre catalogue technique (audit minimal) et texte
  réglementaire (audit complet)
- **CA-R7** — `AgentAttributs`, `AgentRubriques`, `AvertissementsHistorique` : **non
  créées en V009**, **design preview documenté en J3J § 8.3-8.5** pour la Phase 5
- **CA-R8** — Convention ADR-0004 (`Id` partout) appliquée **homogènement** : aucune
  table ne conserve un PK nommé autrement que `Id` (vérification automatique par grep
  dans le DDL, hors `Cotisations.Code` V005 qui est un point séparé à arbitrer en
  J3L — voir § 8.6)

---

## 6. Décisions validées (utilisateur, 15/07/2026)

| Réf | Décision | Lecture validée |
|-----|----------|-----------------|
| **R1** | ✅ **VALIDÉ** — Reporter `AgentAttributs` / `AgentRubriques` / `AvertissementsHistorique` à la Phase 5. Design preview documenté en § 8.3-8.5 pour préserver la cohérence avec les phases suivantes. |
| **R2** | ✅ **VALIDÉ** — Harmonisation `Id` partout. **Application homogène sur l'ensemble du modèle** (pas seulement V009) — vérification grep dans le DDL V001-V008. Point V005 `Cotisations.Code` à arbitrer en J3L. |
| **R3** | ✅ **VALIDÉ** — Suppression du `CHECK IN (...)` sur `ReglesEligibilite.Critere` → FK `CritereId` vers `CriteresEligibilite`. Toute évolution des critères passe par les **données**, jamais par le schéma ou le code. |
| **R4** | ✅ **VALIDÉ AVEC RÉSERVE** — Allègement de l'audit, **avec discernement** : distinguer audit technique / traçabilité réglementaire / gestion métier. Les catalogues purement techniques s'allègent ; les données réglementaires conservent leur audit. **`MessagesRegles` garde l'audit complet** (texte réglementaire). |
| **R5** | ✅ **VALIDÉ** — YAGNI strict, **interprété comme** : aucune abstraction sans besoin fonctionnel identifié, **mais jamais au sacrifice de l'extensibilité** quand un besoin futur est déjà dans la roadmap officielle. **Principe directeur** : *simplicité lorsqu'elle est compatible avec une architecture durable.* |
| **D-compl.** | ✅ **VALIDÉE** — Directive complémentaire à respecter sur tout refactor : aucune logique métier codée en dur, architecture pilotée par les données, cohérence globale du modèle, extensibilité sans recompilation, **lisibilité du modèle avant optimisation**. *« Je préfère parfois une table supplémentaire clairement justifiée qu'une architecture trop compacte mais difficile à comprendre ou à faire évoluer. »* |
| **V009-bis** | 🟡 **À ARBITRER** — Garder **V009** (numérotation monotone) ou renommer (V008-V2, V009-rev) ? Recommandation : V009 + note `rev. 1` dans `SchemaVersions.Name`. |

---

## 7. Plan d'implémentation post-validation

Une fois R1-R5 validés :

1. **MAJ J3I § 9 + DICTIONNAIRE § 8ter** — refléter le schéma refactoré (10 min, fait avant tout code)
2. **Écriture du `V009__workbench_reglementaire.sql`** — script idempotent, test d'upgrade V008 → V009 documenté dans § 8ter.9 du DICTIONNAIRE
3. **Domain (Domain)** — Value Objects `BaremeValue`, `SourceValeur`, `GroupeEligibilite`, services `BaremeResolver`, `SourceValeurResolver`, `RegleEligibiliteEvaluator` (DNF)
4. **Application** — use case `SimulerEvolutionReglementaire` (D8) + port `ISourceValeurCalculator` (Open/Closed)
5. **Tests** — ~30 nouveaux tests (CA-R1 à CA-R4), suite 117 existante verte
6. **Validation jalon J3bis** — point d'arrêt STOP & ASK

---

## 8. Annexes

### 8.1 Index croisé — quels fichiers sont impactés par R1-R5 ?

| Fichier | R1 | R2 | R3 | R4 | R5 |
|---|:---:|:---:|:---:|:---:|:---:|
| `docs/analysis/J3I_WORKBENCH_REGLEMENTAIRE.md` | ✓ (§ 9) | ✓ (§ 9) | ✓ (§ 9) | ✓ (§ 9) | ✓ |
| `docs/DICTIONNAIRE_DONNEES.md` | ✓ (§ 8ter) | ✓ (§ 8ter) | ✓ (§ 8ter) | ✓ (§ 8ter) | — |
| `docs/analysis/J3H_MODELE_AFFECTATION.md` | ✓ (§ 1 — déjà correct) | — | — | — | — |
| `docs/PLAN_ACTION.md` | ✓ (Phase 3bis § 1 — retirer les amorces) | — | — | — | — |
| `docs/adr/0007-workbench-reglementaire.md` | ✓ (§ Décision 7) | — | — | — | — |
| `src/.../Persistence/Migrations/V009__*.sql` (à créer) | ✓ (3 tables en moins) | ✓ (PK renommées) | ✓ (CHECK supprimé, FK) | ✓ (colonnes en moins) | ✓ |
| `src/.../Domain/**/SourcesValeur.cs` (à créer) | — | ✓ (`Id` au lieu de `Code`) | — | ✓ (allégé) | — |
| `src/.../Domain/**/CriteresEligibilite.cs` (à créer) | — | ✓ | — | ✓ | — |
| `src/.../Domain/**/MessagesRegles.cs` (à créer) | — | ✓ | — | ✓ | — |
| `src/.../Domain/**/ReglesEligibilite.cs` (à créer) | — | ✓ (`CritereId` au lieu de `Critere`) | ✓ | — | — |
| `tools/PaieEducation.Tools/seed/...` (à adapter) | — | ✓ (renommer `Code` → `Id`) | ✓ (insérer dans `CriteresEligibilite`) | ✓ (audit allégé) | — |
| `tests/.../V009UpgradeTests.cs` (à créer) | ✓ | ✓ | ✓ | ✓ | — |

### 8.2 Glossaire des décisions de refactoring

- **YAGNI** : "You Aren't Gonna Need It" — ne pas construire ce qui n'a pas d'usage immédiat
- **DNF** : Forme Normale Disjonctive — ET dans le groupe, OU entre groupes (D5)
- **PK = Id** : convention ADR-0004 — les référentiels utilisent une PK TEXT nommée `Id`
- **Catalogue de code** : table de référence listant des concepts (pas des valeurs réglementaires)
- **Audit réglementaire** : colonnes `DateEffet`/`DateFin`/`Version`/`Source`/`Hash` —
  n'a de sens que sur les **valeurs** sujettes à évolution, pas sur les **codes** conceptuels

### 8.3 Design preview — `AgentAttributs` (à créer en Phase 5)

> Conformément à R1 (validé par l'utilisateur) : la table **n'est pas créée** en V009.
> Sa conception est documentée ici pour préserver la cohérence avec les phases suivantes.

Table de gestion (PK GUID, versionnée `DateEffet`/`DateFin`) qui porte les attributs
d'agent non dérivables de la carrière (D3) :

```sql
CREATE TABLE AgentAttributs (
    Id              TEXT NOT NULL PRIMARY KEY,   -- GUID
    AgentId         TEXT NOT NULL REFERENCES Agents(Id),  -- créé en Phase 5
    Code            TEXT NOT NULL,                -- 'ORIGINE_STATUTAIRE', 'EXERCICE_EFFECTIF',
                                                  --   'ANCIENNETE_PRIVEE_ANNEES', 'PROFIL_IRG'
    ValeurTexte     TEXT,                         -- un seul des 3 types rempli selon CriteresEligibilite
    ValeurNumerique REAL,
    ValeurDate      TEXT,                         -- ISO 8601
    DateEffet       TEXT NOT NULL,
    DateFin         TEXT,
    Source          TEXT,                         -- référence réglementaire si applicable
    CreatedAt       TEXT NOT NULL,
    CreatedBy       TEXT NOT NULL,
    UpdatedAt       TEXT,
    UpdatedBy       TEXT
);
CREATE INDEX IX_AgentAttributs_AgentId ON AgentAttributs(AgentId);
CREATE UNIQUE INDEX IX_AgentAttributs_Agent_Code_DateEffet
    ON AgentAttributs(AgentId, Code, DateEffet);
```

**Codes d'attribut Phase 5 (D3) :**
- `ORIGINE_STATUTAIRE` — ENSEIGNANT / AUTRE / INCONNU
- `EXERCICE_EFFECTIF` — 0/1
- `ANCIENNETE_PRIVEE_ANNEES` — entier
- `PROFIL_IRG` — STANDARD / HANDICAPE / RETRAITE_RG

### 8.4 Design preview — `AgentRubriques` (à créer en Phase 5)

> Conformément à R1 : table **non créée** en V009. Conception documentée pour Phase 5.

Table de gestion (PK GUID) qui porte les affectations par agent — suggestions,
acceptations, suppressions, suspensions, occurrences.

```sql
CREATE TABLE AgentRubriques (
    Id              TEXT NOT NULL PRIMARY KEY,   -- GUID
    AgentId         TEXT NOT NULL REFERENCES Agents(Id),
    RubriqueId      TEXT NOT NULL REFERENCES Rubriques(Id),
    Statut          TEXT NOT NULL CHECK (Statut IN
                      ('SUGGEREE', 'ACCEPTEE', 'SUPPRIMEE', 'SUSPENDUE')),
    Origine         TEXT NOT NULL,                -- 'REGLE:<regle_id>@<version>'
                                                  -- ou 'MANUELLE'
    OrigineVersion  TEXT,                         -- version de la règle source
    DateEffet       TEXT NOT NULL,
    DateFin         TEXT,
    Occurrence      INTEGER NOT NULL DEFAULT 1,   -- pour D4 multi-occurrences
    Parametres      TEXT,                         -- JSON overrides (montant, coeff, etc.)
    CreatedAt       TEXT NOT NULL,
    CreatedBy       TEXT NOT NULL,
    UpdatedAt       TEXT,
    UpdatedBy       TEXT
);
CREATE INDEX IX_AgentRubriques_AgentId ON AgentRubriques(AgentId);
CREATE INDEX IX_AgentRubriques_RubriqueId ON AgentRubriques(RubriqueId);
```

**Règles d'invariant Phase 5 :**
- Une seule ligne active (`DateFin IS NULL`) par (AgentId, RubriqueId, Occurrence)
- Si la règle source est mise à jour, l'affectation est marquée « à vérifier »
  (`AvertissementHistorique`), jamais supprimée d'office
- Les `Parametres` JSON sont validés au commit (montant > 0, etc.)

### 8.5 Design preview — `AvertissementsHistorique` (à créer en Phase 5)

> Conformément à R1 : table **non créée** en V009. Conception documentée pour Phase 5.

Table append-only (PK GUID) qui trace tous les avertissements affichés à l'utilisateur
et leurs décisions.

```sql
CREATE TABLE AvertissementsHistorique (
    Id              TEXT NOT NULL PRIMARY KEY,   -- GUID
    OccurredAt      TEXT NOT NULL,                -- ISO 8601 UTC
    AgentId         TEXT REFERENCES Agents(Id),   -- nullable pour avertissements globaux
    RubriqueId      TEXT REFERENCES Rubriques(Id),
    RegleId         TEXT,                         -- nullable
    RegleVersion    TEXT,                         -- version de la règle au moment de l'affichage
    Severite        TEXT NOT NULL CHECK (Severite IN
                      ('INFO', 'RECOMMANDEE', 'OBLIGATOIRE_REGLEMENTAIRE')),
    MessageId       TEXT NOT NULL REFERENCES MessagesRegles(Id),
    Decision        TEXT CHECK (Decision IN        -- nullable tant que non décidé
                      ('ACCEPTE', 'IGNORE', 'SUPPRIME')),
    DecisionAt      TEXT,                         -- ISO 8601 UTC, nullable
    CreatedAt       TEXT NOT NULL
);
CREATE INDEX IX_AvertissementsHistorique_OccurredAt ON AvertissementsHistorique(OccurredAt);
CREATE INDEX IX_AvertissementsHistorique_AgentId ON AvertissementsHistorique(AgentId);
```

**Invariant** : append-only strict. Aucune mise à jour, aucune suppression (sauf
procédure de purge RGPD après expiration du délai légal).

---

*Dernière mise à jour : 15/07/2026 — v1.0 — soumis à validation (STOP & ASK, §6).*
