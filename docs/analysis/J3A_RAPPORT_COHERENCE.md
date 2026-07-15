# J3.a — Rapport de cohérence documentaire

> **Statut :** v1.0 — Soumis à validation avant tout développement J3.
> **Périmètre comparé :** Tomes V4 (A–F) + Tomes V3, `docs/PLAN_ACTION.md` (décisions Q1–Q13),
> schéma SQLite V001–V007, seeders J2, et l'ensemble des documents réglementaires du dossier
> `Reglementation/` (Cascade CSV, Grille indiciaire JO 2007–2024, ISSRP corrigé, IFC 2008+2015,
> Prime de rendement, éléments de paie historisés, régimes paramédicaux 2011–2024, sources IRG).
> **Convention de gravité :** 🔴 CRITIQUE (résultat de paie faux) · 🟠 MAJEUR (donnée/label/référence
> erronée ou lacune de modèle) · 🟡 MINEUR (coquille, ambiguïté documentaire sans impact calcul).

---

## 1. Incohérences entre l'implémentation (J1–J2) et la réglementation

### INC-01 🔴 — Barème IRG 2022+ : le seed applique le barème 2008 au lieu du barème LF 2022

| Élément | Constat |
|---|---|
| Document concerné | `IrgSeeder.cs` (ligne ~129 : `IRG-PER-2022` → `BaremeId = "IRG-2008"`), V006/V007, décision **Q4b** du PLAN_ACTION |
| Référence réglementaire | **LF 2022, Art. 31 → Art. 104 CIDTA révisé** (cf. `evolution_bareme_irg_algerie_2008_2026.html`, section « Loi de Finances 2022 — Refonte totale ») ; pseudo-code IRG, étape 1 : « à partir de 2022-01-01 ⇒ **nouveau barème** + exonération ≤ 30 000 + nouveaux lissages » |
| Règle actuellement implémentée | Période 2022+ = barème 2008 (4 tranches 0/20/30/35 %) + exonération 30 000 + coefficients 137/51, 27925/8 (général) et 93/61, 81213/41 (spécial) |
| Règle correcte | Période 2022+ = **nouveau barème à 6 tranches** (mensuel : ≤ 20 000 → 0 % ; 20 001–40 000 → 23 % ; 40 001–80 000 → 27 % ; 80 001–160 000 → 30 % ; 160 001–320 000 → 33 % ; > 320 000 → 35 %) + exonération ≤ 30 000 + lissage général 137/51 − 27925/8 (30 001 < SI < 35 000) + lissage spécial 93/61 − 81213/41 (30 000 < SI < 42 500) |
| Justification | Les coefficients de lissage ne s'appliquent que dans la fenêtre 30–35 k (générale) / 30–42,5 k (spéciale). **Pour tout SI ≥ 35 000 DA, le modèle actuel calcule l'IRG avec le barème 2008, qui diverge fortement du barème 2022.** Exemple (base imposable 54 800 DA) : LF 2022 → (54 800 − 40 000) × 27 % + 4 600 = **8 596 DA** ; modèle actuel (2008 + abattement plafonné 1 500) → (54 800 − 30 000) × 30 % + 4 000 − 1 500 = **9 940 DA**. Écart ≈ 1 350 DA/mois par agent. |
| Correction proposée | Ajouter en base un barème `IRG-2022` (6 tranches) et faire pointer `IRG-PER-2022` dessus. Le schéma V006/V007 le permet déjà sans modification (la FK `BaremeId` est par période). **Réviser la décision Q4b** → voir **Q-01** (J3F). |

### INC-02 🟠 — Rubrique PAPP : libellé et référence réglementaire erronés

| Élément | Constat |
|---|---|
| Document concerné | `ReglementaireSeeder.cs` (rubrique `PAPP`) |
| Référence réglementaire | Art. 3 D.ex. **10-78** (24/02/2010, effet 01/01/2008), étendu par Art. 3 D.ex. **12-403** (effet 29/05/2012), reconduit Art. 3 D.ex. **25-55** (effet 01/01/2025) |
| Règle actuellement décrite | « Prime d'**ajustement et de péréquation des pensions** (PAPP) … (Décret 07-308) » |
| Règle correcte | « Prime d'**amélioration des performances pédagogiques** (PAPP) », 0–40 % du traitement **selon notation**. Le D.ex. 07-308 concerne les agents contractuels, sans rapport. |
| Justification | `elements_paie_historique_14726.txt` et `Prime-Rendement_historique_26526` §1. Bénéficiaires historisés : 2008→28/05/2012 = enseignants + éducation + orientation/guidance + alimentation scolaire ; 29/05/2012+ = + direction + inspection. Ces deux versions d'éligibilité doivent être seedées avec leurs dates d'effet. |

### INC-03 🟠 — PAPP : caractère cotisable faux dans le seed

| Élément | Constat |
|---|---|
| Document concerné | `ReglementaireSeeder.cs` — `PAPP … EstImposable=1, EstCotisable=0` |
| Référence réglementaire | `ISSRP_Corrige_26526.txt` (titre : « ÉLÉMENTS DE SALAIRE **COTISABLE** — Corps spécifiques de l'EN ») ; par analogie Art. 12 D.ex. 11-200 (« les primes et indemnités prévues aux articles 2 et 7 sont soumises aux cotisations de sécurité sociale et de retraite ») |
| Règle correcte proposée | PAPP imposable **et cotisable** (comme les autres primes/indemnités des corps EN). |
| Justification | Le texte intégral du D.ex. 10-78 n'est pas dans le dossier ; confirmation demandée → **Q-02**. |

### INC-04 🟠 — Périodicité de versement trimestrielle non modélisable

| Élément | Constat |
|---|---|
| Document concerné | Schéma V004 (`Rubriques.Periodicite`, valeur unique), seed PAPP = `MENSUELLE` |
| Référence réglementaire | Tous les décrets rendement/performance : PAPP, PAPG, prime de rendement, PAP paramédicale sont « **calculées mensuellement**, **servies trimestriellement** » (Art. 3–5 D.ex. 10-78/10-134/10-135/10-136/11-200) |
| Lacune | Le modèle ne distingue pas **périodicité de calcul** et **périodicité de versement**. |
| Correction proposée | Ajouter `PeriodiciteVersement` (défaut = périodicité de calcul) en V008, ou décision fonctionnelle de verser mensuellement en V1 → **Q-09**. |

### INC-05 🟠 — IEP : référence erronée et confusion pédagogique/professionnelle

| Élément | Constat |
|---|---|
| Document concerné | `ReglementaireSeeder.cs` — « IEP … (Décret 07-308) » |
| Référence réglementaire | Corps EN : **indemnité d'expérience pédagogique**, Art. 9 D.ex. 10-78 puis Art. 9 D.ex. 25-55 (4 % du traitement de base par échelon). Corps communs/ouvriers : **indemnité d'expérience professionnelle**, D. 85-58 (modifié). Paramédicaux filière enseignement : Art. 10 D.ex. 11-200. |
| Règle correcte | Même formule (4 % × TBASE × n° échelon, conformément à Q2), mais **libellé, source et bénéficiaires diffèrent par filière**. Modéliser soit deux rubriques (EXP_PEDAG / EXP_PROF), soit une rubrique paramétrée par filière — recommandation : deux rubriques (traçabilité réglementaire par décret). |

### INC-06 🟠 — Matrice d'éligibilité ISSRP : placeholders et critère insuffisant

| Élément | Constat |
|---|---|
| Document concerné | `ReglementaireSeeder.cs` (`Issrp45CorpsCodes` = 4 codes ; `Issrp15CorpsCodes` avoué « placeholders ») |
| Référence réglementaire | Art. 10 D.ex. 25-55 + note d'interprétation `ISSRP_Corrige_26526.txt` (groupe 45 % élargi) + décision **Q6** |
| Lacunes | (a) La matrice ne couvre pas les ~48 corps / 185 grades du CSV `Cascade_Corps_Grades_30526.csv`. (b) Le critère « grades de promotion **dont l'origine statutaire est le corps enseignant** » (ex. conseillers d'éducation issus du corps enseignant) n'est pas exprimable par le seul critère `CORPS` : deux agents du même corps peuvent relever de 45 % ou 30 % selon leur origine. |
| Correction proposée | Affectation ligne à ligne des 185 grades du CSV aux groupes 45/30/15 (à générer et faire valider → **Q-03**) + extension du modèle d'éligibilité (critère `ORIGINE_CORPS` au niveau agent, ou attribut de grade) → J3E §4. |

### INC-07 🟡 — Couverture du seed rubriques : 6 sur ~30

Attendu à ce stade (pilote enseignants, Q10). Le catalogue **J3C** fixe l'inventaire cible exhaustif
(corps EN, corps communs, ouvriers/conducteurs/appariteurs, contractuels, paramédicaux, IFC, IRG, cotisations).
Aucune correction immédiate ; à dérouler en Phase 2 (extension) selon le périmètre validé.

---

## 2. Incohérences internes aux documents réglementaires fournis

### INC-08 🟠 — ISSRP : « tous les inspecteurs » contredit le tableau des taux

Le fichier `ISSRP_Corrige_26526.txt` dit dans sa note : le groupe 45 % inclut « **tous les inspecteurs** » ;
mais son propre tableau (aligné Art. 10 D.ex. 25-55) affecte :
- inspecteurs spécialité **disciplines** et **administration des établissements** → 45 % ;
- inspecteurs **alimentation scolaire** et **orientation/guidance** (collèges & lycées) → 30 % ;
- inspecteurs **gestion financière et matérielle** (collèges & lycées) → 15 %.

**Arbitrage proposé :** le tableau (fidèle au décret) prévaut ; la puce « tous les inspecteurs » est une
généralisation abusive. → confirmation **Q-04**.

### INC-09 🟠 — Sources IRG : deux erreurs internes dans le HTML de synthèse

`evolution_bareme_irg_algerie_2008_2026.html`, section 2022–2026 :
1. **Étape 1** présente l'abattement 40 % comme un abattement « frais professionnels » **sur le salaire**
   (base imposable = net CNAS − abattement). Les textes (JO n° 82 du 31/12/2007, JO n° 33 du 04/06/2020,
   `CALCUL IRG ALGERIE.txt`) et la décision validée **Q5** appliquent le 40 % **sur l'IRG brut**
   (borné [1 000 ; 1 500] DA/mois). → Le pseudo-code prévaut.
2. **Étape 4** applique `IRG × 93/61 − 81213/41` à la tranche générale 30 001–35 000. Les paramètres
   corrects (pseudo-code, cohérents avec la LF 2022) sont `137/51 − 27925/8` pour le lissage **général**
   (30–35 k) et `93/61 − 81213/41` pour le lissage **spécial** handicapés/retraités RG (30–42,5 k).

**Arbitrage :** le pseudo-code `IRG_Algerie_2008_2026_PseudoCode.txt` est la référence exécutable ;
le HTML sert uniquement pour le barème 2022 à 6 tranches (INC-01) et le contexte historique.

### INC-10 🟡 — Coquille de date : D.ex. 11-200

`Prime-Rendement_historique_26526.txt` §5 : « Décret exécutif n° 11-200 du 24/05/**2010** » →
lire 24/05/**2011** (JO n° 30 du 01/06/2011, fourni en PDF).

### INC-11 🟡 — ISSRP : fausse contradiction « n'existait pas avant 26/10/2011 » vs « 01/01/2008–31/12/2024 »

Résolution : l'ISSRP est **créée** par le D.ex. 11-373 (26/10/2011) avec **effet rétroactif au 01/01/2008**
(Art. 9 bis). Les deux lignes du tableau sont donc compatibles. Impact moteur : la date d'effet
réglementaire (2008-01-01) pilote le calcul ; la date de publication pilote les **rappels rétroactifs**
(règle Q7). Même mécanique pour l'ind. services techniques et l'ind. nuisance labo (créées par 11-373,
effet 2008), et pour l'ind. qualification (taux 25/30 % du 10-78 remplacés **rétroactivement** par
40/45 % par le 11-373 : la version 25/30 % ne doit jamais servir à un calcul, seulement à l'audit).

### INC-12 🟠 — Paramédicaux : catégorie 11 orpheline à partir de 2025

D.ex. 24-425 (effet 01/01/2025) : soutien aux activités paramédicales = 55 % « catégories **10 et moins** »,
50 % « catégories **12 et plus** » ; technicité = 10 % « catégories **12 et plus** ». Le texte de 2011
(11-200) couvrait « 11 et plus ». **La catégorie 11 n'est couverte par aucune borne en 2025+**
(probablement parce que le statut 24-422 a reclassé les grades hors cat. 11, mais le CSV contient
« Infirmier de santé publique » en cat. 11). → décision **Q-05**.

### INC-13 🟡 — Coquilles diverses dans `elements_paie_historique_14726.txt`

- PAPP 2025 : « Base de calcul : Traitement**)** » (parenthèse orpheline).
- Ind. qualification : la ligne « 01/01/2008 – 31/12/2024 » chevauche la ligne « 29/05/2012 – 31/12/2024 » ;
  ce sont des **versions successives de bénéficiaires** (ajout direction/inspection en 2012), pas des taux
  contradictoires. À historiser en deux périodes d'éligibilité : [2008-01-01 ; 2012-05-28] et [2012-05-29 ; 2024-12-31].

---

## 3. Incohérences documentation projet (Tomes) ↔ décisions / implémentation

| Réf | Gravité | Constat | Arbitrage |
|-----|---------|---------|-----------|
| INC-14 | 🟠 | ADR-062 (V4 Tome D vol. 12) impose « GUID comme clé primaire pour toutes les tables » ; le schéma V004+ utilise des **codes métier** comme PK des référentiels (`Rubriques.Id = "IEP"`). | Déviation volontaire et saine pour les référentiels versionnés (lisibilité des FK, seeds idempotents). **À formaliser par un ADR local** (dérogation ADR-062 : GUID pour les données de gestion, code pour les référentiels). |
| INC-15 | 🟡 | V2 Tome B vol. 7 : tables préfixées `T_` ; V4 Tome D vol. 12 : PascalCase pluriel sans préfixe. | Conflit V2↔V4 résolu par la règle du PLAN_ACTION (« V4.0 prévaut »). Schéma actuel conforme V4. RAS. |
| INC-16 | 🟡 | V4 Tome B vol. 8, invariant « un bulletin par agent » (ambigu). | Lire « un bulletin par agent **et par période** » — confirmé par V4 Tome D vol. 12 §7 (`UNIQUE (AgentId, PeriodeId)`). |
| INC-17 | 🟡 | Tome B mentionne IBAN, NIR « si pris en charge ultérieurement ». | Hors périmètre V1 — ne pas modéliser. |
| INC-18 | 🟡 | Doublon ADR-121–125 entre vol. 24 et vol. 25 du Tome F. | Déjà signalé dans le Tome F lui-même ; renumérotation 126–130 actée. Registre `docs/adr/` fait foi. |
| INC-19 | 🟡 | Tome B (vol. 7 §15) interdit `ILogger` dans Domain ; les Tomes C/D exigent journalisation et explicabilité. | Compatible : l'explicabilité est portée par les **objets de résultat** (CalculationExplanation) retournés par le Domain ; la journalisation technique vit en Infrastructure. Rappel de conformité pour J3. |
| INC-20 | 🟡 | Tome C vol. 10 §2 : contrat `IPayrollCalculator` **async** (`CalculateAsync`) ; Tome C vol. 9 §4 : `IPayrollEngine.Calculate` **synchrone**. | Incohérence interne V4. Recommandation : cœur de calcul **synchrone et pur** (déterminisme, testabilité), asynchronisme porté par la couche Application (chargement du contexte). À figer par ADR local en J3. |

---

## 4. Vérifications positives (aucune divergence)

| Contrôle | Résultat |
|---|---|
| Grille indiciaire CSV (4 colonnes de dates) vs D.p. 07-304 / 22-138 / 23-54 (art. 1 & 2) | ✅ Échantillonnage complet cat. 1–17 + HC-S1/S2 : indices minimaux conformes (ex. cat. 7 : 348/398/473/548 ; HC-S1 : 930/980/1055/1130). Dates d'effet conformes (01/01/2008, 01/03/2022, 01/01/2023, 01/01/2024). |
| Valeur du point indiciaire | ✅ 45 DA (Art. 8 D.p. 07-304), **inchangée** par les décrets 2022/2023 (ils modifient les indices, pas le point). Conforme Q1. |
| Grille des emplois contractuels (CSV lignes 173–185) vs D.p. 07-308 modifié 22-140 / 23-56 | ✅ Indices conformes (ex. OP niv. 1 : 200/250/325/400). |
| Indices d'échelon (1er–12e) | ✅ Tables JO 2007/2022/2023/2024 cohérentes entre elles (progression proportionnelle à l'indice minimal). |
| IFC : barèmes 08-70 (2008) et 15-176 (2015) | ✅ Tableau historisé `l'IFC 2008 + 2015.txt` cohérent ; « le reste sans changement » ⇒ cat. 11–17 restent à 1 500 DA après 2015. |
| ISSRP : assiette = Traitement (indice min + échelon) × 45, pas le traitement de base | ✅ Seed `BaseCalcul = TRAITEMENT` conforme. |
| IRG : 4 périodes (avant 2020-06 / 2020-06→12 / 2021 / 2022+), abattement 40 % [1000;1500] sur l'IRG, exonération ≤ 30 000, fractions exactes en TEXT | ✅ V007 + IrgSeeder conformes au pseudo-code (hors INC-01). |

---

## 5. Plan de correction proposé (sous réserve de validation des Q-xx)

| Priorité | Action | Portée |
|---|---|---|
| P0 | Seed barème `IRG-2022` (6 tranches) + repointage `IRG-PER-2022` (INC-01) | `IrgSeeder` + test d'intégration avec cas chiffrés du HTML (80 000 brut → 8 596 DA) |
| P0 | Corriger libellés/sources/flags PAPP et IEP (INC-02/03/05) | `ReglementaireSeeder` |
| P1 | Migration V008 : `PeriodiciteVersement` (INC-04) + critère d'éligibilité étendu (INC-06) + table `RubriqueBaremes` (J3E) | Schéma |
| P1 | Matrice ISSRP complète 185 grades (INC-06, après validation Q-03) | Seed |
| P2 | ADR local : dérogation ADR-062 (INC-14) + arbitrage sync/async moteur (INC-20) | `docs/adr/` |
| P2 | Extension du seed aux rubriques du catalogue J3C selon périmètre | Phase 2/10 |
