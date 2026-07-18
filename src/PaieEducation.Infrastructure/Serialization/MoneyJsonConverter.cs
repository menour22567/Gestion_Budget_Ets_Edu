using System.Text.Json;
using System.Text.Json.Serialization;
using PaieEducation.Shared.Money;

namespace PaieEducation.Infrastructure.Serialization;

public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token for Money.");

        decimal amount = 0m;
        string currency = "DZD";

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString();
            reader.Read();
            switch (prop)
            {
                case "Amount": amount = reader.GetDecimal(); break;
                case "Currency": currency = reader.GetString() ?? "DZD"; break;
            }
        }

        return new Money(amount, currency);
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Amount", value.Amount);
        writer.WriteString("Currency", value.Currency);
        writer.WriteEndObject();
    }
}
