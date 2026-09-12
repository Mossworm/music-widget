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
        return JsonSerializer.Serialize(new Dictionary<string, object> {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json", ["type"] = "AdaptiveCard", ["version"] = "1.5",
            ["body"] = new object[] {
                new { type = "Image", url = state.Artwork ?? Artwork.Placeholder, width = "120px", height = "120px", horizontalAlignment = "Center", altText = state.Connected ? state.Title : "YouTube Music", spacing = "None" },
                new { type = "TextBlock", text = state.Title, horizontalAlignment = "Center", weight = "Bolder", size = "Small", wrap = false, maxLines = 1, spacing = "Small" },
                new { type = "TextBlock", text = state.Message ?? state.Artist, horizontalAlignment = "Center", size = "Small", isSubtle = true, wrap = false, maxLines = 1, spacing = "None" },
                new { type = "ColumnSet", spacing = "Small", columns = new object[] {
                    new { type = "Column", width = "stretch", items = Array.Empty<object>() },
                    new { type = "Column", width = "auto", items = new object[] { new { type = "ActionSet", actions = new[] {
                        Button("⏮", UiText.Choose("Previous track", "이전 곡"), "previous", state.CanPrevious),
                        Button(state.Playing ? "⏸" : "▶", state.Playing ? UiText.Choose("Pause", "일시정지") : UiText.Choose("Play", "재생"), state.Playing ? "pause" : "play", state.CanToggle),
                        Button("⏭", UiText.Choose("Next track", "다음 곡"), "next", state.CanNext)
                    } } } },
                    new { type = "Column", width = "stretch", items = Array.Empty<object>() }
                } }
            }
        });
    }
}
