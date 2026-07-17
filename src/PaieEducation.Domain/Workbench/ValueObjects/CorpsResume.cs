namespace PaieEducation.Domain.Workbench.ValueObjects;

/// <summary>Projection en lecture d'un <c>Corps</c> (nomenclature) — pour la matrice de couverture (D11, J3I §5.5).</summary>
public sealed record CorpsResume(string Id, string Libelle);
