using PaieEducation.Domain.Workbench.Internal;

namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Source de valeur — d'où une rubrique tire sa « matière première ». Catalogue
/// technique (R4 révisé) : audit minimal. V009 § 8ter.1.
///
/// Exemples : <c>NOTATION_AGENT</c>, <c>ANCIENNETE_PUBLIQUE</c>, <c>POINT_INDICIAIRE</c>.
/// Le calcul effectif est délégué à un <c>ISourceValeurCalculator</c> enregistré
/// en DI (pattern Open/Closed) — cf. ADR-0007 D6.
/// </summary>
public sealed record SourceValeur
{
    /// <summary>Code métier, ex. <c>'NOTATION_AGENT'</c>.</summary>
    public string Id { get; }

    /// <summary>Libellé humain.</summary>
    public string Libelle { get; }

    /// <summary>Description sémantique de la source.</summary>
    public string? Description { get; }

    private SourceValeur(string id, string libelle, string? description)
    {
        Id = id;
        Libelle = libelle;
        Description = description;
    }

    /// <summary>Fabrique validante.</summary>
    public static SourceValeur Creer(string id, string libelle, string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(id);
        Guard.AgainstNullOrWhiteSpace(libelle);
        return new SourceValeur(id, libelle, description);
    }
}

