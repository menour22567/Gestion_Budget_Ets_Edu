namespace PaieEducation.Domain.Calcul.Constants;

/// <summary>
/// Codes sources de résolution de valeur (<c>ISourceValeurCalculator.CodeSource</c>).
/// Constantes sémantiques du Domain — identifiant le calculateur chargé de
/// résoudre une valeur pour le pipeline de calcul.
/// </summary>
public static class SourceValeurCodes
{
    public const string NotationAgent = "NOTATION_AGENT";
    public const string AnciennetePublique = "ANCIENNETE_PUBLIQUE";
    public const string AnciennetePrivee = "ANCIENNETE_PRIVEE";
    public const string IndiceEchelon = "INDICE_ECHELON";
    public const string PointIndiciaire = "POINT_INDICIAIRE";
    public const string BaseAssiette = "BASE_ASSIETTE";
    public const string ConstanteReglementaire = "CONSTANTE_REGLEMENTAIRE";
    public const string Papp = "PAPP";
}
