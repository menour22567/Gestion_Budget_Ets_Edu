namespace PaieEducation.Domain.Calcul.ValueObjects;

/// <summary>
/// Élément de nomenclature (Grade, Catégorie, Échelon...) — projection de
/// lecture minimale pour peupler un sélecteur (code métier + libellé humain).
/// </summary>
public sealed record ReferentielItem(string Id, string Libelle);
