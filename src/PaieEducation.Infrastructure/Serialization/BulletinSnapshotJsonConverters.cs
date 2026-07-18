using System.Text.Json;
using System.Text.Json.Serialization;
using PaieEducation.Domain.Calcul.ValueObjects;
using PaieEducation.Domain.Workbench.Enums;
using PaieEducation.Domain.Workbench.ValueObjects;

namespace PaieEducation.Infrastructure.Serialization;

/// <summary>
/// Options JSON pour sérialiser/désérialiser un
/// <see cref="PaieEducation.Domain.Calcul.Snapshot.BulletinSnapshot"/>
/// (ADR-0008 : le snapshot doit rejouer <c>CalculationPipeline.Calculer</c> à
/// l'identique). Enregistre les convertisseurs des Value Objects à
/// constructeur privé + fabrique <c>.Creer(...)</c>, que System.Text.Json ne
/// sait pas construire par réflexion.
/// </summary>
public static class BulletinSnapshotJson
{
    public static JsonSerializerOptions Options { get; } = Creer();

    private static JsonSerializerOptions Creer()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new FractionJsonConverter());
        options.Converters.Add(new PeriodeReglementaireJsonConverter());
        options.Converters.Add(new BaremeValueJsonConverter());
        options.Converters.Add(new ConditionEligibiliteJsonConverter());
        options.Converters.Add(new CritereEligibiliteJsonConverter());
        options.Converters.Add(new MoneyJsonConverter());
        return options;
    }
}

public sealed class FractionJsonConverter : JsonConverter<Fraction>
{
    private sealed record Dto(long Numerateur, long Denominateur);

    public override Fraction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<Dto>(ref reader, options)!;
        return Fraction.Creer(dto.Numerateur, dto.Denominateur);
    }

    public override void Write(Utf8JsonWriter writer, Fraction value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new Dto(value.Numerateur, value.Denominateur), options);
}

public sealed class PeriodeReglementaireJsonConverter : JsonConverter<PeriodeReglementaire>
{
    private sealed record Dto(string DateEffet, string? DateFin);

    public override PeriodeReglementaire Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<Dto>(ref reader, options)!;
        return PeriodeReglementaire.Creer(dto.DateEffet, dto.DateFin);
    }

    public override void Write(Utf8JsonWriter writer, PeriodeReglementaire value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new Dto(value.DateEffet, value.DateFin), options);
}

public sealed class BaremeValueJsonConverter : JsonConverter<BaremeValue>
{
    private sealed record Dto(
        string RubriqueId, BaremeDimension Dimension, string BorneInf, string? BorneSup,
        BaremeTypeValeur TypeValeur, string Valeur, PeriodeReglementaire Periode);

    public override BaremeValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<Dto>(ref reader, options)!;
        return BaremeValue.Creer(dto.RubriqueId, dto.Dimension, dto.BorneInf, dto.BorneSup, dto.TypeValeur, dto.Valeur, dto.Periode);
    }

    public override void Write(Utf8JsonWriter writer, BaremeValue value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new Dto(
            value.RubriqueId, value.Dimension, value.BorneInf, value.BorneSup, value.TypeValeur, value.Valeur, value.Periode), options);
}

public sealed class ConditionEligibiliteJsonConverter : JsonConverter<ConditionEligibilite>
{
    private sealed record Dto(
        string Id, string RubriqueId, string CritereId, Operateur Operateur, string Valeur,
        string? GroupeId, PeriodeReglementaire Periode);

    public override ConditionEligibilite Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<Dto>(ref reader, options)!;
        return ConditionEligibilite.Creer(dto.Id, dto.RubriqueId, dto.CritereId, dto.Operateur, dto.Valeur, dto.GroupeId, dto.Periode);
    }

    public override void Write(Utf8JsonWriter writer, ConditionEligibilite value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new Dto(
            value.Id, value.RubriqueId, value.CritereId, value.Operateur, value.Valeur, value.GroupeId, value.Periode), options);
}

public sealed class CritereEligibiliteJsonConverter : JsonConverter<CritereEligibilite>
{
    private sealed record Dto(string Id, string Libelle, TypeValeurCritere TypeValeur, SourceResolution SourceResolution);

    public override CritereEligibilite Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<Dto>(ref reader, options)!;
        return CritereEligibilite.Creer(dto.Id, dto.Libelle, dto.TypeValeur, dto.SourceResolution);
    }

    public override void Write(Utf8JsonWriter writer, CritereEligibilite value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, new Dto(value.Id, value.Libelle, value.TypeValeur, value.SourceResolution), options);
}
