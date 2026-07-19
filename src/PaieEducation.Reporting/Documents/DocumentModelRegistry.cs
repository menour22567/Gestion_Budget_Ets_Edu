using System.Collections.Concurrent;

namespace PaieEducation.Reporting.Documents;

/// <summary>
/// Registre de modèles de documents versionnés (Phase 7, 7.1). Indexe les
/// <see cref="IDocumentModel{TInput}"/> par
/// (<see cref="Type"/> de l'entrée, identifiant du modèle, version) et résout
/// la combinaison demandée par le <c>ReportingService</c>.
///
/// L'enregistrement est thread-safe ; les modèles sont conçus pour être
/// des singletons (rendu déterministe, sans état mutable).
/// </summary>
public sealed class DocumentModelRegistry
{
    // Clé = (Type.Name, Id, Version). On utilise Type.Name (pas AssemblyQualifiedName)
    // pour découpler le registre du chemin de l'assembly, et Id/Version tels
    // que déclarés par le modèle.
    private readonly ConcurrentDictionary<ModelKey, object> _models = new();

    /// <summary>
    /// Enregistre <paramref name="model"/>. Refuse les doublons pour rester
    /// strict (un modèle = une seule version d'un identifiant) ; un nouveau
    /// modèle avec un nouvel identifiant ou une version différente coexiste.
    /// </summary>
    public void Register<TInput>(IDocumentModel<TInput> model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var key = new ModelKey(typeof(TInput).Name, model.Id, model.Version);
        if (!_models.TryAdd(key, model))
        {
            throw new InvalidOperationException(
                $"Modèle déjà enregistré pour ({key.TypeName}, id='{key.Id}', version={key.Version}).");
        }
    }

    /// <summary>
    /// Résout le modèle pour <typeparamref name="TInput"/>. Lève
    /// <see cref="DocumentModelNotFoundException"/> si aucun ne correspond.
    /// </summary>
    public IDocumentModel<TInput> Resolve<TInput>(string modelId, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        var key = new ModelKey(typeof(TInput).Name, modelId, version);
        if (_models.TryGetValue(key, out var model))
        {
            return (IDocumentModel<TInput>)model;
        }
        throw new DocumentModelNotFoundException(typeof(TInput), modelId, version);
    }

    /// <summary>
    /// Indique si un modèle est enregistré pour ce triplet. Utile pour les
    /// tests et pour le mode dégradé (ex. un export indisponible).
    /// </summary>
    public bool IsRegistered<TInput>(string modelId, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return _models.ContainsKey(new ModelKey(typeof(TInput).Name, modelId, version));
    }

    /// <summary>Clé d'indexation dans le registre (type d'entrée, identifiant, version).</summary>
    private readonly record struct ModelKey(string TypeName, string Id, int Version);
}
