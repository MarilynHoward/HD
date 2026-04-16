using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf;

public partial class StaffAccessBioMetric : UserControl
{
    private StaffUser? _user;

    public StaffAccessBioMetric()
    {
        InitializeComponent();
    }

    public void LoadFromUser(StaffUser? user)
    {
        _user = user;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var enrolled = _user?.BiometricEnrolled == true;
        TxtBioStatus.Text = enrolled ? "Enrolled — fingerprint template on file" : "Not enrolled";
    }

    private void BtnSimulateEnroll_OnClick(object sender, RoutedEventArgs e)
    {
        if (_user == null)
            return;
        _user.BiometricEnrolled = true;
        RefreshStatus();
    }

    private void BtnClearBio_OnClick(object sender, RoutedEventArgs e)
    {
        if (_user == null)
            return;
        _user.BiometricEnrolled = false;
        RefreshStatus();
    }
}
