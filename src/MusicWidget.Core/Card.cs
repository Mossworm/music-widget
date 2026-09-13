using System.Text.Json;

namespace MusicWidget;

public static class Card
{
    public static string Render(Playback state)
    {
        object Icon(string title, string verb, bool enabled, bool dark) => new Dictionary<string, object> {
            ["type"] = "Image", ["url"] = ControlIcons.Png(verb, dark, enabled),
            ["width"] = "40px", ["height"] = "36px", ["horizontalAlignment"] = "Center",
            ["spacing"] = "None", ["altText"] = title,
            ["$when"] = dark ? "${$host.hostTheme == 'dark'}" : "${$host.hostTheme != 'dark'}"
        };
        object Button(string title, string verb, bool enabled) => new {
            type = "Column", width = "stretch", spacing = "Small",
            items = new[] { new {
                type = "Container", spacing = "None",
                selectAction = new {
                    type = "Action.Execute", title, tooltip = title, verb, isEnabled = enabled,
                    associatedInputs = "none", data = new { sessionId = state.SessionId }
                },
                items = new[] { Icon(title, verb, enabled, false), Icon(title, verb, enabled, true) }
            } }
        };
        return JsonSerializer.Serialize(new Dictionary<string, object> {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json", ["type"] = "AdaptiveCard", ["version"] = "1.5",
            ["body"] = new object[] {
                new { type = "Image", url = state.Artwork ?? Artwork.Placeholder, width = "120px", height = "120px", horizontalAlignment = "Center", altText = state.Connected ? state.Title : "YouTube Music", spacing = "None" },
                new { type = "TextBlock", text = state.Title, horizontalAlignment = "Center", weight = "Bolder", size = "Small", wrap = false, maxLines = 1, spacing = "Small" },
                new { type = "TextBlock", text = state.Message ?? state.Artist, horizontalAlignment = "Center", size = "Small", isSubtle = true, wrap = false, maxLines = 1, spacing = "None" },
                new { type = "ColumnSet", spacing = "Small", columns = new object[] {
                    Button("Open YouTube Music", "open", true),
                    Button("Previous track", "previous", state.CanPrevious),
                    Button(state.Playing ? "Pause" : "Play", state.Playing ? "pause" : "play", state.CanToggle),
                    Button("Next track", "next", state.CanNext),
                    Button(state.Liked ? "Unlike" : "Like", state.Liked ? "unlike" : "like", state.CanLike)
                } }
            }
        });
    }
}
