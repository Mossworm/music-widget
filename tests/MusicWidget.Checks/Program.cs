using System.Text.Json;
using System.Xml.Linq;
using MusicWidget;
using Windows.Media.Control;

if (args.Length == 2 && args[0] == "--identity") {
    Console.WriteLine(YouTubeIdentity.Matches(args[1], "") ? "YouTube Music identity confirmed" : "Unresolved identity");
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
    Console.WriteLine(JsonSerializer.Serialize(new { state.Connected, state.Title, state.Artist, state.Playing, state.CanPrevious, state.CanToggle, state.CanNext, HasArtwork = state.Artwork is not null }));
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
JsonElement[] Actions(JsonDocument doc) => doc.RootElement.GetProperty("body")[3].GetProperty("columns")[1].GetProperty("items")[0].GetProperty("actions").EnumerateArray().ToArray();
Check(Actions(empty).Length == 3, "Exactly three transport controls");
Check(Actions(empty).All(a => !a.GetProperty("isEnabled").GetBoolean()), "Disconnected controls disabled");
using var playing = JsonDocument.Parse(Card.Render(Playback.Sample));
Check(Actions(playing)[1].GetProperty("verb").GetString() == "pause", "Playing renders pause");
using var paused = JsonDocument.Parse(Card.Render(Playback.Sample with { Playing = false }));
Check(Actions(paused)[1].GetProperty("verb").GetString() == "play", "Paused renders play");
Check(Actions(playing)[0].GetProperty("data").GetProperty("sessionId").GetString() == "sample", "Actions bind displayed session");
using var special = JsonDocument.Parse(Card.Render(Playback.Sample with { Title = "노래 \"제목\" <>&\n🎵", Artist = "가수" }));
Check(special.RootElement.GetProperty("body")[1].GetProperty("text").GetString() == "노래 \"제목\" <>&\n🎵", "Metadata safely serialized");
using var limited = JsonDocument.Parse(Card.Render(Playback.Sample with { CanNext = false }));
Check(!Actions(limited)[2].GetProperty("isEnabled").GetBoolean(), "Unsupported next is disabled");
var manifest = XDocument.Load(Path.Combine("packaging", "AppxManifest.xml"));
Check(manifest.Descendants().Where(x => x.Name.LocalName == "Size").Select(x => (string?)x.Attribute("Name")).SequenceEqual(["medium"]), "Only medium widget size declared");
Check(manifest.Descendants().Single(x => x.Name.LocalName == "Definition").Attribute("IsCustomizable")?.Value == "false", "Customize widget disabled");
Check(manifest.Descendants().Any(x => x.Name.LocalName == "Capability" && x.Attribute("Name")?.Value == "globalMediaControl"), "Media control capability declared");
var controllerCheck = new MediaController();
var before = await controllerCheck.RefreshAsync();
var after = await controllerCheck.ExecuteAsync("next", "not-the-displayed-session");
Check(after.Message is null, "Stale action safely ignored without invoking transport");
Console.WriteLine($"{count} checks passed.");
