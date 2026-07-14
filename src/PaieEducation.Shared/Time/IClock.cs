namespace PaieEducation.Shared.Time;

/// <summary>
/// Abstraction du temps. Indispensable au déterminisme et à la testabilité du moteur
/// de paie (les calculs dépendent de la période et des dates d'effet).
/// </summary>
public interface IClock
{
    /// <summary>Date et heure locales courantes.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Date et heure UTC courantes.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Date du jour, sans composante horaire.</summary>
    DateOnly Today { get; }
}
