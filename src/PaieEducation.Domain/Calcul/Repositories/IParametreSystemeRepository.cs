using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Shared.Results;
using PaieEducation.Shared.Guards;

namespace PaieEducation.Domain.Calcul.Repositories;

/// <summary>
/// Lecture des paramètres système versionnés (table <c>Parametres</c>),
/// notamment le mode d'arrondi (<c>ARRONDI_MODE</c>, Q9b). Port du Domain,
/// implémenté par Infrastructure.
/// </summary>
public interface IParametreSystemeRepository
{
    /// <summary>
    /// Lit la valeur texte d'un paramètre à une date d'effet (résolution
    /// point-in-time : dernière version en vigueur).
    /// </summary>
    Task<Result<string?>> LireValeurAsync(string cle, string dateEffet, CancellationToken ct = default);

    /// <summary>
    /// Résout le <see cref="ModeArrondi"/> effectif depuis le paramètre
    /// <c>ARRONDI_MODE</c>. Par défaut <see cref="ModeArrondi.DinarPlusProche"/>
    /// si absent ou invalide (valeur seedée par défaut, Q9b).
    /// </summary>
    Task<Result<ModeArrondi>> LireModeArrondiAsync(string dateEffet, CancellationToken ct = default);

    /// <summary>
    /// Lit un paramètre décimal depuis la table <c>Parametres</c>.
    /// Renvoie la valeur par défaut si le paramètre est absent.
    /// </summary>
    Task<Result<decimal>> LireDecimalAsync(string cle, decimal defaut, string dateEffet, CancellationToken ct = default);

    /// <summary>
    /// Lit un paramètre décimal depuis la table <c>Parametres</c>.
    /// Échoue avec <see cref="Error.NotFound"/> si le paramètre est absent.
    /// </summary>
    Task<Result<decimal>> LireDecimalObligatoireAsync(string cle, string dateEffet, CancellationToken ct = default);
}
