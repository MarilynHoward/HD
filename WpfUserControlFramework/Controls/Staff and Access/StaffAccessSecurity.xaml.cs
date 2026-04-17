using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace RestaurantPosWpf;

public partial class StaffAccessSecurity : UserControl
{
    private const string PwdEmptyTooltip =
        "Enter a new password here to update this account’s sign-in password. Leave blank to keep the current password.";
    private const string ConfirmEmptyTooltip =
        "Re-enter the new password here so it matches the password field above.";

    private enum NewPwdInlineKind
    {
        None,
        Required,
        MinLength
    }

    private enum ConfirmPwdInlineKind
    {
        None,
        Required,
        Mismatch
    }

    private NewPwdInlineKind _newPwdInlineKind;
    private ConfirmPwdInlineKind _confirmPwdInlineKind;

    private bool _revealNew;
    private bool _revealConfirm;

    public StaffAccessSecurity()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshPasswordFieldFeedback();
            UpdateNewEyeUi();
            UpdateConfirmEyeUi();
        };
    }

    public void ApplyToUser(StaffUser user)
    {
        var newPwd = GetNewPassword();
        user.PasswordChangedInSession = !string.IsNullOrEmpty(newPwd);
        if (!string.IsNullOrEmpty(newPwd))
            user.LastPasswordChangedUtc = DateTime.UtcNow;

        PwdNew.Password = "";
        TxtNewPlain.Text = "";
        PwdConfirm.Password = "";
        TxtConfirmPlain.Text = "";
        ForcePasswordFieldsHidden();
        ClearAllInlinePasswordErrors();
        RefreshPasswordFieldFeedback();
    }

    public void LoadFromUser(StaffUser? user)
    {
        PwdNew.Password = "";
        TxtNewPlain.Text = "";
        PwdConfirm.Password = "";
        TxtConfirmPlain.Text = "";
        ForcePasswordFieldsHidden();
        ClearAllInlinePasswordErrors();

        TxtLastPasswordChange.Text = FormatLastPasswordChange(user?.LastPasswordChangedUtc);
        RefreshPasswordFieldFeedback();
    }

    private void ForcePasswordFieldsHidden()
    {
        if (_revealNew)
            PwdNew.Password = TxtNewPlain.Text ?? "";
        TxtNewPlain.Visibility = Visibility.Collapsed;
        PwdNew.Visibility = Visibility.Visible;
        PathNewEyeSlash.Visibility = Visibility.Collapsed;
        _revealNew = false;
        BtnToggleNew.ToolTip = "Show password";

        if (_revealConfirm)
            PwdConfirm.Password = TxtConfirmPlain.Text ?? "";
        TxtConfirmPlain.Visibility = Visibility.Collapsed;
        PwdConfirm.Visibility = Visibility.Visible;
        PathConfirmEyeSlash.Visibility = Visibility.Collapsed;
        _revealConfirm = false;
        BtnToggleConfirm.ToolTip = "Show password";
    }

    private static string FormatLastPasswordChange(DateTime? utc)
    {
        if (utc is not { } u)
            return "—";
        var local = u.Kind == DateTimeKind.Utc ? u.ToLocalTime() : DateTime.SpecifyKind(u, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
    }

    /// <summary>True when the user entered a new password (validated separately before save).</summary>
    public bool HasPendingPasswordChange() => !string.IsNullOrEmpty(GetNewPassword());

    /// <summary>
    /// Validates password fields for save: sets inline errors, focuses the first invalid control.
    /// Returns false when the user started a password change but values are invalid.
    /// </summary>
    public bool TryValidatePasswordsForSave()
    {
        ClearAllInlinePasswordErrors();

        var a = GetNewPassword();
        var b = GetConfirmPassword();
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            return true;

        if (string.IsNullOrEmpty(a))
        {
            ShowNewInlineError(NewPwdInlineKind.Required, "Enter a new password.");
            FocusNewPasswordField();
            return false;
        }

        if (string.IsNullOrEmpty(b))
        {
            ShowConfirmInlineError(ConfirmPwdInlineKind.Required, "Confirm the new password.");
            FocusConfirmPasswordField();
            return false;
        }

        if (a != b)
        {
            ShowConfirmInlineError(ConfirmPwdInlineKind.Mismatch, "Password and confirmation do not match.");
            FocusConfirmPasswordField();
            return false;
        }

        if (a.Length < 4)
        {
            ShowNewInlineError(NewPwdInlineKind.MinLength, "Password must be at least 4 characters.");
            FocusNewPasswordField();
            return false;
        }

        return true;
    }

    private string GetNewPassword() =>
        _revealNew ? TxtNewPlain.Text ?? "" : PwdNew.Password ?? "";

    private string GetConfirmPassword() =>
        _revealConfirm ? TxtConfirmPlain.Text ?? "" : PwdConfirm.Password ?? "";

    private void BtnToggleNew_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleReveal(ref _revealNew, PwdNew, TxtNewPlain, PathNewEyeSlash, BtnToggleNew);
        UpdateNewEyeUi();
        RefreshPasswordFieldFeedback();
    }

    private void BtnToggleConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        ToggleReveal(ref _revealConfirm, PwdConfirm, TxtConfirmPlain, PathConfirmEyeSlash, BtnToggleConfirm);
        UpdateConfirmEyeUi();
        RefreshPasswordFieldFeedback();
    }

    private static void ToggleReveal(
        ref bool reveal,
        PasswordBox pwd,
        TextBox plain,
        Path slash,
        Button toggleBtn)
    {
        if (!reveal)
        {
            plain.Text = pwd.Password;
            plain.Visibility = Visibility.Visible;
            pwd.Visibility = Visibility.Collapsed;
            slash.Visibility = Visibility.Visible;
            reveal = true;
            toggleBtn.ToolTip = "Hide password";
        }
        else
        {
            pwd.Password = plain.Text ?? "";
            plain.Visibility = Visibility.Collapsed;
            pwd.Visibility = Visibility.Visible;
            slash.Visibility = Visibility.Collapsed;
            reveal = false;
            toggleBtn.ToolTip = "Show password";
        }
    }

    private void UpdateNewEyeUi()
    {
        PathNewEyeSlash.Visibility = _revealNew ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleNew.ToolTip = _revealNew ? "Hide password" : "Show password";
    }

    private void UpdateConfirmEyeUi()
    {
        PathConfirmEyeSlash.Visibility = _revealConfirm ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleConfirm.ToolTip = _revealConfirm ? "Hide password" : "Show password";
    }

    private void PwdNew_OnPasswordChanged(object sender, RoutedEventArgs e) => RefreshPasswordFieldFeedback();

    private void PwdConfirm_OnPasswordChanged(object sender, RoutedEventArgs e) => RefreshPasswordFieldFeedback();

    private void TxtNewPlain_OnTextChanged(object sender, TextChangedEventArgs e) => RefreshPasswordFieldFeedback();

    private void TxtConfirmPlain_OnTextChanged(object sender, TextChangedEventArgs e) => RefreshPasswordFieldFeedback();

    private void PwdField_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(RefreshPasswordFieldFeedback), System.Windows.Threading.DispatcherPriority.Input);

    private void PwdField_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(RefreshPasswordFieldFeedback), System.Windows.Threading.DispatcherPriority.Input);

    private void PwdConfirmField_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(RefreshPasswordFieldFeedback), System.Windows.Threading.DispatcherPriority.Input);

    private void PwdConfirmField_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Dispatcher.BeginInvoke(new Action(RefreshPasswordFieldFeedback), System.Windows.Threading.DispatcherPriority.Input);

    private void RefreshPasswordFieldFeedback()
    {
        RefreshPasswordTooltips();
        RefreshWatermarks();
        ClearStaleInlinePasswordErrors();
    }

    private void RefreshPasswordTooltips()
    {
        var newEmpty = string.IsNullOrEmpty(GetNewPassword());
        var newActive = GrdNewPwd.IsKeyboardFocusWithin;
        GrdNewPwd.ToolTip = newEmpty && !newActive ? PwdEmptyTooltip : null;

        var cEmpty = string.IsNullOrEmpty(GetConfirmPassword());
        var cActive = GrdConfirmPwd.IsKeyboardFocusWithin;
        GrdConfirmPwd.ToolTip = cEmpty && !cActive ? ConfirmEmptyTooltip : null;
    }

    private void RefreshWatermarks()
    {
        TbWatermarkNew.Visibility = ShouldShowWatermark(GrdNewPwdInputHost, GetNewPassword())
            ? Visibility.Visible
            : Visibility.Collapsed;
        TbWatermarkConfirm.Visibility = ShouldShowWatermark(GrdConfirmPwdInputHost, GetConfirmPassword())
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool ShouldShowWatermark(UIElement inputHost, string value) =>
        string.IsNullOrEmpty(value) && !inputHost.IsKeyboardFocusWithin;

    private void ClearStaleInlinePasswordErrors()
    {
        var a = GetNewPassword();
        var b = GetConfirmPassword();

        if (_newPwdInlineKind == NewPwdInlineKind.Required && !string.IsNullOrEmpty(a))
            ClearNewInlineError();
        if (_newPwdInlineKind == NewPwdInlineKind.MinLength && (a.Length >= 4 || string.IsNullOrEmpty(a)))
            ClearNewInlineError();

        if (_confirmPwdInlineKind == ConfirmPwdInlineKind.Required && !string.IsNullOrEmpty(b))
            ClearConfirmInlineError();
        if (_confirmPwdInlineKind == ConfirmPwdInlineKind.Mismatch && a == b)
            ClearConfirmInlineError();
    }

    private void ClearAllInlinePasswordErrors()
    {
        ClearNewInlineError();
        ClearConfirmInlineError();
    }

    private void ClearNewInlineError()
    {
        _newPwdInlineKind = NewPwdInlineKind.None;
        TblPwdNewError.Visibility = Visibility.Collapsed;
        TblPwdNewError.Text = "";
    }

    private void ClearConfirmInlineError()
    {
        _confirmPwdInlineKind = ConfirmPwdInlineKind.None;
        TblPwdConfirmError.Visibility = Visibility.Collapsed;
        TblPwdConfirmError.Text = "";
    }

    private void ShowNewInlineError(NewPwdInlineKind kind, string message)
    {
        _newPwdInlineKind = kind;
        TblPwdNewError.Text = message;
        TblPwdNewError.Visibility = Visibility.Visible;
    }

    private void ShowConfirmInlineError(ConfirmPwdInlineKind kind, string message)
    {
        _confirmPwdInlineKind = kind;
        TblPwdConfirmError.Text = message;
        TblPwdConfirmError.Visibility = Visibility.Visible;
    }

    private void FocusNewPasswordField() =>
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (_revealNew)
                    TxtNewPlain.Focus();
                else
                    PwdNew.Focus();
            }),
            System.Windows.Threading.DispatcherPriority.Input);

    private void FocusConfirmPasswordField() =>
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (_revealConfirm)
                    TxtConfirmPlain.Focus();
                else
                    PwdConfirm.Focus();
            }),
            System.Windows.Threading.DispatcherPriority.Input);
}
