using System.Globalization;
using PaieEducation.Domain.Calcul.Audit;
using PaieEducation.Domain.Calcul.Constants;
using PaieEducation.Domain.Calcul.Cotisations;
using PaieEducation.Domain.Calcul.Explicabilite;
using PaieEducation.Domain.Calcul.Formules;
using PaieEducation.Domain.Calcul.Irg;
using PaieEducation.Domain.Calcul.Services;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Calcul.Validation;
using PaieEducation.Shared.Guards;
using PaieEducation.Shared.Money;
using PaieEducation.Shared.Results;
using PaieEducation.Domain.Workbench.Constants;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.Services;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Domain.Calcul.Pipeline;

public sealed class CalculationPipeline
{
    private readonly FormulaEvaluator _evaluateur = new();
    private readonly IrgCalculator _irg;
    private readonly ContributionCalculator _cotisation = new();
    private readonly BaremeResolver _bareme = new();
    private readonly RegleEligibiliteEvaluator _eligibilite = new(new CritereEligibiliteResolver());
    private readonly DependencyResolver _dependances = new();
    private readonly ValidationEngine _validation = new();
    private readonly ArrondiService _arrondi;

    public CalculationPipeline(ArrondiService arrondi, decimal seuilExoneration, decimal plafondLissageGeneral)
    {
        _arrondi = arrondi ?? throw new ArgumentNullException(nameof(arrondi));
        _irg = new IrgCalculator(seuilExoneration, plafondLissageGeneral);
    }

    public Result<Bulletin> Calculer(PayrollInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var gains = input.Rubriques.Where(r => r.Nature == NatureRubrique.Gain)
            .OrderBy(r => r.Ordre).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        var ordre = _dependances.Ordonner(
            gains.Select(g => g.Id).ToList(), Array.Empty<DependanceArete>());
        if (ordre.IsFailure)
            return Result.Failure<Bulletin>(ordre.Error);

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
            {
                if (contexte.SourceAbsente)
                {
                    etapes.Add(new EtapeAudit(rub.Id, rang, false,
                        ResultatEligibilite.Abstention(contexte.SourceAbsenteRubrique!), null));
                    continue;
                }
                return Result.Failure<Bulletin>(
                    Error.Evaluation($"Rubrique « {rub.Id} » : {montant.Error.Message}"));
            }
            var lectures = contexte.PurgerLectures();

            var arrondi = _arrondi.Arrondir(montant.Value);
            variables[rub.Id] = arrondi;
            var montantMoney = new Money(arrondi);
            lignes.Add(new BulletinLigne(rub.Id, NatureRubrique.Gain, montantMoney,
                rub.EstImposable, rub.EstCotisable, new ExplicationLigne(rub.Expression, lectures)));
            etapes.Add(new EtapeAudit(rub.Id, rang, Eligible: true, eval, arrondi));
        }

        var totalGains = new Money(lignes.Where(l => l.Nature == NatureRubrique.Gain).Sum(l => l.Montant.Amount));
        var assietteCotisable = new Money(lignes
            .Where(l => l is { Nature: NatureRubrique.Gain, Cotisable: true }).Sum(l => l.Montant.Amount));
        var gainsImposables = new Money(lignes
            .Where(l => l is { Nature: NatureRubrique.Gain, Imposable: true }).Sum(l => l.Montant.Amount));

        Money cotisationsSalariales = Money.Zero;
        rang = 0;
        foreach (var cot in input.Cotisations)
        {
            rang++;
            var assiette = AssiettePour(cot.Def.Assiette, assietteCotisable, gainsImposables, variables);
            var montantCot = _cotisation.Calculer(cot.Def, assiette);
            if (montantCot.IsFailure)
                return Result.Failure<Bulletin>(montantCot.Error);

            var arrondi = _arrondi.Arrondir(montantCot.Value);
            var montantMoney = new Money(arrondi);
            var explication = new ExplicationLigne(
                $"{cot.Def.Code} sur assiette", new[] { new VariableUtilisee("Assiette", assiette) });
            lignes.Add(new BulletinLigne(cot.Def.Code, NatureRubrique.Cotisation, montantMoney,
                Imposable: false, Cotisable: false, explication));
            etapes.Add(new EtapeAudit(cot.Def.Code, rang, Eligible: true, ResultatEligibilite.Eligible(), arrondi));
            if (cot.EstSalariale)
                cotisationsSalariales += montantMoney;
        }

        var assietteImposable = new Money(Math.Max(0m, gainsImposables.Amount - cotisationsSalariales.Amount));

        Money irg = Money.Zero;
        if (input.RegleIrg is { } regleIrg)
        {
            var resultatIrg = _irg.Calculer(assietteImposable.Amount, input.Profil, regleIrg);
            if (resultatIrg.IsFailure)
                return Result.Failure<Bulletin>(resultatIrg.Error);
            irg = new Money(_arrondi.Arrondir(resultatIrg.Value.Final));
            var explicationIrg = new ExplicationLigne(
                "IRG", Array.Empty<VariableUtilisee>(), resultatIrg.Value);
            lignes.Add(new BulletinLigne("IRG", NatureRubrique.Impot, irg,
                Imposable: false, Cotisable: false, explicationIrg));
            etapes.Add(new EtapeAudit("IRG", ++rang, Eligible: true, ResultatEligibilite.Eligible(), irg.Amount));
        }

        var totalRetenues = cotisationsSalariales + irg
            + new Money(lignes.Where(l => l.Nature == NatureRubrique.Retenue).Sum(l => l.Montant.Amount));
        var net = _arrondi.Arrondir(totalGains - totalRetenues);

        var bulletin = new Bulletin(
            lignes, totalGains, assietteCotisable, assietteImposable, totalRetenues, irg, net,
            new JournalAudit(etapes));

        return _validation.Valider(bulletin);
    }

    private static decimal AssiettePour(
        ReferenceAssiette reference, Money cotisable, Money imposable, IReadOnlyDictionary<string, decimal> vars)
        => reference switch
        {
            ReferenceAssiette.AssietteCotisable => cotisable.Amount,
            ReferenceAssiette.AssietteImposable => imposable.Amount,
            ReferenceAssiette.TraitementBase => vars.GetValueOrDefault(VariablesCles.TraitementBase),
            ReferenceAssiette.TraitementBrut => vars.GetValueOrDefault(VariablesCles.TraitementBrut),
            _ => 0m
        };

    private sealed class PipelineContext : IFormulaContext
    {
        private readonly IReadOnlyDictionary<string, decimal> _variables;
        private readonly IReadOnlyDictionary<string, decimal> _sources;
        private readonly IReadOnlyDictionary<string, string> _clesBareme;
        private readonly IReadOnlyList<BaremeValue> _baremes;
        private readonly string _datePaie;
        private readonly BaremeResolver _resolver;
        private readonly List<VariableUtilisee> _lectures = new();
        private string? _sourceAbsenteRubrique;

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

        public bool SourceAbsente => _sourceAbsenteRubrique is not null;
        public string? SourceAbsenteRubrique => _sourceAbsenteRubrique;

        public Result<decimal> ResoudreSource(string rubrique)
        {
            if (_sources.TryGetValue(rubrique, out var v))
                return Result.Success(v);
            _sourceAbsenteRubrique = rubrique;
            return Result.Failure<decimal>(Error.NotFound($"Source de valeur absente : {rubrique}"));
        }

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

        private static string NormaliserDimension(string d) => BaremeDimensionKeys.Normaliser(d.ToUpperInvariant());

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
