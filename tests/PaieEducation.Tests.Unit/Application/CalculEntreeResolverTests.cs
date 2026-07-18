using Moq;
using PaieEducation.Application.Payroll.Services;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;
using Xunit;

namespace PaieEducation.Tests.Unit.Application;

/// <summary>
/// Critères d'acceptation C2.2 / C2.3 : les entrées de calcul sont dérivées du
/// dossier agent, sans saisie manuelle de l'appelant. La résolution des sources
/// de valeur (notation PAPP) passe par <see cref="ISourceValeurResolver"/>
/// (pattern Open/Closed, ADR-0007 D6), jamais par une valeur codée.
/// </summary>
public class CalculEntreeResolverTests
{
    private static AgentContext AgentDeTest(int categorie, int echelon, string? corps = "PDLP",
        string? grade = "PDLP-G105", string? typeEtab = "PUBLIC", decimal? note = null, int? anc = 5) => new(
        Filiere: "ENSEIGNANT", Corps: corps, Grade: grade, Categorie: categorie, Echelon: echelon,
        AncienneteAnnees: anc, Fonction: null, TypeContrat: "STATUTAIRE", TypeEtablissement: typeEtab,
        OrigineStatutaire: "ENSEIGNANT", Note: note, ValeurPointIndiciaire: null,
        AssietteCotisable: null, AssietteImposable: null);

    /// <summary>
    /// Resolver réel (index du calculateur NOTATION_AGENT) — identique à la DI de
    /// production. La note est lue depuis <see cref="AgentContext.Note"/> par le
    /// calculateur, exactement comme pour un agent réel seedé.
    /// </summary>
    private static CalculEntreeResolver ResolverReel() =>
        new(new SourceValeurResolver(new Dictionary<string, ISourceValeurCalculator>(
            StringComparer.OrdinalIgnoreCase) { ["NOTATION_AGENT"] = new NotationAgentCalculator() }));

    [Fact]
    public void ResoudreClesBareme_derive_les_dimensions_depuis_l_agent()
    {
        var agent = AgentDeTest(categorie: 13, echelon: 5);
        var cles = ResolverReel().ResoudreClesBareme(agent);

        Assert.Equal("13", cles["CATEGORIE"]);
        Assert.Equal("5", cles["ECHELON"]);
        Assert.Equal("5", cles["ANCIENNETE"]);
        Assert.Equal("PUBLIC", cles["TYPE_ETABLISSEMENT"]);
        Assert.Equal("PDLP", cles["CORPS"]);
        Assert.Equal("PDLP-G105", cles["GRADE"]);
    }

    [Fact]
    public void ResoudreClesBareme_sans_type_etablissement_omet_la_cle()
    {
        var agent = AgentDeTest(categorie: 13, echelon: 5, typeEtab: null);
        var cles = ResolverReel().ResoudreClesBareme(agent);

        Assert.False(cles.ContainsKey("TYPE_ETABLISSEMENT"));
        Assert.True(cles.ContainsKey("CATEGORIE"));
    }

    [Fact]
    public void ResoudreSourcesValeur_avec_notation_projette_PAPP_en_taux()
    {
        // Note /20 → taux PAPP = note/20 * 0,40. note=15 → 0,30.
        // La note provient du resolver (NOTATION_AGENT), pas d'une valeur codée.
        var agent = AgentDeTest(categorie: 13, echelon: 5, note: 15m);
        var sources = ResolverReel().ResoudreSourcesValeur(agent, "2025-06-01");

        Assert.True(sources.ContainsKey("PAPP"));
        Assert.Equal(0.30m, sources["PAPP"]);
    }

    [Fact]
    public void ResoudreSourcesValeur_sans_notation_n_applique_pas_de_source()
    {
        // Calculateur en échec sur NOTATION_AGENT (note absente) → aucune source
        // (abstention ADR-0009).
        var agent = AgentDeTest(categorie: 13, echelon: 5, note: null);
        var sources = ResolverReel().ResoudreSourcesValeur(agent, "2025-06-01");

        Assert.Empty(sources);
    }
}
