using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DockMonitor.Service.Config;

public static class JsoncConfigurationExtensions
{
    public static IConfigurationBuilder AddJsoncFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional,
        bool reloadOnChange)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(path);

        builder.Add(new JsoncConfigurationSource
        {
            Path = path,
            Optional = optional,
            ReloadOnChange = reloadOnChange,
        });

        return builder;
    }
}

internal sealed class JsoncConfigurationSource : FileConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new JsoncConfigurationProvider(this);
    }
}

internal sealed class JsoncConfigurationProvider : FileConfigurationProvider
{
    public JsoncConfigurationProvider(JsoncConfigurationSource source) : base(source)
    {
    }

    public override void Load(Stream stream)
    {
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        VisitElement(document.RootElement, parentPath: null, data);
        Data = data;
    }

    private static void VisitElement(JsonElement element, string? parentPath, Dictionary<string, string?> data)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var path = parentPath is null
                        ? prop.Name
                        : parentPath + ConfigurationPath.KeyDelimiter + prop.Name;

                    VisitElement(prop.Value, path, data);
                }

                break;

            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var path = parentPath + ConfigurationPath.KeyDelimiter + i.ToString();
                    VisitElement(item, path, data);
                    i++;
                }

                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;

            default:
                if (parentPath is not null)
                {
                    data[parentPath] = element.ToString();
                }

                break;
        }
    }
}
