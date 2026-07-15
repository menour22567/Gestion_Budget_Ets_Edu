namespace PaieEducation.Application.Workbench.UseCases;

/// <summary>
/// Rapport d'impact d'une évolution réglementaire (D8). Lecture seule — produit
/// par <c>SimulerEvolutionReglementaire</c> avant tout commit. Affiché par
/// l'assistant d'évolution (J3I § 7) et exportable en PDF pour archivage.
/// </summary>
/// <param name="NbAgents">Nombre d'agents impactés par l'évolution.</param>
/// <param name="DeltaMinMensuel">Delta mensuel minimum observé sur les agents impactés (DA, peut être négatif en cas de baisse).</param>
/// <param name="DeltaMaxMensuel">Delta mensuel maximum observé (DA).</param>
/// <param name="MontantTotalMensuel">Somme algébrique des deltas mensuels (DA).</param>
/// <param name="PeriodeImpactee">Période sur laquelle l'évolution s'applique (ISO 8601 YYYY-MM-DD).</param>
/// <param name="BulletinsAvertis">
/// Nombre de bulletins validés qui devraient générer un rappel (D9) si l'évolution
/// est rétroactive. 0 si l'évolution n'est pas rétroactive.
/// </param>
public sealed record RapportImpact(
    int NbAgents,
    decimal DeltaMinMensuel,
    decimal DeltaMaxMensuel,
    decimal MontantTotalMensuel,
    string PeriodeImpactee,
    int BulletinsAvertis);
