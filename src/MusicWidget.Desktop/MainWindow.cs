using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MusicWidget.Desktop;

public sealed class MainWindow : Window
{
    readonly MediaController controller = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly bool sample;
    readonly Grid root = new() { Width = 320, Height = 280 };
    readonly Image cover = new() { Width = 120, Height = 120, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
    readonly TextBlock title = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(16, 8, 16, 0) };
    readonly TextBlock artist = new() { FontSize = 12, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new(16, 3, 16, 0) };
    readonly Button previous = Control("\uE892", UiText.Choose("Previous track", "이전 곡"));
    readonly Button toggle = Control("\uE768", UiText.Choose("Play", "재생"));
    readonly Button next = Control("\uE893", UiText.Choose("Next track", "다음 곡"));
    Playback state;
    bool busy, closed;
    string? imageData;

    public MainWindow(bool sample)
    {
        this.sample = sample; state = sample ? Playback.Sample : Playback.Empty();
        Title = sample ? "Music Controller · Sample" : "Music Controller";
        SizeToContent = SizeToContent.WidthAndHeight; ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new("Segoe UI");
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(16, 11, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        header.Children.Add(new TextBlock { Text = "●", Foreground = Brushes.IndianRed, FontSize = 14, Margin = new(0, 0, 7, 0) });
        header.Children.Add(new TextBlock { Text = "Music Controller", FontSize = 12 });
        root.Children.Add(header);
        var body = new StackPanel { Margin = new(0, 42, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        body.Children.Add(cover); body.Children.Add(title); body.Children.Add(artist);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 9, 0, 0) };
        buttons.Children.Add(previous); buttons.Children.Add(toggle); buttons.Children.Add(next); body.Children.Add(buttons);
        root.Children.Add(body); Content = root;
        previous.Click += async (_, _) => await ExecuteAsync("previous");
        toggle.Click += async (_, _) => await ExecuteAsync(state.Playing ? "pause" : "play");
        next.Click += async (_, _) => await ExecuteAsync("next");
        Loaded += async (_, _) => { if (!sample) { timer.Start(); await RefreshAsync(); } };
        timer.Tick += async (_, _) => await RefreshAsync();
        SystemEvents.UserPreferenceChanged += ThemeChanged;
        Closed += (_, _) => { closed = true; timer.Stop(); SystemEvents.UserPreferenceChanged -= ThemeChanged; };
        ApplyTheme(); Paint();
    }
    static Button Control(string glyph, string label)
    {
        var button = new Button { Content = glyph, FontFamily = new("Segoe Fluent Icons"), FontSize = 18, Width = 42, Height = 34, Margin = new(3, 0, 3, 0), ToolTip = label, Background = Brushes.Transparent, BorderThickness = new(0), Cursor = System.Windows.Input.Cursors.Hand };
        AutomationProperties.SetName(button, label);
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(35, 128, 128, 128)))); template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromArgb(65, 128, 128, 128)))); template.Triggers.Add(focus);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(OpacityProperty, 0.3)); template.Triggers.Add(disabled);
        button.Template = template;
        return button;
    }
    void ThemeChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() => ApplyTheme());
    void ApplyTheme(bool? forceDark = null)
    {
        var dark = forceDark ?? (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0);
        root.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#28282C" : "#FAFAFC"));
        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#F5F5F7" : "#252529"));
        artist.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? "#B7B7BD" : "#6B6B73"));
        if (SystemParameters.HighContrast && forceDark is null) { root.Background = SystemColors.WindowBrush; Foreground = artist.Foreground = SystemColors.WindowTextBrush; }
        previous.Foreground = toggle.Foreground = next.Foreground = Foreground;
    }
    void Paint()
    {
        title.Text = state.Title; title.ToolTip = state.Title; artist.Text = state.Message ?? state.Artist; artist.ToolTip = artist.Text;
        var data = state.Artwork ?? Artwork.Placeholder;
        if (data != imageData) {
            using var stream = new MemoryStream(Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]));
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); cover.Source = bitmap; imageData = data;
        }
        AutomationProperties.SetName(cover, state.Title);
        previous.IsEnabled = !busy && state.CanPrevious; toggle.IsEnabled = !busy && state.CanToggle; next.IsEnabled = !busy && state.CanNext;
        toggle.Content = state.Playing ? "\uE769" : "\uE768";
        var label = state.Playing ? UiText.Choose("Pause", "일시정지") : UiText.Choose("Play", "재생");
        toggle.ToolTip = label; AutomationProperties.SetName(toggle, label);
    }
    async Task RefreshAsync()
    {
        if (busy || closed) return;
        busy = true;
        try { state = await controller.RefreshAsync(); }
        finally { busy = false; if (!closed) Paint(); }
    }
    async Task ExecuteAsync(string verb)
    {
        if (busy || closed) return;
        if (sample) { if (verb is "play" or "pause") state = state with { Playing = verb == "play" }; Paint(); return; }
        busy = true; Paint();
        try { state = await controller.ExecuteAsync(verb, state.SessionId); }
        finally { busy = false; if (!closed) Paint(); }
    }
    public void RenderPreviews(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var dark in new[] { false, true }) {
            ApplyTheme(dark); Paint(); root.Measure(new(320, 280)); root.Arrange(new(0, 0, 320, 280)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(640, 560, 192, 192, PixelFormats.Pbgra32); bitmap.Render(root);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(directory, dark ? "preview-dark.png" : "preview-light.png")); png.Save(output);
        }
    }
}
