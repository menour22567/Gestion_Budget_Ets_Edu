using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Results;

namespace PaieEducation.Tests.Unit;

/// <summary>Tests fumée des primitives transverses (Result / Error / Guard).</summary>
public class SharedSmokeTests
{
    [Fact]
    public void Result_Success_est_un_succes_sans_erreur()
    {
        Result r = Result.Success();

        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.Equal(Error.None, r.Error);
    }

    [Fact]
    public void Result_generique_porte_la_valeur()
    {
        Result<int> r = 42;

        Assert.True(r.IsSuccess);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Result_Failure_expose_l_erreur()
    {
        Result r = Result.Failure(Error.Validation("champ requis"));

        Assert.True(r.IsFailure);
        Assert.Equal("validation", r.Error.Code);
    }

    [Fact]
    public void Guard_AgainstNull_leve_si_null()
        => Assert.Throws<ArgumentNullException>(() => Guard.AgainstNull<object>(null));

    [Fact]
    public void Guard_AgainstNullOrWhiteSpace_leve_si_vide()
        => Assert.Throws<ArgumentException>(() => Guard.AgainstNullOrWhiteSpace("  "));
}
