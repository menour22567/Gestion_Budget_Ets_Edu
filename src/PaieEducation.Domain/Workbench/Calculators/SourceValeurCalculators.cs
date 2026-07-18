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

/// <summary>Source : <c>ANCIENNETE_PRIVEE</c>. Stub V1 — l'attribut D3 <c>ANCIENNETE_PRIVEE_ANNEES</c> sera stocké en Phase 5 dans <c>AgentAttributs</c>.</summary>
public sealed class AnciennetePriveeCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.AnciennetePrivee;
    public Result<object> Calculer(AgentContext agent, string datePaie)
        // V1 : pas d'attribut agent → 0 par défaut (contrat IEP_CONT). L'attribut
        // ANCIENNETE_PRIVEE_ANNEES arrivera en Phase 5 (cf. J3J § 8.3) et sera
        // ajouté à AgentContext.
        => Result.Success<object>(0);
}

/// <summary>Source : <c>INDICE_ECHELON</c>. Non câblée en V1 — la résolution
/// depuis la grille indiciaire (V003) est branchée en Phase 4.</summary>
public sealed class IndiceEchelonCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.IndiceEchelon;
    public Result<object> Calculer(AgentContext agent, string datePaie)
        // Renvoyer agent.Echelon (n° 1-12) serait plausible mais FAUX : l'indice
        // de grille (ex. 578) en diffère d'un ordre de grandeur, et IEP_FONC =
        // IE × VPI produirait des montants faux sans aucune erreur visible.
        // Échec explicite tant que la lecture de la grille n'est pas branchée.
        => Result.Failure<object>(Error.Failure(
            "INDICE_ECHELON non résolu en V1 — la lecture de la grille indiciaire (V003) est branchée en Phase 4."));
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

/// <summary>Source : <c>CONSTANTE_REGLEMENTAIRE</c>. Non câblée en V1 — la
/// résolution depuis <c>RubriqueParametres</c> est branchée en Phase 4 (J3K § 4.2).</summary>
public sealed class ConstanteReglementaireCalculator : ISourceValeurCalculator
{
    public string CodeSource => SourceValeurCodes.ConstanteReglementaire;
    public Result<object> Calculer(AgentContext agent, string datePaie)
        // Un « 0 par défaut » serait une valeur plausible mais fausse (taux,
        // plafond ou borne réglementaire). Échec explicite tant que la lecture
        // de RubriqueParametres n'est pas branchée.
        => Result.Failure<object>(Error.Failure(
            "CONSTANTE_REGLEMENTAIRE non résolue en V1 — la lecture de RubriqueParametres est branchée en Phase 4 (J3K § 4.2)."));
}

