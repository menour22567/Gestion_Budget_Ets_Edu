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

    /// <summary>
    /// Renvoie la fiche détaillée d'un agent (identité + carrière la plus
    /// récente), ou <c>null</c> si l'agent n'existe pas. La résolution du
    /// « non trouvé » en erreur métier est laissée au use case appelant (même
    /// patron que <c>IWorkbenchReadRepository.ObtenirRubriqueAsync</c>).
    /// </summary>
    Task<AgentDetail?> ObtenirAsync(string agentId, CancellationToken ct = default);

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

/// <summary>
/// Fiche détaillée d'un agent : identité complète et carrière la plus récente
/// (dernière <c>DateEffet</c>). Les champs de carrière sont nullables — un
/// agent peut n'avoir aucune carrière (jamais via l'UI, qui crée les deux
/// ensemble), et Fonction/Établissement sont optionnels par nature.
/// </summary>
public sealed record AgentDetail(
    string Id,
    string Matricule,
    string Nom,
    string Prenom,
    string DateNaissance,
    string DateRecrutement,
    string Sexe,
    string SituationFamiliale,
    string Statut,
    string? GradeId,
    string? GradeLibelle,
    string? CorpsLibelle,
    int? CategorieNiveau,
    int? EchelonNumero,
    string? TypeContrat,
    string? FonctionLibelle,
    string? EtablissementNom,
    string? EtablissementType,
    string? CarriereDepuis,
    string? CarriereMotif,
    // Ajoutés pour le chantier « gestion des agents » (édition de carrière) —
    // Fonction/Établissement n'exposaient jusqu'ici que leur libellé (lecture
    // seule) ; l'Id brut de Carrieres est nécessaire pour préselectionner les
    // ComboBox du formulaire de nouvel événement de carrière. En fin de liste
    // positionnelle avec défaut null pour ne pas perturber les constructions
    // positionnelles existantes.
    string? FonctionId = null,
    string? EtablissementId = null)
{
    /// <summary>Libellé « Nom Prénom » pour l'en-tête de la fiche.</summary>
    public string NomComplet => $"{Nom} {Prenom}";
}

/// <summary>Élément de nomenclature (Id + Libelle), pour les listes fermées.</summary>
public sealed record NomenclatureItem(string Id, string Libelle);
