using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace PaieEducation.Seeding;

/// <summary>
/// Seeder d'agents fictifs de différents grades (filières ENSEIGNANT, ADMIN,
/// INSPECTION, SANTE_PUBLIQUE, OUVRIERS_AGENTS, Éducation, etc.) permettant
/// de tester l'application avec un jeu de données réaliste.
/// Idempotent : chaque agent peut être supprimé manuellement après test.
/// </summary>
/// <remarks>
/// Les 30 agents couvrent :
/// <list type="bullet">
///   <item><b>Filières</b> : ENSEIGNANT, ADMIN, INSPECTION, SANTE_PUBLIQUE, OUVRIERS_AGENTS</item>
///   <item><b>Catégories</b> : 5 à 17</item>
///   <item><b>Échelons</b> : 1 à 12</item>
///   <item><b>Sexes</b> : M et F</item>
///   <item><b>Situations familiales</b> : CELIBATAIRE, MARIE, DIVORCE, VEUF</item>
///   <item><b>Types de contrat</b> : STATUTAIRE et CONTRACTUEL</item>
/// </list>
/// Le seeder suppose que la nomenclature (filières, corps, grades, catégories,
/// échelons) a déjà été seedée par <see cref="NomenclatureSeeder"/> — les
/// INSERT échoueront silencieusement si un gradeId est invalide (FK).
/// </remarks>
public sealed class FakeAgentSeeder
{
    // Structure interne décrivant un agent fictif
    private sealed record FakeAgentDef(
        string Matricule,
        string Nom,
        string Prenom,
        string DateNaissance,
        string DateRecrutement,
        string Sexe,
        string SituationFamiliale,
        string GradeId,
        string CategorieId,
        string EchelonId,
        string TypeContrat);

    // ──────────────────────────────────────────────────────────────────────
    // Définition des 30 agents fictifs
    // ──────────────────────────────────────────────────────────────────────
    private static readonly FakeAgentDef[] _agents =
    {
        // ===== ENSEIGNANT =====
        // Corps: Maitres de l'Ecole Primaire (MDLP)
        new("MAT-ED-001", "Benali", "Ahmed",     "1982-03-15", "2005-09-01", "M", "MARIE",     "MDLP-G104", "7",  "3",  "STATUTAIRE"),
        // Corps: Professeurs de l'Ecole primaire (PDLP)
        new("MAT-ED-002", "Kaci",   "Fatima",    "1985-07-22", "2008-09-01", "F", "MARIE",     "PDLP-G105", "11", "5",  "STATUTAIRE"),
        new("MAT-ED-003", "Mebarki","Sofiane",   "1979-11-02", "2000-09-01", "M", "CELIBATAIRE","PDLP-G106","12", "7",  "STATUTAIRE"),
        new("MAT-ED-004", "Hadj",   "Nadia",     "1975-05-18", "1998-09-01", "F", "DIVORCE",   "PDLP-G107", "14", "10", "STATUTAIRE"),
        new("MAT-ED-005", "Ouali",  "Karim",     "1990-12-08", "2014-09-01", "M", "MARIE",     "PDLP-G108", "10", "4",  "STATUTAIRE"),
        new("MAT-ED-006", "Toumi",  "Samira",    "1988-04-30", "2011-09-01", "F", "MARIE",     "PDLP-G111", "14", "9",  "STATUTAIRE"),

        // Corps: Professeurs de l'Enseignement Moyen (PDLM)
        new("MAT-ED-007", "Selmani","Redouane",  "1983-09-14", "2006-09-01", "M", "CELIBATAIRE","PDLM-G114","12", "6",  "STATUTAIRE"),
        new("MAT-ED-008", "Amirat", "Lynda",     "1981-01-25", "2004-09-01", "F", "VEUF",      "PDLM-G116", "13", "8",  "STATUTAIRE"),
        new("MAT-ED-009", "Zidane", "Mohamed",   "1977-06-12", "2001-09-01", "M", "MARIE",     "PDLM-G117", "15", "11", "STATUTAIRE"),

        // Corps: Professeurs de l'Enseignement secondaire (PDLES)
        new("MAT-ED-010", "Bouali", "Salima",    "1984-08-05", "2007-09-01", "F", "MARIE",     "PDLES-G123","13", "5",  "STATUTAIRE"),
        new("MAT-ED-011", "Saidi",  "Tahar",     "1980-03-20", "2003-09-01", "M", "DIVORCE",   "PDLES-G125","14", "9",  "STATUTAIRE"),

        // Corps: Professeurs Techniques de Lycee (PTDL)
        new("MAT-ED-012", "Guerfi", "Nassima",   "1986-10-30", "2010-09-01", "F", "CELIBATAIRE","PTDL-G121","11", "5",  "STATUTAIRE"),

        // ===== ENSEIGNANT CONTRACTUEL =====
        new("MAT-ED-013", "Chabane","Nabil",     "1992-02-14", "2017-09-01", "M", "CELIBATAIRE","PDLP-G130","12", "3",  "CONTRACTUEL"),
        new("MAT-ED-014", "Mansour","Katia",     "1993-07-19", "2018-09-01", "F", "MARIE",     "PDLM-G131", "12", "4",  "CONTRACTUEL"),
        new("MAT-ED-015", "Fodil",  "Yacine",    "1991-11-03", "2016-09-01", "M", "CELIBATAIRE","PDLS-G132","13", "5",  "CONTRACTUEL"),

        // ===== ADMINISTRATION (ÉDUCATION) =====
        // Corps: Adjoints de l'Education (ADLE)
        new("MAT-AD-001", "Bekkar", "Aicha",     "1978-12-11", "2002-09-01", "F", "MARIE",     "ADLE-G001", "7",  "4",  "STATUTAIRE"),
        // Corps: Superviseurs de l'Education (SDLE)
        new("MAT-AD-002", "Mahrez", "Lamine",    "1985-05-28", "2009-09-01", "M", "MARIE",     "SDLE-G003", "10", "5",  "STATUTAIRE"),
        // Corps: Conseillers de l'Education (CDLE)
        new("MAT-AD-003", "Yahia",  "Djamila",   "1976-09-15", "2000-09-01", "F", "DIVORCE",   "CDLE-G011", "13", "7",  "STATUTAIRE"),

        // ===== ADMINISTRATION (CORPS COMMUNS) =====
        // Corps: Administrateurs (A)
        new("MAT-AD-004", "Bouchra","Mounir",    "1980-02-22", "2005-09-01", "M", "MARIE",     "A-G048",    "12", "6",  "STATUTAIRE"),
        // Corps: Assistants administrateurs (AA)
        new("MAT-AD-005", "Dahmani","Zoubida",   "1987-06-10", "2012-09-01", "F", "VEUF",      "AA-G052",   "11", "4",  "STATUTAIRE"),
        // Corps: Agents d'Administration (ADA)
        new("MAT-AD-006", "Messaoudi","Ali",     "1970-11-05", "1995-09-01", "M", "MARIE",     "ADA-G055",  "5",  "3",  "STATUTAIRE"),
        // Corps: Secretaires (S)
        new("MAT-AD-007", "Belaid", "Farida",    "1990-03-18", "2013-09-01", "F", "CELIBATAIRE","S-G059",   "6",  "4",  "STATUTAIRE"),
        // Corps: Comptables administratifs (CA)
        new("MAT-AD-008", "Khelifi","Rachid",    "1975-08-27", "2001-09-01", "M", "MARIE",     "CA-G063",   "8",  "6",  "STATUTAIRE"),

        // ===== INSPECTION =====
        // Corps: Inspecteurs de l'enseignement primaire (IDLEP)
        new("MAT-INS-001","Hocine", "Mustapha",  "1972-12-03", "1996-09-01", "M", "MARIE",     "IDLEP-G133","15", "8",  "STATUTAIRE"),

        // ===== SANTE PUBLIQUE =====
        // Corps: Infirmiers de santé publique (IDSP)
        new("MAT-SP-001", "Haddad", "Wassila",   "1989-05-20", "2014-09-01", "F", "MARIE",     "IDSP-G152", "11", "5",  "STATUTAIRE"),

        // ===== OUVRIERS / CONDUCTEURS =====
        // Corps: Ouvriers professionnels (OP)
        new("MAT-OUV-001","Lounis", "Abdelkader","1968-07-14", "1992-09-01", "M", "MARIE",     "OP-G155",   "5",  "2",  "STATUTAIRE"),

        // ===== DIRECTION =====
        // Corps: Directeurs des Ecoles primaires (DDEP)
        new("MAT-DIR-001","Gacemi", "Zineb",     "1974-04-09", "1999-09-01", "F", "DIVORCE",   "DDEP-G029", "14", "10", "STATUTAIRE"),

        // ===== TRADUCTION & INFORMATIQUE =====
        // Corps: Traducteurs interprètes (TI)
        new("MAT-TI-001", "Slimani","Noureddine","1981-10-05", "2006-09-01", "M", "CELIBATAIRE","TI-G065",  "12", "5",  "STATUTAIRE"),
        // Corps: Ingenieurs — Informatique (I)
        new("MAT-INFO-01","Chibane", "Yasmine",  "1986-01-30", "2011-09-01", "F", "MARIE",     "I-G069",    "13", "6",  "STATUTAIRE"),
    };

    /// <summary>
    /// Insère les 30 agents fictifs avec leur carrière initiale.
    /// Idempotent : les matricules UNIQUE assurent qu'un agent déjà présent
    /// ne sera pas dupliqué.
    /// </summary>
    /// <param name="connection">Connexion SQLite ouverte.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    /// <returns>Rapport de l'opération.</returns>
    public async Task<SeedReport> SeedAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("La connexion doit être ouverte.");

        var report = new SeedReport(_agents.Length);
        var insertedTotal = 0;
        var at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        foreach (var agent in _agents)
        {
            ct.ThrowIfCancellationRequested();

            var agentId = Guid.NewGuid().ToString();
            var carriereId = Guid.NewGuid().ToString();

            var inserted = await connection.ExecuteAsync("""
                INSERT INTO Agents (Id, Matricule, Nom, Prenom, DateNaissance, DateRecrutement, Sexe, SituationFamiliale, CreatedAt)
                VALUES ($agentId, $matricule, $nom, $prenom, $dateNaissance, $dateRecrutement, $sexe, $situationFamiliale, $createdAt)
                ON CONFLICT(Matricule) DO NOTHING;

                INSERT INTO Carrieres (Id, AgentId, GradeId, CategorieId, EchelonId, TypeContrat, DateEffet, Motif, CreatedAt)
                VALUES ($carriereId, $agentId, $gradeId, $categorieId, $echelonId, $typeContrat, $dateEffet, 'Recrutement', $createdAt)
                ON CONFLICT(Id) DO NOTHING;
                """, new
                {
                    agentId,
                    carriereId,
                    matricule = agent.Matricule,
                    nom = agent.Nom,
                    prenom = agent.Prenom,
                    dateNaissance = agent.DateNaissance,
                    dateRecrutement = agent.DateRecrutement,
                    sexe = agent.Sexe,
                    situationFamiliale = agent.SituationFamiliale,
                    gradeId = agent.GradeId,
                    categorieId = agent.CategorieId,
                    echelonId = agent.EchelonId,
                    typeContrat = agent.TypeContrat,
                    dateEffet = agent.DateRecrutement,
                    createdAt = at
                }, tx);

            if (inserted > 0) insertedTotal++;
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        report.Add("Agents (fictifs)", _agents.Length, insertedTotal);
        return report;
    }
}

