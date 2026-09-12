namespace MusicWidget;

public static class LocalStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicWidget");
}
