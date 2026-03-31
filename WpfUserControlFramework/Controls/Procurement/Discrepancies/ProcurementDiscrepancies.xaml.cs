using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf
{
    public partial class ProcurementDiscrepancies : UserControl
    {
        private readonly Action _onClose;
        private readonly Action<string> _onOpenPurchaseOrder;
        private readonly Action<DiscrepancyRecord> _onOpenDispute;
        private readonly Action<DiscrepancyRecord> _onOpenResolve;
        private readonly List<DiscrepancyRecord> _records;

        public ProcurementDiscrepancies(
            DiscrepanciesNavigationContext navigationContext,
            Action<string> onOpenPurchaseOrder,
            Action<DiscrepancyRecord> onOpenDispute,
            Action<DiscrepancyRecord> onOpenResolve,
            Action onClose)
        {
            if (navigationContext is null) throw new ArgumentNullException(nameof(navigationContext));
            _onOpenPurchaseOrder = onOpenPurchaseOrder ?? throw new ArgumentNullException(nameof(onOpenPurchaseOrder));
            _onOpenDispute = onOpenDispute ?? throw new ArgumentNullException(nameof(onOpenDispute));
            _onOpenResolve = onOpenResolve ?? throw new ArgumentNullException(nameof(onOpenResolve));
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

            InitializeComponent();

            _records = navigationContext.Records.Any()
                ? navigationContext.Records
                : ProcurementDiscrepancyStore.GetAll();

            StatusFilterComboBox.ItemsSource = new List<string>
            {
                "All Statuses",
                "Open",
                "Disputed",
                "Resolved",
                "Credited"
            };

            TypeFilterComboBox.ItemsSource = new List<string>
            {
                "All Types",
                "Quantity Short",
                "Quantity Over",
                "Price Variance",
                "Quality Issue",
                "Damaged",
                "Wrong Item",
                "Overdue Delivery"
            };

            StatusFilterComboBox.SelectedItem = navigationContext.InitialStatusFilter == "Open"
                ? "Open"
                : "All Statuses";
            TypeFilterComboBox.SelectedItem = "All Types";

            RefreshView();
        }

        public double GetFooterStripHeight()
        {
            if (FooterStrip == null)
                return 0.0;

            FooterStrip.UpdateLayout();
            return FooterStrip.ActualHeight;
        }

        public void RefreshView()
        {
            var statusFilter = StatusFilterComboBox.SelectedItem?.ToString() ?? "All Statuses";
            var typeFilter = TypeFilterComboBox.SelectedItem?.ToString() ?? "All Types";

            IEnumerable<DiscrepancyRecord> query = _records;

            if (statusFilter != "All Statuses")
            {
                query = query.Where(r => r.Status == statusFilter);
            }

            if (typeFilter != "All Types")
            {
                query = query.Where(r => r.Type == typeFilter);
            }

            var filtered = query.OrderByDescending(r => r.ReportedOn).ToList();
            DiscrepancyList.ItemsSource = filtered;

            OpenIssuesCountText.Text = _records.Count(r => r.Status == "Open").ToString();
            DisputedCountText.Text = _records.Count(r => r.Status == "Disputed").ToString();
            ResolvedCountText.Text = _records.Count(r => r.Status == "Resolved").ToString();
            CreditedCountText.Text = _records.Count(r => r.Status == "Credited").ToString();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _onClose();
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            RefreshView();
        }

        private void TypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            RefreshView();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            StatusFilterComboBox.SelectedItem = "All Statuses";
            TypeFilterComboBox.SelectedItem = "All Types";
            RefreshView();
        }

        private void Dispute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DiscrepancyRecord record })
                return;

            _onOpenDispute(record);
        }

        private void Resolve_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: DiscrepancyRecord record }) return;
            _onOpenResolve(record);
        }

        private void AddDemoRecord_Click(object sender, RoutedEventArgs e)
        {
            var newRecord = new DiscrepancyRecord
            {
                PONumber = ProcurementDiscrepancyStore.NextPONumber(),
                Supplier = "FreshPro Distributors",
                Ingredient = "Tomatoes - 5kg",
                Detail = "1 crate damaged during offload",
                Type = "Damaged",
                Ordered = 14,
                Received = 13,
                Status = "Open",
                ReportedBy = "Operations Team",
                ReportedOn = DateTime.Today
            };

            ProcurementDiscrepancyStore.AddRecord(newRecord);
            _records.Insert(0, newRecord);
            RefreshView();
        }

        private void PONumber_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string poNumber })
                return;

            _onOpenPurchaseOrder(poNumber);
        }
    }
}
