# Plan d'implementation - PaieEducation ERP

> Source officielle: `docs/PLAN_ACTION.md`. Audit de reference: `docs/audit/DOCUMENTATION_TECHNIQUE_REFERENCE.md`. Ce plan couvre uniquement les ecarts constates le 18/07/2026. Contexte: projet hors production, application mono-utilisateur, aucune contrainte de migration ascendante ni conservation obligatoire de donnees existantes.

## Vue d'ensemble

| Chantier | Titre | Priorite | Dependances | Ecart couvert |
|---|---|---|---|---|
| 1 | Verrouillage du zero hardcoding et des sources de valeur | Critique | Aucune | Parametres/sources incomplets, valeurs seed C# |
| 2 | Finalisation du moteur de calcul pilote | Critique | Chantier 1 | Dependances rubriques, validation metier, rappels |
| 3 | Completion du Workbench reglementaire | Haute | Chantiers 1-2 | D7/D8/D11 incomplets |
| 4 | Completion Presentation WPF | Haute | Chantier 3 | UI partielle, controles manquants |
| 5 | Completion Reporting et documents officiels | Haute | Chantiers 2-4 | Phase 7 partielle |
| 6 | Validation reglementaire, qualite et performance | Haute | Chantiers 1-5 | Phase 8 partielle |
| 7 | Deploiement, exploitation et documentation finale | Moyenne | Chantier 6 | Phase 9 absente |
| 8 | Extension aux autres corps | Faible | Chantier 7 | Phase 10 absente |

## Chantier 1 - Verrouillage du zero hardcoding et des sources de valeur

- Objectif :
  Eliminer les ecarts critiques au principe "parametrage en base, zero hardcoding" et rendre toutes les sources de valeur necessaires exploitables sans recompilation du moteur.
- Perimetre fonctionnel et technique :
  Parametres systeme, sources de valeur Workbench, seed reglementaire, lecture point-in-time.
- Prerequis et dependances :
  Aucun.
- Composants concernes :
  `src/PaieEducation.Application/Payroll/UseCases/CalculerBulletin.cs`, `src/PaieEducation.Domain/Workbench/Calculators/SourceValeurCalculators.cs`, `src/PaieEducation.Seeding/Seeding/*`, `src/PaieEducation.Infrastructure/Repositories/Payroll/*`.

### Lot 1.1 - Rendre obligatoires les parametres de calcul critiques

- Taches detaillees :
  - [ ] Confirmer que `BASE_PAPP`, `NOTE_MAX_PAPP`, `SEUIL_EXONERATION_IRG`, `PLAFOND_LISSAGE_GENERAL` et `ARRONDI_MODE` sont seeds dans `Parametres`.
  - [ ] Supprimer tout fallback silencieux susceptible de masquer une configuration absente ou invalide.
  - [ ] Transformer les absences en `Error.NotFound` explicites dans les use cases de calcul.
  - [ ] Ajouter des tests d'integration: parametre absent, parametre corrompu, parametre versionne par date.
- Criteres de validation et d'acceptation :
  `dotnet test` vert; calcul impossible si un parametre obligatoire est absent; message d'erreur exploitable par l'UI.
- Risques identifies :
  Des tests existants peuvent dependre de valeurs implicites.
- Priorite : Critique
- Complexite estimee : Moyenne

### Lot 1.2 - Brancher les sources de valeur non resolues

- Taches detaillees :
  - [ ] Implementer `INDICE_ECHELON` depuis `GrilleIndiciaire` / `IndicesEchelon` ou le snapshot agent, sans confusion avec le numero d'echelon.
  - [ ] Implementer `CONSTANTE_REGLEMENTAIRE` depuis `RubriqueParametres` a la date de paie.
  - [ ] Remplacer le `0` de `ANCIENNETE_PRIVEE` par une lecture `AgentAttributs` versionnee.
  - [ ] Couvrir les echecs par tests unitaires et integration.
- Criteres de validation et d'acceptation :
  Plus aucun message "non resolu en V1" pour une source requise par le pilote; abstention seulement quand la donnee agent est legitimement absente.
- Risques identifies :
  Ambiguite entre indice, echelon et anciennete; necessite de conserver les erreurs explicites.
- Priorite : Critique
- Complexite estimee : Elevee

### Lot 1.3 - Externaliser progressivement les seeds reglementaires

- Taches detaillees :
  - [ ] Inventorier les valeurs reglementaires actuellement codees dans `IrgSeeder`, `FormulesSeeder`, `ReglementaireSeeder`.
  - [ ] Creer un format de seed donnees versionne (CSV/JSON/SQL controle) pour IRG, rubriques, formules, baremes et parametres.
  - [ ] Adapter les seeders pour lire ces fichiers au lieu de porter les valeurs metier en C#.
  - [ ] Conserver `Source`, `Hash`, `DateEffet`, `DateFin` dans chaque insertion.
  - [ ] Ajouter un test d'idempotence et un test de drift hash.
- Criteres de validation et d'acceptation :
  Les valeurs de tranches IRG, taux, forfaits, formules et parametres ne sont plus definies dans le code C# hors identifiants techniques.
- Risques identifies :
  Migration mecanique volumineuse; risque d'erreur d'encodage sur sources francaises/arabe.
- Priorite : Haute
- Complexite estimee : Elevee

## Chantier 2 - Finalisation du moteur de calcul pilote

- Objectif :
  Fermer les ecarts Phase 4: dependances entre rubriques, exactitude du calcul pilote, rappels et validation par scenarios complets.
- Perimetre fonctionnel et technique :
  `PayrollReadRepository`, `CalculationPipeline`, IRG, cotisations, rappels, snapshot.
- Prerequis et dependances :
  Chantier 1.
- Composants concernes :
  `src/PaieEducation.Domain/Calcul/Pipeline/CalculationPipeline.cs`, `src/PaieEducation.Infrastructure/Repositories/Payroll/PayrollReadRepository.cs`, `src/PaieEducation.Domain/Calcul/Rappels/RappelCalculator.cs`, tests calcul/integration.

### Lot 2.1 - Charger et appliquer `RubriqueDependances`

- Taches detaillees :
  - [ ] Etendre `PayrollInput` pour porter les dependances actives a la date de paie.
  - [ ] Charger `RubriqueDependances` dans `PayrollReadRepository`.
  - [ ] Remplacer `Array.Empty<DependanceArete>()` dans `CalculationPipeline` par les dependances chargees.
  - [ ] Tester ordre nominal, dependance absente, cycle, dependance expiree.
- Criteres de validation et d'acceptation :
  Une rubrique dependant d'une autre est calculee apres elle; un cycle echoue explicitement.
- Risques identifies :
  Peut modifier l'ordre actuel des lignes et donc les snapshots attendus.
- Priorite : Critique
- Complexite estimee : Moyenne

### Lot 2.2 - Stabiliser le bulletin pilote enseignant

- Taches detaillees :
  - [ ] Formaliser un jeu de cas pilote: agent avec note, sans note, ISSRP direct, ISSRP origine, cotisations, IRG 2022.
  - [ ] Verifier toutes les lignes de bulletin et pas seulement le net.
  - [ ] Ajouter tests de non-regression des explications et du journal d'audit.
  - [ ] Documenter les hypotheses restantes dans `docs/analysis`.
- Criteres de validation et d'acceptation :
  Les tests de bout en bout prouvent chaque rubrique du pilote, avec explication et audit.
- Risques identifies :
  Les montants actuels peuvent changer apres Lot 1.2.
- Priorite : Haute
- Complexite estimee : Moyenne

### Lot 2.3 - Completer les rappels retroactifs

- Taches detaillees :
  - [ ] Relier les rappels generes a un scenario d'evolution reglementaire complet.
  - [ ] Garantir qu'un bulletin valide n'est jamais modifie.
  - [ ] Ajouter tests: nouvelle version retroactive, delta positif, delta negatif, absence d'impact.
  - [ ] Preparar la restitution UI/reporting des lignes de rappel.
- Criteres de validation et d'acceptation :
  Une evolution retroactive produit uniquement des lignes `Rappels`; les snapshots de bulletins existants restent byte-stables ou semantiquement identiques.
- Risques identifies :
  Couplage avec le simulateur Workbench du Chantier 3.
- Priorite : Haute
- Complexite estimee : Moyenne

## Chantier 3 - Completion du Workbench reglementaire

- Objectif :
  Rendre le Workbench conforme a D7/D8/D11: edition complete, dry-run fiable, audit et matrice exploitable.
- Perimetre fonctionnel et technique :
  Use cases Workbench, repositories, UI Workbench, audit.
- Prerequis et dependances :
  Chantiers 1 et 2.
- Composants concernes :
  `Application/Workbench`, `Domain/Workbench`, `Infrastructure/Repositories/Workbench`, `Presentation/Workbench`.

### Lot 3.1 - Remplacer le simulateur placeholder par un calcul d'impact reel

- Taches detaillees :
  - [ ] Definir l'entree de simulation: rubrique, parametre/formule/bareme, periode, population agent.
  - [ ] Charger les agents impactes et recalculer avant/apres sans ecriture.
  - [ ] Produire `RapportImpact`: nombre agents, min, max, total, erreurs.
  - [ ] Bloquer `AppliquerEvolutionReglementaire` si le rapport est absent ou obsolete.
- Criteres de validation et d'acceptation :
  `SimulerEvolutionReglementaire` ne contient plus de placeholder; tests dry-run nominal, erreur et population vide.
- Risques identifies :
  Performance sur population large; necessite de ne pas ecrire en base.
- Priorite : Critique
- Complexite estimee : Elevee

### Lot 3.2 - Construire l'editeur de baremes

- Taches detaillees :
  - [ ] Creer use cases d'ecriture `RubriqueBaremes` avec continuite temporelle.
  - [ ] Ajouter garde-fous: pas de chevauchement, pas de trou, une seule periode ouverte par cle.
  - [ ] Ajouter UI WPF par dimension/categorie/borne/type.
  - [ ] Tester edition -> base -> recalcul.
- Criteres de validation et d'acceptation :
  L'utilisateur modifie un bareme sans recompilation; le calcul suivant consomme la nouvelle version.
- Risques identifies :
  Regles de continuite temporelle complexes.
- Priorite : Haute
- Complexite estimee : Elevee

### Lot 3.3 - Construire l'editeur DNF d'eligibilite

- Taches detaillees :
  - [ ] Creer use cases de gestion `GroupesEligibilite` et `ReglesEligibilite`.
  - [ ] Fournir UI: groupes OU, conditions ET, severite/message.
  - [ ] Ajouter validation de critere, operateur, valeur et periode.
  - [ ] Tester ISSRP 45/30/15 et origine statutaire.
- Criteres de validation et d'acceptation :
  Une regle DNF peut etre creee/fermee/versionnee puis verifiee par `SuggererRubriques`.
- Risques identifies :
  UI WPF complexe; erreurs de binding possibles.
- Priorite : Haute
- Complexite estimee : Elevee

### Lot 3.4 - Finaliser matrice de couverture et audit

- Taches detaillees :
  - [ ] Transformer la liste plate en matrice corps x rubriques avec etats vert/orange/rouge/gris.
  - [ ] Ajouter drill-down vers fiche rubrique et regle.
  - [ ] Ajouter filtres audit par acteur, action, entite, periode.
  - [ ] Introduire pagination ou chargement incremental d'`AuditLog`.
- Criteres de validation et d'acceptation :
  Matrice exploitable pour validation admin; audit consultable sans plafond arbitraire bloquant.
- Risques identifies :
  Volume UI et lisibilite; conserver MVVM strict.
- Priorite : Moyenne
- Complexite estimee : Moyenne

## Chantier 4 - Completion Presentation WPF

- Objectif :
  Transformer l'interface partielle en application utilisable de bout en bout par un gestionnaire de paie.
- Perimetre fonctionnel et technique :
  Shell, design system, controles, ecrans paie/referentiels/workbench.
- Prerequis et dependances :
  Chantier 3.
- Composants concernes :
  `src/PaieEducation.Presentation`.

### Lot 4.1 - Finaliser le design system metier

- Taches detaillees :
  - [ ] Stabiliser `MoneyTextBox`, `ERPDataGrid`, selectors metier et panneau explicabilite.
  - [ ] Remplacer les champs texte codes referentiels par selecteurs alimentes en base.
  - [ ] Uniformiser erreurs, chargements, etats vides et confirmations.
  - [ ] Ajouter tests ViewModel pour chaque etat critique.
- Criteres de validation et d'acceptation :
  Plus de saisie libre pour les referentiels fermes; erreurs coherentes sur tous les ecrans.
- Risques identifies :
  Retouches nombreuses mais localisees Presentation.
- Priorite : Haute
- Complexite estimee : Moyenne

### Lot 4.2 - Completer FormulaEditor

- Taches detaillees :
  - [ ] Ajouter coloration syntaxique, validation live, auto-completion des variables/fonctions/rubriques.
  - [ ] Ajouter simulation sur agent temoin.
  - [ ] Connecter la sauvegarde de formule existante.
  - [ ] Tester formule valide, invalide, variable inconnue, bareme absent.
- Criteres de validation et d'acceptation :
  Une formule invalide ne peut pas etre sauvegardee; une formule valide peut etre simulee avant commit.
- Risques identifies :
  Composant UI plus avance que les controles actuels.
- Priorite : Haute
- Complexite estimee : Elevee

### Lot 4.3 - Fermer les workflows paie

- Taches detaillees :
  - [ ] Ajouter parcours calcul -> validation -> consultation -> export.
  - [ ] Afficher explications structurées et audit detaille.
  - [ ] Gerer les rappels dans l'ecran bulletin.
  - [ ] Ajouter tests ViewModel des transitions.
- Criteres de validation et d'acceptation :
  Un agent pilote peut etre calcule, valide, consulte, exporte et justifie sans manipulation externe.
- Risques identifies :
  Depend de Reporting et Rappels.
- Priorite : Haute
- Complexite estimee : Moyenne

## Chantier 5 - Completion Reporting et documents officiels

- Objectif :
  Couvrir la Phase 7 au-dela du seul bulletin PDF/Excel.
- Perimetre fonctionnel et technique :
  QuestPDF, ClosedXML, modeles documentaires, rapport impact.
- Prerequis et dependances :
  Chantiers 2 et 4.
- Composants concernes :
  `src/PaieEducation.Reporting`, `src/PaieEducation.Presentation/Payroll`.

### Lot 5.1 - Durcir le bulletin PDF/Excel

- Taches detaillees :
  - [ ] Verifier contenu: agent, periode, lignes, rappels, totaux, mentions.
  - [ ] Ajouter tests de rendu basiques: fichier non vide, extension, contenu textuel/excel attendu.
  - [ ] Ajouter version de modele et source snapshot.
  - [ ] Documenter les champs non disponibles.
- Criteres de validation et d'acceptation :
  Bulletin exporte reproductible depuis un snapshot immuable.
- Risques identifies :
  Tests visuels limites en environnement local.
- Priorite : Haute
- Complexite estimee : Moyenne

### Lot 5.2 - Ajouter les documents officiels V1

- Taches detaillees :
  - [ ] Confirmer la liste V1: attestation salaire CNR, attestation travail, etats recap.
  - [ ] Definir DTO et templates QuestPDF/ClosedXML.
  - [ ] Ajouter use cases d'export et entrees UI.
  - [ ] Tester generation nominale.
- Criteres de validation et d'acceptation :
  Chaque document V1 a un template, un use case, un test et une entree utilisateur.
- Risques identifies :
  Q13 indique que la liste peut etre precisee apres mise en marche; besoin d'arbitrage utilisateur si incertain.
- Priorite : Moyenne
- Complexite estimee : Elevee

### Lot 5.3 - Exporter le rapport d'impact

- Taches detaillees :
  - [ ] Transformer `RapportImpact` en document PDF.
  - [ ] Inclure hypothese, population, deltas, erreurs, horodatage.
  - [ ] Archiver la reference dans `AuditLog` lors du commit.
  - [ ] Tester dry-run -> rapport -> application.
- Criteres de validation et d'acceptation :
  Aucune evolution reglementaire validee sans rapport consultable.
- Risques identifies :
  Depend directement du simulateur reel.
- Priorite : Haute
- Complexite estimee : Moyenne

## Chantier 6 - Validation reglementaire, qualite et performance

- Objectif :
  Passer de "tests techniques verts" a "conformite metier prouvee".
- Perimetre fonctionnel et technique :
  Tests unitaires, integration, reglementaires, performance, non-regression UI/documentaire.
- Prerequis et dependances :
  Chantiers 1 a 5.
- Composants concernes :
  `tests/**`, `docs/analysis`, fixtures bulletins reels.

### Lot 6.1 - Suite de validation sur bulletins reels

- Taches detaillees :
  - [ ] Collecter bulletins reels anonymises fournis par l'utilisateur.
  - [ ] Creer fixtures de reference: entrees, attendus ligne par ligne, net.
  - [ ] Ajouter tests de comparaison avec tolerances explicitement documentees.
  - [ ] Produire un rapport d'ecart par bulletin.
- Criteres de validation et d'acceptation :
  Chaque bulletin pilote est reproduit ou chaque ecart est documente/valide.
- Risques identifies :
  Donnees reelles indisponibles ou incompletes.
- Priorite : Critique
- Complexite estimee : Elevee

### Lot 6.2 - Tests de performance et charge locale

- Taches detaillees :
  - [ ] Mesurer calcul bulletin individuel.
  - [ ] Mesurer simulation 200 agents et lot 500 agents.
  - [ ] Mesurer matrice de couverture et audit log.
  - [ ] Ajouter seuils de non-regression.
- Criteres de validation et d'acceptation :
  Les seuils du plan sont mesures automatiquement et publies dans un rapport.
- Risques identifies :
  Variabilite machine; fixer des marges realistes.
- Priorite : Haute
- Complexite estimee : Moyenne

### Lot 6.3 - Couverture Workbench C-T1 a C-T6

- Taches detaillees :
  - [ ] Pour chaque pattern P1-P14, tester edition UI/use case -> base -> recalcul.
  - [ ] Tester dry-run delta, retroactif, rollback version non propagee.
  - [ ] Tester refus chevauchement/trou/double periode ouverte.
  - [ ] Ajouter rapport de couverture fonctionnelle.
- Criteres de validation et d'acceptation :
  Tous les criteres C-T1 a C-T6 de `docs/PLAN_ACTION.md` sont verts ou explicitement exclus.
- Risques identifies :
  Beaucoup de scenarios; prioriser ceux qui touchent le calcul.
- Priorite : Haute
- Complexite estimee : Elevee

## Chantier 7 - Deploiement, exploitation et documentation finale

- Objectif :
  Rendre l'application installable, sauvegardable et maintenable.
- Perimetre fonctionnel et technique :
  Packaging, initialisation base, sauvegarde/restauration, documentation utilisateur/technique.
- Prerequis et dependances :
  Chantier 6.
- Composants concernes :
  Bootstrapper, Persistence, docs, scripts build/release.

### Lot 7.1 - Packaging desktop

- Taches detaillees :
  - [ ] Choisir mode self-contained ou framework-dependent.
  - [ ] Produire profil de publication Windows.
  - [ ] Verifier lancement sur base vierge et base existante.
  - [ ] Documenter prerequis et emplacement base.
- Criteres de validation et d'acceptation :
  Installation/lancement reproductible sur machine Windows cible.
- Risques identifies :
  Dependances .NET 10/WPF et droits utilisateur.
- Priorite : Moyenne
- Complexite estimee : Moyenne

### Lot 7.2 - Sauvegarde, restauration et integrite

- Taches detaillees :
  - [ ] Ajouter fonction sauvegarde SQLite a froid ou transactionnellement coherente.
  - [ ] Ajouter restauration avec verification schema/hash.
  - [ ] Ajouter verification integrite et espace disque.
  - [ ] Tester sauvegarde/restauration sur base seedee.
- Criteres de validation et d'acceptation :
  Une base peut etre sauvegardee, restauree et reutilisee sans perte.
- Risques identifies :
  SQLite WAL et fichiers annexes a gerer correctement.
- Priorite : Haute
- Complexite estimee : Moyenne

### Lot 7.3 - Documentation d'exploitation

- Taches detaillees :
  - [ ] Rediger guide utilisateur paie.
  - [ ] Rediger procedure de mise a jour reglementaire par Workbench.
  - [ ] Rediger guide technique: architecture, DB, tests, release.
  - [ ] Mettre a jour `README.md`, `docs/PLAN_ACTION.md`, ADR si necessaire.
- Criteres de validation et d'acceptation :
  Un mainteneur peut installer, tester, seed, sauvegarder et mettre a jour la reglementation a partir de la documentation.
- Risques identifies :
  Documentation vite obsolete; lier aux tests et chemins reels.
- Priorite : Moyenne
- Complexite estimee : Moyenne

## Chantier 8 - Extension aux autres corps

- Objectif :
  Etendre le pilote enseignants aux autres corps sans modifier le moteur, par donnees et parametres.
- Perimetre fonctionnel et technique :
  Corps communs, ouvriers, contractuels, paramedicaux, direction, inspection, intendance, laboratoire.
- Prerequis et dependances :
  Chantier 7 et validation pilote.
- Composants concernes :
  Seeds externalises, Workbench, tests reglementaires, reporting.

### Lot 8.1 - Cadre d'extension par corps

- Taches detaillees :
  - [ ] Definir un dossier de specification par corps.
  - [ ] Mapper rubriques, baremes, eligibilites, sources et documents.
  - [ ] Ajouter fixtures de validation par corps.
  - [ ] Prioriser les corps selon besoin utilisateur.
- Criteres de validation et d'acceptation :
  Chaque corps suit le meme processus: donnees -> Workbench -> test bulletin -> validation.
- Risques identifies :
  Regles incompletes ou sources documentaires divergentes.
- Priorite : Faible
- Complexite estimee : Moyenne

### Lot 8.2 - Premiere extension hors enseignants

- Taches detaillees :
  - [ ] Choisir un corps candidat avec sources completes.
  - [ ] Ajouter uniquement donnees/parametres, pas de code moteur.
  - [ ] Tester calcul, eligibilite, reporting.
  - [ ] Documenter tout ecart au principe Open/Closed.
- Criteres de validation et d'acceptation :
  Un bulletin hors enseignants est calcule sans changement du pipeline.
- Risques identifies :
  Si un changement moteur est necessaire, le modele d'extension doit etre revu.
- Priorite : Faible
- Complexite estimee : Elevee

## Strategie de non-regression globale

- Executer `dotnet test PaieEducation.slnx -c Debug --no-restore` apres chaque lot.
- Ajouter au moins un test d'integration SQLite pour chaque modification de schema/repository.
- Ajouter un test Domain pur pour chaque regle de calcul.
- Ajouter un test ViewModel pour chaque commande UI nouvelle ou modifiee.
- Mettre a jour les snapshots/fixtures seulement avec justification dans la PR ou le journal de chantier.
- Ne pas executer de migration destructive sur une base utilisateur; le projet etant hors production, reconstruire les bases de test est autorise.
