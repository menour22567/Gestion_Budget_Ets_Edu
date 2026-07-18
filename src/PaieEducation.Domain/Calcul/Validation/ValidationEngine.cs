using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Shared.Money;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Validation;

/// <summary>
/// Contrôles post-calcul (Validation Engine, RM-081 ; V4 Tome C vol. 9 §15).
/// Dernière étape du pipeline (§13, étape 10 « Contrôles finaux ») : un
/// bulletin qui échoue ces contrôles ne sort jamais du pipeline en succès.
/// </summary>
public sealed class ValidationEngine
{
    /// <summary>Valide un bulletin déjà calculé. Renvoie le même bulletin en cas de succès.</summary>
    public Result<Bulletin> Valider(Bulletin b)
    {
        ArgumentNullException.ThrowIfNull(b);

        foreach (var ligne in b.Lignes)
        {
            if (ligne.Montant < Money.Zero)
            {
                return Result.Failure<Bulletin>(Error.Validation(
                    $"Montant négatif non justifié sur la ligne « {ligne.RubriqueId} » : {ligne.Montant}."));
            }
        }

        if (b.AssietteCotisable > b.TotalGains)
        {
            return Result.Failure<Bulletin>(Error.Validation(
                $"Assiette cotisable ({b.AssietteCotisable}) supérieure au total des gains ({b.TotalGains})."));
        }
        if (b.AssietteImposable > b.TotalGains)
        {
            return Result.Failure<Bulletin>(Error.Validation(
                $"Assiette imposable ({b.AssietteImposable}) supérieure au total des gains ({b.TotalGains})."));
        }
        if (b.Irg < Money.Zero)
        {
            return Result.Failure<Bulletin>(Error.Validation($"IRG négatif : {b.Irg}."));
        }

        // Équilibre des totaux (RM-081) : Net = TotalGains − TotalRetenues, garanti par
        // construction (CalculationPipeline calcule Net à partir des deux autres avec le
        // même service d'arrondi) — ce contrôle est un garde-fou de non-régression, pas
        // une nouvelle règle de calcul.
        if (b.TotalGains - b.TotalRetenues != b.Net)
        {
            return Result.Failure<Bulletin>(Error.Validation(
                $"Équilibre rompu : {b.TotalGains} − {b.TotalRetenues} ≠ {b.Net}."));
        }

        return Result.Success(b);
    }
}
