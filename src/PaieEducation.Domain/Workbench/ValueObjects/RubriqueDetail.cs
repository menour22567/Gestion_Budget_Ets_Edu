namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>
/// Métadonnées d'identité d'une rubrique (<c>Rubriques</c>, V004/V008) — pour
/// l'onglet « Identité » de la fiche rubrique (Phase 6, tâche 4).
/// </summary>
public sealed record RubriqueDetail(
    string Id,
    string Libelle,
    string Nature,
    string BaseCalcul,
    string Periodicite,
    string? PeriodiciteVersement,
    int OrdreCalcul,
    bool EstImposable,
    bool EstCotisable,
    string? Description,
    bool Actif);
