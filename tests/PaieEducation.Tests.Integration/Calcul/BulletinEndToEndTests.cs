using Microsoft.Data.Sqlite;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Tools.Seeding;

namespace PaieEducation.Tests.Integration.Calcul;

/// <summary>
/// Jalon J4 — preuve de bout en bout : migration V001→V009 → seed réglementaire
/// (ReglementaireSeeder + IrgSeeder) → seed des <b>formules</b> (FormulesSeeder,
/// expressions en base) → <see cref="PayrollReadRepository"/> (lecture) →
/// <see cref="CalculationPipeline"/> (calcul) → bulletin enseignant complet.
///
/// Le montant net (46 624 DA) est identique au test unitaire du pipeline
/// (contexte en mémoire) : la même formule, lue en base, produit le même
/// résultat — c'est la démonstration que rien n'est codé en dur.
/// </summary>
public class BulletinEndToEndTests
{
    // Variables de base du pilote (résolues de la grille en Phase 5 ; fournies ici).
    private static readonly Dictionary<string, decimal> VariablesBase = new()
    {
        ["INDICE_MIN"] = 578m, ["INDICE_ECH"] = 100m, ["VPI"] = 45m,
        ["TBASE"] = 26010m, ["TRT"] = 30510m, ["ECH"] = 5m, ["CAT"] = 13m,
    };

    private static AgentContext Enseignant(string corps) => new(
        Filiere: "ENSEIGNANT", Corps: corps, Grade: null, Categorie: 13, Echelon: 5,
        AncienneteAnnees: 10, Fonction: null, TypeContrat: "STATUTAIRE",
        TypeEtablissement: null, OrigineStatutaire: "ENSEIGNANT",
        Note: 0.30m, ValeurPointIndiciaire: 45m, AssietteCotisable: null, AssietteImposable: null);

    private static async Task SeedTout(SqliteConnection conn)
    {
        await new ReglementaireSeeder().SeedAsync(conn);
        await new IrgSeeder().SeedAsync(conn);
        await new FormulesSeeder().SeedAsync(conn);

        // Le pilote consomme l'éligibilité ISSRP_45 via l'évaluateur DNF. La
        // matrice seedée (une condition '=' par corps, GroupeId NULL) est un
        // reliquat de l'ère « résolution SQL brute » : sous la sémantique ET-plat
        // de l'évaluateur, exiger CORPS = CPDE ET = CDC ET … n'est jamais vrai.
        // On la remplace pour la rubrique pilote par une condition IN (OR sur les
        // valeurs), sémantiquement correcte. Suivi : remodéliser la matrice ISSRP
        // du ReglementaireSeeder en groupes DNF (hors périmètre J4.c).
        Exec(conn, "DELETE FROM ReglesEligibilite WHERE RubriqueId = 'ISSRP_45';");
        Exec(conn, """
            INSERT INTO ReglesEligibilite
                (Id, RubriqueId, CritereId, Operateur, Valeur, DateEffet, Hash, CreatedAt)
            VALUES ('RE-ISSRP45-IN', 'ISSRP_45', 'CORPS', 'IN', 'CPDE,CDDL,CDC,CI',
                    '2025-01-01', 'h', '2026-01-01T00:00:00Z');
            """);
    }

    private static async Task<PaieEducation.Domain.Common.Result<PayrollInput>> Charger(
        PayrollReadRepository repo, string corps)
        => await repo.ChargerAsync(
            Enseignant(corps), "2025-06-01", VariablesBase,
            new Dictionary<string, decimal> { ["PAPP"] = 0.30m },
            new Dictionary<string, string>(), ProfilFiscal.Standard);

    [Fact]
    public async Task Bulletin_enseignant_de_bout_en_bout_depuis_la_base()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);

        var repo = new PayrollReadRepository(scope.Conn);
        var input = await Charger(repo, "CPDE");
        Assert.True(input.IsSuccess, input.IsFailure ? input.Error.Message : null);

        // Les formules ont bien été lues en base (pas de valeur codée en dur).
        var traitement = input.Value.Rubriques.Single(r => r.Id == "TRAITEMENT");
        Assert.Equal("(INDICE_MIN + INDICE_ECH) * VPI", traitement.Expression);
        Assert.Contains(input.Value.Rubriques, r => r.Id == "ISSRP_45");
        Assert.DoesNotContain(input.Value.Rubriques, r => r.Id == "ISSRP_30"); // pas de formule seedée

        // Barème IRG 2022 chargé pour une paie de 2025.
        Assert.NotNull(input.Value.RegleIrg);
        Assert.Equal("IRG-PER-2022", input.Value.RegleIrg!.Code);

        var pipeline = new CalculationPipeline(new ArrondiService(ModeArrondi.DinarPlusProche));
        var bulletin = pipeline.Calculer(input.Value);
        Assert.True(bulletin.IsSuccess, bulletin.IsFailure ? bulletin.Error.Message : null);
        var b = bulletin.Value;

        Assert.Equal(30510m, Ligne(b, "TRAITEMENT"));
        Assert.Equal(5202m, Ligne(b, "EXP_PEDAG"));
        Assert.Equal(9153m, Ligne(b, "PAPP"));
        Assert.Equal(13730m, Ligne(b, "ISSRP_45"));
        Assert.Equal(58595m, b.TotalGains);
        Assert.Equal(5274m, Ligne(b, "SS"));
        Assert.Equal(53321m, b.AssietteImposable);
        Assert.Equal(6697m, b.Irg);
        Assert.Equal(46624m, b.Net);
    }

    [Fact]
    public async Task Enseignant_hors_groupe_ISSRP_n_a_pas_la_prime()
    {
        using var scope = SchemaTestSupport.CreateMigrated();
        await SeedTout(scope.Conn);

        var repo = new PayrollReadRepository(scope.Conn);
        var input = await Charger(repo, "AUTRE_CORPS");
        Assert.True(input.IsSuccess);

        var b = new CalculationPipeline(new ArrondiService()).Calculer(input.Value).Value;
        Assert.DoesNotContain(b.Lignes, l => l.RubriqueId == "ISSRP_45");
        Assert.Equal(44865m, b.TotalGains);   // 30510 + 5202 + 9153
    }

    private static decimal Ligne(Bulletin b, string id) => b.Lignes.Single(l => l.RubriqueId == id).Montant;

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
