using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestaurantPosWpf
{
    public partial class ProcurementPO : UserControl
    {
        private readonly Action _onClose;

        public ProcurementPO(ProcurementPurchaseOrderDetail purchaseOrder, Action onClose)
        {
            if (purchaseOrder is null) throw new ArgumentNullException(nameof(purchaseOrder));
            _onClose = onClose ?? throw new ArgumentNullException(nameof(onClose));

            InitializeComponent();
            DataContext = purchaseOrder;
            Loaded += ProcurementPO_Loaded;
        }

        private void ProcurementPO_Loaded(object sender, RoutedEventArgs e)
        {
            Focus();
            Keyboard.Focus(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _onClose();
        }

        private void ProcurementPO_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;
            _onClose();
        }
    }
}
