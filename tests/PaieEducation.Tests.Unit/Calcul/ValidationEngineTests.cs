using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Explicabilite;
using PaieEducation.Domain.Calcul.Pipeline;
using PaieEducation.Domain.Calcul.Validation;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Tests du <see cref="ValidationEngine"/> (RM-081, J4.d). Un bulletin construit
/// à la main (pas besoin du pipeline complet) pour isoler chaque contrôle.
/// </summary>
public class ValidationEngineTests
{
    private static readonly ExplicationLigne ExplicationVide = new("", Array.Empty<VariableUtilisee>());
    private static readonly JournalAudit AuditVide = new(Array.Empty<EtapeAudit>());

    private static BulletinLigne Ligne(string id, NatureRubrique nature, decimal montant, bool imposable = true, bool cotisable = true)
        => new(id, nature, montant, imposable, cotisable, ExplicationVide);

    private static Bulletin BulletinEquilibre(IReadOnlyList<BulletinLigne> lignes,
        decimal totalGains, decimal assietteCotisable, decimal assietteImposable,
        decimal totalRetenues, decimal irg, decimal net)
        => new(lignes, totalGains, assietteCotisable, assietteImposable, totalRetenues, irg, net, AuditVide);

    [Fact]
    public void Bulletin_equilibre_et_coherent_est_valide()
    {
        var lignes = new[]
        {
            Ligne("TRAITEMENT", NatureRubrique.Gain, 30000m),
            Ligne("SS", NatureRubrique.Cotisation, 2700m, imposable: false, cotisable: false),
            Ligne("IRG", NatureRubrique.Impot, 1000m, imposable: false, cotisable: false),
        };
        var b = BulletinEquilibre(lignes, totalGains: 30000m, assietteCotisable: 30000m,
            assietteImposable: 27300m, totalRetenues: 3700m, irg: 1000m, net: 26300m);

        var r = new ValidationEngine().Valider(b);

        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Same(b, r.Value);
    }

    [Fact]
    public void Montant_negatif_sur_une_ligne_est_rejete()
    {
        var lignes = new[] { Ligne("TRAITEMENT", NatureRubrique.Gain, -100m) };
        var b = BulletinEquilibre(lignes, totalGains: -100m, assietteCotisable: -100m,
            assietteImposable: -100m, totalRetenues: 0m, irg: 0m, net: -100m);

        var r = new ValidationEngine().Valider(b);

        Assert.True(r.IsFailure);
        Assert.Contains("Montant négatif", r.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assiette_cotisable_superieure_au_total_des_gains_est_rejetee()
    {
        var lignes = new[] { Ligne("TRAITEMENT", NatureRubrique.Gain, 10000m) };
        // Assiette cotisable incohérente (supérieure au total des gains) — ne peut
        // pas arriver par construction du pipeline, garde-fou de non-régression.
        var b = BulletinEquilibre(lignes, totalGains: 10000m, assietteCotisable: 15000m,
            assietteImposable: 10000m, totalRetenues: 0m, irg: 0m, net: 10000m);

        var r = new ValidationEngine().Valider(b);

        Assert.True(r.IsFailure);
        Assert.Contains("Assiette cotisable", r.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Irg_negatif_est_rejete()
    {
        var lignes = new[] { Ligne("TRAITEMENT", NatureRubrique.Gain, 10000m) };
        var b = BulletinEquilibre(lignes, totalGains: 10000m, assietteCotisable: 10000m,
            assietteImposable: 10000m, totalRetenues: -50m, irg: -50m, net: 10050m);

        var r = new ValidationEngine().Valider(b);

        Assert.True(r.IsFailure);
        Assert.Contains("IRG négatif", r.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Equilibre_rompu_entre_gains_retenues_et_net_est_rejete()
    {
        var lignes = new[] { Ligne("TRAITEMENT", NatureRubrique.Gain, 10000m) };
        // Net incohérent : 10000 − 1000 devrait faire 9000, pas 9500.
        var b = BulletinEquilibre(lignes, totalGains: 10000m, assietteCotisable: 10000m,
            assietteImposable: 10000m, totalRetenues: 1000m, irg: 1000m, net: 9500m);

        var r = new ValidationEngine().Valider(b);

        Assert.True(r.IsFailure);
        Assert.Contains("Équilibre rompu", r.Error.Message, StringComparison.Ordinal);
    }
}
