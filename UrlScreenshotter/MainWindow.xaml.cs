using Microsoft.Playwright;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace UrlScreenshotter;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _log = new();
    private readonly Dictionary<string, AuthCredentials> _credentials = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool>? _manualChoice;
    private IPage? _activePage;
    private bool _running;
    private bool _applyingPreset;
    private bool _closeAfterStop;

    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kelobyte", "UrlScreenshotter", "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = _log;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AppSettings settings = ReadSettings();
        _applyingPreset = true;
        WidthText.Text = settings.Width.ToString(CultureInfo.InvariantCulture);
        HeightText.Text = settings.Height.ToString(CultureInfo.InvariantCulture);
        DelayText.Text = settings.DelaySeconds.ToString(CultureInfo.CurrentCulture);
        TimeoutText.Text = settings.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        OutputText.Text = string.IsNullOrWhiteSpace(settings.OutputFolder) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "UrlScreenshots") : settings.OutputFolder;
        BrowserCombo.SelectedIndex = settings.BrowserChannel == "chrome" ? 1 : 0;
        FullPageCheck.IsChecked = settings.FullPage;
        InteractiveCheck.IsChecked = settings.Interactive;
        PresetCombo.SelectedIndex = 0;
        _applyingPreset = false;
        AddLog("Ready. Paste URLs, select a screenshot size and press Start capture.");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings();
        if (!_running) return;
        e.Cancel = true;
        _closeAfterStop = true;
        RequestStop();
    }

    private static AppSettings ReadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Invalid settings should never prevent the app from opening.
        }
        return new AppSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                Width = int.TryParse(WidthText.Text, out int width) ? width : 1280,
                Height = int.TryParse(HeightText.Text, out int height) ? height : 720,
                DelaySeconds = TryReadDouble(DelayText.Text, out double delay) ? delay : 3,
                TimeoutSeconds = int.TryParse(TimeoutText.Text, out int timeout) ? timeout : 30,
                OutputFolder = OutputText.Text,
                BrowserChannel = BrowserCombo.SelectedIndex == 1 ? "chrome" : "msedge",
                FullPage = FullPageCheck.IsChecked == true,
                Interactive = InteractiveCheck.IsChecked == true
            };
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // A read-only settings directory should not block screenshots.
        }
    }

    private static bool TryReadDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingPreset || WidthText is null || HeightText is null) return;
        if (PresetCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string dimensions) return;
        string[] parts = dimensions.Split(',');
        if (parts.Length != 2) return;
        _applyingPreset = true;
        WidthText.Text = parts[0];
        HeightText.Text = parts[1];
        _applyingPreset = false;
    }

    private void SizeText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_applyingPreset || PresetCombo is null) return;
        PresetCombo.SelectedIndex = 0;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Choose screenshot output folder", ShowNewFolderButton = true };
        if (Directory.Exists(OutputText.Text)) dialog.SelectedPath = OutputText.Text;
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) OutputText.Text = dialog.SelectedPath;
    }

    private void ExploreOutput_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = OutputText.Text.Trim();
            if (folder.Length == 0) return;
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "The folder does not exist yet.", "Explore output folder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadUrls_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", Title = "Load URL list" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            UrlsText.Text = File.ReadAllText(dialog.FileName);
            AddLog("Loaded URL list from " + Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load URL list", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveUrls_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Text files (*.txt)|*.txt", FileName = "urls.txt", Title = "Save URL list" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, UrlsText.Text);
            AddLog("Saved URL list to " + Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save URL list", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static List<Uri> ParseUrls(string text)
    {
        var urls = new List<Uri>();
        string[] lines = text.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (!line.Contains("://", StringComparison.Ordinal)) line = "https://" + line;
            if (!Uri.TryCreate(line, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) continue;
            urls.Add(uri);
        }
        return urls;
    }

    private CaptureOptions ReadCaptureOptions()
    {
        if (!int.TryParse(WidthText.Text, out int width) || width < 100 || width > 16384) throw new ArgumentException("Width must be between 100 and 16384 pixels.");
        if (!int.TryParse(HeightText.Text, out int height) || height < 100 || height > 16384) throw new ArgumentException("Height must be between 100 and 16384 pixels.");
        if ((long)width * height > 50_000_000) throw new ArgumentException("Requested viewport is over 50 million pixels. Use a smaller size.");
        if (!TryReadDouble(DelayText.Text, out double delay) || !double.IsFinite(delay) || delay < 0 || delay > 180) throw new ArgumentException("Wait after load must be between 0 and 180 seconds.");
        if (!int.TryParse(TimeoutText.Text, out int timeout) || timeout < 5 || timeout > 180) throw new ArgumentException("Navigation timeout must be between 5 and 180 seconds.");
        if (string.IsNullOrWhiteSpace(OutputText.Text)) throw new ArgumentException("Choose an output folder.");
        return new CaptureOptions(width, height, delay, timeout, Path.GetFullPath(OutputText.Text.Trim()), BrowserCombo.SelectedIndex == 1 ? "chrome" : "msedge", FullPageCheck.IsChecked == true, InteractiveCheck.IsChecked == true);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        List<Uri> urls;
        CaptureOptions options;
        try
        {
            urls = ParseUrls(UrlsText.Text);
            if (urls.Count == 0) throw new ArgumentException("Paste at least one URL, one per line.");
            options = ReadCaptureOptions();
            Directory.CreateDirectory(options.OutputFolder);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Check your input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettings();
        _credentials.Clear();
        _cts = new CancellationTokenSource();
        _running = true;
        ToggleRunControls(true);
        CaptureProgress.Maximum = urls.Count;
        CaptureProgress.Value = 0;
        AddLog($"Starting {urls.Count} URL(s), {options.Width}x{options.Height} pixels, browser: {options.BrowserChannel}.");

        try
        {
            int saved = await CaptureBatchAsync(urls, options, _cts.Token);
            StatusText.Text = _cts.IsCancellationRequested ? $"Stopped, {saved} saved" : $"Completed, {saved}/{urls.Count} saved";
            AddLog(_cts.IsCancellationRequested ? $"Stopped. Saved {saved} screenshot(s)." : $"Completed. Saved {saved} screenshot(s).");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Stopped";
            AddLog("Capture cancelled.");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error";
            AddLog("Fatal error: " + ex.Message);
            MessageBox.Show(this, "Could not start the browser or capture screenshots.\n\n" + ex.Message + "\n\nMake sure Microsoft Edge (or the selected Chrome browser) is installed.", "Capture error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _running = false;
            _manualChoice = null;
            _activePage = null;
            _credentials.Clear();
            ToggleRunControls(false);
            _cts.Dispose();
            _cts = null;
            if (_closeAfterStop) Close();
        }
    }

    private void ToggleRunControls(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        UrlsText.IsEnabled = !running;
        WidthText.IsEnabled = !running;
        HeightText.IsEnabled = !running;
        PresetCombo.IsEnabled = !running;
        DelayText.IsEnabled = !running;
        TimeoutText.IsEnabled = !running;
        BrowserCombo.IsEnabled = !running;
        FullPageCheck.IsEnabled = !running;
        InteractiveCheck.IsEnabled = !running;
        OutputText.IsEnabled = !running;
        CaptureButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
    }

    private async Task<int> CaptureBatchAsync(IReadOnlyList<Uri> urls, CaptureOptions options, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = options.BrowserChannel, Headless = false });
        var sessions = new Dictionary<string, SiteSession>(StringComparer.OrdinalIgnoreCase);
        int saved = 0;

        try
        {
            for (int i = 0; i < urls.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Uri uri = urls[i];
                string origin = uri.GetLeftPart(UriPartial.Authority);
                StatusText.Text = $"Page {i + 1} of {urls.Count}";
                AddLog($"[{i + 1}/{urls.Count}] Opening {uri.Host}{uri.AbsolutePath}");

                try
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        SiteSession session = await GetSessionAsync(browser, sessions, origin, options);
                        _activePage = session.Page;
                        IResponse? response = null;
                        bool navigationTimedOut = false;

                        try
                        {
                            response = await session.Page.GotoAsync(uri.AbsoluteUri, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = options.TimeoutSeconds * 1000 });
                        }
                        catch (PlaywrightException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!options.Interactive) throw;
                            navigationTimedOut = true;
                            AddLog("Navigation timed out; interactive mode lets you inspect and capture the open page.");
                        }

                        if (response?.Status == 401)
                        {
                            if (attempt == 2) { AddLog("Authentication failed after two retries; skipping page."); break; }
                            if (uri.Scheme == Uri.UriSchemeHttp)
                            {
                                var answer = MessageBox.Show(this, "This site uses unencrypted HTTP. The username and password could be intercepted in transit. Continue?", "Credentials over HTTP", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                                if (answer != MessageBoxResult.Yes) { AddLog("Skipped: insecure authentication declined."); break; }
                            }

                            _credentials.TryGetValue(origin, out AuthCredentials? existing);
                            var dialog = new BasicAuthDialog(origin, existing?.UserName) { Owner = this };
                            if (dialog.ShowDialog() != true) { AddLog("Skipped: Basic Auth credentials not provided."); break; }
                            _credentials[origin] = new AuthCredentials(dialog.UserName, dialog.Password);
                            AddLog("HTTP Basic credentials provided for this site. Retrying.");
                            await session.Context.CloseAsync();
                            sessions.Remove(origin);
                            _activePage = null;
                            continue;
                        }

                        if (response is not null && response.Status >= 400) AddLog($"Warning: HTTP {response.Status}; capturing the displayed response.");
                        if (!navigationTimedOut)
                        {
                            try
                            {
                                await session.Page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = Math.Min(options.TimeoutSeconds * 1000, 7500) });
                            }
                            catch (PlaywrightException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
                            {
                                AddLog("Some resources are still loading; proceeding after the configured wait.");
                            }
                        }

                        if (options.DelaySeconds > 0) await Task.Delay(TimeSpan.FromSeconds(options.DelaySeconds), cancellationToken);
                        if (options.Interactive)
                        {
                            AddLog("Interact with the browser window, then click Capture this page or Skip page.");
                            _manualChoice = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            CaptureButton.IsEnabled = true;
                            SkipButton.IsEnabled = true;
                            StatusText.Text = $"Waiting for Capture ({i + 1}/{urls.Count})";
                            bool shouldCapture;
                            try
                            {
                                shouldCapture = await _manualChoice.Task.WaitAsync(cancellationToken);
                            }
                            finally
                            {
                                _manualChoice = null;
                                CaptureButton.IsEnabled = false;
                                SkipButton.IsEnabled = false;
                            }
                            if (!shouldCapture) { AddLog("Skipped by user."); break; }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        string target = GetUniqueScreenshotPath(options.OutputFolder, uri, i + 1, options.Width, options.Height);
                        await session.Page.ScreenshotAsync(new PageScreenshotOptions { Path = target, FullPage = options.FullPage, Timeout = 60000 });
                        saved++;
                        AddLog("Saved: " + Path.GetFileName(target));
                        ShowPreview(target);
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                    AddLog("Failed: " + ex.Message);
                }
                finally
                {
                    _activePage = null;
                    CaptureProgress.Value = i + 1;
                }
            }
        }
        finally
        {
            _activePage = null;
            foreach (SiteSession session in sessions.Values)
            {
                try { await session.Context.CloseAsync(); } catch (Exception) { }
            }
        }

        return saved;
    }

    private async Task<SiteSession> GetSessionAsync(IBrowser browser, Dictionary<string, SiteSession> sessions, string origin, CaptureOptions options)
    {
        if (sessions.TryGetValue(origin, out SiteSession? existing))
        {
            if (!existing.Page.IsClosed) return existing;
            try { await existing.Context.CloseAsync(); } catch (Exception) { }
            sessions.Remove(origin);
        }
        _credentials.TryGetValue(origin, out AuthCredentials? credentials);
        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = options.Width, Height = options.Height },
            ScreenSize = new ScreenSize { Width = options.Width, Height = options.Height },
            DeviceScaleFactor = 1,
            HttpCredentials = new HttpCredentials { Username = credentials?.UserName ?? string.Empty, Password = credentials?.Password ?? string.Empty, Origin = origin }
        };
        IBrowserContext context = await browser.NewContextAsync(contextOptions);
        try
        {
            IPage page = await context.NewPageAsync();
            var session = new SiteSession(context, page);
            sessions[origin] = session;
            return session;
        }
        catch (Exception)
        {
            await context.CloseAsync();
            throw;
        }
    }

    private static string GetUniqueScreenshotPath(string folder, Uri uri, int number, int width, int height)
    {
        string slug = Regex.Replace(uri.Host + uri.AbsolutePath.Replace('/', '_'), @"[^A-Za-z0-9._-]+", "_").Trim('_', '.');
        if (slug.Length > 70) slug = slug[..70];
        if (string.IsNullOrWhiteSpace(slug)) slug = "page";
        string stem = slug;
        string path = Path.Combine(folder, stem + ".png");
        int suffix = 2;
        while (File.Exists(path)) path = Path.Combine(folder, $"{stem}_{suffix++}.png");
        return path;
    }

    private void ShowPreview(string path)
    {
        try
        {
            if (new FileInfo(path).Length > 30_000_000)
            {
                PreviewImage.Source = null;
                PreviewPlaceholder.Text = "Screenshot saved (too large to preview)";
                PreviewPlaceholder.Visibility = Visibility.Visible;
                return;
            }
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewImage.Source = bitmap;
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            AddLog("Preview unavailable: " + ex.Message);
        }
    }

    private void AddLog(string text)
    {
        _log.Add($"[{DateTime.Now:HH:mm:ss}] {text}");
        while (_log.Count > 500) _log.RemoveAt(0);
        if (_log.Count > 0) LogList.ScrollIntoView(_log[^1]);
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        _manualChoice?.TrySetResult(true);
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        _manualChoice?.TrySetResult(false);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        RequestStop();
    }

    private async void RequestStop()
    {
        if (_cts is null || _cts.IsCancellationRequested) return;
        _cts.Cancel();
        _manualChoice?.TrySetResult(false);
        StopButton.IsEnabled = false;
        StatusText.Text = "Stopping";
        IPage? page = _activePage;
        if (page is not null && !page.IsClosed)
        {
            try { await page.CloseAsync(); } catch (Exception) { }
        }
    }

    private sealed record AuthCredentials(string UserName, string Password);
    private sealed record CaptureOptions(int Width, int Height, double DelaySeconds, int TimeoutSeconds, string OutputFolder, string BrowserChannel, bool FullPage, bool Interactive);
    private sealed record SiteSession(IBrowserContext Context, IPage Page);
}
