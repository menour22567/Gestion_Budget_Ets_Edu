namespace PaieEducation.Reporting.Documents;

/// <summary>
/// Échec explicite : aucun <see cref="IDocumentModel{TInput}"/> n'est enregistré
/// pour le triplet (type d'entrée, identifiant, version) demandé. Distinct des
/// exceptions framework pour permettre aux appelants (use cases, presentation)
/// de la détecter et de remonter une erreur métier claire.
/// </summary>
public sealed class DocumentModelNotFoundException : InvalidOperationException
{
    public Type InputType { get; }
    public string ModelId { get; }
    public int Version { get; }

    public DocumentModelNotFoundException(Type inputType, string modelId, int version)
        : base($"Aucun modèle de document enregistré pour ({inputType.Name}, id='{modelId}', version={version}).")
    {
        InputType = inputType;
        ModelId = modelId;
        Version = version;
    }
}
