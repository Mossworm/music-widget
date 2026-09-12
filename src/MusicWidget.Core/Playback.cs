using System.Globalization;

namespace MusicWidget;

public static class UiText
{
    public static string Choose(string en, string ko) => CultureInfo.CurrentUICulture.Name == "ko-KR" ? ko : en;
}

public sealed record Playback(
    string Title, string Artist, string? Artwork = null, bool Connected = false,
    bool Playing = false, bool CanPrevious = false, bool CanToggle = false, bool CanNext = false,
    string? SessionId = null, string? Message = null)
{
    public static Playback Empty(string? message = null) => new("YouTube Music",
        message ?? UiText.Choose("Play a song in the YouTube Music app", "YouTube Music 앱에서 곡을 재생하세요"));
    public static Playback Sample => new("Midnight Drive", "Mossworm · Sample", MusicWidget.Artwork.Placeholder, true, true, true, true, true, "sample");
}

public static class SessionSelection
{
    // Never fall back to Windows' current session: it may belong to a different player.
    public static int Select(IReadOnlyList<(bool IsYouTubeMusic, bool Playing)> candidates, int current)
    {
        if (current >= 0 && current < candidates.Count && candidates[current] is (true, true)) return current;
        for (var i = 0; i < candidates.Count; i++) if (candidates[i] is (true, true)) return i;
        if (current >= 0 && current < candidates.Count && candidates[current].IsYouTubeMusic) return current;
        for (var i = 0; i < candidates.Count; i++) if (candidates[i].IsYouTubeMusic) return i;
        return -1;
    }
}
