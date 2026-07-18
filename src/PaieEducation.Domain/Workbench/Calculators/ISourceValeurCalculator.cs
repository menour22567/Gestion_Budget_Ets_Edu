using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Workbench.Calculators;

/// <summary>
/// Calculateur typé pour une <c>SourceValeur</c>. Implémentations concrètes
/// en Infrastructure ou Domain/Workbench/Calculators. Pattern Open/Closed :
/// une nouvelle source = un nouveau calculateur enregistré en DI, pas de
/// modification du moteur (ADR-0007 D6).
/// </summary>
public interface ISourceValeurCalculator
{
    /// <summary>
    /// Code de la source gérée (ex. <c>"NOTATION_AGENT"</c>). Doit être unique
    /// dans le registre DI.
    /// </summary>
    string CodeSource { get; }

    /// <summary>
    /// Calcule la valeur de la source pour l'agent à la date demandée.
    /// Renvoie un échec métier (catalogue absent, données manquantes, etc.)
    /// plutôt qu'une exception — les cas métier attendus passent par Result.
    /// </summary>
    Result<object> Calculer(AgentContext agent, string datePaie);
}

