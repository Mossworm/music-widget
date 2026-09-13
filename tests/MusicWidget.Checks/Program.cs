using System.Text.Json;
using System.Xml.Linq;
using MusicWidget;
using Windows.Media.Control;

if (args.Length == 2 && args[0] == "--identity") {
    Console.WriteLine(YouTubeIdentity.Matches(args[1], "") ? "YouTube Music identity confirmed" : "Unresolved identity");
    return;
}
if (args.Length == 4 && args[0] == "--like-status") {
    Console.WriteLine(JsonSerializer.Serialize(await YouTubeApp.ReadLikeAsync(args[1], args[2], args[3])));
    return;
}
if (args.Contains("--open")) {
    Console.WriteLine(await YouTubeApp.OpenAsync(null, null) ? "Opened or focused YouTube Music" : "Unable to focus YouTube Music");
    return;
}
if (args.Contains("--live")) {
    var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    foreach (var session in manager.GetSessions()) {
        var media = await session.TryGetMediaPropertiesAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { session.SourceAppUserModelId, media.Title, media.Artist, Status = session.GetPlaybackInfo().PlaybackStatus.ToString() }));
    }
    var controller = new MediaController();
    var state = await controller.RefreshAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { state.Connected, state.Title, state.Artist, state.Playing, state.CanPrevious, state.CanToggle, state.CanNext, state.CanLike, state.Liked, HasArtwork = state.Artwork is not null }));
    return;
}
var count = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); count++; Console.WriteLine($"PASS {label}"); }
Check(SessionSelection.Select([(false, true)], 0) == -1, "Other media players never selected");
Check(SessionSelection.Select([(false, true), (true, false)], 0) == 1, "Paused YouTube Music beats another active player");
Check(SessionSelection.Select([(true, false), (true, true)], 0) == 1, "Playing YouTube Music preferred");
Check(SessionSelection.Select([(true, true), (true, true)], 1) == 1, "Current YouTube Music preferred among playing sessions");
Check(SessionSelection.Select([], -1) == -1, "No sessions handled");
using var empty = JsonDocument.Parse(Card.Render(Playback.Empty()));
JsonElement[] Actions(JsonDocument doc) => doc.RootElement.GetProperty("body")[3].GetProperty("columns").EnumerateArray()
    .Select(c => c.GetProperty("items")[0].GetProperty("selectAction")).ToArray();
Check(empty.RootElement.GetProperty("body")[3].GetProperty("columns").EnumerateArray().All(c => c.GetProperty("width").GetString() == "stretch"
    && c.GetProperty("items").EnumerateArray().All(i => i.GetProperty("type").GetString() == "Image"
        && i.GetProperty("width").GetString() == "40px" && i.GetProperty("height").GetString() == "36px")), "Five background-free icon controls retain equal columns and click targets");
Check(Actions(empty).Select(a => a.GetProperty("verb").GetString()).SequenceEqual(["open", "previous", "play", "next", "like"]), "Open and like flank the three transport controls");
Check(Actions(empty)[0].GetProperty("isEnabled").GetBoolean(), "Open is available without a media session");
Check(Actions(empty).Skip(1).All(a => !a.GetProperty("isEnabled").GetBoolean()), "Disconnected playback and like controls disabled");
using var playing = JsonDocument.Parse(Card.Render(Playback.Sample));
Check(Actions(playing)[2].GetProperty("verb").GetString() == "pause", "Playing renders pause");
using var paused = JsonDocument.Parse(Card.Render(Playback.Sample with { Playing = false }));
Check(Actions(paused)[2].GetProperty("verb").GetString() == "play", "Paused renders play");
Check(Actions(playing)[0].GetProperty("data").GetProperty("sessionId").GetString() == "sample", "Actions bind displayed session");
using var special = JsonDocument.Parse(Card.Render(Playback.Sample with { Title = "노래 \"제목\" <>&\n🎵", Artist = "가수" }));
Check(special.RootElement.GetProperty("body")[1].GetProperty("text").GetString() == "노래 \"제목\" <>&\n🎵", "Metadata safely serialized");
using var limited = JsonDocument.Parse(Card.Render(Playback.Sample with { CanNext = false }));
Check(!Actions(limited)[3].GetProperty("isEnabled").GetBoolean(), "Unsupported next is disabled");
using var liked = JsonDocument.Parse(Card.Render(Playback.Sample with { Liked = true }));
Check(Actions(liked)[4].GetProperty("verb").GetString() == "unlike", "Liked song offers unlike");
using var noLike = JsonDocument.Parse(Card.Render(Playback.Sample with { CanLike = false }));
Check(!Actions(noLike)[4].GetProperty("isEnabled").GetBoolean() && Actions(noLike)[2].GetProperty("isEnabled").GetBoolean(), "Unavailable accessibility does not disable transport");
Check(YouTubeApp.IsLikeLabel("좋아요") && YouTubeApp.IsLikeLabel("좋아요 취소") && YouTubeApp.IsLikeLabel("Like") && YouTubeApp.IsLikeLabel("Remove like"), "Korean and English like labels recognized");
Check(!YouTubeApp.IsLikeLabel("싫어요") && !YouTubeApp.IsLikeLabel("Dislike") && !YouTubeApp.IsLikeLabel("Liked Music"), "Dislike and library controls never matched");
Check(YouTubeApp.IsMusicWindow("Song - YouTube Music", "chrome", "Chrome._crx_test") && YouTubeApp.IsMusicWindow("YouTube Music", "msedge", null), "PWA windows recognized before and during playback");
Check(YouTubeApp.IsMusicWindow("YouTube Music - 두 사람 | YouTube Music", "chrome", "Chrome._crx_test")
    && YouTubeApp.IsMusicWindow("Song | YouTube Music", "msedge", "Microsoft.MicrosoftEdge_test"), "Playing PWA pipe-separated titles keep like available");
Check(!YouTubeApp.IsMusicWindow("Song | YouTube Music - Google Chrome", "chrome", null)
    && !YouTubeApp.IsMusicWindow("Song | YouTube Music", "msedge", "Chrome._crx_test"), "Pipe-separated titles still exclude browser tabs and mismatched browsers");
Check(!YouTubeApp.IsMusicWindow("YouTube Music - Google Chrome", "chrome", null) && !YouTubeApp.IsMusicWindow("YouTube Music", "notepad", null)
    && !YouTubeApp.IsMusicWindow("YouTube Music", "msedge", "Chrome._crx_test"), "Other windows and mismatched browser excluded");
var manifest = XDocument.Load(Path.Combine("packaging", "AppxManifest.xml"));
Check(manifest.Descendants().Where(x => x.Name.LocalName == "Size").Select(x => (string?)x.Attribute("Name")).SequenceEqual(["medium"]), "Only medium widget size declared");
Check(manifest.Descendants().Single(x => x.Name.LocalName == "Definition").Attribute("IsCustomizable")?.Value == "false", "Customize widget disabled");
Check(manifest.Descendants().Any(x => x.Name.LocalName == "Capability" && x.Attribute("Name")?.Value == "globalMediaControl"), "Media control capability declared");
var controllerCheck = new MediaController();
var before = await controllerCheck.RefreshAsync();
var after = await controllerCheck.ExecuteAsync("next", "not-the-displayed-session");
Check(after.Message is null, "Stale action safely ignored without invoking transport");
Console.WriteLine($"{count} checks passed.");
