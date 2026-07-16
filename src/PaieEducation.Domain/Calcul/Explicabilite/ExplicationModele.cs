using System.Globalization;
using PaieEducation.Domain.Calcul.Irg;

namespace PaieEducation.Domain.Calcul.Explicabilite;

/// <summary>Valeur d'une variable au moment où une formule l'a consommée.</summary>
public sealed record VariableUtilisee(string Nom, decimal Valeur);

/// <summary>
/// Explication d'une ligne de bulletin (Explainability Engine, RM-105 ;
/// V4 Tome C vol. 9 §16). Remplace l'ancien <c>string Detail</c> jetable par
/// une structure exploitable (UI « Pourquoi ce montant ? », audit, snapshot).
/// </summary>
/// <remarks>
/// <see cref="DetailIrg"/> est renseigné uniquement pour la ligne IRG — les
/// données multi-étapes (brut, abattement, lissage) sont déjà produites par
/// <see cref="IrgCalculator"/> et ne sont pas dupliquées ici.
/// </remarks>
public sealed record ExplicationLigne(
    string Formule,
    IReadOnlyList<VariableUtilisee> Variables,
    IrgResultat? DetailIrg = null)
{
    /// <summary>Rendu humain (UI, journaux) — jamais parsé, uniquement affiché.</summary>
    public string Rendu()
    {
        if (DetailIrg is { } irg)
        {
            return $"IRG {irg.EtapeAppliquee} sur {Inv(irg.RevenuImposable)} " +
                   $"(brut {Inv(irg.Brut)} − abattement {Inv(irg.Abattement)}) = {Inv(irg.Final)}";
        }
        var vars = string.Join(", ", Variables.Select(v => $"{v.Nom}={Inv(v.Valeur)}"));
        return vars.Length == 0 ? Formule : $"{Formule} [{vars}]";
    }

    private static string Inv(decimal d) => d.ToString(CultureInfo.InvariantCulture);
}
