using System.Linq;
using System.Windows;
using System.Windows.Controls;

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
