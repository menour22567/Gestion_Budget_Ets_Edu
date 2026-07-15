using PaieEducation.Domain.Workbench.ValueObjects;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Results;

namespace PaieEducation.Application.Workbench.Services;

/// <summary>
/// Validation de la continuité temporelle d'un ensemble de périodes :
/// pas de chevauchement, pas de trou, une seule période ouverte à la fois par
/// clé. Garde-fou Workbench (L-U8) — appelé par le simuler (D8) et par l'UI
/// (Phase 6).
/// </summary>
/// <remarks>
/// La continuité est validée par clé (ex. <c>Cle = (rubrique, dimension, borneInf)</c>
/// pour les barèmes). Pour les conditions d'éligibilité, la clé est simplement
/// la rubrique. Pour les groupes, c'est la rubrique aussi (les groupes ne se
/// chevauchent pas au sein d'une même rubrique).
/// </remarks>
public static class ContinuiteTemporelle
{
    /// <summary>
    /// Valide la continuité temporelle d'un ensemble de périodes indexées par une clé.
    /// </summary>
    /// <param name="periodesParCle">
    /// Périodes avec leur clé d'appartenance. Deux périodes de même clé
    /// ne doivent ni se chevaucher ni laisser de trou.
    /// </param>
    /// <returns>
    /// <see cref="Result.Success"/> si tout est continu ;
    /// <see cref="Result.Failure"/> avec un code <c>validation</c> listant le
    /// premier écart trouvé (par clé).
    /// </returns>
    public static Result Valider(IReadOnlyList<(string Cle, PeriodeReglementaire Periode)> periodesParCle)
    {
        Guard.AgainstNull(periodesParCle);

        // Groupe par clé, trie par DateEffet croissante, vérifie chevauchement / trou / doublon ouvert.
        var parCle = periodesParCle
            .GroupBy(p => p.Cle, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var groupe in parCle)
        {
            var triees = groupe.OrderBy(p => p.Periode.DateEffet, StringComparer.Ordinal).ToList();

            // Au plus une période ouverte (DateFin == null).
            var ouvertes = triees.Count(p => p.Periode.DateFin is null);
            if (ouvertes > 1)
            {
                return Result.Failure(Error.Validation(
                    $"Clé '{groupe.Key}' : {ouvertes} périodes ouvertes — au plus une autorisée."));
            }

            for (var i = 0; i < triees.Count - 1; i++)
            {
                var courante = triees[i].Periode;
                var suivante = triees[i + 1].Periode;

                if (courante.Chevauche(suivante))
                {
                    return Result.Failure(Error.Validation(
                        $"Clé '{groupe.Key}' : chevauchement entre " +
                        $"[{courante.DateEffet}..{courante.DateFin ?? "∞"}] et " +
                        $"[{suivante.DateEffet}..{suivante.DateFin ?? "∞"}]."));
                }

                // Détection de trou : la suivante doit commencer le lendemain de la fin de la courante.
                if (courante.DateFin is not null)
                {
                    var fin = courante.DateFin;
                    // ISO 8601 YYYY-MM-DD → comparaison lexicographique = chronologique.
                    if (string.CompareOrdinal(suivante.DateEffet, Lendemain(fin)) > 0)
                    {
                        return Result.Failure(Error.Validation(
                            $"Clé '{groupe.Key}' : trou entre [{courante.DateEffet}..{fin}] et " +
                            $"[{suivante.DateEffet}..{suivante.DateFin ?? "∞"}] " +
                            $"(attendu reprise au {Lendemain(fin)})."));
                    }
                }
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Calcule le lendemain calendaire d'une date ISO 8601 <c>YYYY-MM-DD</c>.
    /// Gère correctement le passage à l'année suivante (ex. 2024-12-31 → 2025-01-01).
    /// </summary>
    public static string Lendemain(string isoDate)
    {
        var d = DateOnly.ParseExact(isoDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return d.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }
}


