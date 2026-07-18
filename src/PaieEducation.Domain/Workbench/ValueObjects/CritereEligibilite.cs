using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Critère d'éligibilité — concept métier, pas une valeur réglementaire. Catalogue
/// technique (R4 révisé) : audit minimal. V008/V009 § 8ter.3.
/// </summary>
/// <remarks>
/// Un nouveau critère = une ligne dans le dictionnaire <c>CriteresEligibilite</c>,
/// pas une migration. <c>SourceResolution</c> indique à l'évaluateur comment
/// résoudre la valeur côté agent (D3).
/// </remarks>
public sealed record CritereEligibilite
{
    /// <summary>Code métier, ex. <c>'CORPS'</c>, <c>'GRADE'</c>, <c>'ORIGINE_STATUTAIRE'</c>.</summary>
    public string Id { get; }

    /// <summary>Libellé humain.</summary>
    public string Libelle { get; }

    /// <summary>Sémantique de la valeur (TEXT, INT, DATE, ENUM).</summary>
    public TypeValeurCritere TypeValeur { get; }

    /// <summary>Comment l'évaluateur résout la valeur côté agent (D3).</summary>
    public SourceResolution SourceResolution { get; }

    private CritereEligibilite(string id, string libelle, TypeValeurCritere typeValeur, SourceResolution sourceResolution)
    {
        Id = id;
        Libelle = libelle;
        TypeValeur = typeValeur;
        SourceResolution = sourceResolution;
    }

    /// <summary>Fabrique validante. <paramref name="id"/> sert d'identité stable.</summary>
    public static CritereEligibilite Creer(
        string id,
        string libelle,
        TypeValeurCritere typeValeur,
        SourceResolution sourceResolution)
    {
        Guard.AgainstNullOrWhiteSpace(id);
        Guard.AgainstNullOrWhiteSpace(libelle);
        return new CritereEligibilite(id, libelle, typeValeur, sourceResolution);
    }
}

