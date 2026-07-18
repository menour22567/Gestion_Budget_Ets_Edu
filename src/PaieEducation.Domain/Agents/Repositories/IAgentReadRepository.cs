using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Agents.Repositories;

/// <summary>
/// Liste les agents existants (identité métier) pour l'affichage d'un sélecteur
/// (écran « Calculer », C2-UI.2). Port du Domain implémenté par
/// <c>Infrastructure.Repositories.Agents.AgentReadRepository</c> (I/O réelle —
/// inaccessible depuis <c>Application</c>).
/// </summary>
public interface IAgentReadRepository
{
    /// <summary>Renvoie tous les agents (Id + libellé), triés par matricule croissant.</summary>
    Task<Result<IReadOnlyList<AgentResume>>> ListerAsync(CancellationToken ct = default);

    /// <summary>Renvoie les sexes actifs (TypesSexe, V014).</summary>
    Task<Result<IReadOnlyList<NomenclatureItem>>> ListerSexesAsync(CancellationToken ct = default);

    /// <summary>Renvoie les situations familiales actives (SituationsFamiliales, V014).</summary>
    Task<Result<IReadOnlyList<NomenclatureItem>>> ListerSituationsFamilialesAsync(CancellationToken ct = default);

    /// <summary>Renvoie les types de contrat actifs (TypesContrat, V002).</summary>
    Task<Result<IReadOnlyList<NomenclatureItem>>> ListerTypesContratAsync(CancellationToken ct = default);
}

/// <summary>Identité synthétique d'un agent pour un sélecteur de l'UI.</summary>
public sealed record AgentResume(string Id, string Matricule, string Nom, string Prenom)
{
    /// <summary>Libellé complet affiché à l'utilisateur dans le sélecteur.</summary>
    public string Libelle => $"{Matricule} — {Nom} {Prenom}";
}

/// <summary>Élément de nomenclature (Id + Libelle), pour les listes fermées.</summary>
public sealed record NomenclatureItem(string Id, string Libelle);
