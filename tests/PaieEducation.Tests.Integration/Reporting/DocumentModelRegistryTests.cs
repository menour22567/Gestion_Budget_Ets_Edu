using PaieEducation.Reporting.Documents;

namespace PaieEducation.Tests.Integration.Reporting;

/// <summary>
/// Tests unitaires du <see cref="DocumentModelRegistry"/> (Phase 7, 7.1).
/// Le registre est l'entrée officielle pour résoudre un modèle de document
/// versionné ; il doit garantir l'unicité, l'indépendance des versions et
/// un échec explicite quand un modèle est demandé sans avoir été enregistré.
/// </summary>
public class DocumentModelRegistryTests
{
    /// <summary>Modèle de test : implémente le contrat avec des octets fixes.</summary>
    private sealed class FakeModel : IDocumentModel<string>
    {
        private readonly byte[] _bytes;
        public FakeModel(string id, int version, byte[] bytes) { Id = id; Version = version; _bytes = bytes; }
        public string Id { get; }
        public int Version { get; }
        public byte[] Render(string input) => _bytes;
    }

    [Fact]
    public void Register_puis_Resolve_retourne_le_modele_enregistre()
    {
        var registry = new DocumentModelRegistry();
        var model = new FakeModel("bulletin", 1, new byte[] { 1, 2, 3 });
        registry.Register<string>(model);

        var resolved = registry.Resolve<string>("bulletin", 1);

        Assert.Same(model, resolved);
    }

    [Fact]
    public void Resolve_modele_inexistant_leve_DocumentModelNotFoundException()
    {
        var registry = new DocumentModelRegistry();

        var ex = Assert.Throws<DocumentModelNotFoundException>(
            () => registry.Resolve<string>("introuvable", 1));

        Assert.Equal(typeof(string), ex.InputType);
        Assert.Equal("introuvable", ex.ModelId);
        Assert.Equal(1, ex.Version);
    }

    [Fact]
    public void Deux_versions_du_meme_id_coexistent_independamment()
    {
        var registry = new DocumentModelRegistry();
        var v1 = new FakeModel("bulletin", 1, new byte[] { 1 });
        var v2 = new FakeModel("bulletin", 2, new byte[] { 2 });
        registry.Register<string>(v1);
        registry.Register<string>(v2);

        Assert.Same(v1, registry.Resolve<string>("bulletin", 1));
        Assert.Same(v2, registry.Resolve<string>("bulletin", 2));
        Assert.Equal(new byte[] { 1 }, registry.Resolve<string>("bulletin", 1).Render("x"));
        Assert.Equal(new byte[] { 2 }, registry.Resolve<string>("bulletin", 2).Render("x"));
    }

    [Fact]
    public void Register_doublon_Leve_InvalidOperationException()
    {
        var registry = new DocumentModelRegistry();
        registry.Register<string>(new FakeModel("bulletin", 1, new byte[] { 1 }));

        Assert.Throws<InvalidOperationException>(
            () => registry.Register<string>(new FakeModel("bulletin", 1, new byte[] { 2 })));
    }

    [Fact]
    public void IsRegistered_reflete_l_etat_du_registre()
    {
        var registry = new DocumentModelRegistry();

        Assert.False(registry.IsRegistered<string>("bulletin", 1));
        registry.Register<string>(new FakeModel("bulletin", 1, new byte[] { 0 }));
        Assert.True(registry.IsRegistered<string>("bulletin", 1));
        Assert.False(registry.IsRegistered<string>("bulletin", 2));
    }

    [Fact]
    public void Register_modele_null_leve_ArgumentNullException()
    {
        var registry = new DocumentModelRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register<string>(null!));
    }
}
