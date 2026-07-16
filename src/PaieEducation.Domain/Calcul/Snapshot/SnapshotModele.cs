using PaieEducation.Domain.Calcul.Pipeline;

namespace PaieEducation.Domain.Calcul.Snapshot;

/// <summary>
/// Instantané reproductible d'un calcul (Snapshot Engine, RM-105 ; V4 Tome C
/// vol. 9 §18). <see cref="Input"/> porte tout le contexte de calcul (variables,
/// barèmes, conditions, cotisations, règle IRG — déjà immuable) : rejouer
/// <c>CalculationPipeline.Calculer(snapshot.Input)</c> reproduit
/// <see cref="Resultat"/> à l'identique (déterminisme, ADR-0005).
/// </summary>
/// <remarks>
/// C'est la source contre laquelle un rappel se calcule (ADR-0008) : jamais
/// une réévaluation du passé, toujours ce snapshot figé.
/// </remarks>
public sealed record BulletinSnapshot(
    PayrollInput Input,
    Bulletin Resultat,
    string CapturesLe);

/// <summary>
/// Capture un snapshot. Ne fait aucune I/O ni lecture d'horloge —
/// <paramref name="horodatage"/> est fourni par l'appelant (ADR-0005). Pas
/// appelé automatiquement par le pipeline : seul un bulletin que l'appelant
/// choisit de figer est snapshoté (une simulation/dry-run, ADR-0007 D8, n'a
/// pas besoin de l'être).
/// </summary>
public sealed class SnapshotEngine
{
    public BulletinSnapshot Capturer(PayrollInput input, Bulletin resultat, string horodatage)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resultat);
        ArgumentNullException.ThrowIfNull(horodatage);
        return new BulletinSnapshot(input, resultat, horodatage);
    }
}
