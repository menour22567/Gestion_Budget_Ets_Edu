using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Port de lecture d'un paramètre de rubrique (table <c>RubriqueParametres</c>)
/// à une date d'effet donnée. Lot 1.2 — sert au calculateur
/// <c>CONSTANTE_REGLEMENTAIRE</c> pour résoudre un taux/plafond/borne
/// réglementaire depuis la base (zéro hardcoding).
/// </summary>
/// <remarks>
/// La <c>Cle</c> n'est pas unique dans <c>RubriqueParametres</c> (un même
/// code, ex. <c>TAUX_45</c>, peut servir plusieurs rubriques). Lot 1.2 V1 :
/// le contexte de rubrique n'est pas propagé jusqu'au calculateur (le pipeline
/// le fera dans un chantier ultérieur), donc le lookup prend la version la
/// plus récente toutes rubriques confondues. Une ambiguïté est signalée par
/// <see cref="Result{T}"/> échec — le contrat est explicite, pas un 0 muet.
/// </remarks>
public interface IRubriqueParametreLookup
{
    /// <summary>
    /// Lit la valeur d'un paramètre identifié par <paramref name="cle"/> à la
    /// date d'effet <paramref name="dateEffet"/> (résolution point-in-time :
    /// dernière version en vigueur).
    /// </summary>
    /// <returns>
    /// <see cref="Result{T}"/> succès avec la valeur décimale, ou
    /// <see cref="Error.NotFound"/> si aucune version n'est en vigueur.
    /// </returns>
    Task<Result<decimal>> LireParametreAsync(string cle, string dateEffet, CancellationToken ct = default);
}
