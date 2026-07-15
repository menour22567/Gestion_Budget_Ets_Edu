namespace PaieEducation.Domain.Workbench.Enums;

/// <summary>
/// Dimension d'indexation d'un <c>BaremeValue</c>. V008 § 8bis.1 — correspond
/// exactement à la colonne <c>RubriqueBaremes.Dimension</c>.
/// </summary>
public enum BaremeDimension
{
    Categorie,
    Echelon,
    Anciennete,
    TypeEtablissement,
    Corps,
    Grade
}

/// <summary>
/// Type de la valeur portée par un <c>BaremeValue</c>. <c>TAUX</c> = fraction
/// (0..1), <c>MONTANT</c> = entier DA. Cf. V008 § 8bis.1.
/// </summary>
public enum BaremeTypeValeur
{
    Taux,
    Montant
}

/// <summary>
/// Sévérité d'un message réglementaire. D2 — portée par
/// <c>GroupesEligibilite.Severite</c>. Présentation uniquement, jamais bloquante.
/// </summary>
public enum Severite
{
    Info,
    Recommandee,
    ObligatoireReglementaire
}

/// <summary>
/// Opérateur d'une condition d'éligibilité. Conservé à l'identique de V005/V008
/// — V009 n'a fait qu'ajouter la sémantique DNF via <c>GroupesEligibilite</c>.
/// </summary>
public enum Operateur
{
    Egal,
    In,
    NotIn,
    SuperieurEgal,
    InferieurEgal,
    Superieur,
    Inferieur
}

/// <summary>
/// Type sémantique d'une valeur de critère d'éligibilité. Piloté par
/// <c>CriteresEligibilite.TypeValeur</c> (R4 révisé — catalogue technique).
/// </summary>
public enum TypeValeurCritere
{
    Text,
    Int,
    Date,
    Enum
}

/// <summary>
/// Comment l'évaluateur résout la valeur d'un critère côté agent. Piloté par
/// <c>CriteresEligibilite.SourceResolution</c>.
/// </summary>
public enum SourceResolution
{
    /// <summary>Attribut propre à l'agent (D3 : ORIGINE_STATUTAIRE, EXERCICE_EFFECTIF, ...).</summary>
    AttributAgent,

    /// <summary>Attribut du grade de l'agent (D3 : GRADE_ATTRIBUTS).</summary>
    AttributGrade,

    /// <summary>Donnée de carrière (CORPS, GRADE, CATEGORIE, ECHELON, ...).</summary>
    Carriere,

    /// <summary>Donnée calculée (ANCIENNETE, ASSIETTE, ...).</summary>
    Calcule
}

/// <summary>
/// Catégorie d'un message réglementaire (Workbench D7). Piloté par
/// <c>MessagesRegles.Categorie</c>.
/// </summary>
public enum MessageCategorie
{
    Eligibilite,
    Avertissement,
    Suggestion
}
