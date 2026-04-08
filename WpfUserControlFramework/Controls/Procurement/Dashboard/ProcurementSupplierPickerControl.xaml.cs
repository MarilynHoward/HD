using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf
{
    public sealed class ProcurementSupplierPickerItem
    {
        public ProcurementSupplierPickerItem(string supplierCode, string displayText)
        {
            SupplierCode = supplierCode ?? string.Empty;
            DisplayText = displayText ?? string.Empty;
        }

        public string SupplierCode { get; private set; }
        public string DisplayText { get; private set; }
    }

    public sealed class ProcurementSupplierConfirmedEventArgs : EventArgs
    {
        public ProcurementSupplierConfirmedEventArgs(string supplierCode)
        {
            SupplierCode = supplierCode ?? string.Empty;
        }

        public string SupplierCode { get; private set; }
    }

    public partial class ProcurementSupplierPickerControl : UserControl
    {
        public event EventHandler Cancelled;
        public event EventHandler<ProcurementSupplierConfirmedEventArgs> SupplierConfirmed;

        public ProcurementSupplierPickerControl()
        {
            InitializeComponent();
            Loaded += ProcurementSupplierPickerControl_Loaded;
        }

        public void SetSupplierOptions(IEnumerable<ProcurementSupplierPickerItem> supplierOptions)
        {
            cmbSuppliers.ItemsSource = supplierOptions;
            cmbSuppliers.SelectedIndex = -1;
            txtValidation.Visibility = Visibility.Collapsed;
        }

        private void ProcurementSupplierPickerControl_Loaded(object sender, RoutedEventArgs e)
        {
            cmbSuppliers.Focus();
        }

        private void cmbSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSuppliers.SelectedValue != null)
                txtValidation.Visibility = Visibility.Collapsed;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            RaiseCancelled();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            RaiseCancelled();
        }

        private void btnContinue_Click(object sender, RoutedEventArgs e)
        {
            string supplierCode = cmbSuppliers.SelectedValue as string;

            if (string.IsNullOrWhiteSpace(supplierCode))
            {
                txtValidation.Visibility = Visibility.Visible;
                cmbSuppliers.Focus();
                return;
            }

            EventHandler<ProcurementSupplierConfirmedEventArgs> handler = SupplierConfirmed;
            if (handler != null)
                handler(this, new ProcurementSupplierConfirmedEventArgs(supplierCode));
        }

        private void RaiseCancelled()
        {
            EventHandler handler = Cancelled;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}