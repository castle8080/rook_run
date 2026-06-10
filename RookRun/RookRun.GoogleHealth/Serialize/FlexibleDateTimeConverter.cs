using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.GoogleHealth.Serialize;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A custom JSON converter for DateTime that can handle both ISO 8601 format and a custom format "MM/dd/yy HH:mm:ss".
/// </summary>
public sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    private const string CustomFormat = "MM/dd/yy HH:mm:ss";

    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Try System.Text.Json's built-in ISO parsing first
        if (reader.TryGetDateTime(out var dateTime))
            return dateTime;

        var s = reader.GetString();

        if (string.IsNullOrWhiteSpace(s))
            throw new JsonException("Date string is null or empty.");

        if (DateTime.TryParseExact(
                s,
                CustomFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dateTime))
        {
            return dateTime;
        }

        throw new JsonException($"Invalid date format: '{s}'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options)
    {
        // Let STJ write standard ISO format
        writer.WriteStringValue(value);
    }
}