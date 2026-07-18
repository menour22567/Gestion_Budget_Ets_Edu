# Lot 2.3 — Couverture des rappels rétroactifs (état au 18/07/2026)

> Source : `docs/audit/PLAN_IMPLEMENTATION.md` §2.3, critère
> "nouvelle version retroactive, delta positif, delta negatif,
> absence d'impact" + "un bulletin valide n'est jamais modifie".

## Verdict

**Lot 2.3 est fonctionnellement couvert** par les tests existants.
Aucune regression, aucun ajout de code de production. Seuls 2 ajouts
de tests ont ete necessaires pour fermer les 2 derniers angles morts.

## Couverture par scenario du plan

| Scenario | Test | Fichier |
|---|---|---|
| Nouvelle version retroactive | `Executer_apres_une_evolution_retroactive_genere_et_persiste_les_lignes_de_rappel` | `tests/.../UseCases/GenererRappelsTests.cs` |
| Delta positif (rubrique devenue eligible) | `Rubrique_nouvellement_eligible_produit_un_rappel_positif_depuis_zero` | `tests/.../Calcul/RappelCalculatorTests.cs` |
| Delta negatif (rubrique devenue ineligible) | `Rubrique_devenue_ineligible_produit_un_rappel_negatif_vers_zero` | `tests/.../Calcul/RappelCalculatorTests.cs` |
| Absence d'impact (memes montants) | `Montants_identiques_ne_produisent_aucun_rappel` (unitaire) + `Executer_sans_evolution_reglementaire_ne_genere_ni_ne_persiste_aucun_rappel` (integration) | `RappelCalculatorTests.cs` + `GenererRappelsTests.cs` |
| Bulletin valide non modifie (1/2) | `Executer_deux_fois_pour_le_meme_agent_et_la_meme_date_echoue_la_seconde_fois` | `tests/.../UseCases/ValiderBulletinTests.cs` |
| Bulletin valide non modifie (2/2) — *ajoute au Lot 2.3* | `Un_bulletin_valide_n_est_jamais_reecrit_apres_evolution_reglementaire` | `tests/.../UseCases/ValiderBulletinTests.cs` |

## Implementation sous-jacente

- `RappelCalculator.Calculer` (Domain/Calcul/Rappels) — primitive pure
  qui prend (snapshot ancien, nouveau bulletin) et retourne les
  `LigneRappel` (delta != 0). Le signe du delta est preserve tel quel
  : positif pour hausse, negatif pour baisse.
- `GenererRappels.ExecuterAsync` (Application/Payroll/UseCases) —
  orchestre la lecture du snapshot, le recalcul, la comparaison et la
  persistance en table `Rappels`. Refuse une deuxieme execution pour
  le meme bulletin (`Error.Conflict`) — ADR-0008.
- `BulletinRepository.ValiderAsync` (Infrastructure) — refuse une
  re-validation du meme couple (agent, date) avec `Error.Conflict`,
  avant meme d'INSERT. La cle `UNIQUE INDEX IX_Bulletins_Agent_DatePaie`
  reste un filet de securite au niveau base.
- `ValiderBulletin` (Application) — appel unique, refuse de re-valider
  (l'immutabilite est portee par le repository).

## Limites V1 (a traiter dans un futur chantier)

- **Restitution UI/reporting** : la sortie actuelle est un
  `IReadOnlyList<LigneRappel>`. Le plan mentionne "Preparar la
  restitution UI/reporting des lignes de rappel" — c'est du ressort
  du Chantier 4 (Presentation WPF) et 5 (Reporting). Non couvert
  par ce lot.
- **Multi-bulletins** : la memoire de phase note "un agent + un
  bulletin valide a la fois" (J3C §11). L'extension au cas N agents
  / M bulletins par lot n'est pas couverte.
- **Rejeu retroactif en cascade** : si l'evolution touche a la fois
  une rubrique et une condition d'eligibilite, le rappel genere
  reflete la nouvelle regle integralement. Pas de regle metier
  particuliere sur l'ordre d'application — le pipeline fait
  naturellement la derniere version de chaque variable.

## Decisions de design documentees

- ADR-0008 : l'immutabilite du bulletin valide est un invariant
  fondamental (preuve comptable). Une evolution retroactive
  n'ecrase JAMAIS un bulletin existant : elle produit des lignes
  de rappel qui s'ajoutent au bulletin futur.
- ADR-0009 : en cas d'absence de donnee agent legitime, le moteur
  s'abstient (pas de 0 silencieux). Les rappels peuvent donc
  apparaitre ou disparaitre au gre des attributs agents
  renseignes/retires.
