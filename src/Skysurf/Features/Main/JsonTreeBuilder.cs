using System.Text.Json;
using Terminal.Gui.Trees;

namespace skysurf.Features.Main;

/// <summary>Converts a parsed JSON record into a <see cref="TreeNode"/> hierarchy for the
/// record tree: objects become a node per property, arrays a node per element, and scalars
/// leaf nodes rendered as <c>name: value</c>.</summary>
internal static class JsonTreeBuilder
{
    public static ITreeNode Build(JsonElement element)
    {
        return BuildNode("record", element);
    }

    private static TreeNode BuildNode(string name, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var node = new TreeNode($"{name} {{object}}");
                foreach (var property in element.EnumerateObject())
                {
                    node.Children.Add(BuildNode(property.Name, property.Value));
                }

                return node;
            }
            case JsonValueKind.Array:
            {
                var items = element.EnumerateArray().ToList();
                var node = new TreeNode($"{name} [{items.Count}]");
                for (var i = 0; i < items.Count; i++)
                {
                    node.Children.Add(BuildNode($"[{i}]", items[i]));
                }

                return node;
            }
            default:
                return new TreeNode($"{name}: {FormatScalar(element)}");
        }
    }

    private static string FormatScalar(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.Null => "(null)",
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }
}
