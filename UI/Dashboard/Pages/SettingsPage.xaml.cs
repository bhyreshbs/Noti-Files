using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Storage.Pickers;
using WinRT.Interop;
using FileActivityNotifier.Core;
using FileActivityNotifier.Models;
using FileActivityNotifier.Services;

namespace FileActivityNotifier.UI.Dashboard.Pages;

public partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly FileWatcherService _fileWatcherService;
    private readonly AppSettings _settings;
    private bool _isLoading;

    public SettingsPage(SettingsService settingsService, StartupService startupService, FileWatcherService fileWatcherService)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _fileWatcherService = fileWatcherService;
        _settings = _settingsService.Load();

        InitializeComponent();
        Unloaded += SettingsPage_Unloaded;

        _isLoading = true;
        LoadFromSettings();
        _isLoading = false;
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Unloaded -= SettingsPage_Unloaded;
    }

    private void LoadFromSettings()
    {
        FoldersListView.ItemsSource = null;
        FoldersListView.ItemsSource = _settings.WatchedFolders;

        foreach (var checkBox in ExtensionCheckBoxes())
        {
            if (checkBox.Tag is string ext)
            {
                checkBox.IsChecked = _settings.AllowedExtensions.Contains(NormalizeExtension(ext), StringComparer.OrdinalIgnoreCase);
            }
        }

        ThemeComboBox.SelectedItem = _settings.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";

        DurationSlider.Value = _settings.NotificationDurationSeconds;
        DurationLabel.Text = FormatDuration(_settings.NotificationDurationSeconds);

        MaxStackComboBox.Items.Clear();
        for (var i = 1; i <= 10; i++)
        {
            MaxStackComboBox.Items.Add(i);
        }
        MaxStackComboBox.SelectedItem = _settings.MaxStackCount;

        StartupCheckBox.IsChecked = _startupService.IsEnabled() || _settings.StartWithWindows;
    }

    private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var windowHandle = Process.GetCurrentProcess().MainWindowHandle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, windowHandle);

        var folder = await picker.PickSingleFolderAsync();
        var selectedPath = folder?.Path;
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        if (_settings.WatchedFolders.Contains(selectedPath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.WatchedFolders.Add(selectedPath);
        RefreshFolderList();
        SaveSettings(restartWatchers: true);
    }

    private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (FoldersListView.SelectedItem is not string selectedFolder)
        {
            return;
        }

        _settings.WatchedFolders.RemoveAll(x => x.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase));
        RefreshFolderList();
        SaveSettings(restartWatchers: true);
    }

    private void ExtensionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (sender is not CheckBox checkBox || checkBox.Tag is not string ext)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            var normalized = NormalizeExtension(ext);
            if (!_settings.AllowedExtensions.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                _settings.AllowedExtensions.Add(normalized);
            }
        }
        else
        {
            var normalized = NormalizeExtension(ext);
            _settings.AllowedExtensions.RemoveAll(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        }

        SaveSettings();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var selected = ThemeComboBox.SelectedItem?.ToString() ?? "Dark";
        _settings.Theme = selected;
        ApplyTheme(selected);
        SaveSettings();
    }

    private void DurationSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.NotificationDurationSeconds = Math.Round(DurationSlider.Value * 2, MidpointRounding.AwayFromZero) / 2;
        if (DurationLabel != null)
        {
            DurationLabel.Text = FormatDuration(_settings.NotificationDurationSeconds);
        }

        SaveSettings();
    }

    private void MaxStackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (MaxStackComboBox.SelectedItem is int count)
        {
            _settings.MaxStackCount = count;
            SaveSettings();
        }
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        var enableStartup = StartupCheckBox.IsChecked == true;
        _settings.StartWithWindows = enableStartup;

        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            _startupService.SetEnabled(enableStartup, executablePath);
        }

        SaveSettings();
    }

    private void RefreshFolderList()
    {
        FoldersListView.ItemsSource = null;
        FoldersListView.ItemsSource = _settings.WatchedFolders;
    }

    private void SaveSettings(bool restartWatchers = false)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.Normalize();
        _settingsService.Save(_settings);

        if (restartWatchers && _fileWatcherService.IsRunning)
        {
            _fileWatcherService.Restart();
        }
    }

    private static string NormalizeExtension(string extension)
    {
        return (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
    }

    private static string FormatDuration(double seconds)
    {
        return seconds % 1 == 0
            ? $"{seconds:0}s"
            : $"{seconds:0.0}s";
    }

    private static void ApplyTheme(string theme)
    {
        if (Application.Current is App app)
        {
            app.SetAppTheme(theme);
        }
    }

    private IEnumerable<CheckBox> ExtensionCheckBoxes()
    {
        yield return DocxExtensionCheckBox;
        yield return PdfExtensionCheckBox;
        yield return XlsxExtensionCheckBox;
        yield return JpgExtensionCheckBox;
        yield return PngExtensionCheckBox;
        yield return Mp4ExtensionCheckBox;
        yield return ZipExtensionCheckBox;
        yield return TxtExtensionCheckBox;
    }
}
