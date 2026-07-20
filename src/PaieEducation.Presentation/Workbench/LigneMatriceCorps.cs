namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Une ligne de la matrice de couverture pivotée (P7) : un corps, et son
/// <see cref="EtatCouverture"/> pour chaque rubrique visible (clé =
/// <c>RubriqueId</c>, alignée sur <see cref="MatriceCouvertureViewModel.Colonnes"/>).
/// </summary>
public sealed record LigneMatriceCorps(string CorpsId, IReadOnlyDictionary<string, EtatCouverture> EtatsParRubrique);
