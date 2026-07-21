using PaieEducation.Domain.Agents.Repositories;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Time;

namespace PaieEducation.Application.Agents.UseCases;

/// <summary>
/// Définit une nouvelle valeur versionnée pour un attribut d'agent
/// (<c>AgentAttributs</c> — ex. <c>NOTATION_AGENT</c>, <c>ORIGINE_STATUTAIRE</c>,
/// <c>ANCIENNETE_PRIVEE_ANNEES</c>, chantier « gestion des agents »). Ces
/// attributs pilotent directement le moteur de calcul (PAPP, éligibilité
/// ISSRP) mais n'avaient jusqu'ici aucun chemin d'écriture applicatif (seed/SQL
/// uniquement). <see cref="Demande.Valeur"/> reste en saisie libre — même
/// convention que <c>NouvelleRegleValeur</c> dans <c>FicheRubriqueViewModel</c>
/// (les valeurs réglementaires possibles, ex. <c>ENSEIGNANT</c>/<c>AUTRE</c>,
/// sont des données de seed externalisées, pas un enum figé côté UI).
/// </summary>
public sealed class DefinirAttributAgent
{
    public sealed record Demande(string AgentId, string Attribut, string Valeur, string DateEffet, string? Source = null);

    private readonly IAgentRepository _agents;
    private readonly IClock _clock;

    public DefinirAttributAgent(IAgentRepository agents, IClock clock)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<string>> ExecuterAsync(Demande demande, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(demande);
        return await _agents.DefinirAttributAsync(
            demande.AgentId, demande.Attribut, demande.Valeur, demande.DateEffet, demande.Source, _clock.UtcNow, ct);
    }
}
