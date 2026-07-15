using PaieEducation.Domain.Common;

namespace PaieEducation.Domain.Calcul.Formules;

/// <summary>
/// Contexte de résolution d'une formule : fournit les valeurs des variables, des
/// barèmes et des sources de valeur au moment de l'évaluation. Interface pure du
/// Domain — l'implémentation concrète (pipeline de calcul) lit la base et la
/// carrière de l'agent, mais l'évaluateur ne connaît que ce contrat.
/// </summary>
public interface IFormulaContext
{
    /// <summary>
    /// Résout une variable (<c>TBASE</c>, <c>TRT</c>, <c>ECH</c>, un paramètre
    /// versionné, ...). Échec si la variable est inconnue — le moteur ne
    /// substitue jamais silencieusement 0 (un montant faux et muet est pire
    /// qu'une erreur explicite).
    /// </summary>
    Result<decimal> ResoudreVariable(string nom);

    /// <summary>
    /// Résout un barème indexé (<c>bareme(RUB, DIM)</c>) à la clé courante de
    /// l'agent pour la dimension demandée (catégorie, échelon, ...).
    /// </summary>
    Result<decimal> ResoudreBareme(string rubrique, string dimension);

    /// <summary>
    /// Résout une source de valeur (<c>valeurSource(RUB)</c>) — pattern P3, taux
    /// indexé sur une donnée externe (notation, ancienneté, ...). ADR-0007 D6.
    /// </summary>
    Result<decimal> ResoudreSource(string rubrique);
}
