# URL Screenshotter

A Windows desktop application that captures screenshots of multiple URLs with a configurable browser viewport size. Built with WPF (.NET 10) and Microsoft.Playwright.

<img width="1066" height="823" alt="image" src="https://github.com/user-attachments/assets/2b9f6ed8-d441-4f47-a8df-3d3f3edfed50" />

## Features

- Paste or load a list of HTTP/HTTPS URLs, one per line.
- Set any viewport width and height (up to 50 million pixels), or choose a preset such as 1920x1080, 1280x720, 1080x1080, or portrait 1080x1920.
- Capture the viewport as a PNG at its requested pixel dimensions, with device scale factor 1.
- Optionally capture the full scrollable page instead of the viewport.
- Wait after page load for JavaScript-heavy sites, panoramas, video interfaces, WebGL scenes and lazy assets.
- Use an interactive capture mode to log in, dismiss dialogs, manipulate the page, or move the camera before pressing **Capture this page**. A **Skip page** option is also provided.
- HTTP Basic Authentication: on an HTTP 401 response, ask for the username/password in the WPF app and retry the site. Credentials are reused in memory for other URLs on the same origin during the current batch.
- Reuse the same browser page/context for URLs on the same origin so cookies and web-app login sessions survive across those URLs within one batch.
- Output filenames contain the index, hostname/path, and viewport resolution. Existing files are never overwritten.
- Last screenshot preview, per-page log, cancel button, and saved non-secret app settings.

## Requirements

- Windows 10 or 11, x64.
- Microsoft Edge (recommended), or Google Chrome, already installed.
- For building: Visual Studio 2026 with the .NET desktop development workload, or the .NET 10 SDK.
- Internet access for the first NuGet restore.

Playwright uses the installed Edge/Chrome browser channel, so no `playwright install` step or extra Chromium download is required.

## Build and run

1. Open `UrlScreenshotter/UrlScreenshotter.csproj` in Visual Studio, or run `build.bat`.
2. Build and run (`F5` in Visual Studio).
3. To publish a release folder that includes the .NET runtime, run `publish-win-x64.bat`. The application is at `publish/win-x64/UrlScreenshotter.exe`. Distribute the *entire* published folder, not just the EXE.

If you want a smaller framework-dependent deployment, use:

```cmd
cd UrlScreenshotter
dotnet publish -c Release -r win-x64 --self-contained false -o ..\publish\win-x64-framework-dependent
```

That variant requires the .NET 10 Desktop Runtime to be installed on the target PC.

## Typical workflow

1. Paste URLs into the left box, one per line; blank lines and lines starting with `#` are ignored. You may omit `https://` for ordinary domains.
2. Select `1920 x 1080 (16:9)` or type your own width and height. This is the *browser viewport*, not the WPF application's window size.
3. Choose an output folder and an optional post-load delay. Start with 3-10 seconds for WebGL/3D pages that initialize slowly.
4. Enable **Interactive** for pages requiring manual setup. The controlled Edge/Chrome window opens, and you can interact with it; return to the WPF app and click **Capture this page**.
5. For `.htaccess` HTTP Basic Auth, the application should detect a 401 challenge and prompt for a username/password. Enter each site's credentials; they are kept in memory only until the batch finishes.
6. Click **Start capture**. The app saves separate numbered PNGs for each URL.

### Notes and limitations

- **Exact size:** The default screenshot (Full scrollable page **off**) is captured at the selected viewport resolution, independent of the visible WPF app window. The browser's visible native window may be partially outside the physical display at very large sizes, but Playwright still uses the requested virtual viewport. Full-page capture intentionally changes the resulting height to match the document's full scroll height.
- **WebGL/3D:** The page must actually finish rendering. Increase the delay, or use Interactive mode to decide when the canvas is ready. Some GPU-heavy pages may behave differently in a browser controlled by automation.
- **Passwords:** Basic Auth credentials are not written to settings or logs. A separate browser context is used per origin, and credentials are scoped to that origin. Do not enter credentials on plain HTTP unless you accept that the connection is unencrypted.
- **Login forms:** Use Interactive mode to log into web apps. Forms and SSO login pages are not filled automatically. Cookies persist within the same origin for the current batch, not across app restarts.
- **Session boundaries:** A login that jumps across multiple third-party origins or opens a separate window may require extra manual setup.
- **Errors:** HTTP error pages are captured with a warning, except HTTP 401, which triggers the Basic Auth flow. Navigation exceptions are logged and the batch continues.
- **Security:** Only capture sites you have authorization to access. URLs and screenshots can contain personal or private information; review saved output before sharing.
- **Development note:** This project has been written and reviewed but not compiled in a Windows environment here. Restore/build it in Visual Studio to verify against your installed .NET SDK and browser.

## Technology

- `Microsoft.Playwright` 1.64.1
- `.NET 10` WPF
- Playwright `channel = "msedge"` (or `"chrome"`)
- Viewport screenshots via `IPage.ScreenshotAsync` with explicit viewport and device scale factor

Official documentation: https://playwright.dev/dotnet/docs/screenshots and https://playwright.dev/dotnet/docs/browsers
