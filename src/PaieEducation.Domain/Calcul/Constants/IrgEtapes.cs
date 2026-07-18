namespace PaieEducation.Domain.Calcul.Constants;

/// <summary>
/// Labels des étapes du calcul IRG (<c>IrgCalculator</c>). Constantes
/// sémantiques du Domain — utilisés comme clés de regroupement dans le
/// journal d'audit (<c>JournalAudit</c>).
/// </summary>
public static class IrgEtapes
{
    public const string Exoneration = "exoneration";
    public const string LissageSpecial = "lissage_special";
    public const string LissageGeneral = "lissage_general";
    public const string Standard = "standard";
}
