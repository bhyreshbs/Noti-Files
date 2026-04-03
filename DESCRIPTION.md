# FileActivityNotifier — Complete Project Specification
> **For GitHub Copilot & AI-Assisted Development**  
> Version 1.0 | C# WPF | .NET 8 | Windows 10/11  
> Drop this file in your project root. At the start of every Copilot session say: **"Read DESCRIPTION.md before writing any code."**

---

## Table of Contents
1. [Project Overview](#1-project-overview)
2. [Application Lifecycle](#2-application-lifecycle)
3. [Tech Stack](#3-tech-stack)
4. [Complete File Architecture](#4-complete-file-architecture)
4.1 [File Architecture Optimization](#41-file-architecture-optimization)
5. [Resource Requirements](#5-resource-requirements)
6. [Feature Specifications](#6-feature-specifications)
7. [UI Design System](#7-ui-design-system)
8. [Key Code Patterns](#8-key-code-patterns)
9. [Data Flow](#9-data-flow)
10. [Recommended Build Order](#10-recommended-build-order)
11. [Rules for Copilot](#11-rules-for-copilot)

---

## 1. Project Overview

FileActivityNotifier is a **native Windows desktop utility** that runs silently in the system tray and monitors the file system in real time. It detects file creation (save), move, rename, and download events, then surfaces them as elegant **glass-style toast notifications** directly on the desktop — bottom-left corner, above the taskbar.

Users also have access to a **dashboard window** (opened by clicking the tray icon) for viewing activity history, managing settings, and controlling the monitor.

### Core Principles
- Feels like a natural OS extension, not a third-party app
- Zero cloud services, zero database, zero authentication
- Fully local and offline
- Lightweight and fast — minimal resource usage
- Non-intrusive — toasts auto-dismiss, never block workflow

### What This App Is NOT
- Not a full-screen application
- Not a dashboard-first app (dashboard is secondary — toasts are the product)
- Not a cloud sync tool
- Not dependent on any server

---

## 2. Application Lifecycle

### On Windows Startup
```
Windows boots
  └─ App.xaml.cs → OnStartup()
       ├─ TrayIconService.Initialize()     ← tray icon appears in notification area
       ├─ SettingsService.Load()           ← reads settings.json
       ├─ FileWatcherService.Start()       ← begins watching configured folders
       └─ NO window is shown              ← completely silent start
```

### When a File Event is Detected (automatic, no user action needed)
```
File saved / moved / downloaded
  └─ FileWatcherService fires raw event
       └─ EventProcessor.Process()         ← builds FileEvent model object
            └─ FilterEngine.ShouldShow()   ← returns true/false
                 └─ [if true] NotificationManager.Show(fileEvent)
                       ├─ ToastStackManager.Push()    ← toast appears on desktop
                       └─ ActivityLog.Append()        ← logged to activity_log.json
```

### When Tray Icon is Clicked
```
Left-click  → DashboardWindow.ShowOrFocus()   ← opens or brings to front
Right-click → Context menu:
               ├─ Pause / Resume Monitoring
               ├─ Open Dashboard
               ├─ Settings
               └─ Exit
```

### When Dashboard Window is Closed
```
User clicks X on dashboard
  └─ Window hides (Visibility = Hidden)   ← does NOT exit the app
       └─ App continues running in tray
            └─ Toasts still fire automatically
```

---

## 3. Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| Language | C# 12 | Primary implementation language |
| UI Framework | WPF (.NET 8) | Windows Presentation Foundation |
| Future UI Upgrade | WinUI 3 | Optional upgrade for Mica/acrylic blur |
| File Monitoring | FileSystemWatcher | Built-in .NET class — no packages needed |
| Data Storage | JSON (local files only) | settings.json + activity_log.json |
| Serialization | System.Text.Json | Built-in .NET — no Newtonsoft needed |
| Tray Icon | NotifyIcon (WinForms) | Reference System.Windows.Forms |
| MVVM | INotifyPropertyChanged | Manual — no external MVVM framework |
| Animations | WPF Storyboard | Slide-in and fade-out for toasts |
| Target OS | Windows 10/11 | Min: Windows 10 1903+ for acrylic blur |

### .csproj Requirements
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>   <!-- for NotifyIcon -->
    <Nullable>enable</Nullable>
    <ApplicationIcon>Resources\Icons\AppIcon.ico</ApplicationIcon>
  </PropertyGroup>
</Project>
```

### NuGet Packages
**None required.** All core functionality uses built-in .NET 8 libraries.  
Optional: `Hardcodet.NotifyIcon.Wpf` (v1.1.0) — cleaner WPF tray icon integration, but not mandatory.

---

## 4. Complete File Architecture

```
FileActivityNotifier/
│
├── App.xaml
├── App.xaml.cs
├── FileActivityNotifier.csproj
│
├── /Core
│   ├── FileWatcherService.cs
│   ├── EventProcessor.cs
│   ├── FilterEngine.cs
│   └── NotificationManager.cs
│
├── /Models
│   ├── FileEvent.cs
│   ├── AppSettings.cs
│   └── ActivityLog.cs
│
├── /Services
│   ├── TrayIconService.cs
│   ├── SettingsService.cs
│   └── StartupService.cs
│
├── /UI
│   ├── /Toast
│   │   ├── ToastWindow.xaml
│   │   ├── ToastWindow.xaml.cs
│   │   ├── ToastViewModel.cs
│   │   └── ToastStackManager.cs
│   │
│   ├── /Dashboard
│   │   ├── DashboardWindow.xaml
│   │   ├── DashboardWindow.xaml.cs
│   │   ├── DashboardViewModel.cs
│   │   │
│   │   ├── /Pages
│   │   │   ├── ActivityPage.xaml
│   │   │   ├── ActivityPage.xaml.cs
│   │   │   ├── SettingsPage.xaml
│   │   │   └── SettingsPage.xaml.cs
│   │   │
│   │   └── /Controls
│   │       ├── ActivityFeedItem.xaml
│   │       ├── ActivityFeedItem.xaml.cs
│   │       ├── StatCard.xaml
│   │       └── StatCard.xaml.cs
│   │
│   └── /Converters
│       ├── FileTypeToIconConverter.cs
│       └── DateTimeToAgoConverter.cs
│
├── /Resources
│   ├── /Themes
│   │   ├── DarkTheme.xaml
│   │   └── LightTheme.xaml
│   │
│   ├── /Icons
│   │   ├── AppIcon.ico
│   │   ├── TrayIcon.ico
│   │   └── /FileTypes
│   │       ├── docx.png
│   │       ├── xlsx.png
│   │       ├── pdf.png
│   │       ├── img.png
│   │       ├── video.png
│   │       ├── zip.png
│   │       ├── exe.png
│   │       ├── txt.png
│   │       └── generic.png
│   │
│   └── /Fonts
│       └── (custom fonts if needed)
│
└── /Config
    ├── settings.json
    └── activity_log.json
```

**Total: 43 files**

---

### 4.1 Root Files

| File | Responsibility |
|---|---|
| `App.xaml` | WPF application entry point. Declares global resources, merged theme dictionaries, app-level styles. Sets `ShutdownMode="OnExplicitShutdown"` so app doesn't close when dashboard is hidden. |
| `App.xaml.cs` | Startup logic. Calls `TrayIconService.Initialize()`, `SettingsService.Load()`, `FileWatcherService.Start()`. Suppresses main window. Handles `OnExit()` cleanup. |
| `FileActivityNotifier.csproj` | Targets net8.0-windows, enables WPF and WinForms, sets app icon. |

---

### 4.2 /Core — Business Logic

| File | Responsibility |
|---|---|
| `FileWatcherService.cs` | Wraps `FileSystemWatcher`. Creates one watcher per folder in `WatchedFolders`. Subscribes to `Created`, `Renamed`, `Changed` events. Raises a unified `FileActivityDetected` C# event. Handles `Error` event by restarting the watcher. Exposes `Start()`, `Stop()`, `Pause()`, `Resume()` methods. |
| `EventProcessor.cs` | Receives raw `FileSystemEventArgs`. Builds a `FileEvent` model: resolves `FileName`, `FilePath`, `FileExtension`, determines `EventType` (Saved if Created, Moved if old path differs, Downloaded if from Downloads folder). Attaches timestamp. |
| `FilterEngine.cs` | Single method: `bool ShouldShow(FileEvent evt)`. Returns false for: files starting with `~`, files ending with `.tmp`/`.temp`, hidden or system file attributes, paths containing `AppData`/`Windows`/`Program Files`, files under 100 bytes, duplicate events within 2 seconds for the same path. Returns true only for extensions in `AllowedExtensions` setting. |
| `NotificationManager.cs` | Central coordinator. Receives filtered `FileEvent`, calls `ToastStackManager.Push(fileEvent)` to show toast, calls `ActivityLog.Append(fileEvent)`. Enforces max stack limit (dismisses oldest if new arrives over limit). Exposes static `Instance` singleton. |

---

### 4.3 /Models — Data Structures

| File | Responsibility |
|---|---|
| `FileEvent.cs` | Core data model. Properties: `Id` (Guid), `FileName` (string), `FilePath` (string), `FileExtension` (string), `EventType` (enum), `Timestamp` (DateTime), `IsRead` (bool). |
| `AppSettings.cs` | Settings model. Properties: `WatchedFolders` (List\<string\>), `AllowedExtensions` (List\<string\>), `Theme` (enum: Dark/Light), `NotificationDurationSeconds` (int, default 5), `MaxStackCount` (int, default 5), `StartWithWindows` (bool). |
| `ActivityLog.cs` | Log container. Holds `List<FileEvent>` with max cap of 500. Serialized to `activity_log.json`. Provides `Add()`, `Clear()`, `GetRecent(int count)` methods. |

```csharp
// FileEvent.cs
public class FileEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
}

public enum EventType { Saved, Moved, Downloaded, Renamed }
```

```csharp
// AppSettings.cs — default values
public class AppSettings
{
    public List<string> WatchedFolders { get; set; } = new()
    {
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
    };
    public List<string> AllowedExtensions { get; set; } = new()
    {
        ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".pdf",
        ".jpg", ".jpeg", ".png", ".gif", ".mp4", ".mov",
        ".zip", ".rar", ".7z", ".exe", ".msi", ".txt", ".md", ".csv"
    };
    public string Theme { get; set; } = "Dark";
    public int NotificationDurationSeconds { get; set; } = 5;
    public int MaxStackCount { get; set; } = 5;
    public bool StartWithWindows { get; set; } = false;
}
```

---

### 4.4 /Services — App-Level Services

| File | Responsibility |
|---|---|
| `TrayIconService.cs` | Creates and manages `System.Windows.Forms.NotifyIcon`. Sets icon to `TrayIcon.ico`. Builds right-click `ContextMenuStrip` with: Pause/Resume, Open Dashboard, Settings, separator, Exit. Left-click calls `DashboardWindow.ShowOrFocus()`. Exposes `UpdatePauseResumeLabel()` method. |
| `SettingsService.cs` | Reads and writes `settings.json` using `System.Text.Json`. Exposes `static AppSettings Current` property for global access. Fires `static event SettingsChanged` when saved. Settings file path: `AppDomain.CurrentDomain.BaseDirectory + "Config\\settings.json"`. Creates default settings if file doesn't exist. |
| `StartupService.cs` | Manages Windows startup via registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. `Enable()` adds entry with full `.exe` path. `Disable()` removes entry. `IsEnabled()` checks current state. |

---

### 4.5 /UI/Toast — Notification Popup

| File | Responsibility |
|---|---|
| `ToastWindow.xaml` | Borderless WPF window. `WindowStyle="None"`, `AllowsTransparency="True"`, `ShowInTaskbar="False"`, `Topmost="True"`. Width 320px. Dark semi-transparent background. 14px border radius. Drop shadow. Contains: header row (icon + title + time), body row (file icon + name + path), actions row (3 buttons). Blue progress bar at bottom. |
| `ToastWindow.xaml.cs` | Handles: `Loaded` event triggers slide-in `Storyboard`. `DispatcherTimer` for auto-dismiss countdown. `MouseEnter` pauses timer. `MouseLeave` resumes timer. `OpenFile_Click` calls `Process.Start()`. `OpenLocation_Click` calls `explorer.exe /select`. `Dismiss_Click` triggers fade-out animation then closes window. |
| `ToastViewModel.cs` | ViewModel for one toast. Properties: `FileName`, `FilePath`, `EventType`, `TimeAgo` (updates every 30s), `FileIconSource` (mapped from extension), `BadgeText`, `BadgeBackground`, `BadgeForeground`. Implements `INotifyPropertyChanged`. |
| `ToastStackManager.cs` | Manages active toast list. `Push(FileEvent)`: creates new `ToastWindow`, calculates Y position (`workArea.Bottom - height - (index * (height + 10))`), shows window. `Remove(ToastWindow)`: removes from list, repositions remaining toasts with smooth animation. Enforces `MaxStackCount` — dismisses oldest if exceeded. |

---

### 4.6 /UI/Dashboard — Main Window

| File | Responsibility |
|---|---|
| `DashboardWindow.xaml` | Main window. 900x600px default, resizable. Dark theme. Layout: left sidebar 200px (logo + nav items), top bar with 3 StatCards + Pause button, main Frame for page navigation. `WindowStyle="SingleBorderWindow"`. |
| `DashboardWindow.xaml.cs` | `ShowOrFocus()`: if hidden, show and activate; if already visible, bring to front. `OnClosing()` override: cancels close, hides window instead (`e.Cancel = true; this.Hide()`). Sidebar nav click handlers navigate Frame to `ActivityPage` or `SettingsPage`. |
| `DashboardViewModel.cs` | Master ViewModel. `ObservableCollection<FileEvent> AllEvents` — the live log. `int SavedToday`, `MovedToday`, `DownloadedToday` — computed from today's events. `bool IsMonitoring` — bound to Pause button. Subscribed to `NotificationManager` new event — adds to `AllEvents` on UI thread via `Application.Current.Dispatcher`. |

---

### 4.7 /UI/Dashboard/Pages

| File | Responsibility |
|---|---|
| `ActivityPage.xaml` | Default page. `ItemsControl` or `ListView` bound to filtered `AllEvents`. Search `TextBox` at top. Filter `ComboBox` (All / Saved / Moved / Downloaded / Today). Each item is `ActivityFeedItem` UserControl. "Clear Log" button. |
| `ActivityPage.xaml.cs` | Search text changed → filters `AllEvents` by filename/path. Filter dropdown changed → applies EventType filter. Auto-scrolls to top on new event if user is at top of list. |
| `SettingsPage.xaml` | Watched folders `ListBox` with Add/Remove buttons. Extension checkboxes grid. Theme toggle switch. Duration slider (2–15s). Max stack dropdown. Start with Windows toggle. |
| `SettingsPage.xaml.cs` | Add Folder: opens `FolderBrowserDialog`, adds to settings. Remove Folder: removes selected. All changes call `SettingsService.Save()` immediately. Startup toggle calls `StartupService.Enable()` or `Disable()`. |

---

### 4.8 /UI/Dashboard/Controls

| File | Responsibility |
|---|---|
| `ActivityFeedItem.xaml` | UserControl for one activity row. Layout: file type icon (32x32) + event badge + filename (bold) + file path (muted monospace) + timestamp (right) + Open button + Location button. Hover highlight effect. |
| `ActivityFeedItem.xaml.cs` | Open button: `Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true })`. Location button: `Process.Start("explorer.exe", $"/select,\"{FilePath}\"")`. |
| `StatCard.xaml` | Small card showing a number + label. DependencyProperties: `Count` (int), `Label` (string), `AccentColor` (Brush). Animates count change with brief scale bounce. |
| `StatCard.xaml.cs` | DependencyProperty registration for `Count`, `Label`, `AccentColor`. Count change triggers scale animation via Storyboard. |

---

### 4.9 /UI/Converters

| File | Responsibility |
|---|---|
| `FileTypeToIconConverter.cs` | `IValueConverter`. Input: file extension string. Output: `ImageSource` from `/Resources/Icons/FileTypes/`. Maps: `.docx/.doc` → docx.png, `.xlsx/.xls` → xlsx.png, `.pdf` → pdf.png, `.jpg/.jpeg/.png/.gif` → img.png, `.mp4/.mov/.avi` → video.png, `.zip/.rar/.7z` → zip.png, `.exe/.msi` → exe.png, `.txt/.md/.csv` → txt.png, anything else → generic.png. |
| `DateTimeToAgoConverter.cs` | `IValueConverter`. Input: `DateTime`. Output: human-readable string. Logic: < 60s → "just now", < 60m → "X min ago", < 24h → "X hours ago", yesterday → "Yesterday", else → date string. |

---

### 4.10 /Resources/Themes

| File | Responsibility |
|---|---|
| `DarkTheme.xaml` | `ResourceDictionary`. Defines all color brushes, border thickness, corner radius, shadow effects, button styles, progress bar style. Uses semi-transparent `SolidColorBrush` for acrylic simulation. |
| `LightTheme.xaml` | Same keys as `DarkTheme.xaml` with light/glass values. Swapped at runtime by replacing merged dictionary in `App.xaml` when theme setting changes. |

---

### 4.11 /Config

| File | Content |
|---|---|
| `settings.json` | Serialized `AppSettings` object. Created with defaults on first run if missing. |
| `activity_log.json` | Serialized `ActivityLog` object (list of `FileEvent`). Max 500 entries. Read on startup for dashboard history. Appended during session. |

---

## 4.1 File Architecture Optimization

**Total Files: 26-28 (Optimized) vs. 43 (Full Specification)**

The full specification lists 43 files for completeness, but the following optimizations reduce this while maintaining functionality:

### Recommended Consolidations:
1. **ToastStackManager.cs** → Merge logic into `ToastWindow.xaml.cs` (related toast positioning and dismissal)
2. **ActivityFeedItem.xaml UserControl** → Replace with `DataTemplate` in ActivityPage.xaml (simple item display)
3. **StatCard.xaml UserControl** → Replace with `DataTemplate` or inline in DashboardWindow (simple count display)
4. **File Type Icons** → Replace 9 PNG files with **Font Awesome** or emoji (reduces to 1-2 resource files)

### File Distribution (Optimized):
| Category | Count | Rationale |
|---|---|---|
| Project Setup | 1 | .csproj file |
| App-level | 2 | App.xaml + App.xaml.cs |
| Models | 3 | FileEvent, AppSettings, ActivityLog |
| Core Logic | 4 | FileWatcher, EventProcessor, FilterEngine, NotificationManager |
| Services | 3 | Settings, Tray, Startup |
| Toast UI | 3 | ToastWindow (xaml+cs+vm) merged with stack logic |
| Dashboard Main | 3 | DashboardWindow (xaml+cs) + ViewModel |
| Pages | 4 | Activity (xaml+cs) + Settings (xaml+cs) |
| Converters | 2 | FileTypeIcon + DateTimeAgo |
| Themes | 2 | DarkTheme + LightTheme ResourceDictionaries |
| Icons | 2-3 | App icon + Tray icon (+ font library) |
| Config | 2 | settings.json + activity_log.json |
| **TOTAL** | **26-28** | **Fully functional MVP** |

### UI File Breakdown (Optimized):
- **Total UI Files: 20-22 (67% of project)**
- Toast: 3 files (ToastWindow, ToastViewModel, merged stack logic)
- Dashboard: 3 main + 4 pages + 2 converters = 9 files
- Themes: 2 ResourceDictionaries
- Resources: 2-3 icon files (Font Awesome + icons)

---

## 5. Resource Requirements

Before starting UI development, collect the following external resources:

### 5.1 Fonts (2 families required)

**1. Sora Font Family**
- URL: https://fonts.google.com/specimen/Sora
- Used for: Primary UI font (buttons, labels, headings, filenames)
- Weights needed: 400 (Regular), 500 (Medium), 600 (SemiBold), 700 (Bold)
- Installation: Extract .ttf files to `/Resources/Fonts/Sora/`

**2. JetBrains Mono Font Family**
- URL: https://fonts.google.com/specimen/JetBrains+Mono
- Used for: Monospace font (file paths, timestamps, code snippets)
- Weights needed: 400 (Regular)
- Installation: Extract .ttf files to `/Resources/Fonts/JetBrains/`

### 5.2 Icons & Assets

**Application Icons (2 required):**
| File | Size | Purpose | Source |
|---|---|---|---|
| `AppIcon.ico` | 256x256 + 128x128 | Main application icon | Design your own or Flaticon |
| `TrayIcon.ico` | 32x32 + 16x16 | System tray icon | Design your own or Icons8 |

**File Type Icons (alternative strategies):**
- **Option A (Recommended):** Use **Font Awesome** (free) or **Material Design Icons** library (1 file)
  - Add font file to `/Resources/Fonts/FontAwesome.ttf`
  - Map file extensions to icon codes in `FileTypeToIconConverter`
  
- **Option B (Original spec):** Use 9 individual PNG files (40x40px each)
  - docx.png, xlsx.png, pdf.png, img.png, video.png, zip.png, exe.png, txt.png, generic.png
  - Download from: [Flaticon](https://www.flaticon.com/) or [Icons8](https://icons8.com/)
  - Save to `/Resources/Icons/FileTypes/`

**Recommended sources:**
- [Flaticon](https://www.flaticon.com/) - Free icon sets, commercial use OK
- [Icons8](https://icons8.com/) - Professional icons with free tier
- [Font Awesome](https://fontawesome.com/) - Icon font library (free CDN or local)
- Design tools: Figma, Adobe XD, Inkscape

### 5.3 Color Palette Reference

**Already defined in specification (section 6.1)** but recommended to create:
- Digital swatch reference in Figma or [Coolors.co](https://coolors.co/)
- Export as CSS/XAML color palette for reference
- Verify contrast ratios meet WCAG AA standards

### 5.4 Optional Design System Documentation

- **Figma mockup** - Prototype the UI before coding
  - Free tier available at [figma.com](https://figma.com)
  - Create mockups for Toast, Dashboard Activity, Dashboard Settings
  
- **Design system guide** - Document all colors, spacing, typography
  - Motion/animation timings
  - Component specifications
  - State variations

---

## 6. Feature Specifications

### 6.1 File System Monitoring

- One `FileSystemWatcher` instance per folder in `WatchedFolders`
- `IncludeSubdirectories = true`
- Watches: `NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName`
- `InternalBufferSize = 65536` (default 8192 is too small, causes missed events)
- Debounce: 500ms delay — if same file path fires again within 500ms, ignore duplicate
- On watcher `Error` event: dispose and recreate the watcher automatically
- FileSystemWatcher events fire on background threads — always marshal to UI thread with `Application.Current.Dispatcher.Invoke()`

### 6.2 Filter Engine Rules

**Always BLOCK (return false):**
- Filename starts with `~`
- Extension is `.tmp`, `.temp`, `.log`, `.lock`
- `File.GetAttributes()` has `Hidden` or `System` flag
- Path contains: `\AppData\`, `\Windows\`, `\Program Files\`, `\ProgramData\`
- File size < 100 bytes
- Same `FilePath` event seen within last 2 seconds (debounce dictionary)

**Allow only if:**
- Extension is in `SettingsService.Current.AllowedExtensions`

### 6.3 Toast Notification — Full Visual Spec

**Window properties:**
```
WindowStyle        = None
AllowsTransparency = True
ShowInTaskbar      = False
Topmost            = True
Width              = 320
SizeToContent      = Height
ResizeMode         = NoResize
```

**Position calculation:**
```csharp
var workArea = SystemParameters.WorkArea;
toast.Left = workArea.Left + 18;
toast.Top  = workArea.Bottom - toast.ActualHeight - 8 - (stackIndex * (toast.ActualHeight + 10));
```

**Visual values:**
```
Background         : #121214 at 82% opacity with backdrop blur simulation
Border             : 1px, rgba(255,255,255, 8.5%)
BorderRadius       : 14px
BoxShadow          : 0 8px 32px rgba(0,0,0,0.55), 0 2px 8px rgba(0,0,0,0.4)
InnerTopHighlight  : 1px gradient line at top (white 7% → transparent)
ProgressBar        : 2px, bottom, blue (#3B82F6 → #60A5FA), animates width 100%→0%
```

**Layout sections:**
```
[HEADER]
  App icon (18x18, blue rounded square) | "File Activity" label | timestamp (right)

[BODY]
  File icon (40x40, rounded 9px)        | Filename (bold, 13.5px)
  + Event badge (bottom-right of icon)  | File path (muted mono, 10.5px, truncated)

[ACTIONS]
  [Open File]  [Open Location]  [✕]
```

**Event badge colors:**
| Event | Background | Text |
|---|---|---|
| Saved | `#166534` | `#4ADE80` |
| Moved | `#713F12` | `#FBBF24` |
| Downloaded | `#1E3A5F` | `#60A5FA` |

**Behavior:**
- Slide-in: 380ms, `cubic-bezier(0.22, 1, 0.36, 1)`, `translateY(24px) → 0` + `scale(0.96) → 1`
- Auto-dismiss: configurable (default 5s). Timer starts after slide-in completes.
- Hover: `MouseEnter` → pause timer. `MouseLeave` → resume timer.
- Dismiss animation: 320ms fade + `translateX(-18px)` + `scale(0.97)`
- Max stack: 5 toasts. If 6th arrives, oldest is force-dismissed first.
- Stack gap: 10px between toasts
- Stack repositions smoothly when middle toast is dismissed (remaining toasts animate to new Y positions)

### 6.4 Dashboard Window

**Layout:**
```
┌─────────────────────────────────────────────────────────┐
│  [Logo] FileActivityNotifier         [_] [□] [×]        │
├──────────────┬──────────────────────────────────────────┤
│              │  [Saved: 24]  [Moved: 5]  [Down: 12]  [⏸]│
│  📋 Activity │─────────────────────────────────────────  │
│  ⚙ Settings  │                                          │
│              │   ActivityPage or SettingsPage here      │
│              │                                          │
│              │                                          │
└──────────────┴──────────────────────────────────────────┘
```

**Activity Page features:**
- Scrollable reverse-chronological list of all `FileEvent` objects
- Real-time updates — new events appear at top instantly
- Search bar: filters by `FileName` or `FilePath` (case-insensitive, live as you type)
- Filter dropdown: All / Saved / Moved / Downloaded / Today / This Week
- Each row: `ActivityFeedItem` control (icon + badge + name + path + time + buttons)
- "Clear Log" button: calls `ActivityLog.Clear()`, clears `DashboardViewModel.AllEvents`
- Auto-scroll to top when new event arrives (only if user is already at top)

**Settings Page features:**
- Watched folders list with Add (FolderBrowserDialog) and Remove buttons
- Extension checkboxes (one per extension in default list, plus custom add field)
- Theme toggle: Dark / Light (swaps ResourceDictionary at runtime)
- Notification duration slider: 2s to 15s with live label
- Max stack count: ComboBox 1–10
- Start with Windows: ToggleSwitch (calls StartupService)
- All changes auto-save on change — no explicit Save button

### 6.5 System Tray

- `NotifyIcon` visible in notification area at all times
- Left-click → `DashboardWindow.ShowOrFocus()`
- Right-click context menu:
  - **Pause Monitoring** / **Resume Monitoring** (toggles, label updates dynamically)
  - **Open Dashboard**
  - **Settings** (opens dashboard on settings page)
  - `────────────`
  - **Exit** (calls `Application.Current.Shutdown()`)
- Tooltip text: "FileActivityNotifier — Monitoring X folders"
- Icon changes slightly when paused (dimmed version of tray icon)

### 6.6 Windows Startup

- Registry path: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Key name: `FileActivityNotifier`
- Value: full path to `.exe` (e.g. `C:\Program Files\FileActivityNotifier\FileActivityNotifier.exe`)
- Managed entirely by `StartupService.cs`
- Toggled from Settings page via `ToggleSwitch`

---

## 7. UI Design System

### 7.1 Color Palette (Dark Theme)

| Token | Value | Usage |
|---|---|---|
| `Background` | `#121214` | Toast and window background base |
| `Surface` | `#1A1A2E` | Cards, sidebar, elevated surfaces |
| `SurfaceHover` | `#22223A` | Hover states on surface elements |
| `Border` | `rgba(255,255,255, 8.5%)` | All borders and dividers |
| `BorderHover` | `rgba(255,255,255, 13%)` | Hover border state |
| `AccentBlue` | `#3B82F6` | Primary buttons, progress bar, links |
| `AccentBlueDark` | `#1D4ED8` | App icon background, pressed state |
| `AccentBlueLight` | `#93C5FD` | Button text, highlights |
| `TextPrimary` | `rgba(255,255,255, 92%)` | File names, headings |
| `TextSecondary` | `rgba(255,255,255, 55%)` | Labels, subtitles |
| `TextMuted` | `rgba(255,255,255, 28%)` | File paths, timestamps |
| `Green` | `#4ADE80` | Saved badge text |
| `GreenBg` | `#166534` | Saved badge background |
| `Amber` | `#FBBF24` | Moved badge text |
| `AmberBg` | `#713F12` | Moved badge background |
| `Blue` | `#60A5FA` | Downloaded badge text |
| `BlueBg` | `#1E3A5F` | Downloaded badge background |
| `Danger` | `#EF4444` | Dismiss button hover |

### 7.2 Typography

| Element | Font | Size | Weight |
|---|---|---|---|
| File name (toast) | Sora | 13.5px | SemiBold 600, letter-spacing -0.01em |
| File path | JetBrains Mono | 10.5px | Regular 400 |
| App title label | Sora | 10.5px | Medium 500, uppercase, letter-spacing 0.02em |
| Timestamp | JetBrains Mono | 10px | Regular 400 |
| Buttons | Sora | 11.5px | Medium 500, letter-spacing 0.01em |
| Dashboard heading | Sora | 18px | SemiBold 600 |
| Stat count | Sora | 28px | Bold 700, accent color |
| Sidebar nav item | Sora | 13px | Medium 500 |
| Body text | Sora | 13px | Regular 400 |

### 7.3 Spacing & Sizing

```
Toast width           : 320px
Toast padding         : 14px 14px 12px
Toast border-radius   : 14px
Toast section gap     : 10-12px
File icon size        : 40x40px, border-radius 9px
App icon size         : 18x18px, border-radius 5px
Button height         : ~30px (6px padding top/bottom)
Button border-radius  : 7px
Stack gap             : 10px between toasts
Toast to taskbar gap  : 8px
Toast left margin     : 18px from screen edge
Dashboard size        : 900x600px default
Sidebar width         : 200px
```

### 7.4 Button Styles

```
Primary (Open File):
  Background  : rgba(59,130,246, 0.20)
  Text        : #93C5FD
  Border      : 1px rgba(59,130,246, 0.25)
  Hover bg    : rgba(59,130,246, 0.32)
  Hover text  : #BFDBFE

Secondary (Open Location):
  Background  : rgba(255,255,255, 0.055)
  Text        : rgba(255,255,255, 0.55)
  Border      : 1px rgba(255,255,255, 0.08)
  Hover bg    : rgba(255,255,255, 0.10)
  Hover text  : rgba(255,255,255, 0.75)

Dismiss (✕):
  Width       : 30px
  Background  : rgba(255,255,255, 0.04)
  Text        : rgba(255,255,255, 0.30)
  Border      : 1px rgba(255,255,255, 0.07)
  Hover bg    : rgba(239,68,68, 0.15)
  Hover text  : rgba(239,68,68, 0.80)
  Hover border: rgba(239,68,68, 0.20)
```

---

## 8. Key Code Patterns

### 8.1 Toast Window Positioning
```csharp
private void PositionToast(int stackIndex)
{
    var workArea = SystemParameters.WorkArea;
    this.Left = workArea.Left + 18;
    this.Top  = workArea.Bottom - this.ActualHeight - 8
                - (stackIndex * (this.ActualHeight + 10));
}
```

### 8.2 Slide-In Animation (XAML Storyboard)
```xml
<Window.RenderTransform>
    <TranslateTransform x:Name="SlideTransform" Y="24"/>
</Window.RenderTransform>

<Window.Triggers>
    <EventTrigger RoutedEvent="Loaded">
        <BeginStoryboard>
            <Storyboard>
                <DoubleAnimation Storyboard.TargetProperty="Opacity"
                    From="0" To="1" Duration="0:0:0.38"/>
                <DoubleAnimation Storyboard.TargetName="SlideTransform"
                    Storyboard.TargetProperty="Y"
                    From="24" To="0" Duration="0:0:0.38">
                    <DoubleAnimation.EasingFunction>
                        <CubicEase EasingMode="EaseOut"/>
                    </DoubleAnimation.EasingFunction>
                </DoubleAnimation>
            </Storyboard>
        </BeginStoryboard>
    </EventTrigger>
</Window.Triggers>
```

### 8.3 Auto-Dismiss Timer
```csharp
private DispatcherTimer _dismissTimer;

private void StartDismissTimer()
{
    _dismissTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromSeconds(SettingsService.Current.NotificationDurationSeconds)
    };
    _dismissTimer.Tick += (s, e) => DismissToast();
    _dismissTimer.Start();
}

private void Toast_MouseEnter(object sender, MouseEventArgs e) => _dismissTimer.Stop();
private void Toast_MouseLeave(object sender, MouseEventArgs e) => _dismissTimer.Start();
```

### 8.4 Open File and Open Location
```csharp
// Open the file with its default application
Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

// Open containing folder and highlight the file
Process.Start("explorer.exe", $"/select,\"{filePath}\"");
```

### 8.5 FileSystemWatcher Setup
```csharp
var watcher = new FileSystemWatcher(folderPath)
{
    NotifyFilter       = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
    IncludeSubdirectories = true,
    InternalBufferSize = 65536,
    EnableRaisingEvents = true
};
watcher.Created += OnFileCreated;
watcher.Renamed += OnFileRenamed;
watcher.Error   += OnWatcherError;
```

### 8.6 Background Thread → UI Thread (CRITICAL)
```csharp
// FileSystemWatcher fires on background threads!
// ALWAYS use Dispatcher before touching UI or ObservableCollection
private void OnFileCreated(object sender, FileSystemEventArgs e)
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        var fileEvent = EventProcessor.Process(e);
        if (FilterEngine.ShouldShow(fileEvent))
            NotificationManager.Instance.Show(fileEvent);
    });
}
```

### 8.7 Settings Save
```csharp
public static void Save(AppSettings settings)
{
    Current = settings;
    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(SettingsFilePath, json);
    SettingsChanged?.Invoke(null, settings);
}
```

### 8.8 Dashboard ShowOrFocus
```csharp
public void ShowOrFocus()
{
    if (!this.IsVisible)
    {
        this.Show();
    }
    this.Activate();
    this.WindowState = WindowState.Normal;
}
```

### 8.9 Prevent App Close on Dashboard X
```csharp
// In DashboardWindow.xaml.cs
protected override void OnClosing(CancelEventArgs e)
{
    e.Cancel = true;   // Don't close
    this.Hide();       // Just hide
}
```

### 8.10 Theme Switching at Runtime
```csharp
public static void ApplyTheme(string theme)
{
    var dict = new ResourceDictionary
    {
        Source = new Uri($"/Resources/Themes/{theme}Theme.xaml", UriKind.Relative)
    };
    var existing = Application.Current.Resources.MergedDictionaries
        .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme") == true);
    if (existing != null)
        Application.Current.Resources.MergedDictionaries.Remove(existing);
    Application.Current.Resources.MergedDictionaries.Add(dict);
}
```

---

## 9. Data Flow

```
[ Windows File System ]
        │
        │  FileSystemWatcher events (background thread)
        ↓
[ FileWatcherService.cs ]
        │  Dispatcher.Invoke() → back to UI thread
        │  Raises: FileActivityDetected(RawEvent)
        ↓
[ EventProcessor.cs ]
        │  Builds FileEvent model
        │  Resolves: FileName, FilePath, Extension, EventType, Timestamp
        ↓
[ FilterEngine.cs ]
        │  Returns: bool ShouldShow
        │  Blocks: .tmp, hidden, system, duplicates, wrong extensions
        ↓ (only if ShouldShow = true)
[ NotificationManager.cs ]
        │
        ├──────────────────────────────────────────┐
        │                                          │
        ↓                                          ↓
[ ToastStackManager.cs ]             [ DashboardViewModel.cs ]
        │                                          │
        │  Calculates stack position               │  AllEvents.Insert(0, fileEvent)
        │  Creates ToastWindow                     │  Updates SavedToday / MovedToday
        ↓                                          ↓
[ ToastWindow.xaml ]                 [ ActivityPage ListView ]
  Slide-in animation                   New row appears at top
  Progress bar countdown               StatCards update
  Auto-dismiss timer
        │
        ↓
[ ActivityLog.cs ]
  Append to List<FileEvent>
  Serialize to activity_log.json
```

---

## 10. Recommended Build Order

Build in this sequence — each step has everything it depends on already built:

| Step | What to Build | Why This Order |
|---|---|---|
| 1 | `FileEvent.cs`, `AppSettings.cs`, `ActivityLog.cs` | Everything depends on these models — build first |
| 2 | `SettingsService.cs` + `settings.json` default | Services need settings to initialize |
| 3 | `FilterEngine.cs` | Pure logic, no UI dependencies, easy to test in isolation |
| 4 | `FileWatcherService.cs` + `EventProcessor.cs` | Core engine — verify events fire correctly before UI |
| 5 | `ToastWindow.xaml` (hardcoded dummy data first) | Build and style the visual before wiring real data |
| 6 | `ToastViewModel.cs` + `ToastStackManager.cs` | Connect real `FileEvent` data to the toast UI |
| 7 | `NotificationManager.cs` | Wire the full pipeline: watcher → filter → toast |
| 8 | `TrayIconService.cs` + `App.xaml.cs` startup | App now runs silently, toasts fire on real file events |
| 9 | `DashboardWindow.xaml` + `ActivityPage` | Dashboard now has real events to display |
| 10 | `SettingsPage` + `StartupService.cs` + `Converters` | Final layer — user control and polish |

---

## 11. Rules for Copilot

> **Read these before writing any code in this project.**

1. **This is C# WPF .NET 8, Windows-only.** Do not suggest cross-platform approaches.

2. **MVVM pattern throughout.** ViewModels implement `INotifyPropertyChanged` manually. No external MVVM frameworks (no CommunityToolkit, no Prism, no MVVM Light).

3. **No databases.** All storage is `System.Text.Json` to local `.json` files only.

4. **No NuGet packages unless absolutely unavoidable.** Use built-in .NET 8 libraries first.

5. **Toast windows MUST always have:** `WindowStyle="None"`, `AllowsTransparency="True"`, `ShowInTaskbar="False"`, `Topmost="True"`.

6. **FileSystemWatcher fires on background threads.** ALWAYS use `Application.Current.Dispatcher.Invoke()` before touching any UI element or `ObservableCollection`.

7. **Use `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })` for opening files.** Use `Process.Start("explorer.exe", $"/select,\"{path}\"")` for opening folder locations.

8. **Settings are accessed via `SettingsService.Current` (static singleton).** Never hardcode folder paths. Always use `Environment.GetFolderPath()`.

9. **Default watched folders:** Documents, Downloads, Desktop — resolved via `Environment.SpecialFolder`.

10. **The app MUST NOT show any window on startup.** Only the tray icon appears. Set `ShutdownMode="OnExplicitShutdown"` in `App.xaml`.

11. **Dashboard `OnClosing()` MUST cancel the close and hide instead.** Only `Exit` from the tray menu calls `Application.Current.Shutdown()`.

12. **Themes are ResourceDictionaries.** Switch themes at runtime by swapping the merged dictionary — do not hardcode colors in XAML files directly.

13. **`InternalBufferSize` on FileSystemWatcher MUST be set to `65536`.** The default 8192 causes missed events on busy folders.

14. **Activity log max cap is 500 entries.** When adding a new event, if count exceeds 500, remove the oldest entry first.

15. **Debounce duplicate events.** If the same `FilePath` fires within 2 seconds, ignore the second event. Use a `Dictionary<string, DateTime>` in `FilterEngine` to track last-seen times.

---

*End of specification — FileActivityNotifier v1.0*
