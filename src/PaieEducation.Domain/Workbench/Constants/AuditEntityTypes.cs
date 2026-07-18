namespace PaieEducation.Domain.Workbench.Constants;

/// <summary>
/// Types d'entités tracés dans la table <c>AuditLog</c> (V001). Constantes
/// sémantiques du Domain — les valeurs correspondent aux types métier
/// identifiant l'objet ciblé par l'action d'audit.
/// </summary>
public static class AuditEntityTypes
{
    /// <summary>Valeur du point indiciaire (grille indiciaire).</summary>
    public const string ValeurPoint = "ValeurPoint";

    /// <summary>Bulletin de paie.</summary>
    public const string Bulletin = "Bulletin";

    /// <summary>Agent (enseignant/éducateur).</summary>
    public const string Agent = "Agent";

    /// <summary>Affectation agent-rubrique.</summary>
    public const string Affectation = "Affectation";
}
