# J3.b — Catalogue exhaustif des règles métier

> **Statut :** v1.0 — Base d'implémentation du Domaine (Phase 3) et du moteur (Phase 4).
> **Format :** identifiant stable `RM-xxx`, regroupé par domaine. Chaque règle référence sa source
> (texte réglementaire ou décision validée Qx du PLAN_ACTION). Les règles marquées ⛳ ont une
> question ouverte associée (voir J3F).

---

## A. Rémunération de base (traitement)

| ID | Règle | Source |
|----|-------|--------|
| RM-001 | `Traitement de base = Indice minimal de la catégorie du grade × Valeur du point indiciaire`. | Art. 5 D.p. 07-304 ; Q1 |
| RM-002 | `Traitement = (Indice minimal + Indice d'échelon) × Valeur du point indiciaire`. | Art. 5 D.p. 07-304 ; Q1 |
| RM-003 | Valeur du point indiciaire = 45 DA, **paramétrable et versionnée** (jamais codée en dur). | Art. 8 D.p. 07-304 ; Q1 |
| RM-004 | L'indice minimal applicable dépend de la **date de la période de paie** : grille 2008 (01/01/2008), 22-138 (01/03/2022), 23-54 art. 1 (01/01/2023), 23-54 art. 2 (01/01/2024). | D.p. 07-304, 22-138, 23-54 |
| RM-005 | L'indice d'échelon dépend de (catégorie ou subdivision HC, n° d'échelon 1–12) dans la grille en vigueur à la date de paie. | Art. 2 D.p. 07-304 modifié |
| RM-006 | Les subdivisions hors catégorie (HC-S1…S7) suivent la même mécanique que les catégories 1–17. | Art. 2 D.p. 07-304 |
| RM-007 | La rémunération = traitement + primes et indemnités ; le traitement de base rémunère les obligations statutaires. | Art. 4–6 D.p. 07-304 |
| RM-008 | Agents contractuels : le traitement est calculé sur la grille des **emplois** (art. 45 D.p. 07-308 modifié 22-140, 23-56), avec les mêmes dates d'effet que RM-004. | D.p. 07-308 modifié |
| RM-009 ⛳ | Titulaire d'un poste supérieur : bonification indiciaire additionnelle (tables D.p. 07-307 modifié 22-139, 23-55), s'ajoutant à la rémunération du grade ; exclusive de toute prime attachée au poste. Périmètre V1 à confirmer (**Q-07**). | D.p. 07-307 |

## B. Carrière, échelons, ancienneté

| ID | Règle | Source |
|----|-------|--------|
| RM-020 | Avancement d'échelon : passage continu à l'échelon immédiatement supérieur, 12 échelons max, durées mini/moyenne/maxi = 2,5 / 3 / 3,5 ans. | Art. 10–11 D.p. 07-304 |
| RM-021 | Proportions d'avancement : 4/4/2 sur 10 fonctionnaires (ou 6/4 si le statut particulier consacre deux rythmes). Avancement de droit à la durée maximale. | Art. 12–13 D.p. 07-304 |
| RM-022 | Titulaire d'un poste supérieur ou d'une fonction supérieure : avancement de droit à la durée **minimale**, hors proportions. | Art. 14 D.p. 07-304 |
| RM-023 | Promotion de grade : reclassement à l'échelon d'indice égal ou immédiatement supérieur à celui détenu ; le **reliquat d'ancienneté est conservé**. | Art. 15 D.p. 07-304 |
| RM-024 | L'indemnité d'expérience professionnelle (IEP) est servie à **tous** les employés, en deux variantes selon le statut : `IEP_FONC` (fonctionnaires) et `IEP_CONT` (contractuels). Elle est distincte de l'indemnité d'expérience **pédagogique** (RM-024c). | Clarification fonctionnelle 14/07/2026 (révise Q2) |
| RM-024a | `IEP_FONC = IE × VPI` (indice d'échelon × valeur du point) : rétribution liée à la progression de carrière ; `TRT = TBASE + IEP_FONC`. À chaque avancement d'échelon, l'IEP augmente via la grille. | Art. 5 D.p. 07-304 ; clarification 14/07/2026 |
| RM-024b | `IEP_CONT = TBASE × min(ANC_PUB × 1,4 % + ANC_PRIV × 0,7 % ; 60 %)` : prime d'ancienneté composite des contractuels. Le plafond 60 % s'applique au **taux composite**, jamais aux années de service. Taux 1,4/0,7/plafond 60 = paramètres versionnés. | Art. 16 D.p. 07-304 ; clarification 14/07/2026 |
| RM-024c | L'indemnité d'expérience **pédagogique** (`EXP_PEDAG = TBASE × 4 % × ECH`) est réservée aux fonctionnaires des corps EN **hors Intendance et Laboratoire** ; bénéficiaires versionnés (direction + inspection ajoutés au 29/05/2012). | Art. 9 D.ex. 10-78 ; Art. 3 D.ex. 12-403 ; Art. 9 D.ex. 25-55 |
| RM-025 | Reclassement lors d'un changement de grille : même catégorie, même échelon détenus à la date d'effet (2022-03-01, 2023-01-01, 2024-01-01). | Art. 3 D.p. 22-138 ; Art. 4 D.p. 23-54 |
| RM-026 | Ancienneté (pour l'IEP et les règles d'éligibilité) = années de service effectif ; disponibilité et suspension **non déduites**. | Q8 |
| RM-027 | Toute décision de carrière est historisée : date d'effet, référence réglementaire, motif. | Tome B V2 vol. 7 §7 ; Tome B V4 vol. 8 |

## C. Éligibilité des rubriques

| ID | Règle | Source |
|----|-------|--------|
| RM-040 | Une rubrique n'est calculée que si **toutes** ses conditions d'éligibilité sont satisfaites à la date d'effet (critères possibles : filière, corps, grade, catégorie, fonction, type de contrat, échelon, ancienneté, établissement, période). | Tome B V2 vol. 9 §6 |
| RM-041 | L'éligibilité est **versionnée** : le même couple (rubrique, corps) peut entrer/sortir du bénéfice à une date d'effet (ex. PAPP étendue à direction+inspection au 29/05/2012). | D.ex. 12-403 |
| RM-042 | ISSRP 2008–2024 : taux unique 15 % pour **tous** les corps EN (éducation, orientation/guidance, alimentation, direction, inspection, intendance, laboratoire). | Art. 9 bis D.ex. 11-373 |
| RM-043 | ISSRP 2025+ : 3 groupes — 45 % (enseignants ; éducateurs issus du corps enseignant ; direction ; inspection « disciplines » et « administration des établissements » ; censeurs ; grades de promotion d'origine enseignante) ; 30 % (éducateurs non issus du corps enseignant, orientation/guidance, alimentation scolaire, inspecteurs alimentation, inspecteurs orientation/guidance collèges & lycées) ; 15 % (intendance, laboratoire, inspecteurs gestion financière & matérielle). | Art. 10 D.ex. 25-55 ; ISSRP_Corrige ; Q6 |
| RM-044 ⛳ | L'appartenance au groupe ISSRP 45 % peut dépendre de l'**origine statutaire** de l'agent (grade de promotion issu du corps enseignant), pas seulement de son corps actuel. Nécessite un attribut d'origine au niveau agent ou grade (**Q-03**). | ISSRP_Corrige |
| RM-045 | Ind. qualification : cat. ≤ 12 → 40 % ; cat. ≥ 13 → 45 % (l'éligibilité par corps évolue en 2012 comme RM-041). | Art. 7 D.ex. 11-373 (rétroactif 2008) |
| RM-046 | Ind. documentation pédagogique : montant forfaitaire par tranche de catégorie (≤ 10 ; 11–12 ; ≥ 13). Intendance ajoutée par 11-373 ; direction+inspection par 12-403. | Art. 8 D.ex. 10-78 ; 11-373 ; 12-403 |
| RM-047 | Ind. direction d'établissement : réservée aux directeurs **en exercice effectif**, montant selon le type d'établissement (primaire/collège/lycée). | Art. 9 bis 1 D.ex. 15-271 |
| RM-048 | Corps communs : ind. services administratifs (25 % ou 40 % selon corps) ; ind. services techniques communs (25 % ou 40 %) ; soutien activités administration 10 % (à partir du 01/01/2012). | D.ex. 10-134 ; 13-188 |
| RM-049 | Ouvriers professionnels : nuisance 25 % (OP uniquement) ; forfaitaire de service 25 % (conducteurs + appariteurs) ; soutien 10 % (2012+). | D.ex. 10-135 ; 13-189 |
| RM-050 | Agents contractuels : prime de rendement 0–30 % ; nuisance 25 % (emploi OP) ; forfaitaire service 25 % (service/conducteur/chef de parc/gardien) ; risque & astreinte 25 % (agents de prévention) ; soutien 10 % (2012+). Assiette = traitement de **l'emploi occupé**. | D.ex. 10-136 ; 13-190 |
| RM-051 | Paramédicaux : régime selon la **filière** (soins vs enseignement paramédical), taux versionnés 2011/2025. | D.ex. 11-200 ; 24-425 |
| RM-052 | IFC : servie aux fonctionnaires **et** agents contractuels, montant selon la catégorie (barème 2008 par groupes ; barème 2015 par catégorie, cat. 11–17 inchangées à 1 500 DA). | D.ex. 08-70 ; 15-176 |
| RM-053 ⛳ | Enseignants contractuels (CSV lignes 130–132) : régime indemnitaire à confirmer — le D.p. 07-308/D.ex. 10-136 couvre les emplois ouvriers/services, pas les enseignants contractuels (**Q-08**). | — |

## D. Ordre de calcul, dépendances, cumuls

| ID | Règle | Source |
|----|-------|--------|
| RM-060 | Pipeline invariant : contexte → validation → éligibilité → variables → dépendances → gains → retenues → cotisations → IRG → net → contrôles. | Tome C vol. 9 §13 ; Tome B V2 vol. 9 §3 |
| RM-061 | Les dépendances entre rubriques forment un **DAG** ; tout cycle interrompt le calcul avec erreur explicite. | Tome C vol. 9 §10 |
| RM-062 | Une rubrique ne peut être calculée **deux fois** pour un même bulletin. | Tome B V4 vol. 7 §13 |
| RM-063 | Assiette cotisable = Σ rubriques marquées `EstCotisable` ; assiette imposable = Σ rubriques `EstImposable` − cotisations salariales déductibles. L'IRG se calcule **après** les cotisations. | Q3/Q5 ; Tome B V2 vol. 9 §7 |
| RM-064 | Le caractère imposable/cotisable est un **flag paramétrable par rubrique** (et par période via l'assiette de cotisation `CotisationAssietteRubriques`). | Q5 ; schéma V004/V005 |
| RM-065 | Lissage spécial IRG (handicapé/retraité RG) **non cumulable** avec le lissage général 30–35 k : le spécial prime si le profil s'applique. | JO n° 33 du 04/06/2020 ; pseudo-code étapes 5–6 |
| RM-066 | Abattement handicapés 2010 (80/60/30/10 % plafonné 1 000 DA) : **abrogé de fait au 01/06/2020** (remplacé par le lissage spécial). Applicable uniquement aux périodes 2010-01-01 → 2020-05-31. | JO n° 49 du 29/08/2010 ; `CALCUL IRG ALGERIE.txt` |
| RM-067 | Bonification indiciaire de poste supérieur **exclusive** des primes/indemnités attachées au poste (notamment ind. de responsabilité). | Art. 16 D.p. 07-307 |
| RM-068 | Retenues facultatives, distinctes des cotisations obligatoires, sous **deux formes** : montant **fixe** choisi par l'agent (œuvres sociales) ou **taux versionné sur une assiette** — ex. mutuelle **MUNATEC** = 1 % du salaire soumis aux cotisations (`BaseCalcul = ASSIETTE_COTISABLE`, taux dans `RubriqueParametres`). Jamais dans l'assiette IRG en tant que déduction (sauf disposition contraire paramétrée). | Q3b ; Q3b-rev (14/07/2026) |

## E. Validation & contrôles

| ID | Règle | Source |
|----|-------|--------|
| RM-080 | Avant calcul : contrat actif requis, grade/échelon valides, période ouverte, paramètres réglementaires disponibles à la date d'effet, pas de doublon de bulletin (sauf régularisation). | Tome B V2 vol. 9 §9 |
| RM-081 | Après calcul : équilibre des totaux (brut − retenues − cotisations − IRG = net), aucun montant négatif non justifié, rubriques obligatoires présentes. | Tome C vol. 9 §15 |
| RM-082 | Un bulletin validé est **immuable** ; aucun recalcul après clôture ; un agent = un bulletin par période. | Tome B V4 vol. 8 §8 ; Tome D vol. 12 §7 |
| RM-083 | Matricule unique ; unicité des codes réglementaires (rubrique, grade, corps, établissement, période). | Tome B V2 vol. 7 §17 |
| RM-084 | Cohérence des dates : `DateDebut ≤ DateFin` partout ; périodes de validité d'un même paramètre **sans chevauchement**. | Tome B V2 vol. 7 §17 ; schéma V004+ |
| RM-085 | Suppression interdite : agent avec bulletins, grade référencé, période clôturée → suppression logique/archivage seulement. | Tome D vol. 12 §9 |

## F. Historisation, dates d'effet, rétroactivité

| ID | Règle | Source |
|----|-------|--------|
| RM-100 | Toute valeur réglementaire (taux, montant, indice, barème, formule, éligibilité) porte `DateEffet`/`DateFin` ; le moteur sélectionne la version **en vigueur à la date de la période calculée**. | Tome B V2 vol. 9 §12 ; principe cardinal PLAN_ACTION |
| RM-101 | Aucune information réglementaire n'est écrasée : une évolution crée une **nouvelle version** et clôt la précédente (`DateFin`). | Tome B V2 vol. 7 §18 |
| RM-102 | Effet rétroactif ≠ date de publication : une règle publiée à T avec effet à T−n s'applique aux périodes ≥ T−n ; les périodes déjà payées entre T−n et T génèrent des **rappels** (delta ancien/nouveau calcul). | Q7 ; D.ex. 11-373 (effet 01/01/2008) |
| RM-103 | Non-régression : une évolution réglementaire ne modifie jamais rétroactivement un bulletin **validé** ; la correction passe par un rappel/régularisation sur une période ouverte. | Tome B V2 vol. 9 §4 |
| RM-104 | Versions remplacées rétroactivement (ex. qualification 25/30 % du 10-78) sont conservées pour l'audit mais **jamais sélectionnées** pour un calcul (la version rétroactive les masque sur toute leur plage). | ISSRP_Corrige [HIST] ; INC-11 |
| RM-105 | Chaque calcul produit un journal d'explication (variables, formule, versions de paramètres utilisées) et un snapshot reproductible. | Tome C vol. 9 §16–18 |

## G. Arrondis & présentation

| ID | Règle | Source |
|----|-------|--------|
| RM-120 | Arrondi **centralisé et uniforme** (rubriques + net) : au dinar le plus proche par défaut, mode paramétrable (dinar/dizaine). Aucun calculateur n'implémente son propre arrondi. | Q9/Q9b ; Tome C vol. 10 §22 |
| RM-121 | Tous les montants sont en **DZD**. | Tome B V2 vol. 9 §8 |
| RM-122 | Les taux « 0 à X % selon notation » (PAPP, PAPG, rendement, PAP) : le **plafond** est réglementaire (paramètre), la valeur effective est une **variable mensuelle/trimestrielle par agent** (notation). | Art. 3 D.ex. 10-78 etc. |
| RM-123 | Primes de performance : calcul mensuel, versement trimestriel (modalité de service V1 → **Q-09**). | D.ex. 10-78/10-134/10-135/10-136/11-200 |
