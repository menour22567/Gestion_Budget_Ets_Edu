namespace PaieEducation.Bootstrapper;

/// <summary>
/// Point d'entrée WPF (Composition Root). Sera enrichi en Phase 5/6
/// (configuration, injection de dépendances, migrations, ouverture du Shell).
/// Le type de base est pleinement qualifié pour éviter la collision avec
/// le namespace <c>PaieEducation.Application</c>.
/// </summary>
public partial class App : System.Windows.Application
{
}
