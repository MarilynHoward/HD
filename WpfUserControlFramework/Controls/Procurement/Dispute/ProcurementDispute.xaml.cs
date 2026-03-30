using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf
{
    public partial class ProcurementDispute : UserControl
    {
        private readonly DiscrepancyRecord _record;
        private readonly Action _onClose;
        private readonly Action<DiscrepancyRecord, string, string, string> _onConfirmDispute;

        public ProcurementDispute(
            DiscrepancyRecord record,
            Action onClose,
            Action<DiscrepancyRecord, string, string, string> onConfirmDispute)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
            _onConfirmDispute = onConfirmDispute ?? throw new ArgumentNullException(nameof(onConfirmDispute));

            InitializeComponent();
            DataContext = _record;
            Loaded += ProcurementDispute_Loaded;
        }

        private void ProcurementDispute_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_record.DisputeReason))
                ReasonTextBox.Text = _record.DisputeReason;
            if (!string.IsNullOrEmpty(_record.DisputeContactPerson))
                ContactPersonTextBox.Text = _record.DisputeContactPerson;
            if (!string.IsNullOrEmpty(_record.DisputeContactEmail))
                ContactEmailTextBox.Text = _record.DisputeContactEmail;

            Focus();
            Keyboard.Focus(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _onClose();
        }

        private void MarkAsDisputed_Click(object sender, RoutedEventArgs e)
        {
            var reason = ReasonTextBox.Text?.Trim() ?? string.Empty;
            var contact = ContactPersonTextBox.Text?.Trim() ?? string.Empty;
            var email = ContactEmailTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(reason)
                || string.IsNullOrWhiteSpace(contact)
                || string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show(
                    "Please fill in all required fields.",
                    "Dispute Discrepancy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var at = email.IndexOf('@', StringComparison.Ordinal);
            if (at <= 0 || at >= email.Length - 1)
            {
                MessageBox.Show(
                    "Please enter a valid email address.",
                    "Dispute Discrepancy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _onConfirmDispute(_record, reason, contact, email);
        }

        private void ProcurementDispute_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;
            _onClose();
        }
    }
}
