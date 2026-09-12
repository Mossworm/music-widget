using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Windows.Widgets.Providers;

namespace MusicWidget.Provider;

[ComVisible(true), ComDefaultInterface(typeof(IWidgetProvider)), Guid(Program.ClassId)]
public sealed class WidgetProvider : IWidgetProvider
{
    public static readonly ManualResetEvent Exit = new(false);
    readonly object gate = new();
    readonly HashSet<string> widgets = new();
    readonly HashSet<string> active = new();
    readonly Dictionary<string, string> rendered = new();
    readonly MediaController controller = new();
    readonly Timer timer;
    int refreshing;
    public WidgetProvider()
    {
        foreach (var info in WidgetManager.GetDefault().GetWidgetInfos() ?? []) widgets.Add(info.WidgetContext.Id);
        timer = new(_ => _ = RefreshAsync(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }
    async Task RefreshAsync()
    {
        lock (gate) if (active.Count == 0) return;
        if (Interlocked.Exchange(ref refreshing, 1) != 0) return;
        try {
            var state = await controller.RefreshAsync();
            lock (gate) foreach (var id in active) Update(id, state);
        } catch (Exception e) { System.Diagnostics.Trace.WriteLine(e.GetType().Name); }
        finally { Volatile.Write(ref refreshing, 0); }
    }
    void Update(string id, Playback state)
    {
        if (!widgets.Contains(id)) return;
        var template = Card.Render(state);
        if (rendered.GetValueOrDefault(id) == template) return;
        try {
            WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(id) { Template = template, Data = "{}", CustomState = "" });
            rendered[id] = template;
        } catch (Exception e) when (e is COMException or InvalidOperationException) { System.Diagnostics.Trace.WriteLine(e.GetType().Name); }
    }
    public void CreateWidget(WidgetContext context) => Activate(context);
    public void Activate(WidgetContext context)
    {
        lock (gate) { widgets.Add(context.Id); active.Add(context.Id); rendered.Remove(context.Id); Update(context.Id, controller.State); }
        _ = RefreshAsync();
    }
    public void Deactivate(string widgetId) { lock (gate) active.Remove(widgetId); }
    public void DeleteWidget(string widgetId, string customState)
    {
        lock (gate) {
            widgets.Remove(widgetId); active.Remove(widgetId); rendered.Remove(widgetId);
            if (widgets.Count == 0) { timer.Dispose(); Exit.Set(); }
        }
    }
    public void OnWidgetContextChanged(WidgetContextChangedArgs args)
    {
        lock (gate) { rendered.Remove(args.WidgetContext.Id); Update(args.WidgetContext.Id, controller.State); }
    }
    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        string? sessionId;
        try {
            using var data = JsonDocument.Parse(args.Data);
            sessionId = data.RootElement.TryGetProperty("sessionId", out var value) ? value.GetString() : null;
        } catch (Exception e) when (e is JsonException or InvalidOperationException) { return; }
        _ = ExecuteAsync(args.WidgetContext.Id, args.Verb, sessionId);
    }
    async Task ExecuteAsync(string id, string verb, string? sessionId)
    {
        lock (gate) if (!widgets.Contains(id)) return;
        try {
            var state = await controller.ExecuteAsync(verb, sessionId);
            lock (gate) foreach (var widgetId in active) Update(widgetId, state);
        } catch (Exception e) { System.Diagnostics.Trace.WriteLine(e.GetType().Name); }
    }
}
