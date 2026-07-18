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
    /// <c>ARRONDI_MODE</c> à la date d'effet. Strict (Lot 1.1) : échoue
    /// avec <see cref="Error.NotFound"/> si la clé est absente à cette date
    /// et avec <see cref="Error.Validation"/> si la valeur n'est pas un
    /// mode reconnu. Aucun fallback silencieux — un paramètre corrompu
    /// doit être diagnostiqué, pas masqué.
    /// </summary>
    Task<Result<ModeArrondi>> LireModeArrondiAsync(string dateEffet, CancellationToken ct = default);

    /// <summary>
    /// Lit un paramètre décimal depuis la table <c>Parametres</c>.
    /// Helper réservé aux paramètres NON critiques : renvoie la valeur
    /// par défaut si le paramètre est absent ou si la valeur n'est pas
    /// décimale. Pour un paramètre métier dont l'absence doit bloquer le
    /// calcul, utiliser <see cref="LireDecimalObligatoireAsync"/> à la place.
    /// </summary>
    Task<Result<decimal>> LireDecimalOuDefautAsync(string cle, decimal defaut, string dateEffet, CancellationToken ct = default);

    /// <summary>
    /// Lit un paramètre décimal depuis la table <c>Parametres</c>.
    /// Échoue avec <see cref="Error.NotFound"/> si le paramètre est absent
    /// à la date d'effet et avec <see cref="Error.Validation"/> si la valeur
    /// n'est pas décimale. À utiliser pour tout paramètre métier dont
    /// l'absence doit bloquer le calcul.
    /// </summary>
    Task<Result<decimal>> LireDecimalObligatoireAsync(string cle, string dateEffet, CancellationToken ct = default);
}
