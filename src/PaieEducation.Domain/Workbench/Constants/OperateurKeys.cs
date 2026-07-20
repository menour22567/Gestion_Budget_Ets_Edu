using PaieEducation.Domain.Workbench.Enums;

namespace PaieEducation.Domain.Workbench.Constants;

/// <summary>
/// Constantes et parser pour l'opérateur d'une condition d'éligibilité
/// (<c>ReglesEligibilite.Operateur</c>, CHECK V005/V008/V009). Unifie la
/// conversion string/enum côté écriture avec le <c>ParseOperateur</c> déjà
/// utilisé côté lecture (<c>WorkbenchReadRepository</c>).
/// </summary>
public static class OperateurKeys
{
    public const string Egal = "=";
    public const string In = "IN";
    public const string NotIn = "NOT_IN";
    public const string SuperieurEgal = ">=";
    public const string InferieurEgal = "<=";
    public const string Superieur = ">";
    public const string Inferieur = "<";

    public static readonly IReadOnlyList<string> Valides =
        [Egal, In, NotIn, SuperieurEgal, InferieurEgal, Superieur, Inferieur];

    public static Operateur Parser(string operateur) => operateur switch
    {
        Egal => Operateur.Egal,
        In => Operateur.In,
        NotIn => Operateur.NotIn,
        SuperieurEgal => Operateur.SuperieurEgal,
        InferieurEgal => Operateur.InferieurEgal,
        Superieur => Operateur.Superieur,
        Inferieur => Operateur.Inferieur,
        _ => throw new ArgumentOutOfRangeException(nameof(operateur), operateur, "Opérateur inconnu.")
    };
}
