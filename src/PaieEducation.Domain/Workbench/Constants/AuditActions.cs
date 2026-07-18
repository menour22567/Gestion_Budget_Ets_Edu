namespace PaieEducation.Domain.Workbench.Constants;

/// <summary>
/// Actions d'audit tracées dans la table <c>AuditLog</c> (V001). Constantes
/// sémantiques du Domain — ne pas confondre avec les paramètres configurables
/// de la table <c>Parametres</c>.
/// </summary>
public static class AuditActions
{
    /// <summary>Application d'une évolution réglementaire (Phase 5, tâche 6).</summary>
    public const string AppliquerEvolution = "APPLIQUER_EVOLUTION";

    /// <summary>Application d'une évolution réglementaire avec bypass dry-run (Phase 5, tâche 6).</summary>
    public const string AppliquerEvolutionBypass = "APPLIQUER_EVOLUTION_BYPASS";

    /// <summary>Calcul de bulletin de paie.</summary>
    public const string Calcul = "CALCUL";
}
