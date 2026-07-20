using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaieEducation.Seeding;

/// <summary>
/// Lecteur des groupes DNF d'éligibilité ISSRP et des 4 grades hors
/// catégorie, embarqués en ressource
/// (<c>PaieEducation.Seeding.Donnees.Reglementaire.groupes_dnf_issrp_v1.json</c>).
/// Chantier P2 (audit du 19/07/2026) : dernière section réglementaire
/// « plate » encore codée en dur dans <see cref="ReglementaireSeeder"/> —
/// reportée lors du Lot 1.3 (volume ~92 grade IDs, couplage DNF), traitée
/// ici séparément avec un format dédié.
/// </summary>
/// <remarks>
/// Mêmes garanties que <see cref="ReglementaireJsonDataReader"/> : JSON
/// embarqué, hash SHA-256 canonique par ligne insérée (<see cref="ReglementaireJsonDataReader.HashLigne"/>).
/// Les listes de grades nommées (<see cref="GroupesDnfIssrpData.Grades"/>)
/// évitent de dupliquer ~92 identifiants entre les groupes qui les
/// partagent — chaque condition <c>GRADE IN (...)</c> référence une ou
/// plusieurs listes nommées par <see cref="ConditionDnfSeed.GradesRefs"/>,
/// résolues et unionnées (dans l'ordre déclaré) par
/// <see cref="ResoudreGrades(GroupesDnfIssrpData, IReadOnlyList{string})"/> —
/// même sémantique que l'union C# d'origine
/// (<c>Issrp45DirectGrades.Concat(...)</c>).
/// </remarks>
public static class GroupesDnfIssrpJsonDataReader
{
    private const string ResourceName =
        "PaieEducation.Seeding.Donnees.Reglementaire.groupes_dnf_issrp_v1.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Désérialise le JSON embarqué en structure fortement typée.</summary>
    public static GroupesDnfIssrpData Load()
    {
        var assembly = typeof(GroupesDnfIssrpJsonDataReader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Ressource embarquée introuvable : {ResourceName}");
        var data = JsonSerializer.Deserialize<GroupesDnfIssrpData>(stream, Options)
            ?? throw new InvalidOperationException(
                $"JSON invalide ou vide : {ResourceName}");
        return data;
    }

    /// <summary>
    /// Résout une liste de références de listes de grades nommées
    /// (<see cref="ConditionDnfSeed.GradesRefs"/>) en une union ordonnée
    /// (concaténation dans l'ordre déclaré, sans déduplication — comme
    /// l'union C# d'origine). Lève si une référence est inconnue.
    /// </summary>
    public static IReadOnlyList<string> ResoudreGrades(GroupesDnfIssrpData data, IReadOnlyList<string> refs)
    {
        var resultat = new List<string>();
        foreach (var refName in refs)
        {
            if (!data.Grades.TryGetValue(refName, out var liste))
                throw new InvalidOperationException($"Référence de liste de grades inconnue : « {refName} ».");
            resultat.AddRange(liste);
        }
        return resultat;
    }
}

/// <summary>Racine du JSON <c>groupes_dnf_issrp_v1.json</c>.</summary>
public sealed record GroupesDnfIssrpData(
    string Version,
    string SourceGlobale,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Grades,
    IReadOnlyList<GroupeDnfSeed> Groupes,
    GradesHorsCategorieSeed GradesHorsCategorie);

/// <summary>Un groupe DNF ISSRP (1 ligne <c>GroupesEligibilite</c> + conditions ETées).</summary>
public sealed record GroupeDnfSeed(
    string GroupeId,
    string RubriqueId,
    string DateEffet,
    string? DateFin,
    string Source,
    IReadOnlyList<ConditionDnfSeed> Conditions);

/// <summary>
/// Une condition atomique d'un groupe DNF. <see cref="GradesRefs"/> (résolu
/// via <see cref="GroupesDnfIssrpJsonDataReader.ResoudreGrades"/>) sert les
/// conditions <c>GRADE IN (...)</c> ; <see cref="Valeur"/> sert les
/// conditions à valeur littérale (ex. <c>ORIGINE_STATUTAIRE = ENSEIGNANT</c>).
/// Exactement l'un des deux est renseigné.
/// </summary>
public sealed record ConditionDnfSeed(
    string CritereId,
    string Operateur,
    string? Valeur = null,
    IReadOnlyList<string>? GradesRefs = null);

/// <summary>
/// Les 4 grades « hors catégorie » (Q-C3, HC-S1/HC-S2) — Filiere, Corps,
/// Categories, GrilleIndiciaire et Grades associés.
/// </summary>
public sealed record GradesHorsCategorieSeed(
    string Source,
    FiliereSeed Filiere,
    CorpsSeed Corps,
    IReadOnlyList<CategorieSeed> Categories,
    IReadOnlyList<GrilleIndiciaireSeed> GrilleIndiciaire,
    IReadOnlyList<GradeSeed> Grades);

public sealed record FiliereSeed(string Id, string Libelle);

public sealed record CorpsSeed(string Id, string Libelle, string FiliereId);

public sealed record CategorieSeed(string Id, int Niveau, string Libelle);

public sealed record GrilleIndiciaireSeed(
    string CategorieId, string DateEffet, string? DateFin, int Indice, string Version);

public sealed record GradeSeed(string Id, string Libelle, int Ordre);
