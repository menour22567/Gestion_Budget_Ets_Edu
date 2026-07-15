# ISSRP — Indemnité de Soutien Scolaire et Remédiation Pédagogique

**Statut : CLÔTURÉ — CONFORME — VERROUILLÉ CONTRE RÉGRESSION**

Cette page est la synthèse métier de référence pour les trois rubriques ISSRP gérées par PaieEducation : `ISSRP_15`, `ISSRP_30`, `ISSRP_45`. Elle fige les règles validées par les migrations M115, M116 et M117 et leurs tests de non-régression associés.

> Source réglementaire de référence : [ISSRP_Corrige_26526.txt](../scratch/Audit_ISSRP_Conformite/ISSRP_Corrige_26526.txt) (Décret 11-373 Art. 9 bis effet rétroactif 01/01/2008 — Décret 25-55 Art. 10 entrée en vigueur 01/01/2025).

---

## 1. Régime historique 2008-2024

| Aspect | Valeur |
|---|---|
| **Période** | `01/01/2008 → 31/12/2024` |
| **Rubrique** | `ISSRP_15` |
| **Taux** | `15 %` (taux unique) |
| **Base** | `Traitement` (TP = Traitement Principal) |
| **Périodicité** | Mensuelle |
| **Population** | **Tous les corps EN régis par le statut particulier de l'Éducation nationale** (décret 08-315 / 25-54) |
| **Critère technique projet** | `T_Corps_Grades.ID_StatutSecteur = 1` (référentiel `T_Ref_StatutsSecteur.Code_StatutSecteur='CORPS_EDUCATION'`) |
| **Exceptions laboratoire** | Corps de laboratoire historiquement classés `StatutSecteur=2` ou `StatutSecteur=6` mais réglementairement éligibles : `ATTALABO (84)`, `AGENTECHLABO (324)`, `AIDETECHLABO (323)`, `AGENLABO (54)` |
| **Exclusions** | `CONTRACTUEL` (ID_TypeContrat=1), `VACATAIRE` (ID_TypeContrat=5) — priorité 100 |
| **Base de calcul** | `Math.Round(TP × 0.15, 2, MidpointRounding.AwayFromZero)` |

### 1.1 Liste des corps couverts en historique

**14 corps couverts par M116** :
- Enseignants : `PROFECOLPRIM (56)`, `PROFENSEFOND (57)`, `PROFENSEMOYE (58)`, `PROFENSESECO (60)`, `PROFTECHLYCE (91)`, `MAITECOLPRIM (90)`
- Intendance : `INTE (25)`, `SOUSINTE (24)`, `ADJOSERVECON (345)`
- Laboratoire : `ATTALABO (84)`, `AGENTECHLABO (324)`, `AIDETECHLABO (323)`, `AGENLABO (54)`, `ADJOTECHLABO (28)`

**20 corps complémentaires couverts par M117** (tous `StatutSecteur=1` non encore couverts) :
- Éducation : `ADJOEDUC (12)`, `SUPEEDUC (13)`, `CONSEDUC (14)`
- Direction : `DIRECOLL (82)`, `DIRELYCE (83)`, `DIREECOLPRIM (322)`
- Censeurs : `CENS (16)`, `CENSLYCE (81)`
- Orientation / guidance : `CONSORIESCOLPROF (17)`, `CONSORIEGUIDSCOLPROF (18)`, `INSPORIEGUIDSCOLPROF (329)`
- Alimentation scolaire : `CONSALIMSCOL (19)`
- Inspection enseignement : `INSPEDUCNATI (331)`, `INSPENSEMOYE (330)`, `INSPENSEPRIM (328)`, `INSPENSESECO (332)`
- Enseignants contractuels (corps EN officiels) : `PROFCONTENSEPRIM (325)`, `PROFCONTENSEMOYE (326)`, `PROFCONTENSESECO (327)` — soumis aux EXCLUSIONS Type_Contrat
- Adjoints techniques : `ADJOTECH (40)`

**Total : 34 corps couverts en historique** (30 corps `StatutSec=1` + 4 corps laboratoire en exceptions).

---

## 2. Régime 2025+ (Décret 25-55 Art. 10, effet 01/01/2025)

> ⚠️ Les trois rubriques 2025+ partagent la même base (TP) et la même périodicité (mensuelle). Seul change le taux différencié par famille de corps.

### 2.1 Groupe **ISSRP_45**

| Aspect | Valeur |
|---|---|
| **Rubrique** | `ISSRP_45` |
| **Taux** | `45 %` |
| **Base** | `Traitement` (TP) |
| **Date_Debut formule** | `01/01/2025` |

**Population — origine pédagogique / responsabilité pédagogique** :
- Enseignants : `PROFECOLPRIM`, `PROFENSEFOND`, `PROFENSEMOYE`, `PROFENSESECO`, `PROFTECHLYCE`, `MAITECOLPRIM`
- Direction d'établissement : `DIRECOLL`, `DIRELYCE`, `DIREECOLPRIM`
- Censeurs : `CENS`, `CENSLYCE`
- Inspecteurs discipline / administration : `INSPEDUCNATI`, `INSPENSEMOYE`, `INSPENSEPRIM`, `INSPENSESECO`
- Conseillers d'éducation issus du corps enseignant : `CONSEDUC`
- Grades de promotion à origine statutaire pédagogique

> ⚠️ Le code technique `ISRM_ENS_2025` (legacy) ne doit **pas** être interprété comme « enseignants uniquement ». La rubrique 45 % couvre un groupe élargi à origine pédagogique. Le bon code projet est `ISSRP_45`.

### 2.2 Groupe **ISSRP_30**

| Aspect | Valeur |
|---|---|
| **Rubrique** | `ISSRP_30` |
| **Taux** | `30 %` |
| **Base** | `Traitement` (TP) |
| **Date_Debut formule** | `01/01/2025` |

**Population — éducation non enseignante / orientation / alimentation** :
- Éducation non enseignante : `ADJOEDUC`, `SUPEEDUC`
- Orientation / guidance : `CONSORIESCOLPROF`, `CONSORIEGUIDSCOLPROF`
- Alimentation scolaire : `CONSALIMSCOL`
- Inspecteurs orientation / guidance : `INSPORIEGUIDSCOLPROF` *(corrigé par M115/E1 : ce corps était à tort classé ISSRP_45)*
- Inspecteurs EP alimentation : *aucun corps précis n'existe actuellement dans le référentiel — extension future réglementaire*

### 2.3 Groupe **ISSRP_15** (en vigueur depuis 2008, taux inchangé en 2025+)

| Aspect | Valeur |
|---|---|
| **Rubrique** | `ISSRP_15` |
| **Taux** | `15 %` |
| **Base** | `Traitement` (TP) |
| **Date_Debut formule** | `01/01/2008` (couvre historique + 2025+) |

**Population — intendance / laboratoire / gestion fin. & matérielle** :
- Intendance : `INTE (25)`, `SOUSINTE (24)`, `ADJOSERVECON (345)`
- Laboratoire : `ATTALABO (84)`, `AGENTECHLABO (324)`, `AIDETECHLABO (323)`, `AGENLABO (54)`, `ADJOTECHLABO (28)`
- Inspecteurs gestion financière / matérielle : *aucun corps précis n'existe actuellement dans le référentiel — extension future réglementaire*

### 2.4 Exclusions 2025+

Sur les **trois rubriques** ISSRP_15/30/45, les agents `CONTRACTUEL` (`ID_TypeContrat=1`) et `VACATAIRE` (`ID_TypeContrat=5`) sont systématiquement exclus avec règles `Priorite=100` (priorité absolue sur les INCLUSIONS de corps qui sont en `Priorite=10`).

---

## 3. Migrations concernées

### 3.1 Pilote `ISSRPCalculator`

- **Fichier** : [PaieEducation/Metier/Calculateurs/ISSRPCalculator.vb](../PaieEducation/Metier/Calculateurs/ISSRPCalculator.vb)
- **Rôle** : calculateur typé VB.NET qui remplace l'évaluation par expression SQL pour les codes `ISSRP_15`, `ISSRP_30`, `ISSRP_45`. Toutes les autres rubriques restent sur `ModCalculPaie.EvaluerExpression`.
- **Algorithme** :
  1. Vérification d'éligibilité via la logique existante `DALEligibiliteIndemnites.EvaluerEligibilite`.
  2. Calcul : `TP × taux` (15, 30 ou 45 %).
  3. Arrondi final : appliqué par l'appelant — `Math.Round(..., 2, MidpointRounding.AwayFromZero)`.
- **Fail-fast** : si l'éligibilité ne peut pas être déterminée (`CONTEXTE_INVALIDE`), une `InvalidOperationException` est levée — pas de paiement silencieux.
- **Routage** : un seul `If RubriqueCalculatorRegistry.EstSupporte(code)` dans [ModCalculPaie.vb:1141](../PaieEducation/Metier/ModCalculPaie.vb#L1141). Aucune autre rubrique impactée.

### 3.2 `Migration_115_ConformiteReglementaireISSRP`

**Périmètre** : corrections du référentiel pour le régime 2025+.

| Écart | Action |
|---|---|
| **E1** | `INSPORIEGUIDSCOLPROF` (329) : désactivation de la règle INCLUSION `ISSRP_45` (mal classée) + création INCLUSION `ISSRP_30` |
| **E2** | Création INCLUSION `ISSRP_15` 2025+ pour `ADJOSERVECON` (345) — rattachement intendance |
| **E3** | Création INCLUSION `ISSRP_15` 2025+ pour `ADJOTECHLABO` (28) — rattachement laboratoire |
| **E6** | Réactivation des EXCLUSIONS `CONTRACTUEL`/`VACATAIRE` sur `ISSRP_30` et `ISSRP_45` (laissées à `Est_Active=False` par M057.DésactiverAncienISRM) |
| **E8** | Correction cosmétique du libellé "Attache" → "Attachés" (ID_Corps=84) |

Idempotente. Aucune modification d'`ISSRP_30`/`ISSRP_45` formule, aucune migration squashée.

### 3.3 `Migration_116_ConformiteISSRPHistorique`

**Périmètre** : régime historique 2008-2024, première vague (enseignants + intendance + laboratoire + exclusions).

| Action | Détail |
|---|---|
| **H1.a** | 2 EXCLUSIONS historiques `CONTRACTUEL`/`VACATAIRE` sur `ISSRP_15` (Date_Debut=2008-01-01, Date_Fin=2024-12-31) avec `ID_TypeContrat` peuplé |
| **H1.b** | 8 INCLUSIONS historiques intendance + laboratoire |
| **H2** | 6 INCLUSIONS historiques enseignants (uniquement 2008-2024, ils basculent vers `ISSRP_45` en 2025+) |

### 3.4 `Migration_117_CompleterISSRPHistoriqueTousCorpsEN`

**Périmètre** : couverture historique complète — tous les corps `StatutSec=1` du référentiel non couverts par M116.

| Action | Détail |
|---|---|
| Lecture dynamique | `SELECT ID_Corps, Libelle_Corps FROM T_Corps_Grades WHERE ID_StatutSecteur = ID_StatutSecteur(Code='CORPS_EDUCATION')` |
| Filtre M116 | Exclusion des 14 ID_Corps déjà couverts (constante `CORPS_DEJA_COUVERTS_M116`) |
| INSERT | 20 INCLUSIONS historiques ISSRP_15 avec `Priorite=10`, `Date_Debut=2008-01-01`, `Date_Fin=2024-12-31`, `Est_Active=True` |

Idempotente : discriminant strict `(Code_Indemnite, Type_Regle, ID_Corps, Date_Debut=2008-01-01)`.

---

## 4. Garde-fous (à respecter pour toute évolution future)

> Cette section fige les invariants de non-régression. Toute modification du code ou des règles d'éligibilité qui viole un de ces points doit être refusée.

### 4.1 Périodes — invariants temporels

- ❌ **Ne jamais activer `ISSRP_30` ou `ISSRP_45` avant `01/01/2025`.**
  - `T_Formules_Calcul.Date_Debut` doit rester `≥ 2025-01-01` pour ces deux codes.
  - Tests d'invariant : `HR30`, `HR31`, `CR43`, `CR44`, `TC44`.
- ✅ `ISSRP_15` peut s'appliquer depuis le `01/01/2008` (formule `Date_Debut=2008-01-01`, `Date_Fin=NULL`).
- ✅ Les règles INCLUSION historiques portent `Date_Fin=2024-12-31` pour les corps qui basculent vers un autre taux en 2025+ (enseignants vers `ISSRP_45`, orientation vers `ISSRP_30`, etc.).

### 4.2 Exclusions — invariants protection

- ❌ **Ne pas supprimer ni désactiver les EXCLUSIONS `CONTRACTUEL` / `VACATAIRE`** sur les 4 segments :
  - `ISSRP_15` historique 2008-2024 (créées par M116/H1.a)
  - `ISSRP_15` 2025+ (créées par M095)
  - `ISSRP_30` 2025+ (réactivées par M115/E6)
  - `ISSRP_45` 2025+ (réactivées par M115/E6)
- ❌ **Ne jamais retirer le peuplement `ID_TypeContrat`** sur ces règles — ARCH-7D rejette toute règle texte sans ID.

### 4.3 Legacy ISRM

- ❌ **Ne pas réactiver les anciens codes `ISRM_*`.**
  - `ISRM` (ID 34), `ISRM_HIST`, `ISRM_2025`, `ISRM_INTLABO_2025` doivent rester `Actif=False`.
  - Les règles `ISRM_INTLABO_2025` (ID 114-121) doivent rester `Est_Active=False` (désactivées par M092/M096).
  - Toute migration future qui activerait l'un de ces codes serait une régression réglementaire.

### 4.4 Couverture EN — invariant exhaustivité

- ✅ **Tout nouveau corps avec `ID_StatutSecteur=1` ajouté à `T_Corps_Grades`** doit être couvert par une INCLUSION ISSRP_15 historique (Date_Debut=2008-01-01, Date_Fin=2024-12-31).
- ✅ Le test `TC20_TousCorpsEN_Couverts_ISSRP_15_2024` valide automatiquement cette couverture à chaque CI — si un nouveau corps EN n'est pas couvert, le test FAIL et bloque la livraison.
- ✅ Le test `IG01_ISSRP_Invariants_Globaux` (cf. §5) consolide tous les invariants en un seul point de vérification.

### 4.5 Pas de double-classement 2025+

- ❌ **Aucun corps ne doit avoir une INCLUSION ACTIVE sur plus d'un code ISSRP en 2025+.**
  - Un corps de la famille intendance ne doit pas être en `ISSRP_30` ni `ISSRP_45`.
  - Un corps enseignant ne doit pas être en `ISSRP_15` 2025+ ni `ISSRP_30`.
  - Un corps d'éducation non enseignante ne doit pas être en `ISSRP_45` ni `ISSRP_15` 2025+.
- Note : le régime historique 2008-2024 fait exception : tout corps EN peut avoir ISSRP_15 historique **et** son code 2025+ correspondant, car les périodes sont disjointes (`Date_Fin=2024-12-31` côté historique, `Date_Debut=2025-01-01` côté 2025+).

### 4.6 Calculateur typé

- ❌ **Ne pas remettre `ISSRPCalculator` en mode SQL** (régression du pilote).
- ❌ **Ne pas modifier le routage** dans `ModCalculPaie.vb` pour exclure ISSRP du registry.
- ❌ **Ne pas dupliquer la logique d'éligibilité** dans le calculateur — elle doit rester portée par `DALEligibiliteIndemnites.EvaluerEligibilite`.

---

## 5. Tests de non-régression actifs

| Suite | Tests | Couverture |
|---|---|---|
| `ISSRPCalculatorTests` | 12 (ISSRP01-ISSRP12) | Registry, parité TP×taux, exclusions, fail-fast |
| `ISSRPConformiteReglementaireTests` | 31 (CR01-CR53) | 2025+ : groupes 45/30/15, exclusions, idempotence M115 |
| `ISSRPHistoriqueTests` | 23 (HR01-HR35) | Historique : enseignants 2008/2024, intendance, labo, exclusions, invariants M116 |
| `ISSRPHistoriqueTousCorpsENTests` | 30 (TC01-TC51) | Couverture EN complète, paramétrique exhaustif TC20, non-régression M115/M116 |
| `ISSRPInvariantsGlobauxTests` | 1 (IG01) | Agrégation finale de tous les invariants — voir §5.1 |
| **Total ISSRP** | **97 tests** | |

### 5.1 Test pivotal `IG01_ISSRP_Invariants_Globaux`

Ce test final consolide en une seule passe les invariants non couverts ailleurs :
- EXCLUSIONS `ISSRP_15` 2025+ sont actives avec `ID_TypeContrat` peuplé.
- Aucun code legacy `ISRM_*` n'a de formule active.
- Aucune règle INCLUSION `ISRM_INTLABO_2025` active.
- Aucun corps cartographié 2025+ n'est double-classé entre `ISSRP_45`, `ISSRP_30` et `ISSRP_15`.

---

## 6. Décision finale

> **CLÔTURÉ — CONFORME — VERROUILLÉ CONTRE RÉGRESSION**

La conformité réglementaire ISSRP est désormais complète et protégée par 97 tests automatisés. Aucune modification métier ISSRP ne doit être effectuée sans :
1. Référence textuelle au décret applicable (11-373 Art. 9 bis, 25-55 Art. 10, ou un texte plus récent).
2. Nouvelle migration `Migration_XXX` numérotée après M117.
3. Mise à jour de cette documentation.
4. Ajout de tests de non-régression.
5. Passage de la campagne `RunTestsCI.ps1` à 100 % de réussite.

---

*Dernière mise à jour : génération automatique post-M117, état CI 437/437 PASS.*
