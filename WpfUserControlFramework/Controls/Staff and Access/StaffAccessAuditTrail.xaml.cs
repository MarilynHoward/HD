using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf;

public partial class StaffAccessAuditTrail : UserControl
{
    public event EventHandler? CloseRequested;

    private Visibility _activityScrollLastBarVisibility = Visibility.Collapsed;

    public StaffAccessAuditTrail()
    {
        InitializeComponent();
    }

    public void Bind(StaffUser user)
    {
        var s = StaffAccessAuditPresentation.BuildSummary(user);
        TxtSubtitle.Text =
            $"Activity log for {user.UserName} (ID: {user.NumericId.ToString(CultureInfo.CurrentCulture)})";
        TxtStatLogins.Text = s.TotalLogins.ToString(CultureInfo.CurrentCulture);
        TxtStatSecurity.Text = s.SecurityEvents.ToString(CultureInfo.CurrentCulture);
        TxtStatFailed.Text = s.FailedAttempts.ToString(CultureInfo.CurrentCulture);
        TxtStatActions.Text = s.TotalActions.ToString(CultureInfo.CurrentCulture);
        ItemsActivity.ItemsSource = s.Entries;
        Dispatcher.BeginInvoke(new Action(ApplyActivityScrollSymmetricPadding), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void StaffAccessAuditTrail_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SvActivityList == null)
            return;
        SvActivityList.LayoutUpdated -= SvActivityList_OnLayoutUpdated;
        SvActivityList.LayoutUpdated += SvActivityList_OnLayoutUpdated;
        _activityScrollLastBarVisibility = SvActivityList.ComputedVerticalScrollBarVisibility;
        ApplyActivityScrollSymmetricPadding();
    }

    private void StaffAccessAuditTrail_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (SvActivityList != null)
            SvActivityList.LayoutUpdated -= SvActivityList_OnLayoutUpdated;
    }

    private void SvActivityList_OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (SvActivityList == null)
            return;
        var v = SvActivityList.ComputedVerticalScrollBarVisibility;
        if (v == _activityScrollLastBarVisibility)
            return;
        _activityScrollLastBarVisibility = v;
        ApplyActivityScrollSymmetricPadding();
    }

    /// <summary>
    /// Reserve <see cref="SystemParameters.VerticalScrollBarWidth"/> on the left always, and on the right when the bar
    /// is hidden (same gutter pattern as the Staff user list), so audit rows stay visually centered whether the
    /// scrollbar is showing.
    /// </summary>
    private void ApplyActivityScrollSymmetricPadding()
    {
        if (SvActivityList == null)
            return;
        const double rightBase = 0.0;
        var sbw = SystemParameters.VerticalScrollBarWidth;
        var scrollbarVisible = SvActivityList.ComputedVerticalScrollBarVisibility == Visibility.Visible;
        var left = rightBase + sbw;
        var right = scrollbarVisible ? rightBase : rightBase + sbw;
        SvActivityList.Padding = new Thickness(left, 0, right, 0);
    }

    private void BtnClose_OnClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
