using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FileActivityNotifier.UI.Dashboard;

namespace FileActivityNotifier.UI.Dashboard.Pages;

public partial class ActivityPage : Page
{
    private readonly DashboardViewModel _viewModel;
    public ObservableCollection<DashboardEventItem> FilteredEvents { get; } = new();

    public ActivityPage(DashboardViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();

        DataContext = this;
        _viewModel.AllEvents.CollectionChanged += AllEvents_CollectionChanged;
        Unloaded += ActivityPage_Unloaded;

        RefreshFilteredEvents();
    }

    private void ActivityPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.AllEvents.CollectionChanged -= AllEvents_CollectionChanged;
        Unloaded -= ActivityPage_Unloaded;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFilteredEvents();
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshFilteredEvents();
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearEvents();
    }

    private bool ApplyFilter(DashboardEventItem item)
    {
        if (item == null)
        {
            return false;
        }

        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var fileName = item.FileName ?? string.Empty;
            var filePath = item.FilePath ?? string.Empty;
            var searchMatch = fileName.Contains(query, StringComparison.OrdinalIgnoreCase)
                              || filePath.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (!searchMatch)
            {
                return false;
            }
        }

        var selectedFilter = FilterComboBox?.SelectedItem?.ToString() ?? "All";
        return selectedFilter switch
        {
            "Renamed" => string.Equals(item.EventType, "Renamed", StringComparison.OrdinalIgnoreCase),
            "Moved" => string.Equals(item.EventType, "Moved", StringComparison.OrdinalIgnoreCase),
            "Downloaded" => string.Equals(item.EventType, "Downloaded", StringComparison.OrdinalIgnoreCase),
            "Today" => item.Timestamp.Date == DateTime.Today,
            "This Week" => item.Timestamp >= DateTime.Today.AddDays(-7),
            _ => true
        };
    }

    private void RefreshFilteredEvents()
    {
        if (_viewModel?.AllEvents == null)
        {
            return;
        }

        FilteredEvents.Clear();
        foreach (var item in _viewModel.AllEvents)
        {
            if (item != null && ApplyFilter(item))
            {
                FilteredEvents.Add(item);
            }
        }

        if (EmptyStatePanel != null)
        {
            EmptyStatePanel.Visibility = FilteredEvents.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void AllEvents_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFilteredEvents();
    }
}
