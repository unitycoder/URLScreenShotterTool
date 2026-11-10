namespace UrlScreenshotter;

public sealed class AppSettings
{
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public double DelaySeconds { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public string OutputFolder { get; set; } = "";
    public string BrowserChannel { get; set; } = "msedge";
    public bool FullPage { get; set; }
    public bool Interactive { get; set; }
}
