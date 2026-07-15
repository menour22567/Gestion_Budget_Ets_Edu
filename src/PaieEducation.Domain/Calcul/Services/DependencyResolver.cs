using PaieEducation.Domain.Common;

namespace PaieEducation.Domain.Calcul.Services;

/// <summary>
/// Une arête du graphe de dépendances : <see cref="Rubrique"/> a besoin du
/// résultat de <see cref="DependDe"/> avant d'être calculée (V004
/// <c>RubriqueDependances</c>).
/// </summary>
public readonly record struct DependanceArete(string Rubrique, string DependDe);

/// <summary>
/// Ordonne les rubriques par leurs dépendances (tri topologique) et détecte les
/// cycles. Le pipeline de calcul (Phase 4) consomme l'ordre produit : une
/// rubrique n'est calculée qu'après toutes celles dont elle dépend.
/// </summary>
/// <remarks>
/// Pur, déterministe : à ensemble d'entrée égal, l'ordre de sortie est stable
/// (les nœuds sans contrainte gardent leur ordre d'apparition). Détection de
/// cycle par coloriage DFS (blanc / gris / noir) — un retour sur un nœud gris
/// est un cycle, rapporté avec le chemin fautif.
/// </remarks>
public sealed class DependencyResolver
{
    /// <summary>
    /// Calcule un ordre de calcul valide (dépendances d'abord). Les
    /// <paramref name="rubriques"/> fixent l'univers des nœuds et l'ordre stable ;
    /// les <paramref name="aretes"/> portant sur des rubriques hors univers sont
    /// rejetées (échec de validation).
    /// </summary>
    public Result<IReadOnlyList<string>> Ordonner(
        IReadOnlyList<string> rubriques,
        IReadOnlyList<DependanceArete> aretes)
    {
        ArgumentNullException.ThrowIfNull(rubriques);
        ArgumentNullException.ThrowIfNull(aretes);

        var univers = new HashSet<string>(rubriques, StringComparer.Ordinal);
        if (univers.Count != rubriques.Count)
            return Result.Failure<IReadOnlyList<string>>(
                Error.Validation("Rubrique en double dans la liste des nœuds."));

        var adjacence = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var r in rubriques)
            adjacence[r] = new List<string>();

        foreach (var a in aretes)
        {
            if (!univers.Contains(a.Rubrique))
                return Result.Failure<IReadOnlyList<string>>(
                    Error.Validation($"Dépendance sur une rubrique inconnue : « {a.Rubrique} »."));
            if (!univers.Contains(a.DependDe))
                return Result.Failure<IReadOnlyList<string>>(
                    Error.Validation($"Dépendance vers une rubrique inconnue : « {a.DependDe} »."));
            // Arête « Rubrique dépend de DependDe » ⇒ DependDe doit être calculé
            // avant Rubrique : on garde DependDe comme prédécesseur de Rubrique.
            adjacence[a.Rubrique].Add(a.DependDe);
        }

        // 0 = blanc (non visité), 1 = gris (en cours), 2 = noir (terminé).
        var couleur = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in rubriques) couleur[r] = 0;

        var ordre = new List<string>(rubriques.Count);
        var pileChemin = new List<string>();

        foreach (var depart in rubriques)
        {
            var echec = Visiter(depart, adjacence, couleur, ordre, pileChemin);
            if (echec is not null)
                return Result.Failure<IReadOnlyList<string>>(echec);
        }

        return Result.Success<IReadOnlyList<string>>(ordre);
    }

    private static Error? Visiter(
        string noeud,
        Dictionary<string, List<string>> adjacence,
        Dictionary<string, int> couleur,
        List<string> ordre,
        List<string> pileChemin)
    {
        if (couleur[noeud] == 2) return null;   // déjà finalisé
        if (couleur[noeud] == 1)
        {
            var debut = pileChemin.IndexOf(noeud);
            var cycle = pileChemin.GetRange(debut, pileChemin.Count - debut);
            cycle.Add(noeud);
            return Error.Cycle("Cycle de dépendances : " + string.Join(" → ", cycle));
        }

        couleur[noeud] = 1;
        pileChemin.Add(noeud);
        foreach (var predecesseur in adjacence[noeud])
        {
            var echec = Visiter(predecesseur, adjacence, couleur, ordre, pileChemin);
            if (echec is not null) return echec;
        }
        pileChemin.RemoveAt(pileChemin.Count - 1);
        couleur[noeud] = 2;
        ordre.Add(noeud);       // post-ordre DFS = dépendances avant le nœud
        return null;
    }
}
