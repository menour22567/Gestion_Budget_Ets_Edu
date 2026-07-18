using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Workbench.Services;

/// <summary>
/// Délègue la résolution d'une <see cref="SourceValeur"/> au calculateur typé
/// enregistré (Open/Closed). Le Domain définit le contrat et le résultat ;
/// l'Infrastructure enregistre les <c>ISourceValeurCalculator</c> concrets.
/// </summary>
/// <remarks>
/// Pattern Open/Closed : un nouveau type de source = <c>INSERT</c> dans
/// <c>SourcesValeur</c> + une classe <c>ISourceValeurCalculator</c> enregistrée
/// en DI. Aucune modification du moteur de calcul. ADR-0007 D6.
/// </remarks>
public interface ISourceValeurResolver
{
    /// <summary>
    /// Résout la valeur d'une <paramref name="source"/> pour un agent donné à
    /// une date. Renvoie un <see cref="Result{T}"/> : succès avec valeur
    /// (typée en <see cref="object"/>) ou échec (catalogue absent, agent sans
    /// la donnée requise, etc.).
    /// </summary>
    Result<object> Resoudre(
        SourceValeur source,
        AgentContext agent,
        string datePaie);
}

/// <summary>
/// Contexte agent passé aux services de résolution. Snapshot immuable des
/// données nécessaires (caractéristiques carrière + attributs D3). Construit
/// par la couche Application à partir de la base.
/// </summary>
/// <remarks>
/// Ce record est minimal en V1 : on n'expose que ce dont les calculateurs ont
/// besoin. Les champs sont volontairement nullable pour les attributs qui
/// peuvent être absents (D3 : un agent n'a pas forcément tous ses attributs
/// renseignés).
/// </remarks>
public sealed record AgentContext(
    string? Filiere,
    string? Corps,
    string? Grade,
    int? Categorie,
    int? Echelon,
    int? AncienneteAnnees,
    string? Fonction,
    string? TypeContrat,
    string? TypeEtablissement,
    string? OrigineStatutaire,
    decimal? Note,
    decimal? ValeurPointIndiciaire,
    decimal? AssietteCotisable,
    decimal? AssietteImposable,
    /// <summary>
    /// Indice d'échelon effectif de l'agent à la date de paie (lu depuis
    /// <c>IndicesEchelon</c> via le numéro d'échelon de carrière). Lot 1.2 :
    /// permet à <c>INDICE_ECHELON</c> de retourner la bonne valeur (≠
    /// <see cref="Echelon"/> qui est le n° d'échelon 1-12). <c>null</c> si
    /// la grille ne couvre pas la date.
    /// </summary>
    decimal? IndiceEchelon = null,
    /// <summary>
    /// Ancienneté du secteur privé en années (attribut agent D3
    /// <c>ANCIENNETE_PRIVEE_ANNEES</c>). Lot 1.2 : alimenté depuis
    /// <c>AgentAttributs</c> versionné. <c>null</c> si l'agent n'a pas
    /// d'ancienneté privée (cas le plus fréquent, IEP_CONT).
    /// </summary>
    int? AnciennetePriveeAnnees = null);
