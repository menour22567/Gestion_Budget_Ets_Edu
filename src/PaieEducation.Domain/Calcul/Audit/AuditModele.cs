using PaieEducation.Domain.Workbench.Services;

namespace PaieEducation.Domain.Calcul.Audit;

/// <summary>
/// Une étape du pipeline de calcul (Audit Engine, V4 Tome C vol. 9 §17) : quelle
/// rubrique, à quel rang, avec quel verdict d'éligibilité et quel montant.
/// </summary>
/// <remarks>
/// La durée d'exécution (mentionnée par la spec V4) est délibérément absente :
/// elle nécessite une horloge, ce qui violerait la pureté du cœur de calcul
/// (ADR-0005). L'instrumentation temporelle relève d'un wrapper Application
/// autour de <c>CalculationPipeline.Calculer</c> (Phase 5), pas du Domain.
/// </remarks>
public sealed record EtapeAudit(
    string RubriqueId,
    int Ordre,
    bool Eligible,
    ResultatEligibilite Eligibilite,
    decimal? Montant);

/// <summary>Journal d'exécution complet d'un calcul de bulletin.</summary>
public sealed record JournalAudit(IReadOnlyList<EtapeAudit> Etapes);
