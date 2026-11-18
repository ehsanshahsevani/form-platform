using System.Text.Json;

namespace FormPlatform.Modules.Forms.Domain;

/// <summary>Validates the deliberately small JSON schema used by the Forms module.</summary>
public static class SchemaDocument
{
    public static bool IsValid(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields.EnumerateArray())
            {
                if (field.ValueKind != JsonValueKind.Object ||
                    !TryReadNonEmptyString(field, "id", out var id) ||
                    !TryReadNonEmptyString(field, "type", out _) ||
                    !TryReadNonEmptyString(field, "name", out _) ||
                    !TryReadConfiguration(field, out var configuration) ||
                    configuration.ValueKind != JsonValueKind.Object ||
                    !identifiers.Add(id))
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadNonEmptyString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var candidate = element.GetString();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static bool TryReadConfiguration(JsonElement field, out JsonElement configuration)
    {
        if (field.TryGetProperty("configuration", out configuration) || field.TryGetProperty("config", out configuration))
        {
            return configuration.ValueKind == JsonValueKind.Object;
        }

        return false;
    }
}
