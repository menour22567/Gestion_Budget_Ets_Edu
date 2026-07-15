using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Domain.Workbench.Internal;

namespace PaieEducation.Domain.Workbench.Services;

/// <summary>
/// Implémentation par défaut de <see cref="ISourceValeurResolver"/>. Délègue au
/// calculateur enregistré pour le code de la source. Si aucun calculateur n'est
/// enregistré, renvoie un échec (catalogue absent) plutôt qu'une exception —
/// ADR-0007 D6 : l'échec métier est attendu, pas exceptionnel.
/// </summary>
public sealed class SourceValeurResolver : ISourceValeurResolver
{
    private readonly IReadOnlyDictionary<string, ISourceValeurCalculator> _calculators;

    /// <summary>
    /// Constructeur. <paramref name="calculators"/> indexé par code de source
    /// (ex. <c>"NOTATION_AGENT"</c>). La résolution se fait par lookup direct,
    /// O(1).
    /// </summary>
    public SourceValeurResolver(IReadOnlyDictionary<string, ISourceValeurCalculator> calculators)
    {
        ArgumentNullException.ThrowIfNull(calculators);
        _calculators = calculators;
    }

    /// <inheritdoc />
    public Result<object> Resoudre(SourceValeur source, AgentContext agent, string datePaie)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(datePaie);

        if (!_calculators.TryGetValue(source.Id, out var calculator) || calculator is null)
        {
            return Result.Failure<object>(Error.NotFound(
                $"Aucun calculateur enregistré pour la source '{source.Id}'."));
        }

        return calculator.Calculer(agent, datePaie);
    }
}

