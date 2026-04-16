using System.Windows.Controls;

namespace RestaurantPosWpf;

public partial class StaffAccessSecurity : UserControl
{
    public StaffAccessSecurity()
    {
        InitializeComponent();
    }

    public void ApplyToUser(StaffUser user)
    {
        user.SecurityQuestion = TxtSecurityQuestion.Text?.Trim() ?? "";
        user.RequirePasswordChangeOnNextSignIn = TglRequirePasswordChange.IsChecked == true;
        user.PasswordChangedInSession = !string.IsNullOrEmpty(PwdNew.Password);
        PwdNew.Password = "";
        PwdConfirm.Password = "";
    }

    public void LoadFromUser(StaffUser? user)
    {
        TxtSecurityQuestion.Text = user?.SecurityQuestion ?? "";
        TglRequirePasswordChange.IsChecked = user?.RequirePasswordChangeOnNextSignIn ?? false;
        PwdNew.Password = "";
        PwdConfirm.Password = "";
    }

    /// <summary>Validates password pair; returns error message or null when OK or empty (no change).</summary>
    public string? ValidatePasswords()
    {
        if (string.IsNullOrEmpty(PwdNew.Password) && string.IsNullOrEmpty(PwdConfirm.Password))
            return null;
        if (PwdNew.Password != PwdConfirm.Password)
            return "Password and confirmation do not match.";
        if (PwdNew.Password.Length < 4)
            return "Password must be at least 4 characters.";
        return null;
    }
}
