using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Workbench.Services;

/// <summary>
/// Résout le barème applicable à une (rubrique, dimension, clé, date) donnée.
/// Implémentation concrète en Infrastructure ; le Domain définit le contrat
/// (ADR-0005 — moteur pur et synchrone). Le FormulaEngine expose
/// <c>bareme(RUBRIQUE, dimension)</c> qui consomme cette résolution.
/// </summary>
public interface IBaremeResolver
{
    /// <summary>
    /// Trouve le barème actif à la date demandée pour la clé fournie. Renvoie
    /// <c>null</c> si aucun barème ne couvre la clé à cette date (cas métier
    /// attendu : l'agent n'a pas de valeur de barème applicable, à signaler
    /// par l'appelant via un avertissement, pas une exception).
    /// </summary>
    BaremeValue? Resoudre(
        string rubriqueId,
        BaremeDimension dimension,
        string cle,
        string datePaie,
        IReadOnlyList<BaremeValue> baremes);
}
