using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RestaurantPosWpf;

public partial class StaffAccessRoles : UserControl
{
    public static readonly DependencyProperty SelectedRoleProperty = DependencyProperty.Register(
        nameof(SelectedRole),
        typeof(StaffAccessRole),
        typeof(StaffAccessRoles),
        new FrameworkPropertyMetadata(StaffAccessRole.User, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedRoleChanged));

    public StaffAccessRole SelectedRole
    {
        get => (StaffAccessRole)GetValue(SelectedRoleProperty);
        set => SetValue(SelectedRoleProperty, value);
    }

    public event EventHandler<StaffAccessRole>? RoleChanged;

    /// <summary>False only if the combo has no selection (should not happen in normal use).</summary>
    public bool HasValidRoleSelection => RoleCombo.SelectedItem != null;

    public void FocusRoleCombo() =>
        Dispatcher.BeginInvoke(new Action(() => RoleCombo.Focus()), DispatcherPriority.Input);

    public StaffAccessRoles()
    {
        InitializeComponent();
        RoleCombo.ItemsSource = Enum.GetValues(typeof(StaffAccessRole)).Cast<StaffAccessRole>().ToList();
        Loaded += (_, _) => SyncComboFromProperty();
    }

    private static void OnSelectedRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StaffAccessRoles c && c.IsLoaded)
            c.SyncComboFromProperty();
    }

    private void SyncComboFromProperty()
    {
        if (RoleCombo.SelectedItem is StaffAccessRole r && r == SelectedRole)
            return;
        RoleCombo.SelectedItem = SelectedRole;
    }

    private void RoleCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoleCombo.SelectedItem is StaffAccessRole r && !Equals(SelectedRole, r))
        {
            SetCurrentValue(SelectedRoleProperty, r);
            RoleChanged?.Invoke(this, r);
        }
    }
}
