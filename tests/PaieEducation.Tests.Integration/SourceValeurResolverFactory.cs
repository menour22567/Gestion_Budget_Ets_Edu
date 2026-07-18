using PaieEducation.Application.Payroll.Services;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Fabrique de <see cref="CalculEntreeResolver"/> pour les tests d'intégration :
/// construit le <see cref="SourceValeurResolver"/> réel (index des 7 calculateurs
/// de sources, pattern Open/Closed ADR-0007 D6), comme le fait la DI de
/// production — sans mock, la notation est résolue depuis le dossier agent.
/// </summary>
public static class SourceValeurResolverFactory
{
    public static CalculEntreeResolver ResolverReel()
    {
        var calculators = new ISourceValeurCalculator[]
        {
            new NotationAgentCalculator(),
            new AnciennetePubliqueCalculator(),
            new AnciennetePriveeCalculator(),
            new IndiceEchelonCalculator(),
            new PointIndiciaireCalculator(),
            new BaseAssietteCalculator(),
            new ConstanteReglementaireCalculator(),
        };
        var index = calculators.ToDictionary(c => c.CodeSource, c => c, StringComparer.OrdinalIgnoreCase);
        return new CalculEntreeResolver(new SourceValeurResolver(index));
    }
}
