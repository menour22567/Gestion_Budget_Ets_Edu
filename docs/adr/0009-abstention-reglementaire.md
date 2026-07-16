# ADR-0009 — Principe d'abstention réglementaire : jamais de droit inventé, extrapolé ou déduit

**Statut :** Accepté — 15/07/2026. Formulé par l'utilisateur comme règle d'architecture du
projet, à l'issue du traitement des grades conditionnels ISSRP (Q-C1) et de la résolution
d'un conflit de sources réglementaires (Q-08, contractuels), documenté dans
`docs/analysis/J4E_DOSSIER_CONCEPTION_AFFECTATION.md` § 6.2 et
`docs/analysis/J4F_TABLEAU_ISSRP_GRAIN_GRADE.md` § 3bis.

## Contexte

Deux épisodes du 15/07/2026 ont révélé le même risque sous deux formes différentes.

**Q-C1 (grades conditionnels ISSRP).** Sept grades (Éducateurs spécialisés, Conseillers de
l'Education) touchent 45 % si `ORIGINE_STATUTAIRE = ENSEIGNANT`, 30 % sinon. La
recommandation initiale proposait de résoudre le cas `ORIGINE_STATUTAIRE = INCONNU` (donnée
non renseignée) en repliant sur le taux plancher (30 %) via un opérateur `<> ENSEIGNANT` —
un choix pragmatique, mais qui **invente un droit** (30 %) à partir d'une information
absente. L'utilisateur a rejeté cette recommandation explicitement.

**Q-08 (enseignants contractuels).** Deux sources se sont avérées contradictoires sur
l'éligibilité ISSRP des grades contractuels 130-132 : la réponse orale de l'utilisateur
(« les contractuels touchent les mêmes indemnités que les titulaires ») contre
`Reglementation/elements_paie_historique_14726/ISSRP_Regles_Metier.md` (exclusion
`CONTRACTUEL`/`VACATAIRE` documentée, verrouillée par 97 tests dans le système source). La
question a été posée explicitement plutôt que tranchée par hypothèse — et a ensuite été
résolue par un troisième document (arrêté 6 primes contractuels) qui a permis de réconcilier
les deux sources sans qu'aucune n'ait eu tort dans son propre périmètre.

Dans les deux cas, la voie évitée était la même : **combler un manque d'information ou un
conflit de sources par une hypothèse plausible**, plutôt que par une donnée démontrable ou
un silence explicite. L'utilisateur a formulé le principe correspondant :

> *« En cas d'information réglementaire incomplète, absente ou ambiguë, le système ne doit
> jamais inventer, extrapoler ou déduire un droit. Il doit soit appliquer une règle
> explicitement démontrable, soit s'abstenir et produire un avertissement explicable,
> toujours non bloquant. Je considère désormais ce principe comme une règle d'architecture
> du projet. »*

## Décision

1. **Aucun droit n'est jamais inventé, extrapolé ou déduit** d'une information
   réglementaire incomplète, absente ou ambiguë — que ce soit dans le moteur de calcul,
   les moteurs de suggestion/avertissement (module d'affectation), ou tout outillage de
   seed (ex. `CsvCascadeParser`, `ReglementaireSeeder`).
2. **Deux issues seulement sont admises** face à une information manquante ou ambiguë :
   appliquer une **règle explicitement démontrable** (texte réglementaire, donnée
   vérifiée) ; ou **s'abstenir** — ne produire aucun montant, aucune suggestion, aucun
   taux par défaut — et émettre un **avertissement explicable**, décrivant précisément ce
   qui manque.
3. **L'abstention n'est jamais bloquante.** Elle ne doit interrompre ni le calcul, ni la
   saisie, ni aucune action utilisateur (cohérent avec le principe C.1 du module
   d'affectation : « le logiciel conseille, l'utilisateur décide »). Un montant absent ou
   un opérateur refusant de conclure n'est pas une erreur système — c'est une donnée
   manquante signalée, jamais une exception qui casse le flux.
4. **Un conflit entre deux sources documentaires est un signal d'arrêt**, pas un signal de
   choix arbitraire. Il déclenche une question STOP & ASK présentant les deux sources et
   leur contenu exact — jamais une préférence tacite pour l'une sur l'autre sans
   justification traçable.
5. **Ce principe prime sur toute recommandation de commodité.** Une option plus simple à
   implémenter mais qui comble un manque d'information par une hypothèse reste rejetée,
   même si le coût de l'abstention est un périmètre V1 réduit (cf. Q-C1 : les 7 grades
   restent sans taux si l'origine est inconnue, malgré l'inconfort d'un agent sans
   indemnité pendant que sa situation est clarifiée).

## Justification

- **Une hypothèse non validée introduite dans un calcul de paie est une faute grave**
  (protocole STOP & ASK, `docs/prompts/PROMPT_MOTEUR_AFFECTATION_RUBRIQUES.md` § F) : ce
  principe est la traduction opérationnelle de cette règle déjà actée, appliquée
  spécifiquement au cas de l'information manquante ou contradictoire, qui n'était pas
  couvert explicitement jusqu'ici.
- **Le cas Q-C1 aurait produit une erreur silencieuse et systématique** : replier sur 30 %
  par défaut aurait payé un taux à des agents dont l'origine statutaire n'est simplement
  pas encore saisie — une erreur qui ne se serait jamais signalée d'elle-même, contrairement
  à une exception ou un blocage.
- **Le cas Q-08 démontre que deux sources réputées fiables peuvent se contredire
  légitimement** (chacune correcte dans son périmètre — enseignants contractuels vs
  personnel administratif contractuel générique). Choisir l'une sans les confronter
  explicitement aurait eu 50 % de chances de payer une population entière au mauvais taux,
  dans un sens ou dans l'autre.
- **L'abstention non bloquante est cohérente avec l'architecture déjà posée** :
  `RegleEligibiliteEvaluator` traite déjà un critère non résolu comme « condition non
  satisfaite + diagnostic », jamais comme une exception (§ 7.1 J4E, contrat
  d'explicabilité) ; ce principe généralise ce comportement existant en règle
  d'architecture explicite plutôt que de le laisser comme un choix d'implémentation isolé.

## Alternatives considérées

- **Valeur par défaut sûre systématique** (ex. toujours le taux le plus bas en cas
  d'ambiguïté) : rejeté — reste une hypothèse, seulement moins coûteuse en apparence ; le
  taux le plus bas n'est pas plus « démontrable » que le plus haut, il est juste moins
  visible quand il est faux.
- **Blocage strict tant que l'information manque** (empêcher le calcul ou l'affectation) :
  rejeté — contredit le principe C.1 du module d'affectation (jamais de blocage) et
  transformerait une donnée manquante en incident opérationnel bloquant, alors qu'un
  avertissement explicable suffit à alerter sans paralyser.
- **Décision au cas par cas sans règle générale** : rejeté par l'utilisateur — l'objectif
  explicite était d'élever la leçon des cas Q-C1/Q-08 en règle transversale, pas de la
  cantonner à ces deux décisions ponctuelles.
- **Préférence par défaut pour la source la plus récente ou la plus détaillée** en cas de
  conflit documentaire : rejeté — aurait résolu Q-08 dans le mauvais sens si le troisième
  document (arrêté) n'avait pas été trouvé ; la fraîcheur ou le niveau de détail d'un
  document n'est pas une preuve de son exactitude pour la population concernée.

## Conséquences

- **Tout moteur de suggestion/avertissement** (lot 3, Phase 5) doit exposer une voie
  d'abstention explicite dans son contrat de sortie — pas seulement « éligible » /
  « non éligible », mais une troisième valeur « donnée manquante », déjà portée par
  `ExplicationCondition.Detail` (J4E § 7.1) et à préserver dans `SuggestionEngine`/
  `AvertissementEngine`.
- **Tout outillage de seed** (`ReglementaireSeeder`, `CsvCascadeParser`, seeders futurs)
  doit signaler explicitement — jamais silencieusement — toute ligne exclue faute de
  donnée exploitable (cf. Q-C3 : 4 grades HC-S1/HC-S2 exclus du seed ISSRP, documentés en
  J4F § 3, jamais comblés par une valeur inventée).
- **Le protocole STOP & ASK** (déjà en vigueur) est renforcé sur un point précis : un
  conflit entre deux sources documentaires **doit** être présenté comme tel à
  l'utilisateur, avec le contenu exact des deux sources, jamais résumé en une
  recommandation unique qui masquerait le désaccord.
- **Revue rétroactive recommandée, non engagée** : les résolutions de critère « non
  résolu » déjà en place ailleurs dans le projet (ex. `AgentAttributs` J3H § 4, valeurs
  par défaut documentées comme `INCONNU`/`0`/`STANDARD`) devraient être réexaminées à la
  lumière de ce principe au lot 3 — certaines de ces valeurs par défaut pourraient
  constituer le même type de droit implicitement déduit que celui rejeté en Q-C1. Ce
  réexamen n'est pas fait par cette ADR, il est signalé comme suite à donner.

## Action immédiate

Aucune action de code immédiate hors ce qui est déjà livré (Q-C1, Q-08). Le réexamen des
valeurs par défaut de `AgentAttributs` (J3H § 4) sous ce prisme est à porter au lot 3
(Phase 5) lors de la conception des use cases d'affectation.
