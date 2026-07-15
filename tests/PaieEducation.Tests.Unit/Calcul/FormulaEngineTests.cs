using PaieEducation.Domain.Calcul.Formules;

namespace PaieEducation.Tests.Unit.Calcul;

/// <summary>
/// Tests du FormulaEngine (parser + évaluateur). Enjeu Phase 4 : les formules
/// sont lues en base, jamais codées en dur — l'évaluateur doit traiter
/// fidèlement les expressions réelles du catalogue J3C (IEP, EXP_PEDAG, PAPP,
/// ISSRP, DOC forfaitaire, ...).
/// </summary>
public class FormulaEngineTests
{
    private static readonly FormulaEvaluator Eval = new();

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("10 - 2 - 3", 5)]          // associativité gauche
    [InlineData("100 / 4 / 5", 5)]
    [InlineData("-5 + 8", 3)]
    [InlineData("2 * -3", -6)]
    [InlineData("--4", 4)]
    [InlineData("3.5 * 2", 7.0)]
    public void Evalue_l_arithmetique_avec_priorites(string expr, decimal attendu)
    {
        var r = Eval.Evaluer(expr, new FakeFormulaContext());
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(attendu, r.Value);
    }

    [Fact]
    public void Resout_les_variables_du_contexte()
    {
        // EXP_PEDAG = TBASE × 4 % × ECH (J3C §2).
        var ctx = new FakeFormulaContext().Var("TBASE", 30000m).Var("ECH", 5m);
        var r = Eval.Evaluer("TBASE * 0.04 * ECH", ctx);
        Assert.True(r.IsSuccess);
        Assert.Equal(6000m, r.Value);
    }

    [Fact]
    public void Variable_inconnue_echoue_sans_substituer_zero()
    {
        var r = Eval.Evaluer("TBASE * 2", new FakeFormulaContext());
        Assert.True(r.IsFailure);
        Assert.Contains("TBASE", r.Error.Message);
    }

    [Fact]
    public void Fonction_min_borne_le_taux_composite_IEP_CONT()
    {
        // IEP_CONT = TBASE × min(ANC_PUB×1,4% + ANC_PRIV×0,7% ; 60%) (J3C §1bis).
        // ANC_PUB=30, ANC_PRIV=30 → 42%+21% = 63% → borné à 60% → 30000×0,60 = 18000.
        var ctx = new FakeFormulaContext()
            .Var("TBASE", 30000m).Var("ANC_PUB", 30m).Var("ANC_PRIV", 30m)
            .Var("TAUX_PUB", 0.014m).Var("TAUX_PRIV", 0.007m).Var("PLAFOND", 0.60m);
        var r = Eval.Evaluer("TBASE * min(ANC_PUB * TAUX_PUB + ANC_PRIV * TAUX_PRIV, PLAFOND)", ctx);
        Assert.True(r.IsSuccess);
        Assert.Equal(18000m, r.Value);
    }

    [Fact]
    public void Fonction_max_et_abs()
    {
        var ctx = new FakeFormulaContext();
        Assert.Equal(9m, Eval.Evaluer("max(3, 9, 5)", ctx).Value);
        Assert.Equal(7m, Eval.Evaluer("abs(-7)", ctx).Value);
    }

    [Fact]
    public void Fonction_round_avec_et_sans_decimales()
    {
        var ctx = new FakeFormulaContext();
        Assert.Equal(3m, Eval.Evaluer("round(2.5)", ctx).Value);
        Assert.Equal(2.35m, Eval.Evaluer("round(2.345, 2)", ctx).Value);
    }

    [Fact]
    public void Fonction_bareme_lit_deux_identifiants()
    {
        // DOC_PEDAG forfaitaire par catégorie via un barème.
        var ctx = new FakeFormulaContext().Bareme("DOC_PEDAG", "CATEGORIE", 2500m);
        var r = Eval.Evaluer("bareme(DOC_PEDAG, CATEGORIE)", ctx);
        Assert.True(r.IsSuccess);
        Assert.Equal(2500m, r.Value);
    }

    [Fact]
    public void Fonction_valeurSource_pour_PAPP()
    {
        // PAPP = TRT × valeurSource(PAPP) (taux de notation, source NOTATION_AGENT).
        var ctx = new FakeFormulaContext().Var("TRT", 50000m).Source("PAPP", 0.30m);
        var r = Eval.Evaluer("TRT * valeurSource(PAPP)", ctx);
        Assert.True(r.IsSuccess);
        Assert.Equal(15000m, r.Value);
    }

    [Fact]
    public void Bareme_avec_mauvais_arguments_echoue()
    {
        var r = Eval.Evaluer("bareme(1 + 2, CATEGORIE)", new FakeFormulaContext());
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Division_par_zero_echoue()
    {
        var r = Eval.Evaluer("10 / 0", new FakeFormulaContext());
        Assert.True(r.IsFailure);
        Assert.Contains("Division", r.Error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 2")]
    [InlineData("* 3")]
    [InlineData("bareme(A, B")]
    [InlineData("1 @ 2")]
    public void Syntaxe_invalide_echoue(string expr)
    {
        Assert.True(FormulaParser.Parser(expr).IsFailure);
    }

    [Fact]
    public void Parser_une_fois_evaluer_plusieurs_fois()
    {
        // Le pipeline parse une fois puis réévalue par agent : on vérifie que
        // l'arbre est réutilisable avec des contextes différents.
        var arbre = FormulaParser.Parser("TRT * 0.45");
        Assert.True(arbre.IsSuccess);
        Assert.Equal(45000m, Eval.Evaluer(arbre.Value, new FakeFormulaContext().Var("TRT", 100000m)).Value);
        Assert.Equal(22500m, Eval.Evaluer(arbre.Value, new FakeFormulaContext().Var("TRT", 50000m)).Value);
    }
}
