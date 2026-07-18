using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PaieEducation.Domain.Calcul.Explicabilite;
using PaieEducation.Domain.Calcul.Pipeline;

namespace PaieEducation.Presentation.Controls;

public sealed partial class ExplainabilityPanel : UserControl
{
    public static readonly DependencyProperty BulletinProperty =
        DependencyProperty.Register(
            nameof(Bulletin), typeof(Bulletin), typeof(ExplainabilityPanel),
            new PropertyMetadata(null, OnBulletinChanged));

    public ExplainabilityPanel()
    {
        InitializeComponent();
    }

    public Bulletin? Bulletin
    {
        get => (Bulletin?)GetValue(BulletinProperty);
        set => SetValue(BulletinProperty, value);
    }

    public ObservableCollection<NœudExplication> Nœuds { get; } = [];

    private static void OnBulletinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (ExplainabilityPanel)d;
        panel.Nœuds.Clear();
        if (e.NewValue is Bulletin bulletin)
            panel.ConstruireArbre(bulletin);
    }

    private void ConstruireArbre(Bulletin bulletin)
    {
        foreach (var ligne in bulletin.Lignes)
            Nœuds.Add(ConstruireNœudLigne(ligne));
    }

    private static NœudExplication ConstruireNœudLigne(BulletinLigne ligne)
    {
        var nœud = new NœudExplication(ligne.RubriqueId, $"{ligne.Montant.Amount:N0} DA", FontWeights.SemiBold);

        nœud.Enfants.Add(new NœudExplication("Nature", ligne.Nature.ToString()));
        nœud.Enfants.Add(new NœudExplication("Formule", ligne.Explication.Formule));

        foreach (var v in ligne.Explication.Variables)
            nœud.Enfants.Add(new NœudExplication(v.Nom, $"{v.Valeur:N2}"));

        if (ligne.Explication.DetailIrg is { } irg)
        {
            var irgNœud = new NœudExplication("IRG", irg.EtapeAppliquee);
            irgNœud.Enfants.Add(new NœudExplication("Brut", $"{irg.Brut:N0} DA"));
            irgNœud.Enfants.Add(new NœudExplication("Abattement", $"{irg.Abattement:N0} DA"));
            irgNœud.Enfants.Add(new NœudExplication("Revenu imposable", $"{irg.RevenuImposable:N0} DA"));
            irgNœud.Enfants.Add(new NœudExplication("Final", $"{irg.Final:N0} DA"));
            nœud.Enfants.Add(irgNœud);
        }

        return nœud;
    }
}

public sealed class NœudExplication
{
    public string Libellé { get; }
    public string? Valeur { get; }
    public FontWeight Poids { get; }
    public ObservableCollection<NœudExplication> Enfants { get; } = [];

    public NœudExplication(string libellé, string? valeur = null, FontWeight? poids = null)
    {
        Libellé = libellé;
        Valeur = valeur;
        Poids = poids ?? FontWeights.Normal;
    }
}
