using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Workbench.Calculators;

/// <summary>
/// Calculateur de la note de l'agent (PAPP, PAPG, REND). Source : <c>NOTATION_AGENT</c>.
/// Renvoie la note portée par <see cref="AgentContext.Note"/>.
/// </summary>
public sealed class NotationAgentCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.NotationAgent;
    public Result<object> Calculer(AgentContext agent, string datePaie)
    {
        if (agent.Note is null)
        {
            return Result.Failure<object>(Error.NotFound(
                "Aucune note enregistrée pour l'agent à la période demandée."));
        }
        return Result.Success<object>(agent.Note.Value);
    }
}

/// <summary>Source : <c>ANCIENNETE_PUBLIQUE</c>. Renvoie l'ancienneté publique en années.</summary>
public sealed class AnciennetePubliqueCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.AnciennetePublique;
    public Result<object> Calculer(AgentContext agent, string datePaie)
        => agent.AncienneteAnnees is null
            ? Result.Failure<object>(Error.NotFound("Ancienneté publique absente du snapshot agent."))
            : Result.Success<object>(agent.AncienneteAnnees.Value);
}

/// <summary>
/// Source : <c>ANCIENNETE_PRIVEE</c>. Lit l'attribut D3
/// <c>ANCIENNETE_PRIVEE_ANNEES</c> depuis <see cref="AgentContext"/> (chargé
/// depuis <c>AgentAttributs</c> versionné).
/// </summary>
public sealed class AnciennetePriveeCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.AnciennetePrivee;
    public Result<object> Calculer(AgentContext agent, string datePaie)
        // Lot 1.2 : la valeur est portée par l'agent context (lecture
        // versionnée). Null = pas d'ancienneté privée (cas le plus fréquent,
        // IEP_CONT) → abstention explicite plutôt qu'un 0 silencieux qui
        // contaminerait les formules la consommant.
        => agent.AnciennetePriveeAnnees is null
            ? Result.Failure<object>(Error.NotFound(
                "Ancienneté privée absente du dossier agent (ANCIENNETE_PRIVEE_ANNEES non renseigné)."))
            : Result.Success<object>(agent.AnciennetePriveeAnnees.Value);
}

/// <summary>
/// Source : <c>INDICE_ECHELON</c>. Lit l'indice de grille effectif de l'agent
/// depuis <see cref="AgentContext.IndiceEchelon"/> (chargé depuis
/// <c>IndicesEchelon</c> via le n° d'échelon de carrière).
/// </summary>
public sealed class IndiceEchelonCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.IndiceEchelon;
    public Result<object> Calculer(AgentContext agent, string datePaie)
        // Lot 1.2 : la valeur est portée par l'agent context. Renvoyer
        // agent.Echelon (n° 1-12) serait plausible mais FAUX : l'indice de
        // grille (ex. 578) en diffère d'un ordre de grandeur, et toute
        // formule (IEP_FONC = IE × VPI) produirait des montants faux sans
        // erreur visible. D'où l'abstention explicite si la grille ne
        // couvre pas la date.
        => agent.IndiceEchelon is null
            ? Result.Failure<object>(Error.NotFound(
                $"Indice d'échelon absent de la grille indiciaire à la date {datePaie}."))
            : Result.Success<object>(agent.IndiceEchelon.Value);
}

/// <summary>Source : <c>POINT_INDICIAIRE</c>. Renvoie la valeur du point portée par le snapshot.</summary>
public sealed class PointIndiciaireCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.PointIndiciaire;
    public Result<object> Calculer(AgentContext agent, string datePaie)
        => agent.ValeurPointIndiciaire is null
            ? Result.Failure<object>(Error.NotFound("Valeur du point indiciaire absente du snapshot."))
            : Result.Success<object>(agent.ValeurPointIndiciaire.Value);
}

/// <summary>Source : <c>BASE_ASSIETTE</c>. Renvoie l'assiette cotisable portée par le snapshot (à défaut, l'imposable).</summary>
public sealed class BaseAssietteCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.BaseAssiette;
    public Result<object> Calculer(AgentContext agent, string datePaie)
    {
        if (agent.AssietteCotisable is { } cotisable) return Result.Success<object>(cotisable);
        if (agent.AssietteImposable is { } imposable) return Result.Success<object>(imposable);
        return Result.Failure<object>(Error.NotFound("Assiette absente du snapshot agent."));
    }
}

// Note Lot 1.2 : la source CONSTANTE_REGLEMENTAIRE a besoin d'un lookup
// I/O (table RubriqueParametres). Le calculateur correspondant vit donc
// dans Infrastructure (voir PaieEducation.Infrastructure.Workbench.Calculators
// .ConstanteReglementaireCalculator), câblé en DI derrière l'interface
// ISourceValeurCalculator. Pattern Open/Closed respecté : aucune
// modification du moteur de calcul.

