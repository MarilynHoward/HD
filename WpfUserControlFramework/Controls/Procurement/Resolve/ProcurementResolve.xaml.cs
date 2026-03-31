using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf
{
    public partial class ProcurementResolve : UserControl
    {
        private readonly DiscrepancyRecord _record;
        private readonly Action _onClose;
        private readonly Action<DiscrepancyRecord, string, string> _onConfirmResolve;

        public ProcurementResolve(
            DiscrepancyRecord record,
            Action onClose,
            Action<DiscrepancyRecord, string, string> onConfirmResolve)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));
            _onConfirmResolve = onConfirmResolve ?? throw new ArgumentNullException(nameof(onConfirmResolve));

            InitializeComponent();
            DataContext = _record;
            Loaded += ProcurementResolve_Loaded;
        }

        private void ProcurementResolve_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_record.ResolveCreditNoteNumber))
                CreditNoteTextBox.Text = _record.ResolveCreditNoteNumber;
            if (!string.IsNullOrEmpty(_record.ResolveDetails))
                ResolutionDetailsTextBox.Text = _record.ResolveDetails;

            Focus();
            Keyboard.Focus(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _onClose();
        }

        private void MarkAsResolved_Click(object sender, RoutedEventArgs e)
        {
            var creditNote = CreditNoteTextBox.Text?.Trim() ?? string.Empty;
            var resolutionDetails = ResolutionDetailsTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(resolutionDetails))
            {
                MessageBox.Show(
                    "Please provide resolution details.",
                    "Resolve Discrepancy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _onConfirmResolve(_record, creditNote, resolutionDetails);
        }

        private void ProcurementResolve_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;
            _onClose();
        }
    }
}
