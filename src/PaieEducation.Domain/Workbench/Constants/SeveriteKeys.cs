using PaieEducation.Domain.Workbench.Enums;

namespace PaieEducation.Domain.Workbench.Constants;

/// <summary>
/// Constantes et parser pour la sévérité d'un groupe DNF (<c>GroupesEligibilite.Severite</c>,
/// CHECK V009). Unifie la conversion string/enum côté écriture avec le
/// <c>ParseSeverite</c> déjà utilisé côté lecture (<c>WorkbenchReadRepository</c>).
/// </summary>
public static class SeveriteKeys
{
    public const string Info = "INFO";
    public const string Recommandee = "RECOMMANDEE";
    public const string ObligatoireReglementaire = "OBLIGATOIRE_REGLEMENTAIRE";

    public static readonly IReadOnlyList<string> Valides = [Info, Recommandee, ObligatoireReglementaire];

    public static Severite Parser(string severite) => severite switch
    {
        Info => Severite.Info,
        Recommandee => Severite.Recommandee,
        ObligatoireReglementaire => Severite.ObligatoireReglementaire,
        _ => throw new ArgumentOutOfRangeException(nameof(severite), severite, "Sévérité inconnue.")
    };
}
