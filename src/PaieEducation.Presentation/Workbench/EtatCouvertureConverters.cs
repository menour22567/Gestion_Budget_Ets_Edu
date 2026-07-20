using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// Couleur de fond par état (P7) — utilisé par les colonnes de la matrice
/// générées dynamiquement en code-behind (<see cref="MatriceCouvertureView"/>).
/// Couleur redondante avec un symbole (<see cref="EtatCouvertureVersSymboleConverter"/>)
/// pour ne pas reposer sur la seule teinte (accessibilité daltonisme).
/// </summary>
public sealed class EtatCouvertureVersBrushConverter : IValueConverter
{
    public static readonly EtatCouvertureVersBrushConverter Instance = new();

    private static readonly Brush Vert = new SolidColorBrush(Color.FromRgb(0x1E, 0x7A, 0x34));
    private static readonly Brush Orange = new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B));
    private static readonly Brush Rouge = new SolidColorBrush(Color.FromRgb(0xB0, 0x2A, 0x2A));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EtatCouverture.Active => Vert,
        EtatCouverture.Inactive => Orange,
        _ => Rouge,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Symbole par état (P7), en complément de la couleur.</summary>
public sealed class EtatCouvertureVersSymboleConverter : IValueConverter
{
    public static readonly EtatCouvertureVersSymboleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EtatCouverture.Active => "✓",
        EtatCouverture.Inactive => "~",
        _ => "—",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
