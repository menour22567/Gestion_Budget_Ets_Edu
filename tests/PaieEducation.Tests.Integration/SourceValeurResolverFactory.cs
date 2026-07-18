using Microsoft.Data.Sqlite;
using PaieEducation.Application.Payroll.Services;
using PaieEducation.Domain.Workbench.Calculators;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Infrastructure.Repositories.Payroll;
using PaieEducation.Infrastructure.Workbench.Calculators;

namespace PaieEducation.Tests.Integration;

/// <summary>
/// Fabrique de <see cref="CalculEntreeResolver"/> pour les tests d'intégration :
/// construit le <see cref="SourceValeurResolver"/> réel (index des 7 calculateurs
/// de sources, pattern Open/Closed ADR-0007 D6), comme le fait la DI de
/// production — sans mock, la notation est résolue depuis le dossier agent.
/// </summary>
public static class SourceValeurResolverFactory
{
    public static CalculEntreeResolver ResolverReel(SqliteConnection conn)
    {
        // Lot 1.2 : ConstanteReglementaireCalculator a besoin d'un lookup
        // I/O (table RubriqueParametres). On partage la connexion du scope
        // de test pour rester cohérent avec la production.
        var constanteCalc = new ConstanteReglementaireCalculator(new RubriqueParametreLookup(conn));
        var calculators = new ISourceValeurCalculator[]
        {
            new NotationAgentCalculator(),
            new AnciennetePubliqueCalculator(),
            new AnciennetePriveeCalculator(),
            new IndiceEchelonCalculator(),
            new PointIndiciaireCalculator(),
            new BaseAssietteCalculator(),
            constanteCalc,
        };
        var index = calculators.ToDictionary(c => c.CodeSource, c => c, StringComparer.OrdinalIgnoreCase);
        return new CalculEntreeResolver(new SourceValeurResolver(index));
    }

    /// <summary>
    /// Variante sans connexion SQLite (utilisée par les tests qui n'ont pas
    /// de scope migré sous la main). <see cref="ConstanteReglementaireCalculator"/>
    /// est instancié avec un lookup SQLite éphémère qui pointera sur une
    /// base vide : la source CONSTANTE_REGLEMENTAIRE renverra NotFound, ce
    /// qui est le comportement attendu en l'absence de paramètres.
    /// </summary>
    public static CalculEntreeResolver ResolverReelSansDb()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return ResolverReel(conn);
    }
}
