using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Période de validité d'une donnée réglementaire (date d'effet / date de fin).
/// Fondational : toutes les autres Value Objects du Workbench (BaremeValue,
/// SourceValeur, MessageRegle, GroupeEligibilite) en dépendent.
///
/// Invariants :
///  - <see cref="DateEffet"/> &lt;= <see cref="DateFin"/> (ou <see cref="DateFin"/> = null)
///  - <see cref="DateEffet"/> est non nulle
///  - <see cref="DateFin"/> = null signifie « toujours en vigueur » (cf. J3E § 1)
/// </summary>
/// <remarks>
/// Modèle : V003-V007 partagent la même convention. Cf. J3E § 1 « Patron de
/// résolution unique ». La <see cref="Contient"/> reproduit la requête de
/// résolution par date (modèle V008).
/// </remarks>
public readonly record struct PeriodeReglementaire
{
    /// <summary>Début de validité, ISO 8601 <c>YYYY-MM-DD</c>.</summary>
    public string DateEffet { get; }

    /// <summary>Fin de validité, ISO 8601 <c>YYYY-MM-DD</c>. <c>null</c> = toujours en vigueur.</summary>
    public string? DateFin { get; }

    private PeriodeReglementaire(string dateEffet, string? dateFin)
    {
        DateEffet = dateEffet;
        DateFin = dateFin;
    }

    /// <summary>
    /// Crée une période. Lève <see cref="ArgumentException"/> si les invariants
    /// ne sont pas respectés.
    /// </summary>
    public static PeriodeReglementaire Creer(string dateEffet, string? dateFin)
    {
        Guard.AgainstNullOrWhiteSpace(dateEffet);
        if (dateFin is not null && string.CompareOrdinal(dateFin, dateEffet) < 0)
        {
            throw new ArgumentException(
                $"La date de fin ({dateFin}) doit être ≥ à la date d'effet ({dateEffet}).",
                nameof(dateFin));
        }
        return new PeriodeReglementaire(dateEffet, dateFin);
    }

    /// <summary>
    /// Vrai si la période englobe la date demandée. <paramref name="dateDemandee"/>
    /// est comparée au format ISO 8601 <c>YYYY-MM-DD</c>.
    /// </summary>
    public bool Contient(string dateDemandee)
    {
        Guard.AgainstNullOrWhiteSpace(dateDemandee);
        var dansLaBorneInf = string.CompareOrdinal(dateDemandee, DateEffet) >= 0;
        var dansLaBorneSup = DateFin is null
            || string.CompareOrdinal(dateDemandee, DateFin) <= 0;
        return dansLaBorneInf && dansLaBorneSup;
    }

    /// <summary>Vrai si la période chevauche une autre période.</summary>
    /// <remarks>
    /// Deux périodes se chevauchent si elles ont au moins une date en commun.
    /// <c>[A1..A2]</c> chevauche <c>[B1..B2]</c> ssi <c>A1 &lt;= B2 AND B1 &lt;= A2</c>,
    /// avec <c>null</c> interprété comme +infini.
    /// </remarks>
    public bool Chevauche(PeriodeReglementaire autre)
    {
        var finA = DateFin ?? int.MaxValue.ToString("D4");
        var finB = autre.DateFin ?? int.MaxValue.ToString("D4");
        // comparaison lexicographique valide pour ISO 8601 YYYY-MM-DD
        return string.CompareOrdinal(DateEffet, finB) <= 0
            && string.CompareOrdinal(autre.DateEffet, finA) <= 0;
    }
}

