using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PaieEducation.Presentation.Controls;

public sealed partial class MoneyTextBox : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(decimal), typeof(MoneyTextBox),
            new FrameworkPropertyMetadata(0m, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty DeviseProperty =
        DependencyProperty.Register(
            nameof(Devise), typeof(string), typeof(MoneyTextBox),
            new PropertyMetadata("DZD"));

    private static readonly CultureInfo DzdCulture = new("fr-DZ") { NumberFormat = { CurrencySymbol = "DA" } };

    private bool _isFormatted;

    public MoneyTextBox()
    {
        InitializeComponent();
        MettreAJourAffichage();
    }

    public decimal Value
    {
        get => (decimal)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Devise
    {
        get => (string)GetValue(DeviseProperty);
        set => SetValue(DeviseProperty, value);
    }

    private string DisplayText
    {
        get => InputTextBox.Text;
        set => InputTextBox.Text = value;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (MoneyTextBox)d;
        if (!ctrl._isFormatted)
            ctrl.MettreAJourAffichage();
    }

    private void MettreAJourAffichage()
    {
        InputTextBox.Text = Value == 0m ? string.Empty : $"{Value:N0} {Devise}";
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var texte = InputTextBox.Text[..InputTextBox.SelectionStart]
                    + e.Text
                    + InputTextBox.Text[InputTextBox.SelectionStart..];
        e.Handled = !decimal.TryParse(texte, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                    && texte != "-";
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (Value != 0m)
            InputTextBox.Text = Value.ToString("0.##", CultureInfo.InvariantCulture);
        InputTextBox.SelectAll();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        var texte = InputTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(texte))
        {
            Value = 0m;
            MettreAJourAffichage();
            return;
        }

        texte = texte.Replace(Devise, "").Replace("DA", "").Trim();
        if (decimal.TryParse(texte, NumberStyles.Number, DzdCulture, out var valeur)
            || decimal.TryParse(texte, NumberStyles.Number, CultureInfo.InvariantCulture, out valeur))
        {
            _isFormatted = true;
            Value = Math.Round(valeur, 2, MidpointRounding.AwayFromZero);
            _isFormatted = false;
            MettreAJourAffichage();
        }
        else
        {
            MettreAJourAffichage();
        }
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var texte = (string)e.DataObject.GetData(typeof(string))!;
            if (!decimal.TryParse(texte, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }
}
