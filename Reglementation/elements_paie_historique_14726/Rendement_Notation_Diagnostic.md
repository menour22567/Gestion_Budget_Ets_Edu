# Diagnostic du taux de rendement

## Objectif

Clarifier la signification de `TauxApplique = 0` dans le calcul des primes de rendement (PAPP et tout futur calculateur s'appuyant sur `DALNotation`).

Avant ce ticket, cinq causes très différentes produisaient toutes le même résultat `TauxApplique = 0` sans signal exploitable côté appelant :

| Cause | Avant | Risque |
|---|---|---|
| Paramètres invalides (`idEmploye ≤ 0` ou `idPeriode ≤ 0`) | `TauxApplique=0`, `Motif=NOTE_ABSENTE` | confondu avec une absence de notation |
| Aucune ligne dans `T_Notations_Employes` | `TauxApplique=0`, `Motif=NOTE_ABSENTE` | légitime, mais indistinct |
| Ligne présente mais `Statut <> CLOTURE` | `TauxApplique=0`, `Motif=NOTE_NON_CLOTUREE` | légèrement mieux, mais regroupe SAISIE/BROUILLON sans nuance |
| Note clôturée, note = 0 → taux = 0 par règle métier | `TauxApplique=0`, `Motif=OK_NOTE_CLOTUREE` | OK_NOTE_CLOTUREE est ambigu (0 OK ou 0 anormal ?) |
| Exception SQL/JET interceptée | `TauxApplique=0`, `Motif=NOTE_ABSENTE` | **dangereux** : une erreur technique se déguise en absence de notation |

## États possibles

Le résultat `DALNotation.ResultatTauxRendement` expose désormais une propriété `Statut As StatutTauxRendement` aux valeurs :

| Statut | Sens | TauxApplique | EstBloquant | Motif (string, rétro-compatible) |
|---|---|---|---|---|
| `TauxApplique` | Note clôturée, taux > 0 (éventuellement plafonné) | `> 0` | `False` | `OK_NOTE_CLOTUREE` ou `TAUX_PLAFONNE` |
| `ZeroLegitime` | Note clôturée, taux = 0 par règle métier (note = 0 ou plafond réglementaire = 0) | `0` | `False` | `OK_NOTE_CLOTUREE` ou `TAUX_PLAFONNE` |
| `NoteAbsente` | Aucune ligne dans `T_Notations_Employes` pour (employé, période) | `0` | `True` | `NOTE_ABSENTE` |
| `NoteNonCloturee` | Notation présente, statut ≠ `CLOTURE` (SAISIE, BROUILLON, autre) | `0` | `True` | `NOTE_NON_CLOTUREE` |
| `ErreurTechnique` | Exception interceptée pendant le calcul. `MessageUtilisateur` contient le détail technique | `0` | `True` | `ERREUR_TECHNIQUE` |
| `IndetermineTechnique` | Paramètres d'entrée invalides (`idEmploye ≤ 0`, `idPeriode ≤ 0`, etc.). État de défaut tant qu'aucun chemin de résolution n'a tranché | `0` | `True` | `INDETERMINE_TECHNIQUE` |

## Règle importante

**Un taux nul n'est pas toujours une absence de droit.** Il peut aussi provenir de :
- une absence de notation (responsabilité opérationnelle : il faut saisir la note),
- une notation non encore clôturée (responsabilité workflow),
- une notation clôturée à 0 (responsabilité métier : l'agent a vraiment 0 — pas de prime),
- un incident technique (responsabilité IT/infra).

La nouvelle propriété `Statut` permet à l'appelant de **décider quoi faire** :
- `ZeroLegitime` : ne rien afficher d'alarmant, le 0 est la bonne réponse.
- `NoteAbsente` / `NoteNonCloturee` : alerter le responsable du dossier (déjà fait via `b.AlertesCalcul` dans `ModCalculPaie`).
- `ErreurTechnique` : remonter l'incident technique sans payer.
- `IndetermineTechnique` : bug logiciel, à investiguer.

## Compatibilité

Ce ticket **ne modifie pas encore** :
- la formule SQL PAPP (`[TP]*[TAUX_RENDEMENT]` en base, inchangée).
- le calcul de PAPP/PAPG/PRP/RND/PAP_PM dans le moteur (`ModCalculPaie.CalculerBulletinComplet` lit toujours `c.TauxRendement = diagnostic.TauxApplique`).
- les règles d'éligibilité (`T_Regles_Eligibilite_Indemnite` intacte).
- les constantes string `Motif*` exposées par `DALNotation` : elles restent **rétro-compatibles** pour les consommateurs existants (notamment `BatchCalculPaie.vb` qui agrège par `Motif`).

Ajouts non-bloquants :
- nouvelle propriété `Statut` (énumération typée).
- nouvelles constantes `MotifZeroLegitime`, `MotifErreurTechnique`, `MotifIndetermineTechnique` et messages associés.
- `MessageUtilisateur` enrichi sur `ErreurTechnique` : il contient désormais le détail technique de l'exception.

**Changement comportemental volontaire** (le seul de ce ticket) :
- avant : exception SQL interceptée → `Motif = NOTE_ABSENTE`.
- après : exception SQL interceptée → `Motif = ERREUR_TECHNIQUE` + `Statut = ErreurTechnique` + détail dans `MessageUtilisateur`.
  - `EstBloquant` reste à `True`, `TauxApplique` reste à `0` : le bulletin **continue d'être produit** comme avant (avec la prime à 0 et l'alerte non bloquante), mais l'incident est désormais identifiable dans le journal et les rapports batch.

## Suite prévue

Le futur **ticket A — Pilote PAPP typé** s'appuiera sur ce diagnostic pour décider comment traiter les cas bloquants (`EstBloquant = True` avec `Statut = NoteAbsente` / `NoteNonCloturee` / `ErreurTechnique` / `IndetermineTechnique`) — par exemple :
- politique permissive (comportement actuel) : prime à 0 + alerte non bloquante ;
- politique stricte : fail-fast côté moteur si `Statut ∈ {ErreurTechnique, IndetermineTechnique}`.

Cette décision sera prise dans le ticket dédié, pas ici. Le présent ticket se limite à **rendre la cause observable et testée**.

## Tests dédiés

Voir [`PaieEducation/Tests/DALNotationTests.vb`](../PaieEducation/Tests/DALNotationTests.vb) — 11 tests verrouillent la sémantique :

| Test | Cas couvert |
|---|---|
| `DALN00` | Paramètres invalides → `IndetermineTechnique` |
| `DALN01` | Notation clôturée, taux positif → `TauxApplique` |
| `DALN02` | Notation clôturée, note = 0 → `ZeroLegitime` (≠ NoteAbsente) |
| `DALN03` | Aucune notation → `NoteAbsente`, Motif rétro-compatible |
| `DALN04` | Notation `SAISIE` → `NoteNonCloturee` |
| `DALN05` | Notation `BROUILLON` → `NoteNonCloturee` |
| `DALN06` | Connexion fermée → exception → `ErreurTechnique` + détail technique dans le message |
| `DALN07` | **Verrou central** : 3 sources de `TauxApplique=0` ⇒ 3 `Statut` distincts |
| `DALN08` | `TauxApplique` reste une fraction décimale (0..1), pas un pourcentage |
| `DALN09` | `MessageUtilisateur` contient une cause lisible pour chaque état d'échec |
| `DALN10` | Non-régression : la formule PAPP en base n'est pas touchée |

### Déterminisme des tests d'erreur technique

`DALN06` force une exception technique en passant une connexion `Dispose()` à
`DALNotation`. Ce test est **déterministe et sans effet de bord** :
`ModConnexion.GetConnexion()` retourne une `OleDbConnection` jamais ouverte, et le
pooling OleDb est désactivé (`OLE DB Services=-4`). Disposer une connexion non
ouverte ne pose aucun verrou JET et ne laisse aucun état résiduel pouvant
perturber les tests suivants — aucun changement n'est nécessaire.

Côté PAPP, le test équivalent (`PAPP20`) n'utilise plus de connexion `Dispose()` :
il injecte directement un diagnostic `ErreurTechnique` préconstruit dans le
contexte, ce qui rend la vérification de la politique du calculateur totalement
déterministe et indépendante de l'état de la base.
