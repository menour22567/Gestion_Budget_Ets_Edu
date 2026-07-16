using System.Globalization;
using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Explicabilite;
using PaieEducation.Domain.Calcul.Formules;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Calcul.Validation;
using PaieEducation.Domain.Common;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Calcul.Pipeline;

/// <summary>
/// Orchestre le calcul d'un bulletin à partir d'un <see cref="PayrollInput"/>
/// résolu : ordre de calcul (DAG) → éligibilité DNF → évaluation des formules
/// (lues en base) → assiettes → cotisations → IRG → totaux → net → contrôles
/// finaux (RM-081). Pur, déterministe (ADR-0005) : aucune I/O, tout arrondi
/// passe par <see cref="ArrondiService"/> (RM-120).
/// </summary>
public sealed class CalculationPipeline
{
    private readonly FormulaEvaluator _evaluateur = new();
    private readonly IrgCalculator _irg = new();
    private readonly ContributionCalculator _cotisation = new();
    private readonly BaremeResolver _bareme = new();
    private readonly RegleEligibiliteEvaluator _eligibilite = new(new CritereEligibiliteResolver());
    private readonly DependencyResolver _dependances = new();
    private readonly ValidationEngine _validation = new();
    private readonly ArrondiService _arrondi;

    public CalculationPipeline(ArrondiService arrondi)
        => _arrondi = arrondi ?? throw new ArgumentNullException(nameof(arrondi));

    /// <summary>Calcule le bulletin. Renvoie le premier échec rencontré (formule, IRG, ...).</summary>
    public Result<Bulletin> Calculer(PayrollInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Ordre de calcul : les gains d'abord, ordonnés par OrdreCalcul (pas de
        // dépendances inter-rubriques dans le pilote → ordre stable). Le
        // DependencyResolver valide l'absence de cycle même sans arête.
        var gains = input.Rubriques.Where(r => r.Nature == NatureRubrique.Gain)
            .OrderBy(r => r.Ordre).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        var ordre = _dependances.Ordonner(
            gains.Select(g => g.Id).ToList(), Array.Empty<DependanceArete>());
        if (ordre.IsFailure)
            return Result.Failure<Bulletin>(ordre.Error);

        // Variables mutables : les résultats des rubriques déjà calculées
        // deviennent disponibles pour les suivantes (référence par code).
        var variables = new Dictionary<string, decimal>(input.Variables, StringComparer.Ordinal);
        var contexte = new PipelineContext(variables, input.SourcesValeur, input.ClesBareme,
            input.Baremes, input.DatePaie, _bareme);

        var lignes = new List<BulletinLigne>();
        var etapes = new List<EtapeAudit>();
        var indexGain = gains.ToDictionary(g => g.Id, StringComparer.Ordinal);
        var rang = 0;

        foreach (var id in ordre.Value)
        {
            var rub = indexGain[id];
            rang++;

            // Éligibilité (DNF) : une rubrique sans condition est due à tous.
            var eval = _eligibilite.Evaluer(rub.Id, input.Agent, input.DatePaie, input.Conditions, input.Criteres);
            if (!eval.EstEligible)
            {
                etapes.Add(new EtapeAudit(rub.Id, rang, Eligible: false, eval, Montant: null));
                continue;
            }

            if (rub.Expression is null)
                return Result.Failure<Bulletin>(
                    Error.Validation($"Rubrique GAIN « {rub.Id} » sans formule."));

            contexte.PurgerLectures();
            var montant = _evaluateur.Evaluer(rub.Expression, contexte);
            if (montant.IsFailure)
                return Result.Failure<Bulletin>(
                    Error.Evaluation($"Rubrique « {rub.Id} » : {montant.Error.Message}"));
            var lectures = contexte.PurgerLectures();

            var arrondi = _arrondi.Arrondir(montant.Value);
            variables[rub.Id] = arrondi;   // disponible pour les rubriques suivantes
            lignes.Add(new BulletinLigne(rub.Id, NatureRubrique.Gain, arrondi,
                rub.EstImposable, rub.EstCotisable, new ExplicationLigne(rub.Expression, lectures)));
            etapes.Add(new EtapeAudit(rub.Id, rang, Eligible: true, eval, arrondi));
        }

        var totalGains = lignes.Where(l => l.Nature == NatureRubrique.Gain).Sum(l => l.Montant);
        var assietteCotisable = lignes.Where(l => l is { Nature: NatureRubrique.Gain, Cotisable: true }).Sum(l => l.Montant);
        var gainsImposables = lignes.Where(l => l is { Nature: NatureRubrique.Gain, Imposable: true }).Sum(l => l.Montant);

        // Cotisations : salariales retenues sur le net ET déductibles de l'imposable.
        // Toujours appliquées si présentes en entrée (aucun DNF ne les conditionne dans
        // le pilote actuel) — éligibilité de convention pour le journal d'audit.
        decimal cotisationsSalariales = 0m;
        rang = 0;
        foreach (var cot in input.Cotisations)
        {
            rang++;
            var assiette = AssiettePour(cot.Def.Assiette, assietteCotisable, gainsImposables, variables);
            var montantCot = _cotisation.Calculer(cot.Def, assiette);
            if (montantCot.IsFailure)
                return Result.Failure<Bulletin>(montantCot.Error);

            var arrondi = _arrondi.Arrondir(montantCot.Value);
            var explication = new ExplicationLigne(
                $"{cot.Def.Code} sur assiette", new[] { new VariableUtilisee("Assiette", assiette) });
            lignes.Add(new BulletinLigne(cot.Def.Code, NatureRubrique.Cotisation, arrondi,
                Imposable: false, Cotisable: false, explication));
            etapes.Add(new EtapeAudit(cot.Def.Code, rang, Eligible: true, ResultatEligibilite.Eligible(), arrondi));
            if (cot.EstSalariale)
                cotisationsSalariales += arrondi;
        }

        // Assiette imposable = Σ gains imposables − cotisations salariales déductibles (J3C §10).
        var assietteImposable = Math.Max(0m, gainsImposables - cotisationsSalariales);

        // IRG sur l'assiette imposable.
        decimal irg = 0m;
        if (input.RegleIrg is { } regleIrg)
        {
            var resultatIrg = _irg.Calculer(assietteImposable, input.Profil, regleIrg);
            if (resultatIrg.IsFailure)
                return Result.Failure<Bulletin>(resultatIrg.Error);
            irg = _arrondi.Arrondir(resultatIrg.Value.Final);
            var explicationIrg = new ExplicationLigne(
                "IRG", Array.Empty<VariableUtilisee>(), resultatIrg.Value);
            lignes.Add(new BulletinLigne("IRG", NatureRubrique.Impot, irg,
                Imposable: false, Cotisable: false, explicationIrg));
            etapes.Add(new EtapeAudit("IRG", ++rang, Eligible: true, ResultatEligibilite.Eligible(), irg));
        }

        var totalRetenues = cotisationsSalariales + irg
            + lignes.Where(l => l.Nature == NatureRubrique.Retenue).Sum(l => l.Montant);
        var net = _arrondi.Arrondir(totalGains - totalRetenues);

        var bulletin = new Bulletin(
            lignes, totalGains, assietteCotisable, assietteImposable, totalRetenues, irg, net,
            new JournalAudit(etapes));

        return _validation.Valider(bulletin);
    }

    private static decimal AssiettePour(
        ReferenceAssiette reference, decimal cotisable, decimal imposable, IReadOnlyDictionary<string, decimal> vars)
        => reference switch
        {
            ReferenceAssiette.AssietteCotisable => cotisable,
            ReferenceAssiette.AssietteImposable => imposable,
            ReferenceAssiette.TraitementBase => vars.GetValueOrDefault("TBASE"),
            ReferenceAssiette.TraitementBrut => vars.GetValueOrDefault("TRT"),
            _ => 0m   // MontantFixe : assiette ignorée par le calculateur
        };

    /// <summary>Contexte de formule adossé au bundle d'entrée + résultats courants.</summary>
    private sealed class PipelineContext : IFormulaContext
    {
        private readonly IReadOnlyDictionary<string, decimal> _variables;
        private readonly IReadOnlyDictionary<string, decimal> _sources;
        private readonly IReadOnlyDictionary<string, string> _clesBareme;
        private readonly IReadOnlyList<BaremeValue> _baremes;
        private readonly string _datePaie;
        private readonly BaremeResolver _resolver;
        private readonly List<VariableUtilisee> _lectures = new();

        public PipelineContext(
            IReadOnlyDictionary<string, decimal> variables,
            IReadOnlyDictionary<string, decimal> sources,
            IReadOnlyDictionary<string, string> clesBareme,
            IReadOnlyList<BaremeValue> baremes,
            string datePaie,
            BaremeResolver resolver)
        {
            _variables = variables;
            _sources = sources;
            _clesBareme = clesBareme;
            _baremes = baremes;
            _datePaie = datePaie;
            _resolver = resolver;
        }

        /// <summary>
        /// Renvoie les variables lues par <see cref="ResoudreVariable"/> depuis le
        /// dernier appel, puis vide le tampon (Explainability Engine, RM-105) —
        /// un appel par rubrique évaluée, encadrant <c>FormulaEvaluator.Evaluer</c>.
        /// </summary>
        public IReadOnlyList<VariableUtilisee> PurgerLectures()
        {
            var copie = _lectures.ToList();
            _lectures.Clear();
            return copie;
        }

        public Result<decimal> ResoudreVariable(string nom)
        {
            if (!_variables.TryGetValue(nom, out var v))
                return Result.Failure<decimal>(Error.Evaluation($"Variable inconnue : {nom}"));
            _lectures.Add(new VariableUtilisee(nom, v));
            return Result.Success(v);
        }

        public Result<decimal> ResoudreSource(string rubrique) =>
            _sources.TryGetValue(rubrique, out var v)
                ? Result.Success(v)
                : Result.Failure<decimal>(Error.NotFound($"Source de valeur absente : {rubrique}"));

        public Result<decimal> ResoudreBareme(string rubrique, string dimension)
        {
            if (!Enum.TryParse<BaremeDimension>(NormaliserDimension(dimension), out var dim))
                return Result.Failure<decimal>(Error.Evaluation($"Dimension de barème inconnue : {dimension}"));
            if (!_clesBareme.TryGetValue(dimension, out var cle))
                return Result.Failure<decimal>(Error.NotFound($"Clé de barème absente pour la dimension {dimension}"));

            var trouve = _resolver.Resoudre(rubrique, dim, cle, _datePaie, _baremes);
            if (trouve is null)
                return Result.Failure<decimal>(Error.NotFound($"Barème absent : {rubrique}/{dimension}/{cle}"));

            return ParserValeurBareme(trouve.Valeur);
        }

        private static string NormaliserDimension(string d) => d.ToUpperInvariant() switch
        {
            "CATEGORIE" => "Categorie",
            "ECHELON" => "Echelon",
            "ANCIENNETE" => "Anciennete",
            "TYPE_ETABLISSEMENT" => "TypeEtablissement",
            "CORPS" => "Corps",
            "GRADE" => "Grade",
            _ => d
        };

        private static Result<decimal> ParserValeurBareme(string valeur)
        {
            if (valeur.Contains('/'))
            {
                var f = Fraction.Parser(valeur);
                return f.IsFailure
                    ? Result.Failure<decimal>(f.Error)
                    : Result.Success(f.Value.VersDecimal());
            }
            return decimal.TryParse(valeur, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
                ? Result.Success(d)
                : Result.Failure<decimal>(Error.Evaluation($"Valeur de barème non numérique : {valeur}"));
        }
    }
}
