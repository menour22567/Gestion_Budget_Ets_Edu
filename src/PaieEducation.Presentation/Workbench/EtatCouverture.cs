namespace PaieEducation.Presentation.Workbench;

/// <summary>
/// État d'une cellule de la matrice de couverture (P7) — 3 états, décidés
/// avec l'utilisateur le 20/07/2026 : le 4e état du mockup d'origine
/// (J3I §5.5, « Gris = non applicable ») est hors périmètre, faute d'un
/// concept de portée sur <c>Rubriques</c> (pas de <c>FiliereId</c>/scope)
/// permettant de le distinguer honnêtement d'un vrai trou de couverture.
/// </summary>
public enum EtatCouverture
{
    Active,
    Inactive,
    NonCouverte,
}

/// <summary>Dérive l'<see cref="EtatCouverture"/> depuis les deux booléens produits par <c>ListerMatriceCouverture</c>.</summary>
public static class EtatCouvertureClassificateur
{
    public static EtatCouverture Classifier(bool couverte, bool active) => (couverte, active) switch
    {
        (true, true) => EtatCouverture.Active,
        (true, false) => EtatCouverture.Inactive,
        _ => EtatCouverture.NonCouverte,
    };
}
