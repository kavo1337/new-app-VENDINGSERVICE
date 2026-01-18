using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace app.CLIENT;

public sealed class DashboardTile : INotifyPropertyChanged
{
    private string _selectedStatusText = "";

    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;

    public int EfficiencyPercent { get; set; }
    public int WorkingCount { get; set; }
    public int OfflineCount { get; set; }
    public int ServiceCount { get; set; }

    public decimal SalesTotal { get; set; }
    public decimal CashTotal { get; set; }
    public int MaintenanceTotal { get; set; }

    public ObservableCollection<ChartItem> ChartItems { get; } = new();
    public ObservableCollection<string> NewsItems { get; } = new();

    public string SelectedStatusText
    {
        get => _selectedStatusText;
        set
        {
            if (_selectedStatusText != value)
            {
                _selectedStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ChartItem
{
    public string Day { get; init; } = string.Empty;
    public double BarHeight { get; init; }
    public string ValueText { get; init; } = string.Empty;
}

public sealed class TileTemplateSelector : DataTemplateSelector
{
    public DataTemplate? EfficiencyTemplate { get; set; }
    public DataTemplate? NetworkStateTemplate { get; set; }
    public DataTemplate? SummaryTemplate { get; set; }
    public DataTemplate? SalesTemplate { get; set; }
    public DataTemplate? NewsTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not DashboardTile tile)
        {
            return null;
        }

        return tile.Key switch
        {
            "Efficiency" => EfficiencyTemplate,
            "Network" => NetworkStateTemplate,
            "Summary" => SummaryTemplate,
            "Sales" => SalesTemplate,
            "News" => NewsTemplate,
            _ => null
        };
    }
}
