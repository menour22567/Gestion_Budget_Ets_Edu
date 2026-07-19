# ADR-0011 — Maintien de `decimal` + arrondi centralisé : dérogation à `CONVENTIONS.md §5`

**Statut :** Accepté — 19/07/2026. Formulé à l'issue de l'audit d'avancement du 19/07/2026
(synthèse chantier C8.1) comme décision d'architecture venant clore une dette documentée
depuis la Phase 4. Validé par l'utilisateur le 19/07/2026 (STOP & ASK clos).

## Contexte

`docs/CONVENTIONS.md §5` prescrit l'usage d'un Value Object `Money` (DZD + arrondi centralisé)
comme type porteur de tout montant dans le domaine.

**Correction du 19/07/2026 (relecture avant commit) :** la formulation initiale de ce
contexte affirmait que « tous les montants circulent en `decimal` natif », ce qui est
inexact — le VO `Money` (`PaieEducation.Shared/Money/Money.cs`, `readonly record struct
Money(decimal Amount, string Currency)`) existe et est déjà utilisé à l'**intérieur** du
moteur de calcul : `CalculationPipeline.cs` porte `BulletinLigne.Montant` en `Money`
(lignes de gain, cotisation, IRG, net), `RappelCalculator`, `ValidationEngine` et
`CumulsAnnuels` (Reporting) le manipulent également, et il est sérialisé pour les
snapshots persistés (`Bulletins.SnapshotJson` V012) via
`Infrastructure/Serialization/MoneyJsonConverter.cs`. Ce qui est vrai, en revanche, c'est
que **les frontières publiques restent en `decimal` natif** : signatures des use cases
(`CalculerBulletin.Demande`, `RapportImpact`, etc.), DTO, ViewModels Presentation, colonnes
SQL hors snapshot, exports Reporting. La dette réelle n'est donc pas « `Money` absent »
mais « `Money` présent en interne du pipeline, jamais généralisé aux frontières » — un état
hybride assumé, pas une absence totale. La décision ci-dessous est reformulée en
conséquence (point 1).

La prescription `CONVENTIONS.md §5` n'a donc pas été appliquée **au sens où elle l'exigeait**
(un VO porteur de *tout* montant, y compris aux frontières) : depuis la Phase 4 (livraison
du moteur de calcul), les DTO, signatures publiques, snapshots JSON sérialisés et tables SQL
(`decimal` / `REAL` / `TEXT`) restent en `decimal` natif ; seul l'intérieur du pipeline de
calcul utilise `Money`.

La dette est explicitement signalée dans `docs/PLAN_ACTION.md` Phase 5, tranche du
16/07/2026 (Tâche 3 « Composition Root ») :

> « Écart doc/code relevé en passant, non traité : `docs/CONVENTIONS.md §5` prescrit un
> objet valeur `Money`, jamais utilisé depuis Phase 4 (`decimal` nu partout) — hors
> périmètre de cette tranche. »

L'audit d'avancement du 19/07/2026 a classé le chantier C8.1 (« Décision `Money` ») comme
🟡 **partiel**, critère N5 (décision STOP & ASK en attente). La trancher lève un point
d'arrêt formel, et conditionne deux chantiers du backlog :

- ADR-0010 §6 « Action immédiate 🔲 » : migration V015 `PeriodiciteVersement` →
  `PeriodiciteService` (évaluation indépendante, mais qui ne devait pas s'encombrer d'une
  migration `Money` simultanée) ;
- toute évolution future des DTO `BulletinSnapshot` et des snapshots JSON persistés en V012.

## Décision

1. **`decimal` reste le type porteur aux frontières publiques** (signatures des use cases,
   DTO, ViewModels, Reporting, colonnes SQL hors snapshot) **et l'usage interne existant de
   `Money` dans le pipeline de calcul est conservé tel quel, sans être généralisé.** Aucun
   nouveau point d'entrée public ni nouvelle signature de use case n'introduit `Money` sans
   amendement préalable du présent ADR — l'état hybride (Money interne au pipeline / decimal
   aux frontières) est un choix assumé, pas une étape transitoire vers une généralisation.
   La prescription de `CONVENTIONS.md §5` (VO porteur de *tout* montant, y compris aux
   frontières) est **formellement amendée** par le présent ADR (qui prévaut sur la
   convention en cas de conflit, conformément à la hiérarchie ADR > convention du registre
   `docs/adr/README.md`).

2. **L'arrondi est centralisé et obligatoire.** Tout calcul de montant qui aboutit à un
   arrondi (bulletin, cotisation, IRG, rappel) passe par `ArrondiService`
   (`PaieEducation.Domain/Calcul/Services/ArrondiService.cs`), déjà livré Phase 4 et lu
   depuis `Parametres` (clé d'arrondi, défaut « dinar le plus proche », C2.1). Aucune
   instruction `Math.Round` / `decimal.Round` / cast implicite en `int` / `double` dans
   le code métier. La règle est portée par un **test d'architecture** (regex sur les
   fichiers `.cs` de `Domain` + `Application`, qui ne référencent aucun de ces appels
   hors `ArrondiService.cs`).

3. **L'arrondi est paramétrable** par modification de la valeur en base (`Parametres`),
   sans recompilation. Le défaut seedé (dinar le plus proche) est conservé comme
   **invariant de test** (les 466+ tests existants figés sur ce défaut ne sont pas remis
   en cause).

4. **Pas de retraitement des snapshots V012.** Les bulletins validés et persistés en base
   (`Bulletins.SnapshotJson`) restent en `decimal` JSON, jamais en `Money`. Le format de
   sérialisation est **gelé** (cf. ADR-0008 « Immutabilité des périodes clôturées » — un
   snapshot ne change jamais de forme, même si l'API qui l'a produit évolue).

## Justification

- **Coût d'introduction disproportionné en V1.** Introduire un VO `Money` reviendrait à
  migrer toutes les signatures publiques des use cases, tous les DTO, tous les snapshots,
  tous les tests, et la couche `Reporting` (rendu PDF, export Excel, modèles
  `IDocumentModel`). Cette migration est estimée à 3-5 jours de travail pour un gain de
  typage marginal (le `decimal` C# est déjà non-breaking : pas de NaN, pas de précision
  cachée comme `double`, et la conversion implicite `decimal → decimal` est sans piège).

- **L'arrondi est déjà centralisé.** La valeur principale d'un VO `Money` (garantir
  qu'aucun arrondi « sauvage » n'existe dans le code) est déjà couverte par
  `ArrondiService`. Le VO ajouterait de la cérémonie (`.Amount` / `.Currency` /
  `.Round()`) sans apporter de garantie supplémentaire au-delà de ce que le test
  d'architecture portera mécaniquement.

- **Le test d'architecture peut porter la règle d'or.** « Pas d'arrondi en dehors
  d'`ArrondiService` » est une règle simple, vérifiable mécaniquement par un test
  d'architecture (regex sur les `.cs`). Plus simple, plus rapide, plus robuste qu'un VO
  qui obligerait à inspecter chaque appel.

- **Cohérence avec l'existant.** `decimal` est le type de toutes les colonnes SQL
  monétaires (`REAL` SQLite pour les taux, `TEXT` pour les montants snapshotés en JSON),
  de toutes les sérialisations JSON, de tous les ViewModels. Introduire `Money` créerait
  une frontière artificielle entre la couche métier et ses frontières (sérialisation,
  persistance, UI) sans bénéfice mesurable.

- **Aucune raison métier ne justifie le VO.** Le périmètre V1 est mono-utilisateur,
  mono-devise (DZD), hors production. Les gains d'un VO `Money` (multi-devise, gestion
  d'arrondi par devise, sérialisation canonique) ne correspondent à aucun cas d'usage
  recensé dans `Reglementation/`, `J3B_CATALOGUE_REGLES_METIER.md`, ou les ADR en vigueur.

## Alternatives considérées

- **Introduire un VO `Money` (DZD + arrondi) maintenant** : rejeté — coût disproportionné
  en V1 ; les mêmes garanties sont obtenues par `ArrondiService` + test d'architecture.

- **Introduire un VO `Money` plus tard (V2+)** : non retenu dans cet ADR — si le besoin
  émerge (multi-devise, multi-entité, multi-pays), un ADR-XXXX futur le traitera. Cet
  ADR-0011 ne ferme pas la porte, il constate simplement que V1 n'en a pas l'usage.

- **Garder `decimal` mais interdire l'arrondi** (tout reste en virgule flottante) :
  rejeté — les bulletins de référence sont en dinars entiers (Q11), l'arrondi est
  nécessaire et déjà spécifié par la réglementation (Q9, Q9b).

- **Adopter `double` (performance, comme JavaScript)** : rejeté — perd la précision
  décimale, source classique de bugs de paie (cf. `floating-point-gui.de`).

## Conséquences

- **`docs/CONVENTIONS.md §5` est amendé** par ajout d'un paragraphe final : « Cette
  prescription est suspendue par ADR-0011 au profit de `decimal` + `ArrondiService` ; tout
  `Money` introduit dans le code sans amendement préalable d'ADR-0011 doit être refusé en
  revue. » (action 11.1 ci-dessous.)

- **C8.1 est marqué « finalisé »** dans le registre d'avancement (audit du 19/07/2026).
  La dette `Money` est officiellement close par cet ADR.

- **Test d'architecture** : ajout d'une assertion au test d'archi existant
  (`DependencyRulesTests`) : aucun `Math.Round` / `decimal.Round` / `Math.Floor` /
  `Math.Ceiling` / `Math.Truncate` dans `PaieEducation.Domain/**` et
  `PaieEducation.Application/**` en dehors de `ArrondiService.cs`.

- **Aucune migration SQL** n'est requise (les types `REAL` / `TEXT` restent).

- **Aucune migration de snapshot** n'est requise (le format JSON `decimal` reste
  canonique ; cf. ADR-0008).

- **Limite connue du garde d'architecture, documentée plutôt que cachée.**
  `Money.Arrondir(int decimals)` (`PaieEducation.Shared/Money/Money.cs`, ligne 35-36)
  appelle `Math.Round` directement, hors `ArrondiService`. Le garde d'architecture ne le
  détecte pas — il ne scanne que `PaieEducation.Domain` et `PaieEducation.Application`, et
  `Money` vit dans `PaieEducation.Shared`, une couche que `Domain` consomme et qui ne peut
  pas dépendre de `Domain` en retour (cycle interdit) : `Money.Arrondir` ne peut donc pas
  déléguer à `ArrondiService.ArrondirDecimales`. Vérifié le 19/07/2026 : cette méthode n'a
  **aucun appelant** dans le code actuel (tous les sites d'arrondi du pipeline passent par
  l'instance `ArrondiService` injectée, cf. `CalculationPipeline.cs`) — ce n'est pas une
  violation active, mais une mine latente pour un futur appelant. Action de suivi (hors
  périmètre du présent commit) : supprimer `Money.Arrondir(int)` si un audit futur confirme
  qu'il reste inutilisé, ou le documenter explicitement comme méthode de confort sans
  garantie de centralisation si un besoin d'arrondi *sur un `Money` isolé, hors pipeline*
  apparaît.

- **Le backlog ADR-0010 §6** (renommage `PeriodiciteVersement` → `PeriodiciteService`)
  reste indépendant et peut être traité sans interaction avec cette décision.

## Action immédiate

- ✅ 19/07/2026 : ADR-0011 rédigé (présent fichier), statut **Accepté** (validé
  par l'utilisateur le 19/07/2026).
- ✅ 19/07/2026 : ajout au registre `docs/adr/README.md`, amendement de
  `CONVENTIONS.md §5`, ajout du test d'architecture, marquage C8.1 « finalisé ».
- 🔲 À intégrer dans la tranche C6.1 (conjointe) : le test d'architecture
  `Arrondi_centralise_uniquement_dans_ArrondiService` est déjà ajouté au
  `DependencyRulesTests` (cf. tranche C6.1 sous-tâche 6.1.10).

## Voir aussi

- ADR-0005 — Moteur de calcul synchrone et pur (l'arrondi est synchrone, centralisé,
  déterministe — cohérent).
- ADR-0008 — Immutabilité des périodes clôturées (le format de snapshot `decimal` JSON
  est gelé).
- ADR-0010 — Abstention phase paiement (§6, renommage `PeriodiciteVersement`
  indépendant de cette décision).
- `docs/PLAN_ACTION.md` Phase 5, tranche du 16/07/2026 (Tâche 3) — signalement initial
  de la dette.
- `docs/CONVENTIONS.md §5` — prescription amendée par cet ADR.
