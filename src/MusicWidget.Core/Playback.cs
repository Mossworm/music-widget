namespace MusicWidget;

public sealed record Playback(
    string Title, string Artist, string? Artwork = null, bool Connected = false,
    bool Playing = false, bool CanPrevious = false, bool CanToggle = false, bool CanNext = false,
    string? SessionId = null, string? Message = null, bool CanLike = false, bool Liked = false)
{
    public static Playback Empty(string? message = null) => new("YouTube Music",
        message ?? "Play a song in the YouTube Music app");
    public static Playback Sample => new("Midnight Drive", "Mossworm · Sample", MusicWidget.Artwork.Placeholder, true, true, true, true, true, "sample", CanLike: true);
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
