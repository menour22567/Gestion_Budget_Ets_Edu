using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Chantier P5 (audit du 19/07/2026, éditeur de barèmes) : définit une
/// nouvelle valeur de barème pour une tranche
/// <c>(rubriqueId, dimension, borneInf)</c> à compter d'une date d'effet.
/// Même patron que <see cref="PaieEducation.Application.Referentiels.UseCases.DefinirIndiceMinGrille"/> —
/// enveloppe mince du port d'écriture.
/// </summary>
public sealed class DefinirValeurBareme
{
    /// <summary>
    /// Nouvelle valeur de barème pour la tranche
    /// <c>(RubriqueId, Dimension, BorneInf)</c> à compter de
    /// <see cref="DateEffet"/>. <see cref="BorneSup"/> = <c>null</c> signifie
    /// « +infini ».
    /// </summary>
    public sealed record Demande(
        string RubriqueId,
        string Dimension,
        string BorneInf,
        string? BorneSup,
        string TypeValeur,
        string Valeur,
        string DateEffet,
        string? Source = null);

    private readonly IRubriqueBaremeRepository _baremes;
    private readonly IClock _clock;

    public DefinirValeurBareme(IRubriqueBaremeRepository baremes, IClock clock)
    {
        _baremes = baremes ?? throw new ArgumentNullException(nameof(baremes));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        Guard.AgainstNullOrWhiteSpace(demande.RubriqueId);
        Guard.AgainstNullOrWhiteSpace(demande.Dimension);
        Guard.AgainstNullOrWhiteSpace(demande.BorneInf);
        Guard.AgainstNullOrWhiteSpace(demande.TypeValeur);
        Guard.AgainstNullOrWhiteSpace(demande.Valeur);
        Guard.AgainstNullOrWhiteSpace(demande.DateEffet);

        return await _baremes.DefinirValeurBaremeAsync(
            demande.RubriqueId, demande.Dimension, demande.BorneInf, demande.BorneSup,
            demande.TypeValeur, demande.Valeur, demande.DateEffet, demande.Source, _clock.UtcNow, ct);
    }
}
