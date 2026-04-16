using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf;

public partial class StaffAccessDocuments : UserControl
{
    public event EventHandler? UploadRequested;
    public event EventHandler? ViewRequested;
    public event EventHandler? ReuploadRequested;

    private StaffUser? _user;

    public StaffAccessDocuments()
    {
        InitializeComponent();
    }

    public void LoadFromUser(StaffUser? user)
    {
        _user = user;
        RefreshChrome();
    }

    public void RefreshChrome()
    {
        var has = _user?.IdDocumentPdfBytes is { Length: > 0 };
        var name = _user?.IdDocumentFileName;
        TxtFileName.Text = has && !string.IsNullOrWhiteSpace(name)
            ? name!
            : has
                ? "ID document (PDF)"
                : "No document uploaded";
        BtnViewId.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        BtnReuploadId.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnUploadId_OnClick(object sender, RoutedEventArgs e) =>
        UploadRequested?.Invoke(this, EventArgs.Empty);

    private void BtnViewId_OnClick(object sender, RoutedEventArgs e) =>
        ViewRequested?.Invoke(this, EventArgs.Empty);

    private void BtnReuploadId_OnClick(object sender, RoutedEventArgs e) =>
        ReuploadRequested?.Invoke(this, EventArgs.Empty);
}
