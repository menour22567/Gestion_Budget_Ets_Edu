# ADR-0010 — Abstention phase paiement : l'application gère la paie administrative, jamais son exécution bancaire

**Statut :** Accepté — 22/07/2026 (validé par l'utilisateur lors de l'audit delta 21/07/2026,
cf. P1 du plan `docs/audit/PLAN_ACTION_2026-07-19.md`). Formulé à l'issue de l'audit
d'architecture du 19/07/2026 ayant
révélé des reliquats documentaires (« ordre de virement » dans `Tome F` v4.0 § 5,
entités `Banque` / `ModePaiement` / `IBAN` dans `Tome B` v4.0, section « Finance »
dans le Tome B v3.0 archivé, commentaire de code dans
`BulletinDocumentModelV1.cs`), alors que le code implémenté est déjà conforme au
périmètre administratif strict.

## Contexte

L'application `PaieEducation` est positionnée dès l'origine comme un **système de
gestion administrative de la paie** : production et distribution de documents
(bulletins, attestations, relevés d'émoluments, attestations 60 mois retraite),
calcul des montants nets, paramétrage réglementaire, historique d'audit. Elle
n'a jamais vocation à **exécuter** le paiement des salaires — c'est-à-dire à
émettre des virements, des ordres de paiement, à détenir ou manipuler des
coordonnées bancaires d'agents, ou à réconcilier des flux financiers.

Or, l'audit du 19/07/2026 a mis en évidence plusieurs **pollutions résiduelles**
dans la documentation de référence, héritées d'une conception antérieure plus
ambitieuse (V3.0 « Tome B — Fonctionnel » décrit une section *Finance* complète :
Banques, Agences, Comptes ; le Tome C V3.0 introduit une table conceptuelle
`T_Banque` ; le Tome A V3.0 liste « Banques » parmi les référentiels ; le Tome F
V4.0 inclut « ordre de virement » parmi les types de documents produits ;
le Tome B V4.0 mentionne `Banque` comme sous-agrégat de l'agent et
`ModePaiement` / `IBAN` comme référentiels).

Le **code implémenté** est, lui, déjà propre : aucune migration SQL ne crée de
table `Banques` / `ComptesBancaires` / `Virements` / `OrdresPaiement`, aucun use
case applicatif ne déclenche de virement, aucun endpoint n'expose d'API
bancaire. La pollution est donc essentiellement **documentaire** — mais une
documentation qui mentionne « ordre de virement » comme livrable V1 invite
implicitement un développeur à l'implémenter « par symétrie » avec les autres
documents, sans avoir à justifier le franchissement d'une frontière métier
rédhibitoire.

La décision est donc prise de **formaliser explicitement cette frontière
métier** par un ADR, sur le modèle d'ADR-0009 (abstention réglementaire), afin
de :

1. Geler définitivement le périmètre applicatif à la phase administrative ;
2. Servir de référence à toute revue de code, revue de schéma, revue de doc ;
3. Éviter le coût d'un éventuel retrait tardif, après que des tables ou des
   use cases paiement aient été introduits.

## Décision

1. **L'application ne détient, ne calcule, ni n'exécute aucun acte de la phase
   paiement.** Elle s'arrête à la production des documents administratifs qui
   documentent ce qui **doit** être payé. Le déclenchement du paiement effectif
   (virement bancaire, ordre de paiement, émission de chèque, mandat de
   prélèvement, compensation interbancaire) relève d'un système d'information
   bancaire distinct, hors périmètre.

2. **Aucun concept lié à l'exécution du paiement n'est modélisé dans le
   domaine.** Sont **interdits** dans le modèle de domaine, le schéma SQL, les
   use cases applicatifs et le moteur documentaire :
   - `Banque`, `CompteBancaire`, `Domiciliation`, `ModePaiement`,
     `OrdreVirement`, `OrdrePaiement`, `MandatPrelevement`, `LettreChange`,
     `RIB`, `IBAN`, `BIC`, `CodeAgence`, `NumeroCompte`, `CleRIB`,
     `TitulaireCompte`, `StatutVirement`, `EtatCompte` (relevé de compte
     bancaire) ;
   - toute agrégation `Agent.Banque`, `Bulletin.CompteCredite`,
     `Periode.EtatPaiement` ;
   - tout attribut de rubrique ou d'agent qualifié de « mode de versement »
     autre que la **métadonnée** `PeriodiciteVersement` (voir § 6 ci-dessous).

3. **Si une attestation administrative doit afficher une information
   bancaire** (ex. RIB/IBAN de l'agent sur un document destiné à un tiers), elle
   est **importée en lecture seule** depuis le SIRH ou toute source de vérité
   externe, et **affichée sans transformation**. L'application ne persiste pas
   l'information bancaire : elle la présente, point. Toute évolution de la
   domiciliation est gérée par le SIRH, jamais par l'application de paie.

4. **Aucune table, aucune migration, aucun use case, aucun service, aucun
   endpoint, aucun modèle documentaire** ne peut être ajouté au code de
   production pour exécuter ou tracer un acte de paiement, sans avoir
   **préalablement** amendé cet ADR (qui deviendrait `Accepté` avec une
   dérogation explicite et limitée) **et** la documentation de référence V4
   (Tome A, Tome B, Tome F). Cette double barrière (ADR + doc de référence) est
   délibérée : elle impose un coût de révision à toute régression de scope,
   plutôt qu'un simple édit de doc isolé.

5. **Cette abstention n'est pas bloquante** pour les fonctions administratives
   légitimes : un bulletin dont le net à payer est documenté reste produit,
   validé, archivé, même en l'absence totale de coordonnées bancaires dans
   l'application. L'absence d'IBAN n'est ni une erreur, ni un avertissement,
   ni un blocage — c'est une information qui appartient à un autre système.

6. **Métadonnée `Rubriques.PeriodiciteVersement` (colonne SQL V008) — cas
   particulier conservé.** Cette colonne indique la **périodicité de service**
   d'une rubrique (ex. PAPP : calcul mensuel, service trimestriel), c'est-à-dire
   *à quelle fréquence le montant calculé est intégré au bulletin servi à
   l'agent*. Ce n'est pas un ordre de paiement, ce n'est pas un virement, ce
   n'est pas une domiciliation : c'est une **propriété de la rubrique** qui
   pilote la manière dont le moteur provisionne et ventile les montants dans
   le temps. Elle est conservée en l'état, sous réserve d'être re-nommée en
   `PeriodiciteService` dans une migration future si le vocabulaire « versement
   » continue de prêter à confusion (cf. action immédiate § suite à donner).
   La présence du mot « versement » dans le nom de la colonne **ne**
   constitue **pas** une violation de la présente ADR.

## Justification

- **Frontière métier franche et défendable.** La séparation entre « produire
  les documents qui justifient ce qui est dû » et « exécuter le paiement qui
  transfère l'argent » est une frontière classique des SI d'entreprise. La
  franchir sans nécessité expose l'application à des domaines réglementaires
  supplémentaires (DSP2, normes monétiques, certifications PCI, règles de
  lutte anti-blanchiment) sans valeur ajoutée pour le métier de l'utilisateur
  final (le service RH / gestion de la paie), dont la finalité s'arrête à
  l'édition du bulletin et des attestations.

- **Conformité au besoin utilisateur.** L'utilisateur a réaffirmé le
  19/07/2026 que le périmètre attendu est strictement administratif. Aucun
  cas d'usage transmis ne requiert l'exécution du paiement — la « liste V1 »
  de l'ancien `docs/audit/PLAN_IMPLEMENTATION.md` mêlait par erreur
  « ordre virement » avec des documents administratifs (attestation CNR,
  attestation de travail, état récapitulatif), correction appliquée le même
  jour.

- **Cohérence avec l'architecture déjà en place.** Le code implémenté
  (schéma SQL V001-V014, couches Domain / Application / Reporting) ne contient
  **aucune** référence à un objet paiement. Formaliser l'abstention par un
  ADR fige cette propriété existante en règle d'architecture, plutôt que de
  la laisser comme un choix d'implémentation par défaut susceptible d'être
  révoqué à la première opportunité.

- **Symétrie avec ADR-0009 (abstention réglementaire).** ADR-0009 pose la
  règle « ne jamais inventer un droit ». ADR-0010 pose la règle
  complémentaire « ne jamais exécuter un paiement ». Les deux sont des
  abstentions non bloquantes, les deux sont des règles d'architecture, les
  deux ont été déclenchées par un constat (pollution documentaire + risque de
  régression). Le parallèle rend l'ADR-0010 immédiatement lisible par toute
  personne ayant déjà rencontré ADR-0009.

- **Risque d'un ajout tardif.** Sans cette ADR, la simple présence des mots
  « ordre de virement » dans `Tome F` v4.0 § 5 et « Banque » / `IBAN` dans
  `Tome B` v4.0 suffirait à un développeur ultérieur pour proposer (et
  obtenir) l'ajout d'un use case `GenererOrdreVirement` au motif que « c'est
  dans la documentation de référence ». Une ADR explicite ferme cette
  échappatoire sans avoir à re-justifier l'abstention à chaque fois.

## Alternatives considérées

- **Ne rien formaliser, laisser la documentation se corriger au fil de
  l'eau** : rejeté — la pollution résiduelle (Tome F § 5, Tome B v4.0,
  Tome B v3.0 archivé) est précisément ce qui crée le risque ; ne pas la
  neutraliser par une règle explicite, c'est accepter le risque.

- **Adopter un périmètre minimaliste pur (uniquement bulletin + état
  récapitulatif)**, sans aucune autre production documentaire : rejeté —
  l'utilisateur a explicitement listé les attestations (CNR, travail, 60
  mois retraite, relevés d'émoluments) comme faisant partie du périmètre
  administratif. Réduire davantage le périmètre n'est pas conforme au besoin.

- **Étendre le périmètre à la phase paiement** (intégration API bancaire,
  émission de virements, réconciliation) : rejeté — hors besoin utilisateur,
  expose à des contraintes réglementaires bancaires nouvelles, n'apporte
  aucune valeur au métier de la paie administrative, et contreviendrait à
  l'esprit de l'ensemble du projet (mono-utilisateur, mono-poste, hors
  production, sans authentification).

- **Importer les RIB / IBAN en base en lecture seule, en les marquant
  explicitement « importés du SIRH, non éditables »** : envisagé, puis
  neutralisé par le principe 3 ci-dessus — si l'application n'en a pas
  l'usage, ne pas les importer du tout. Si un cas d'usage futur émerge
  (ex. attestation mentionnant l'IBAN), l'import se fera au point d'usage
  via un service dédié, sans schéma persistant.

- **Renommer immédiatement `PeriodiciteVersement` en `PeriodiciteService`
  dans la migration V008** : non retenu comme livrable de cette ADR — le
  renommage a un coût (migration SQL de données, mise à jour de tous les
  DTOs, ViewModels, tests, documentation) qui n'est pas justifié tant que la
  confusion sémantique n'a pas effectivement causé une erreur. Signalé en
  suite à donner.

## Conséquences

- **Documentation de référence V4** : `Tome F` § 5, `Tome B` (3 endroits) mis
  à jour le 19/07/2026 ; `Tome B` V3.0 archivé reçoit un bandeau explicite
  renvoyant à cet ADR.
- **Ancien plan d'implémentation** : `docs/audit/PLAN_IMPLEMENTATION.md`
  ligne 273 purgée de la mention « ordre virement » le 19/07/2026.
- **Code de production** : commentaire de
  `src/PaieEducation.Reporting/BulletinDocumentModelV1.cs` neutralisé le
  19/07/2026 (suppression de « ordre de virement » dans la liste des modèles
  futurs anticipés).
- **Revue de code / revue de schéma** : toute PR ou migration ajoutant un
  nom ou une colonne évoquant un concept de la liste du principe 2
  (`Banque`, `CompteBancaire`, `Virement`, `OrdrePaiement`, `IBAN`, `RIB`,
  etc.) doit être rejetée par revue, sauf si elle est accompagnée d'une
  dérogation explicite à cet ADR. La checklist de revue de schéma
  (lorsqu'elle existera) inclut cette ligne.
- **Tests** : aucun test de la suite existante ne porte sur un concept
  paiement — vérifié le 19/07/2026 sur les 328+ tests verts (unitaires,
  intégration, tools). La consigne de non-régression s'applique : aucun
  test paiement ne doit apparaître sans amendement préalable de cet ADR.
- **ADR-0009 (abstention réglementaire)** n'est pas modifié — les deux ADR
  sont complémentaires et indépendants. Une décision ultérieure qui
  amenderait l'un n'amenderait pas l'autre automatiquement.

## Action immédiate

- ✅ 19/07/2026 : `Tome F` v4.0 § 5 purgé de « ordre de virement » (avec
  note de renvoi vers cet ADR).
- ✅ 19/07/2026 : `Tome B` v4.0 purgé de `Banque` (sous-agrégat), `Banque` et
  `ModePaiement` (référentiels), `IBAN` (value object).
- ✅ 19/07/2026 : `docs/audit/PLAN_IMPLEMENTATION.md` l. 273 purgé de
  « ordre virement ».
- ✅ 19/07/2026 : `src/PaieEducation.Reporting/BulletinDocumentModelV1.cs`
  commentaire l. 11 neutralisé.
- ✅ 19/07/2026 : `Tome B` v3.0 archivé reçoit un bandeau explicite.
- ✅ 19/07/2026 : registre `docs/adr/README.md` mis à jour (entrée 0010).
- 🔲 Suite à donner : évaluer (lot C8 dette technique, ou chantier dédié) la
  possibilité de renommer `Rubriques.PeriodiciteVersement` en
  `PeriodiciteService` pour lever l'ambiguïté sémantique, par migration
  V015 (ALTER TABLE + mise à jour de tous les DTOs, ViewModels Workbench,
  tests `RubriquesV2BaremesSchemaTests` et `ReglementaireSeederTests`).
  Décision à prendre au plus tard avant la V1-D « Conforme ».

## Voir aussi

- ADR-0009 — Principe d'abstention réglementaire (symétrique, sur la
  donnée réglementaire).
- ADR-0005 — Moteur de calcul synchrone et pur (l'asynchronisme est en
  Application, jamais dans le moteur ; symétrie avec l'abstention : aucune
  « exécution » non plus dans le moteur).
- `Documentation de Référence du Projet/Version 4.0/Tome F — Reporting et
  Production Documentaire.md` § 5 (note de renvoi ajoutée).
- `Documentation de Référence du Projet/Version 4.0/Tome B — Modèle de
  Domaine.md` (nettoyé).
- `Documentation de Référence du Projet/Version 3.0/Tome B — Fonctionnel.md`
  (bandeau archivé).
