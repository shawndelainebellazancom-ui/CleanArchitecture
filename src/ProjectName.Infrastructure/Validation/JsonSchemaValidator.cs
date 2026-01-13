using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ProjectName.Infrastructure.Validation;

/// <summary>
/// Production-grade JSON Schema validator
/// Use this for strict schema enforcement across all agents
/// </summary>
public class JsonSchemaValidator
{
    private readonly ILogger<JsonSchemaValidator> _logger;

    public JsonSchemaValidator(ILogger<JsonSchemaValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates JSON against a schema and returns detailed errors
    /// </summary>
    public ValidationResult Validate(string json, object schema)
    {
        var errors = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var schemaJson = JsonSerializer.Serialize(schema);
            using var schemaDoc = JsonDocument.Parse(schemaJson);

            ValidateElement(doc.RootElement, schemaDoc.RootElement, "", errors);

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation error");
            errors.Add($"Validation exception: {ex.Message}");
            return new ValidationResult { IsValid = false, Errors = errors };
        }
    }

    private static void ValidateElement(
        JsonElement element,
        JsonElement schema,
        string path,
        List<string> errors)
    {
        // Get type requirement
        if (schema.TryGetProperty("type", out var typeElement))
        {
            var expectedType = typeElement.GetString();
            if (!IsTypeMatch(element, expectedType))
            {
                errors.Add($"{path}: Expected type '{expectedType}', got '{element.ValueKind}'");
                return;
            }
        }

        // Check required fields for objects
        if (element.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("required", out var requiredElement))
        {
            foreach (var req in requiredElement.EnumerateArray())
            {
                var fieldName = req.GetString();
                if (fieldName != null && !element.TryGetProperty(fieldName, out _))
                {
                    errors.Add($"{path}: Missing required field '{fieldName}'");
                }
            }
        }

        // Validate object properties
        if (element.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("properties", out var propsElement))
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (propsElement.TryGetProperty(prop.Name, out var propSchema))
                {
                    var propPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    ValidateElement(prop.Value, propSchema, propPath, errors);
                }
            }
        }

        // Validate array items
        if (element.ValueKind == JsonValueKind.Array &&
            schema.TryGetProperty("items", out var itemsSchema))
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ValidateElement(item, itemsSchema, $"{path}[{index}]", errors);
                index++;
            }

            // Check minItems/maxItems
            if (schema.TryGetProperty("minItems", out var minItems) &&
                index < minItems.GetInt32())
            {
                errors.Add($"{path}: Array has {index} items, minimum is {minItems.GetInt32()}");
            }

            if (schema.TryGetProperty("maxItems", out var maxItems) &&
                index > maxItems.GetInt32())
            {
                errors.Add($"{path}: Array has {index} items, maximum is {maxItems.GetInt32()}");
            }
        }

        // Validate string constraints
        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString() ?? "";

            if (schema.TryGetProperty("minLength", out var minLength) &&
                str.Length < minLength.GetInt32())
            {
                errors.Add($"{path}: String length {str.Length} < minimum {minLength.GetInt32()}");
            }

            if (schema.TryGetProperty("enum", out var enumElement))
            {
                var validValues = enumElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(v => v != null)
                    .ToList();

                if (!validValues.Contains(str))
                {
                    errors.Add($"{path}: Value '{str}' not in allowed values: {string.Join(", ", validValues)}");
                }
            }
        }

        // Validate number constraints
        if (element.ValueKind == JsonValueKind.Number)
        {
            var num = element.GetDouble();

            if (schema.TryGetProperty("minimum", out var minimum) &&
                num < minimum.GetDouble())
            {
                errors.Add($"{path}: Value {num} < minimum {minimum.GetDouble()}");
            }

            if (schema.TryGetProperty("maximum", out var maximum) &&
                num > maximum.GetDouble())
            {
                errors.Add($"{path}: Value {num} > maximum {maximum.GetDouble()}");
            }
        }
    }

    private static bool IsTypeMatch(JsonElement element, string? expectedType)
    {
        return expectedType switch
        {
            "object" => element.ValueKind == JsonValueKind.Object,
            "array" => element.ValueKind == JsonValueKind.Array,
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _),
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            "null" => element.ValueKind == JsonValueKind.Null,
            _ => true
        };
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public override string ToString()
    {
        return IsValid
            ? "Validation successful"
            : $"Validation failed:\n  - {string.Join("\n  - ", Errors)}";
    }
}