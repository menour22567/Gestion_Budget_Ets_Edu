using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Calcul.Repositories;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Infrastructure.Workbench.Calculators;

/// <summary>
/// Source : <c>CONSTANTE_REGLEMENTAIRE</c>. Lit un paramètre de rubrique
/// (taux, plafond, borne) depuis la table <c>RubriqueParametres</c> à la
/// date d'effet, via <see cref="IRubriqueParametreLookup"/>.
/// </summary>
/// <remarks>
/// Lot 1.2 — V1 : la <c>Cle</c> n'est pas unique dans
/// <c>RubriqueParametres</c> (un même code peut servir plusieurs rubriques)
/// et le contexte de rubrique n'est pas encore propagé jusqu'au
/// calculateur. Le lookup prend donc la version la plus récente toutes
/// rubriques confondues. Une ambiguïté est signalée par un échec explicite
/// — pas un 0 silencieux qui contaminerait les formules consommatrices.
/// <br/>
/// Évolution prévue : transmettre le contexte de rubrique (par le
/// pipeline) pour filtrer <c>RubriqueParametres</c> par
/// <c>(RubriqueId, Cle)</c> ; le port <see cref="IRubriqueParametreLookup"/>
/// gagnera alors un paramètre <c>rubriqueId</c>.
/// </remarks>
public sealed class ConstanteReglementaireCalculator : ISourceValeurCalculator
{
    private readonly IRubriqueParametreLookup _lookup;

    public ConstanteReglementaireCalculator(IRubriqueParametreLookup lookup)
        => _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));

    public string CodeSource => SourceValeurCodes.ConstanteReglementaire;

    public Result<object> Calculer(AgentContext agent, string datePaie)
    {
        // La Cle est portée par la source — pour la V1, on l'extrait du
        // CodeSource du calculator (CONSTANTE_REGLEMENTAIRE), ce qui signifie
        // que toutes les rubriques partagent la même Cle tant que le
        // contexte de rubrique n'est pas propagé. Le lookup sélectionne la
        // version la plus récente.
        var result = _lookup.LireParametreAsync(CodeSource, datePaie).GetAwaiter().GetResult();
        if (result.IsFailure)
            return Result.Failure<object>(result.Error);
        return Result.Success<object>(result.Value);
    }
}
