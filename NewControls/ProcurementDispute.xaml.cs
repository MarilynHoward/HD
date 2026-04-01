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

            if (!ValidateForm())
            {
                FocusFirstInvalidField();
                return;
            }

            _onConfirmDispute(_record, reason, contact, email);
        }

        private bool ValidateForm()
        {
            var reason = ReasonTextBox.Text?.Trim() ?? string.Empty;
            var contact = ContactPersonTextBox.Text?.Trim() ?? string.Empty;
            var email = ContactEmailTextBox.Text?.Trim() ?? string.Empty;

            bool isReasonValid = !string.IsNullOrWhiteSpace(reason);
            bool isContactValid = !string.IsNullOrWhiteSpace(contact);
            bool isEmailPresent = !string.IsNullOrWhiteSpace(email);
            bool isEmailValid = isEmailPresent && IsValidEmail(email);

            lblReasonError.Visibility = isReasonValid ? Visibility.Collapsed : Visibility.Visible;
            lblContactPersonError.Visibility = isContactValid ? Visibility.Collapsed : Visibility.Visible;
            lblContactEmailError.Visibility = isEmailPresent ? Visibility.Collapsed : Visibility.Visible;
            lblContactEmailInvalidError.Visibility = (isEmailPresent && !isEmailValid)
                ? Visibility.Visible
                : Visibility.Collapsed;

            return isReasonValid && isContactValid && isEmailValid;
        }

        private static bool IsValidEmail(string email)
        {
            var at = email.IndexOf('@', StringComparison.Ordinal);
            return at > 0 && at < email.Length - 1;
        }

        private void FocusFirstInvalidField()
        {
            if (lblReasonError.Visibility == Visibility.Visible)
            {
                ReasonTextBox.Focus();
                Keyboard.Focus(ReasonTextBox);
                return;
            }

            if (lblContactPersonError.Visibility == Visibility.Visible)
            {
                ContactPersonTextBox.Focus();
                Keyboard.Focus(ContactPersonTextBox);
                return;
            }

            ContactEmailTextBox.Focus();
            Keyboard.Focus(ContactEmailTextBox);
        }

        private void ReasonTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var reason = ReasonTextBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(reason))
                lblReasonError.Visibility = Visibility.Collapsed;
        }

        private void ContactPersonTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var contact = ContactPersonTextBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(contact))
                lblContactPersonError.Visibility = Visibility.Collapsed;
        }

        private void ContactEmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var email = ContactEmailTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(email))
            {
                lblContactEmailInvalidError.Visibility = Visibility.Collapsed;
                return;
            }

            lblContactEmailError.Visibility = Visibility.Collapsed;
            lblContactEmailInvalidError.Visibility = IsValidEmail(email)
                ? Visibility.Collapsed
                : Visibility.Visible;
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
