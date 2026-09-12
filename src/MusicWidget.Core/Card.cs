using System.Text.Json;

namespace MusicWidget;

public static class Card
{
    public static string Render(Playback state)
    {
        object Button(string icon, string title, string verb, bool enabled) => new {
            type = "Action.Execute", title = icon, tooltip = title, verb, isEnabled = enabled,
            associatedInputs = "none", data = new { sessionId = state.SessionId }
        };
        object Column(object action) => new {
            type = "Column", width = "stretch", spacing = "Small",
            items = new object[] { new { type = "ActionSet", actions = new[] { action } } }
        };
        return JsonSerializer.Serialize(new Dictionary<string, object> {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json", ["type"] = "AdaptiveCard", ["version"] = "1.5",
            ["body"] = new object[] {
                new { type = "Image", url = state.Artwork ?? Artwork.Placeholder, width = "120px", height = "120px", horizontalAlignment = "Center", altText = state.Connected ? state.Title : "YouTube Music", spacing = "None" },
                new { type = "TextBlock", text = state.Title, horizontalAlignment = "Center", weight = "Bolder", size = "Small", wrap = false, maxLines = 1, spacing = "Small" },
                new { type = "TextBlock", text = state.Message ?? state.Artist, horizontalAlignment = "Center", size = "Small", isSubtle = true, wrap = false, maxLines = 1, spacing = "None" },
                new { type = "ColumnSet", spacing = "Small", columns = new object[] {
                    Column(Button("↗", "Open YouTube Music", "open", true)),
                    Column(Button("⏮", "Previous track", "previous", state.CanPrevious)),
                    Column(Button(state.Playing ? "⏸" : "▶", state.Playing ? "Pause" : "Play", state.Playing ? "pause" : "play", state.CanToggle)),
                    Column(Button("⏭", "Next track", "next", state.CanNext)),
                    Column(Button(state.Liked ? "♥" : "♡", state.Liked ? "Unlike" : "Like", state.Liked ? "unlike" : "like", state.CanLike))
                } }
            }
        });
    }
}
