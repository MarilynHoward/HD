using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public partial class StaffAccessBioMetric : UserControl
{
    private StaffUser? _user;
    private DispatcherTimer? _enrollTimer;
    private bool _simulating;

    /// <summary>Raised when enroll / simulated capture updates biometric state.</summary>
    public event EventHandler? BiometricStateChanged;

    public StaffAccessBioMetric()
    {
        InitializeComponent();
        Unloaded += (_, _) => CancelEnrollmentSimulation();
    }

    public void LoadFromUser(StaffUser? user)
    {
        CancelEnrollmentSimulation();
        _user = user;
        RefreshChrome();
    }

    private void CancelEnrollmentSimulation()
    {
        if (_enrollTimer != null)
        {
            _enrollTimer.Stop();
            _enrollTimer.Tick -= EnrollTimer_OnTick;
            _enrollTimer = null;
        }

        _simulating = false;
        TxtEnrollingHint.Visibility = Visibility.Collapsed;
    }

    private void RefreshChrome()
    {
        var hasUser = _user != null;
        var enrolled = hasUser && _user!.BiometricEnrolled;
        BioEnrollmentCard.IsEnabled = hasUser;
        BtnPrimaryBioAction.IsEnabled = hasUser && !_simulating;
        BioStatusBanner.Visibility = enrolled ? Visibility.Visible : Visibility.Collapsed;
        TxtFingerprintSubtitle.Text = enrolled ? "Enrolled" : "Not enrolled";
        BtnPrimaryBioAction.Content = enrolled ? "Re-enroll" : "Enroll fingerprint";
    }

    private void BtnPrimaryBioAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (_user == null || _simulating)
            return;
        StartEnrollmentSimulation();
    }

    private void StartEnrollmentSimulation()
    {
        _simulating = true;
        TxtEnrollingHint.Text = "Place your finger on the reader…";
        TxtEnrollingHint.Visibility = Visibility.Visible;
        BtnPrimaryBioAction.IsEnabled = false;

        _enrollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        _enrollTimer.Tick += EnrollTimer_OnTick;
        _enrollTimer.Start();
    }

    private void EnrollTimer_OnTick(object? sender, EventArgs e)
    {
        CancelEnrollmentSimulation();
        if (_user == null)
            return;
        _user.BiometricEnrolled = true;
        RefreshChrome();
        BiometricStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
