using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Seeding;

/// <summary>
/// Seed optionnel d'un agent de démonstration (<c>A-PILOTE</c>) permettant un
/// parcours immédiat « créer agent → suggérer rubriques → calculer bulletin »
/// sans saisie manuelle. Activé uniquement sur opt-in (C1.3) : en production le
/// flag reste désactivé. Idempotent.
/// </summary>
/// <remarks>
/// L'agent utilise un corps/grade/catégorie déjà seedés par la nomenclature
/// embarquée (corps EN, grade PDLP-G105). La carrière est ouverte à la date de
/// recrutement.
/// </remarks>
public sealed class DemoAgentSeeder
{
    private const string AgentId = "A-PILOTE";
    private const string GradeId = "PDLP-G105";
    private const string CorpsId = "PDLP";
    private const string FiliereId = "ENSEIGNANT";
    private const string CategorieId = "13";
    private const string EchelonId = "5";

    public async Task<SeedReport> SeedAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit être ouverte.");

        var report = new SeedReport(1);
        var at = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Garantit le socle nomenclature (au cas où le seed complet n'aurait
        // pas tourné) — idempotent.
        await connection.ExecuteAsync("""
            INSERT INTO Filieres (Id, Libelle, CreatedAt, Hash) VALUES ($fid, 'Enseignant', $at, 'h-demo')
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO Corps (Id, Libelle, FiliereId, CreatedAt, Hash) VALUES ($cid, 'Prof. École primaire', $fid, $at, 'h-demo')
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO Categories (Id, Niveau, Libelle, CreatedAt, Hash) VALUES ($cat, 13, 'Catégorie 13', $at, 'h-demo')
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO Echelons (Id, Numero, Libelle, CreatedAt, Hash) VALUES ($ech, 5, 'Échelon 5', $at, 'h-demo')
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO Grades (Id, Libelle, CorpsId, Ordre, CreatedAt, Hash) VALUES ($gid, 'Professeur de l''École primaire', $cid, 1, $at, 'h-demo')
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO ValeurPoint (Id, DateEffet, Valeur, Version, Hash, CreatedAt) VALUES ('VP-PILOTE', '2007-01-01', 45, 'v', 'h-demo', $at)
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO GrilleIndiciaire (Id, CategorieId, DateEffet, IndiceMin, Version, Hash, CreatedAt)
            VALUES ('GI-PILOTE', $cat, '2020-01-01', 578, 'v', 'h-demo', $at)
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO IndicesEchelon (Id, EchelonId, DateEffet, Indice, Version, Hash, CreatedAt)
            VALUES ('IE-PILOTE', $ech, '2020-01-01', 100, 'v', 'h-demo', $at)
            ON CONFLICT(Id) DO NOTHING;
            """, new { fid = FiliereId, cid = CorpsId, cat = CategorieId, ech = EchelonId, gid = GradeId, at }, tx);

        var inserted = await connection.ExecuteAsync("""
            INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, CreatedAt)
            VALUES ($id, 'MAT-PILOTE', 'Test', 'Pilote', '1985-01-01', '2010-09-01', 'M', $at)
            ON CONFLICT(Id) DO NOTHING;
            INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
            VALUES ('C-PILOTE', $id, $gid, $cat, $ech, 'STATUTAIRE', '2010-09-01', 'Recrutement', $at)
            ON CONFLICT(Id) DO NOTHING;
            """, new { id = AgentId, gid = GradeId, cat = CategorieId, ech = EchelonId, at }, tx);

        await tx.CommitAsync(ct).ConfigureAwait(false);
        report.Add("Agents (demo)", 1, inserted);
        return report;
    }
}
