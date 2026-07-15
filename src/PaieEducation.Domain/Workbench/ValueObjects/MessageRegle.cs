using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Internal;

namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Message réglementaire — texte paraphrasant ou citant un décret, présenté à
/// l'utilisateur (sévérité ELIGIBILITE / AVERTISSEMENT / SUGGESTION). Nature :
/// texte réglementaire (R4 révisé) — audit complet préservé (Source +
/// DateEffet/Fin).
/// </summary>
/// <remarks>
/// V009 § 8ter.4. La <c>Source</c> est obligatoire à la création (référence
/// réglementaire). Le wording peut changer au gré des décrets ; la version
/// précédente reste consultable via la période (DateEffet/DateFin).
/// </remarks>
public sealed record MessageRegle
{
    /// <summary>Code métier, ex. <c>'MSG-ISSRP-45-INCONNU-ORIGINE'</c>.</summary>
    public string Id { get; }

    /// <summary>Catégorie du message.</summary>
    public MessageCategorie Categorie { get; }

    /// <summary>Texte en français (V1).</summary>
    public string TexteFr { get; }

    /// <summary>Texte en arabe (post-V1, nullable).</summary>
    public string? TexteAr { get; }

    /// <summary>Référence réglementaire (décret, arrêté) — obligatoire.</summary>
    public string Source { get; }

    /// <summary>Période de validité du wording.</summary>
    public PeriodeReglementaire Periode { get; }

    private MessageRegle(
        string id,
        MessageCategorie categorie,
        string texteFr,
        string? texteAr,
        string source,
        PeriodeReglementaire periode)
    {
        Id = id;
        Categorie = categorie;
        TexteFr = texteFr;
        TexteAr = texteAr;
        Source = source;
        Periode = periode;
    }

    /// <summary>Fabrique validante. <c>source</c> obligatoire.</summary>
    public static MessageRegle Creer(
        string id,
        MessageCategorie categorie,
        string texteFr,
        string? texteAr,
        string source,
        PeriodeReglementaire periode)
    {
        Guard.AgainstNullOrWhiteSpace(id);
        Guard.AgainstNullOrWhiteSpace(texteFr);
        Guard.AgainstNullOrWhiteSpace(source, nameof(source));
        return new MessageRegle(id, categorie, texteFr, texteAr, source, periode);
    }
}

