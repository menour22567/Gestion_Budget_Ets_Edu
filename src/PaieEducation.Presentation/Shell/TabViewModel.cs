using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PaieEducation.Presentation.Shell;

/// <summary>
/// Un onglet du Shell : titre affiché, ViewModel d'écran hébergé (la Vue est
/// résolue par le <c>DataTemplate</c> implicite de <c>ViewTemplates.xaml</c>),
/// et si l'onglet peut être fermé (faux uniquement pour l'onglet Accueil).
/// </summary>
public sealed partial class TabViewModel : ObservableObject
{
    private readonly Action<TabViewModel> _fermer;

    public TabViewModel(string titre, object contenu, bool estFermable, Action<TabViewModel> fermer)
    {
        Titre = titre;
        Contenu = contenu;
        EstFermable = estFermable;
        _fermer = fermer ?? throw new ArgumentNullException(nameof(fermer));
    }

    public string Titre { get; }

    public object Contenu { get; }

    public bool EstFermable { get; }

    [RelayCommand]
    private void Fermer() => _fermer(this);
}
