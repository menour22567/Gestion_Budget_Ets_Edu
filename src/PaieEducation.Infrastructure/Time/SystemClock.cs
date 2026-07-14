using PaieEducation.Shared.Time;

namespace PaieEducation.Infrastructure.Time;

/// <summary>
/// Implémentation système de <see cref="IClock"/> basée sur l'horloge du poste.
/// Enregistrée en singleton dans le conteneur d'injection (Composition Root).
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
