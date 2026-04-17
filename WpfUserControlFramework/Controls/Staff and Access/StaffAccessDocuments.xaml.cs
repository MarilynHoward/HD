using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RestaurantPosWpf;

public partial class StaffAccessDocuments : UserControl
{
    private StaffUser? _user;

    public event EventHandler? IdAttachRequested;
    public event EventHandler? IdViewRequested;
    public event EventHandler? ProfileAttachRequested;

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
        if (_user == null)
        {
            TxtIdDocPath.Text = "";
            TxtProfilePath.Text = "";
            BtnIdView.IsEnabled = false;
            BtnIdAttach.IsEnabled = false;
            BtnProfileAttach.IsEnabled = false;
            BdrIdSync.Visibility = Visibility.Collapsed;
            BdrProfileSync.Visibility = Visibility.Collapsed;
            TxtAttachedIdLine.Visibility = Visibility.Collapsed;
            TxtAttachedProfileLine.Visibility = Visibility.Collapsed;
            return;
        }

        BtnIdAttach.IsEnabled = true;
        BtnProfileAttach.IsEnabled = true;

        var hasId = _user.IdDocumentPdfBytes is { Length: > 0 };
        var idPath = hasId
            ? (!string.IsNullOrWhiteSpace(_user.IdDocumentFileName)
                ? NormalizeDisplayPath(_user.IdDocumentFileName)
                : $"docs{Path.DirectorySeparatorChar}{_user.NumericId.ToString(CultureInfo.InvariantCulture)}_id.pdf")
            : "(none)";
        TxtIdDocPath.Text = idPath;
        BtnIdView.IsEnabled = hasId;
        ApplySyncBadge(BdrIdSync, TxtIdSync, _user.IdDocumentRemoteSyncStatus, hasId);

        var hasProf = _user.ProfileImageBytes is { Length: > 0 };
        var profPath = hasProf
            ? (!string.IsNullOrWhiteSpace(_user.ProfileImageRepositoryRelativePath)
                ? NormalizeDisplayPath(_user.ProfileImageRepositoryRelativePath)
                : "—")
            : "(none)";
        TxtProfilePath.Text = profPath;
        ApplySyncBadge(BdrProfileSync, TxtProfileSync, _user.ProfileImageRemoteSyncStatus, hasProf);

        TxtAttachedIdLine.Visibility = hasId ? Visibility.Visible : Visibility.Collapsed;
        TxtAttachedIdLine.Text = hasId ? $"• ID document: {idPath}" : "";

        TxtAttachedProfileLine.Visibility = hasProf ? Visibility.Visible : Visibility.Collapsed;
        TxtAttachedProfileLine.Text = hasProf ? $"• Profile image: {profPath}" : "";
    }

    private static string NormalizeDisplayPath(string path) =>
        (path ?? "").Replace('/', Path.DirectorySeparatorChar);

    private static void ApplySyncBadge(Border badge, TextBlock label, StaffFileRemoteSyncStatus status, bool show)
    {
        if (!show)
        {
            badge.Visibility = Visibility.Collapsed;
            return;
        }

        badge.Visibility = Visibility.Visible;
        if (status == StaffFileRemoteSyncStatus.PendingSync)
        {
            // Amber chip (distinct from success) — same family as prior demo state.
            badge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
            badge.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 211, 77));
            label.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));
            label.Text = "Pending sync";
        }
        else
        {
            // Match StaffAccessBioMetric success banner (#DCFCE7 / #BBF7D0 / #166534).
            badge.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
            badge.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208));
            label.Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52));
            label.Text = "Synced";
        }
    }

    private void BtnIdView_OnClick(object sender, RoutedEventArgs e) =>
        IdViewRequested?.Invoke(this, EventArgs.Empty);

    private void BtnIdAttach_OnClick(object sender, RoutedEventArgs e) =>
        IdAttachRequested?.Invoke(this, EventArgs.Empty);

    private void BtnProfileAttach_OnClick(object sender, RoutedEventArgs e) =>
        ProfileAttachRequested?.Invoke(this, EventArgs.Empty);
}
