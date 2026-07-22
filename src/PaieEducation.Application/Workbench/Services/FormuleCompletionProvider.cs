using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Workbench.Repositories;
using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Results;

namespace PaieEducation.Application.Workbench.Services;

/// <summary>
/// Élément d'auto-complétion pour l'éditeur de formule (P10, FormulaEditor
/// avancé). Inséré dans le <c>Popup</c> WPF du
/// <c>EditerRubriqueView</c>.
/// </summary>
/// <param name="Token">Texte inséré à la position du caret (sans le préfixe
/// déjà tapé). Pour les fonctions, inclut les parenthèses ouvrantes
/// (ex. <c>"round("</c>).</param>
/// <param name="Libelle">Texte affiché dans la liste (avec libellé long).
/// </param>
/// <param name="Categorie">Catégorie (fonction, variable, source, rubrique)
/// pour le tri et l'icône éventuelle.</param>
/// <param name="Description">Description courte (survol / ToolTip).</param>
public sealed record CompletionItem(
    string Token,
    string Libelle,
    CompletionCategorie Categorie,
    string Description);

/// <summary>Catégorie d'un <see cref="CompletionItem"/> (P10).</summary>
public enum CompletionCategorie
{
    Fonction = 0,
    Variable = 1,
    SourceValeur = 2,
    Rubrique = 3,
}

/// <summary>
/// Port d'auto-complétion de formule (P10). Le
/// <c>EditerRubriqueViewModel</c> le consomme pour peupler son
/// <c>Popup</c> ; l'implémentation agrège un catalogue statique
/// (constantes du Domain) et la lecture des rubriques actives en base.
/// </summary>
public interface IFormuleCompletionProvider
{
    /// <summary>
    /// Renvoie les <see cref="CompletionItem"/> dont le <c>Token</c>
    /// commence par <paramref name="prefixe"/> (insensible à la casse).
    /// Tri stable : Fonction &lt; Variable &lt; SourceValeur &lt; Rubrique,
    /// puis alphabétique. Limité à <paramref name="max"/> résultats
    /// (défaut 20 — taille raisonnable pour un Popup WPF).
    /// </summary>
    Task<IReadOnlyList<CompletionItem>> ProposerAsync(
        string prefixe, int max = 20, CancellationToken ct = default);
}

/// <inheritdoc cref="IFormuleCompletionProvider"/>
/// <remarks>
/// Catalogue statique (construit une fois pour le process) :
/// <list type="bullet">
///   <item>6 fonctions : <c>round</c>, <c>abs</c>, <c>min</c>, <c>max</c>,
///         <c>bareme</c>, <c>valeurSource</c> (cf. <c>FormulaEvaluator</c>).</item>
///   <item>7 variables de base : <c>TBASE</c>, <c>TRT</c>,
///         <c>INDICE_MIN</c>, <c>INDICE_ECH</c>, <c>VPI</c>, <c>ECH</c>,
///         <c>CAT</c> (cf. doc d'hypothèses Lot 2.2 §2).</item>
///   <item>8 sources de valeur : <c>SourceValeurCodes</c>.</item>
/// </list>
/// Les rubriques sont lues en base à chaque appel (cache applicatif à
/// ajouter en post-V1 si la latence devient visible).
/// </remarks>
public sealed class FormuleCompletionProvider : IFormuleCompletionProvider
{
    private readonly IWorkbenchReadRepository _workbench;

    public FormuleCompletionProvider(IWorkbenchReadRepository workbench)
        => _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    // -------- catalogue statique (jamais modifié à chaud) --------

    private static readonly IReadOnlyList<CompletionItem> CatalogueStatique =
    new CompletionItem[]
    {
        // Fonctions (ordre alphabétique). Insertion = nom + "(" pour ouvrir
        // la parenthèse et laisser l'utilisateur taper les arguments.
        new("round(", "round(x[, n])", CompletionCategorie.Fonction,
            "Arrondi : round(x) au dinar le plus proche, round(x, n) à n décimales (ADR-0011)."),
        new("abs(", "abs(x)", CompletionCategorie.Fonction,
            "Valeur absolue."),
        new("min(", "min(x, y, ...)", CompletionCategorie.Fonction,
            "Plus petit des arguments (au moins 2)."),
        new("max(", "max(x, y, ...)", CompletionCategorie.Fonction,
            "Plus grand des arguments (au moins 2)."),
        new("bareme(", "bareme(RUB, DIM)", CompletionCategorie.Fonction,
            "Résolution d'un barème versionné : code rubrique + code dimension (CATEGORIE, ECHELON, ANCIENNETE…)."),
        new("valeurSource(", "valeurSource(SRC)", CompletionCategorie.Fonction,
            "Résolution d'une source de valeur (NOTATION_AGENT, ANCIENNETE_PUBLIQUE, …) à la date de paie."),

        // Variables de base (cf. doc Lot 2.2 §2). Insertion = nom seul.
        new("TBASE", "TBASE", CompletionCategorie.Variable,
            "Traitement de base = INDICE_MIN(CAT) × VPI."),
        new("TRT", "TRT", CompletionCategorie.Variable,
            "Traitement = TBASE + IEP_FONC (fonctionnaire) ou autre (contractuel)."),
        new("INDICE_MIN", "INDICE_MIN", CompletionCategorie.Variable,
            "Indice minimum de la catégorie (grille indiciaire)."),
        new("INDICE_ECH", "INDICE_ECH", CompletionCategorie.Variable,
            "Indice de l'échelon détenu."),
        new("VPI", "VPI", CompletionCategorie.Variable,
            "Valeur du point indiciaire (paramètre système)."),
        new("ECH", "ECH", CompletionCategorie.Variable,
            "Numéro d'échelon (1-12)."),
        new("CAT", "CAT", CompletionCategorie.Variable,
            "Catégorie du grade (1-20)."),

        // Sources de valeur (cf. ADR-0007 D6 + SourceValeurCodes).
        new(SourceValeurCodes.NotationAgent, SourceValeurCodes.NotationAgent, CompletionCategorie.SourceValeur,
            "Note PAPP/PAPG/REND portée par l'agent (0..1)."),
        new(SourceValeurCodes.AnciennetePublique, SourceValeurCodes.AnciennetePublique, CompletionCategorie.SourceValeur,
            "Ancienneté publique en années."),
        new(SourceValeurCodes.AnciennetePrivee, SourceValeurCodes.AnciennetePrivee, CompletionCategorie.SourceValeur,
            "Ancienneté privée en années (attribut D3)."),
        new(SourceValeurCodes.IndiceEchelon, SourceValeurCodes.IndiceEchelon, CompletionCategorie.SourceValeur,
            "Indice de l'échelon effectif (grille en vigueur à datePaie)."),
        new(SourceValeurCodes.PointIndiciaire, SourceValeurCodes.PointIndiciaire, CompletionCategorie.SourceValeur,
            "Valeur du point indiciaire (snapshot agent)."),
        new(SourceValeurCodes.BaseAssiette, SourceValeurCodes.BaseAssiette, CompletionCategorie.SourceValeur,
            "Assiette cotisable (ou imposable à défaut) portée par le snapshot."),
        new(SourceValeurCodes.ConstanteReglementaire, SourceValeurCodes.ConstanteReglementaire, CompletionCategorie.SourceValeur,
            "Constante réglementaire lue dans RubriqueParametres."),
        new(SourceValeurCodes.Papp, SourceValeurCodes.Papp, CompletionCategorie.SourceValeur,
            "Alias historique de NOTATION_AGENT."),
    };

    public async Task<IReadOnlyList<CompletionItem>> ProposerAsync(
        string prefixe, int max = 20, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        var prefixeNormalise = (prefixe ?? string.Empty).Trim();
        var comparaison = StringComparison.OrdinalIgnoreCase;

        // 1) filtrer le catalogue statique
        var statiqueFiltre = CatalogueStatique
            .Where(c => c.Token.StartsWith(prefixeNormalise, comparaison))
            .ToList();

        // 2) lire les rubriques actives (échec lecture = on rend au moins le statique,
        //    on ne casse pas l'auto-complétion pour un incident I/O). La V1 ne
        //    filtre pas par datePaie (la table Rubriques n'a pas de versionnement
        //    temporel ; le `Actif = 1` suffit pour l'auto-complétion éditeur).
        IReadOnlyList<RubriqueResume> rubriques = Array.Empty<RubriqueResume>();
        try
        {
            var res = await _workbench.ListerRubriquesActivesAsync(ct).ConfigureAwait(false);
            if (res is not null) rubriques = res;
        }
        catch
        {
            // silencieux : voir remarque
        }

        var rubriquesFiltrees = rubriques
            .Select(r => new CompletionItem(
                Token: r.Id,
                Libelle: $"{r.Id} — {r.Libelle}",
                Categorie: CompletionCategorie.Rubrique,
                Description: $"Rubrique : {r.Libelle}."))
            .Where(c => c.Token.StartsWith(prefixeNormalise, comparaison))
            .ToList();

        // 3) assembler dans l'ordre Fonction / Variable / Source / Rubrique
        //    puis alphabétique, puis plafonner à `max`.
        var tous = statiqueFiltre
            .Concat(rubriquesFiltrees)
            .OrderBy(c => (int)c.Categorie)
            .ThenBy(c => c.Token, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

        return tous;
    }
}
