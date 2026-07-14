# Glossaire métier — Langage omniprésent (Ubiquitous Language)

> Référence terminologique de PaieEducation ERP. Tous les noms de classes, propriétés,
> tables et rubriques reprennent ce vocabulaire. Toute évolution passe par ce fichier.

## Structure & carrière

| Terme | Définition |
|-------|-----------|
| **Agent** | Fonctionnaire ou agent contractuel de l'établissement (agrégat racine RH). |
| **Corps** | Ensemble de grades d'une même filière (ex. *Corps des Professeurs de l'Enseignement Moyen*). |
| **Grade** | Rang au sein d'un corps (ex. *Professeur principal de l'enseignement moyen*). |
| **Échelon** | Position d'avancement dans un grade (1 à 12). Détermine `indice_échelon`. |
| **Catégorie** | Niveau de classement (1 à 17 + Hors Catégorie HC-S1/S2) fixant l'`indice minimal`. |
| **Filière / Type_Filiere** | Grande famille : `ENSEIGNANT`, `ADMIN`, `INSPECTION`, `SANTE_PUBLIQUE`, `OUVRIERS_AGENTS`. |
| **Type de contrat** | `Statut_Fonctionnaire` ou `Regime_Contractuel`. |
| **Ancienneté** | Années de service effectif (sans déduction des interruptions — décision Q8). |

## Rémunération (base)

| Terme | Définition |
|-------|-----------|
| **Indice minimal** | Indice de la catégorie (dépend de la date d'effet : 2007 / 01-03-2022 / 01-01-2023 / 01-01-2024). |
| **Indice d'échelon** | Points additionnels liés à l'échelon détenu. |
| **Valeur du point (indiciaire)** | Multiplicateur monétaire. **45 DA** (paramétrable, versionné). |
| **Traitement de base** | `indice_minimal × valeur_point`. |
| **Traitement** | `(indice_minimal + indice_échelon) × valeur_point`. |

## Rubriques (éléments de paie)

| Code | Libellé | Base | Règle (pilote enseignants) |
|------|---------|------|----------------------------|
| **IEP** | Indemnité d'expérience pédagogique | Traitement de base | `4 % × n° échelon`, cumulée, sans plafond (fonctionnaires). |
| **PAPP** | Prime d'amélioration des performances pédagogiques | Traitement | 0–40 % (selon notation), servie trimestriellement. |
| **Ind. Qualification** | Indemnité de qualification | Traitement de base | 40 % (cat. ≤ 12) / 45 % (cat. ≥ 13). |
| **Ind. Documentation** | Indemnité de documentation pédagogique | Forfait | 2 000 / 2 500 / 3 000 DA selon catégorie. |
| **ISSRP** | Indemnité de soutien scolaire et remédiation pédagogique | Traitement | 45 % (groupe pédagogique élargi) / 30 % / 15 % selon corps. |
| **Cotisation SS** | Sécurité sociale / retraite (part ouvrière) | Assiette cotisable | Taux paramétrable (défaut 9 %). |
| **IRG** | Impôt sur le Revenu Global | Assiette imposable | Barème par tranches + règles de période (voir ci-dessous). |

> **ISSRP à 45 %** : groupe **élargi** à origine/responsabilité pédagogique (enseignants, directeurs,
> inspecteurs, censeurs, conseillers issus du corps enseignant, grades de promotion d'origine enseignante) —
> pas seulement les enseignants.

## Retenues facultatives (au choix de l'agent)

| Terme | Définition |
|-------|-----------|
| **Œuvres sociales** | Retenue temporaire/facultative à **montant fixe**, pilotée par l'agent. |
| **Mutuelle** | Cotisation mutualiste **facultative**. |

## Fiscalité (IRG)

| Terme | Définition |
|-------|-----------|
| **Assiette imposable** | Base soumise à l'IRG (rubriques marquées imposables − cotisations). Flag paramétrable par rubrique. |
| **Barème IRG 2008** | Tranches mensuelles : ≤10 000 → 0 % ; 10 001–30 000 → 20 % ; 30 001–120 000 → 30 % ; >120 000 → 35 %. |
| **Abattement** | 40 % de l'**IRG brut**, borné [1 000 ; 1 500 DA]. |
| **Exonération** | IRG nul si imposable ≤ 30 000 DA (périodes 2020+). |
| **Lissage** | Formules de raccord par période (2020, 2021, 2022+) via coefficients (`coefGeneral/constGeneral`, etc.). |
| **Profil handicapé/retraité RG** | Abattement spécial (`coefSpecial/constSpecial`, plafond dédié). |

## Paie & documents

| Terme | Définition |
|-------|-----------|
| **Période (de paie)** | Mois + année ; états : Ouverte, En calcul, Validée, Clôturée, Archivée. |
| **Bulletin** | Agrégat immuable et versionné, résultat certifié d'un calcul (le PDF n'en est qu'une représentation). |
| **Rappel / Régularisation** | Recalcul rétroactif à la **date d'effet réglementaire** (décision Q7). |
| **Explicabilité** | Justification traçable de chaque montant (base, taux, formule, arrondi). |
| **Arrondi** | Centralisé, uniforme (défaut : au dinar le plus proche ; paramétrable). |

## Devise

Tous les montants sont exprimés en **dinar algérien (DZD)** via l'objet valeur `Money`.
