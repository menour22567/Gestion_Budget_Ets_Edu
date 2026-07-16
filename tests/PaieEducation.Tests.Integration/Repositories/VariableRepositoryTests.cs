using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;

namespace PaieEducation.Tests.Integration.Repositories;

/// <summary>
/// Tests de <see cref="VariableRepository"/> (Phase 5, suite du jalon D) :
/// résolution point-in-time des variables de base (<c>INDICE_MIN</c>,
/// <c>INDICE_ECH</c>, <c>VPI</c>, <c>TBASE</c>, <c>TRT</c>, <c>ECH</c>, <c>CAT</c>)
/// depuis <c>GrilleIndiciaire</c>/<c>IndicesEchelon</c>/<c>ValeurPoint</c>.
/// </summary>
public class VariableRepositoryTests
{
    private static void SeedCategorieEtEchelon(SqliteConnection c)
    {
        SchemaTestSupport.Exec(c, """
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ('13', 13, 'Catégorie 13', '2026-01-01T00:00:00Z', 'h');
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ('5', 5, 'Échelon 5', '2026-01-01T00:00:00Z', 'h');
            """);
    }

    private static void SeedGrilleIndiciaire(
        SqliteConnection c, string id, string categorieId, int indiceMin, string dateEffet, string? dateFin = null)
        => SchemaTestSupport.Exec(c, """
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, DateFin, IndiceMin, Version, Hash, CreatedAt)
            VALUES ($id, $categorieId, $dateEffet, $dateFin, $indiceMin, 'v', 'h', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$categorieId", categorieId), ("$dateEffet", dateEffet), ("$dateFin", dateFin), ("$indiceMin", indiceMin));

    private static void SeedIndicesEchelon(
        SqliteConnection c, string id, string echelonId, int indice, string dateEffet)
        => SchemaTestSupport.Exec(c, """
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt)
            VALUES ($id, $echelonId, $dateEffet, $indice, 'v', 'h', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$echelonId", echelonId), ("$dateEffet", dateEffet), ("$indice", indice));

    private static void SeedValeurPoint(SqliteConnection c, string id, double valeur, string dateEffet)
        => SchemaTestSupport.Exec(c, """
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt)
            VALUES ($id, $dateEffet, $valeur, 'v', 'h', '2026-01-01T00:00:00Z');
            """, ("$id", id), ("$dateEffet", dateEffet), ("$valeur", valeur));

    private static AgentContext Contexte(int? categorie = 13, int? echelon = 5) => new(
        Filiere: null, Corps: null, Grade: null, Categorie: categorie, Echelon: echelon,
        AncienneteAnnees: null, Fonction: null, TypeContrat: null, TypeEtablissement: null,
        OrigineStatutaire: null, Note: null, ValeurPointIndiciaire: null,
        AssietteCotisable: null, AssietteImposable: null);

    [Fact]
    public async Task Resolution_nominale_calcule_TBASE_et_TRT()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        SeedGrilleIndiciaire(scope.Conn, "GI-1", "13", 578, "2020-01-01");
        SeedIndicesEchelon(scope.Conn, "IE-1", "5", 100, "2020-01-01");
        SeedValeurPoint(scope.Conn, "VP-1", 45, "2007-01-01");

        var repo = new VariableRepository(scope.Conn);
        var result = await repo.ResoudreAsync(Contexte(), "2025-06-01");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        var v = result.Value;
        Assert.Equal(578m, v["INDICE_MIN"]);
        Assert.Equal(100m, v["INDICE_ECH"]);
        Assert.Equal(45m, v["VPI"]);
        Assert.Equal(26010m, v["TBASE"]);
        Assert.Equal(30510m, v["TRT"]);
        Assert.Equal(5m, v["ECH"]);
        Assert.Equal(13m, v["CAT"]);
    }

    [Fact]
    public async Task Changement_de_version_de_grille_resout_la_valeur_en_vigueur_a_la_date()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        SeedGrilleIndiciaire(scope.Conn, "GI-1", "13", 500, "2020-01-01", "2023-12-31");
        SeedGrilleIndiciaire(scope.Conn, "GI-2", "13", 578, "2024-01-01");
        SeedIndicesEchelon(scope.Conn, "IE-1", "5", 100, "2020-01-01");
        SeedValeurPoint(scope.Conn, "VP-1", 45, "2007-01-01");

        var repo = new VariableRepository(scope.Conn);

        var avant = await repo.ResoudreAsync(Contexte(), "2023-06-01");
        Assert.True(avant.IsSuccess);
        Assert.Equal(500m, avant.Value["INDICE_MIN"]);

        var apres = await repo.ResoudreAsync(Contexte(), "2025-06-01");
        Assert.True(apres.IsSuccess);
        Assert.Equal(578m, apres.Value["INDICE_MIN"]);
    }

    [Fact]
    public async Task Categorie_sans_grille_en_vigueur_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        SeedIndicesEchelon(scope.Conn, "IE-1", "5", 100, "2020-01-01");
        SeedValeurPoint(scope.Conn, "VP-1", 45, "2007-01-01");

        var repo = new VariableRepository(scope.Conn);
        var result = await repo.ResoudreAsync(Contexte(), "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Contains("grille", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Echelon_sans_indice_en_vigueur_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        SeedGrilleIndiciaire(scope.Conn, "GI-1", "13", 578, "2020-01-01");
        SeedValeurPoint(scope.Conn, "VP-1", 45, "2007-01-01");

        var repo = new VariableRepository(scope.Conn);
        var result = await repo.ResoudreAsync(Contexte(), "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Contains("échelon", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Aucune_valeur_du_point_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        SeedCategorieEtEchelon(scope.Conn);
        SeedGrilleIndiciaire(scope.Conn, "GI-1", "13", 578, "2020-01-01");
        SeedIndicesEchelon(scope.Conn, "IE-1", "5", 100, "2020-01-01");

        var repo = new VariableRepository(scope.Conn);
        var result = await repo.ResoudreAsync(Contexte(), "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Contains("point", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentContext_sans_categorie_ou_echelon_echoue_explicitement()
    {
        using var scope = SchemaTestSupport.CreateMigrated();

        var repo = new VariableRepository(scope.Conn);
        var result = await repo.ResoudreAsync(Contexte(categorie: null), "2025-06-01");

        Assert.True(result.IsFailure);
        Assert.Contains("Categorie", result.Error.Message);
    }
}
