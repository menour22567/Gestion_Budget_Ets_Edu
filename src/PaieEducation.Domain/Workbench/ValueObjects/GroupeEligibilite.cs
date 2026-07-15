using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Internal;

namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// En-tête de groupe de conditions d'éligibilité (DNF, D5). Conditions d'un
/// groupe ETées, groupes OUés. Nature : règle réglementaire — audit complet.
/// V009 § 8ter.5.
/// </summary>
/// <remarks>
/// Sévérité présentation uniquement (D2) — jamais bloquante. <c>Priorite</c> est
/// l'ordre d'affichage des suggestions (≠ ordre d'application).
/// </remarks>
public sealed record GroupeEligibilite
{
    /// <summary>Code métier, ex. <c>'GE-ISSRP45-ORIGINE'</c>.</summary>
    public string Id { get; }

    /// <summary>Rubrique concernée (FK).</summary>
    public string RubriqueId { get; }

    /// <summary>Sévérité présentation (D2).</summary>
    public Severite Severite { get; }

    /// <summary>Code du message associé (FK vers <c>MessagesRegles</c>), nullable.</summary>
    public string? MessageId { get; }

    /// <summary>Ordre d'affichage (par défaut 100).</summary>
    public int Priorite { get; }

    /// <summary>Période de validité.</summary>
    public PeriodeReglementaire Periode { get; }

    /// <summary>Référence réglementaire.</summary>
    public string? Source { get; }

    private GroupeEligibilite(
        string id,
        string rubriqueId,
        Severite severite,
        string? messageId,
        int priorite,
        PeriodeReglementaire periode,
        string? source)
    {
        Id = id;
        RubriqueId = rubriqueId;
        Severite = severite;
        MessageId = messageId;
        Priorite = priorite;
        Periode = periode;
        Source = source;
    }

    /// <summary>Fabrique validante.</summary>
    public static GroupeEligibilite Creer(
        string id,
        string rubriqueId,
        Severite severite,
        string? messageId,
        int priorite,
        PeriodeReglementaire periode,
        string? source)
    {
        Guard.AgainstNullOrWhiteSpace(id);
        Guard.AgainstNullOrWhiteSpace(rubriqueId);
        if (priorite < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priorite), priorite, "Priorité ≥ 0.");
        }
        return new GroupeEligibilite(id, rubriqueId, severite, messageId, priorite, periode, source);
    }
}

