namespace TrussAnalyzer.Core.Domain.V1;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ProjectDocumentFormatException : Exception
{
    public ProjectDocumentFormatException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public static class ProjectDocumentJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != ProjectDocument.CurrentSchemaVersion)
            throw new ProjectDocumentFormatException($"Cannot write unsupported schema version '{document.SchemaVersion}'.");
        return JsonSerializer.Serialize(document, Options);
    }

    public static ProjectDocument Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ProjectDocumentFormatException("Project JSON is empty.");

        try
        {
            using var jsonDocument = JsonDocument.Parse(json);
            if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                throw new ProjectDocumentFormatException("Project JSON root must be an object.");
            string[] requiredRootProperties =
            {
                "schemaVersion", "projectInfo", "unitPreferences", "model", "loadDefinitions",
                "analysisDefinitions", "presentationSettings", "auditMetadata"
            };
            foreach (string property in requiredRootProperties)
            {
                if (!jsonDocument.RootElement.TryGetProperty(property, out _))
                    throw new ProjectDocumentFormatException($"Required root property '{property}' is missing.");
            }

            var document = JsonSerializer.Deserialize<ProjectDocument>(json, Options)
                ?? throw new ProjectDocumentFormatException("Project JSON produced no document.");
            if (document.SchemaVersion != ProjectDocument.CurrentSchemaVersion)
                throw new ProjectDocumentFormatException(
                    $"Unsupported Model3D schema version '{document.SchemaVersion}'. Expected '{ProjectDocument.CurrentSchemaVersion}'.");
            if (document.ProjectInfo is null || document.UnitPreferences is null || document.Model is null ||
                document.LoadDefinitions is null || document.AnalysisDefinitions is null ||
                document.PresentationSettings is null || document.AuditMetadata is null)
                throw new ProjectDocumentFormatException("Required ProjectDocument sections must not be null.");
            return document;
        }
        catch (ProjectDocumentFormatException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ProjectDocumentFormatException($"Malformed or unknown Model3D V1 JSON: {ex.Message}", ex);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            IgnoreReadOnlyProperties = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
