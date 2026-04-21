using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RestaurantPosWpf;

/// <summary>
/// Role picker combo. Items are loaded from <c>public.roles</c> via <c>App.aps.sql.SelectActiveRoles()</c>
/// on first use; each row is mapped to the legacy <see cref="StaffAccessRole"/> enum so badge colors and
/// permission code keep working. When the database is unreachable or returns no rows, we fall back to the
/// five fixed enum values.
/// </summary>
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
        RoleCombo.ItemsSource = LoadRoleItems();
        RoleCombo.DisplayMemberPath = nameof(RoleComboItem.DisplayName);
        Loaded += (_, _) => SyncComboFromProperty();
    }

    private static List<RoleComboItem> LoadRoleItems()
    {
        var items = new List<RoleComboItem>();
        try
        {
            var dt = App.aps.pda.GetDataTable(App.aps.sql.SelectActiveRoles(), 30);
            foreach (DataRow r in dt.Rows)
            {
                var roleId = Convert.ToInt32(r["role_id"], CultureInfo.InvariantCulture);
                var descr = Convert.ToString(r["descr"]) ?? "";
                if (roleId == AppStatus.RoleIdSystem)
                    continue; // System role is internal only; never offered in the UI picker
                items.Add(new RoleComboItem(StaffAccessStore.RoleIdToEnum(roleId),
                    string.IsNullOrWhiteSpace(descr) ? StaffAccessStore.RoleIdToEnum(roleId).ToString() : descr));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[StaffAccessRoles] SelectActiveRoles failed: " + ex.Message);
        }

        if (items.Count == 0)
        {
            items.AddRange(
                Enum.GetValues(typeof(StaffAccessRole))
                    .Cast<StaffAccessRole>()
                    .Where(r => r != StaffAccessRole.System)
                    .Select(r => new RoleComboItem(r, r.ToString())));
        }

        return items;
    }

    private static void OnSelectedRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StaffAccessRoles c && c.IsLoaded)
            c.SyncComboFromProperty();
    }

    private void SyncComboFromProperty()
    {
        if (RoleCombo.ItemsSource is not IEnumerable<RoleComboItem> items)
            return;
        var match = items.FirstOrDefault(i => i.Role == SelectedRole);
        if (match != null && !ReferenceEquals(RoleCombo.SelectedItem, match))
            RoleCombo.SelectedItem = match;
    }

    private void RoleCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoleCombo.SelectedItem is RoleComboItem item && !Equals(SelectedRole, item.Role))
        {
            SetCurrentValue(SelectedRoleProperty, item.Role);
            RoleChanged?.Invoke(this, item.Role);
        }
    }

    private sealed class RoleComboItem
    {
        public RoleComboItem(StaffAccessRole role, string displayName)
        {
            Role = role;
            DisplayName = displayName;
        }

        public StaffAccessRole Role { get; }
        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }
}
