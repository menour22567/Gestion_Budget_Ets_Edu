namespace PaieEducation.Reporting.Documents;

/// <summary>
/// Contrat d'un modèle de document (Phase 7, 7.1). Un modèle est une version
/// immuable d'un template : il porte un identifiant logique (<see cref="Id"/>),
/// un numéro de version (<see cref="Version"/>) et sait rendre
/// <typeparamref name="TInput"/> en flux binaire. Plusieurs modèles peuvent
/// coexister (ex. bulletin v1, attestation v1, attestation v2) ; le
/// <see cref="DocumentModelRegistry"/> les résout par
/// (<see cref="Type"/>, <see cref="Id"/>, <see cref="Version"/>).
/// </summary>
/// <remarks>
/// Le rendu est strictement déterministe : aucune I/O, aucun recalcul métier
/// (le <c>BulletinSnapshot</c> est déjà figé par construction).
/// </remarks>
public interface IDocumentModel<TInput>
{
    /// <summary>Identifiant logique du modèle (ex. <c>"bulletin"</c>).</summary>
    string Id { get; }

    /// <summary>Numéro de version (1, 2, ...). Verrouillé à la publication.</summary>
    int Version { get; }

    /// <summary>
    /// Génère le document (PDF/Excel) à partir de l'entrée figée. Renvoie les
    /// octets du document, jamais un chemin disque.
    /// </summary>
    byte[] Render(TInput input);
}
