using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Windows.Media.Control;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MusicWidget;

public sealed class MediaController
{
    readonly SemaphoreSlim gate = new(1, 1);
    GlobalSystemMediaTransportControlsSessionManager? manager;
    GlobalSystemMediaTransportControlsSession? selected;
    string? artworkKey;
    string? artwork;
    public Playback State { get; private set; } = Playback.Empty();

    public async Task<Playback> RefreshAsync()
    {
        await gate.WaitAsync();
        try { return await ReadAsync(); }
        finally { gate.Release(); }
    }

    async Task<Playback> ReadAsync()
    {
        try {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var sessions = manager.GetSessions();
            var candidates = new List<(GlobalSystemMediaTransportControlsSession Session, GlobalSystemMediaTransportControlsSessionMediaProperties Media, bool Playing)>();
            foreach (var session in sessions) {
                try {
                    var media = await session.TryGetMediaPropertiesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                    if (YouTubeIdentity.Matches(session.SourceAppUserModelId, media.Title))
                        candidates.Add((session, media, session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing));
                } catch (Exception e) when (e is COMException or InvalidOperationException or TimeoutException) { }
            }
            var current = manager.GetCurrentSession();
            var index = SessionSelection.Select(candidates.Select(c => (true, c.Playing)).ToArray(), candidates.FindIndex(c => Equals(c.Session, current)));
            if (index < 0) { selected = null; artworkKey = null; artwork = null; return State = Playback.Empty(); }
            var candidate = candidates[index];
            selected = candidate.Session;
            var info = selected.GetPlaybackInfo();
            var mediaProps = candidate.Media;
            var key = $"{selected.SourceAppUserModelId}\n{mediaProps.Title}\n{mediaProps.Artist}\n{mediaProps.AlbumTitle}";
            if (key != artworkKey || artwork is null) {
                artwork = await ReadArtworkAsync(mediaProps.Thumbnail);
                artworkKey = key;
            }
            var controls = info.Controls;
            return State = new(
                string.IsNullOrWhiteSpace(mediaProps.Title) ? UiText.Choose("Unknown title", "제목 없음") : mediaProps.Title,
                string.IsNullOrWhiteSpace(mediaProps.Artist) ? "YouTube Music" : mediaProps.Artist,
                artwork, true, candidate.Playing, controls.IsPreviousEnabled,
                (candidate.Playing ? controls.IsPauseEnabled : controls.IsPlayEnabled) || controls.IsPlayPauseToggleEnabled,
                controls.IsNextEnabled, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))));
        } catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException or TimeoutException) {
            manager = null; selected = null; artwork = null; artworkKey = null;
            return State = Playback.Empty(UiText.Choose("Reopen YouTube Music to reconnect", "YouTube Music을 다시 열어 연결하세요"));
        }
    }

    public async Task<Playback> ExecuteAsync(string verb, string? expectedSession)
    {
        await gate.WaitAsync();
        try {
            await ReadAsync();
            if (selected is null || expectedSession is null || State.SessionId != expectedSession) return State;
            var controls = selected.GetPlaybackInfo().Controls;
            bool? result = verb switch {
                "previous" when State.CanPrevious => await selected.TrySkipPreviousAsync(),
                "next" when State.CanNext => await selected.TrySkipNextAsync(),
                "pause" when State.CanToggle && State.Playing => controls.IsPauseEnabled ? await selected.TryPauseAsync() : await selected.TryTogglePlayPauseAsync(),
                "play" when State.CanToggle && !State.Playing => controls.IsPlayEnabled ? await selected.TryPlayAsync() : await selected.TryTogglePlayPauseAsync(),
                _ => null
            };
            await ReadAsync();
            if (result == false) State = State with { Message = UiText.Choose("Control unavailable. Try in YouTube Music.", "YouTube Music에서 직접 재생해 주세요") };
            return State;
        } catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException) {
            selected = null;
            return State = Playback.Empty(UiText.Choose("Reopen YouTube Music to reconnect", "YouTube Music을 다시 열어 연결하세요"));
        } finally { gate.Release(); }
    }

    static async Task<string?> ReadArtworkAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;
        try {
            using var source = await thumbnail.OpenReadAsync();
            if (source.Size == 0 || source.Size > 20 * 1024 * 1024) return null;
            var decoder = await BitmapDecoder.CreateAsync(source);
            // Re-encode at widget resolution so the Adaptive Card stays small.
            var scale = Math.Min(1d, 256d / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
            var transform = new BitmapTransform { ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)), ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)) };
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
            using var output = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            output.Seek(0);
            using var reader = new DataReader(output.GetInputStreamAt(0));
            await reader.LoadAsync((uint)output.Size);
            var bytes = new byte[(int)output.Size]; reader.ReadBytes(bytes);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        } catch (Exception e) when (e is COMException or ArgumentException or IOException) { return null; }
    }
}
