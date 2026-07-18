using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Cotisations;

/// <summary>Référence d'assiette d'une cotisation (V005 <c>Cotisations.AssietteRef</c>).</summary>
public enum ReferenceAssiette
{
    AssietteCotisable,
    AssietteImposable,
    TraitementBase,
    TraitementBrut,
    MontantFixe
}

/// <summary>
/// Définition d'une cotisation résolue à la date de paie (V005). <see cref="Taux"/>
/// pour les cotisations proportionnelles (ex. SS 9 %), <see cref="MontantFixe"/>
/// pour les facultatives à montant fixe (mutuelle, œuvres sociales, Q3b).
/// </summary>
public sealed record CotisationDef(
    string Code,
    ReferenceAssiette Assiette,
    decimal? Taux,
    decimal? MontantFixe);

/// <summary>
/// Applique une cotisation : <c>assiette × taux</c> pour les cotisations
/// proportionnelles, montant fixe pour les facultatives. Pur — l'assiette
/// (somme des rubriques cotisables via <c>CotisationAssietteRubriques</c>) est
/// résolue par le pipeline et fournie en entrée.
/// </summary>
public sealed class ContributionCalculator
{
    /// <summary>
    /// Calcule le montant d'une cotisation. <paramref name="assietteResolue"/>
    /// est ignorée pour une cotisation à montant fixe.
    /// </summary>
    public Result<decimal> Calculer(CotisationDef cotisation, decimal assietteResolue)
    {
        ArgumentNullException.ThrowIfNull(cotisation);

        if (cotisation.Assiette == ReferenceAssiette.MontantFixe)
        {
            return cotisation.MontantFixe is { } fixe
                ? (fixe >= 0
                    ? Result.Success(fixe)
                    : Result.Failure<decimal>(Error.Validation($"Montant fixe négatif pour « {cotisation.Code} ».")))
                : Result.Failure<decimal>(Error.Validation(
                    $"Cotisation à montant fixe sans montant : « {cotisation.Code} »."));
        }

        if (cotisation.Taux is not { } taux)
            return Result.Failure<decimal>(Error.Validation(
                $"Cotisation proportionnelle sans taux : « {cotisation.Code} »."));
        if (taux < 0 || taux > 1)
            return Result.Failure<decimal>(Error.Validation(
                $"Taux hors [0 ; 1] pour « {cotisation.Code} » : {taux}."));
        if (assietteResolue < 0)
            return Result.Failure<decimal>(Error.Validation(
                $"Assiette négative pour « {cotisation.Code} »."));

        return Result.Success(assietteResolue * taux);
    }
}
