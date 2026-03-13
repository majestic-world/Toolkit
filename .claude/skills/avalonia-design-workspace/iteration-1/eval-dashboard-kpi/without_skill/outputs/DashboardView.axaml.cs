using Avalonia.Controls;

namespace UnrealTools.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        ActivitiesGrid.ItemsSource = ActivityItem.GetSampleData();
    }
}
