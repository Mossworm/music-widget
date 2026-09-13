using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;

namespace MusicWidget;

// Shared vector silhouettes, rasterized locally for the Adaptive Card image actions.
public static class ControlIcons
{
    static readonly ConcurrentDictionary<(string Verb, bool Dark, bool Enabled), string> images = new();
    static readonly IReadOnlyDictionary<string, Geometry> shapes = CreateShapes();

    static IReadOnlyDictionary<string, Geometry> CreateShapes()
    {
        const string heart = "M12,20 C10,18 3,14 3,8.5 C3,3.5 9,2.5 12,7 C15,2.5 21,3.5 21,8.5 C21,14 14,18 12,20 Z";
        var result = new Dictionary<string, Geometry>();
        foreach (var (verb, path) in new Dictionary<string, string> {
            ["open"] = "M6,18 L18,6 M7,6 L18,6 L18,17",
            ["previous"] = "M4,4 H6 V20 H4 Z M20,4 L8,12 L20,20 Z",
            ["play"] = "M7,4 L21,12 L7,20 Z",
            ["pause"] = "M5,4 H10 V20 H5 Z M14,4 H19 V20 H14 Z",
            ["next"] = "M18,4 H20 V20 H18 Z M4,4 L16,12 L4,20 Z",
            ["like"] = heart, ["unlike"] = heart
        }) {
            var geometry = Geometry.Parse(path);
            if (verb is "open" or "like")
                geometry = geometry.GetWidenedPathGeometry(new Pen(Brushes.Black, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round });
            geometry.Freeze();
            result[verb] = geometry;
        }
        return result;
    }

    public static DrawingImage Drawing(string verb, Brush foreground)
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 24, 24))));
        drawing.Children.Add(new GeometryDrawing(foreground, null, shapes[verb]));
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }

    public static string Png(string verb, bool dark, bool enabled) => images.GetOrAdd((verb, dark, enabled), key => {
        var shape = shapes[key.Verb];
        var color = key.Dark ? 255 : 0;
        // 40 x 36 logical pixels retain a comfortable transparent click target.
        return Artwork.Png(120, 108, (x, y) => {
            var coverage = 0;
            for (var sy = 0; sy < 2; sy++) for (var sx = 0; sx < 2; sx++)
                if (shape.FillContains(new Point((x + (sx + 0.5) / 2) / 3 - 8, (y + (sy + 0.5) / 2) / 3 - 6))) coverage++;
            return (color, color, color, (int)Math.Round(coverage / 4d * (key.Enabled ? 255 : 76)));
        });
    });
}
