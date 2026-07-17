namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Projection en lecture d'une ligne <c>AgentRubriques</c> (V011, J3H §7) —
/// l'affectation d'une rubrique à un agent, telle que vue/décidée par
/// l'utilisateur. Pas une entité du Domain de calcul : un simple miroir de
/// la ligne, pour lister ce sur quoi <c>AccepterSuggestion</c>/
/// <c>SupprimerAffectation</c>/<c>SuspendreAffectation</c> peuvent agir.
/// </summary>
/// <param name="Id">Identifiant (GUID) de la ligne — cible des transitions d'état.</param>
/// <param name="RubriqueId">Rubrique affectée.</param>
/// <param name="Occurrence">Occurrence (1 sauf retenues à montant fixe/rappels, D4).</param>
/// <param name="Statut"><c>SUGGEREE</c>, <c>ACCEPTEE</c>, <c>SUSPENDUE</c> ou <c>SUPPRIMEE</c> (terminal).</param>
/// <param name="Origine"><c>MANUELLE</c> ou <c>GROUPE:&lt;Id&gt;@&lt;DateEffet&gt;</c> (règle déclencheuse).</param>
/// <param name="DateEffet">Date d'effet de l'affectation.</param>
/// <param name="DateFin"><c>null</c> = toujours en vigueur.</param>
public sealed record AffectationRubrique(
    string Id,
    string RubriqueId,
    int Occurrence,
    string Statut,
    string Origine,
    string DateEffet,
    string? DateFin);
