using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RestaurantPosWpf
{
    // ===== Models and records (co-located per convention) =====

    public class RecentPurchaseOrder
    {
        public string PONumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public int ItemCount { get; set; }
        public decimal Amount { get; set; }
        public DateTime? InvoiceDate { get; set; }

        // Display helpers
        public string DateDisplay => OrderDate.ToString("yyyy/MM/dd");
        public string ItemCountDisplay => $"{ItemCount} items";
        public string AmountDisplay => $"R{Amount:#,##0.00}";
        public string InvoiceDateDisplay => InvoiceDate.HasValue
            ? $"Invoiced {InvoiceDate.Value:yyyy/MM/dd}"
            : "Not invoiced";

        public Brush StatusBadgeBg => Status switch
        {
            "Draft" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")),
            "Sent" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")),
            "Pending Invoice" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")),
            "Partial Shipment" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCE7F3")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"))
        };

        public Brush StatusBadgeText => Status switch
        {
            "Draft" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
            "Sent" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF")),
            "Pending Invoice" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")),
            "Partial Shipment" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9D174D")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
        };
    }

    public class DiscrepancyRecord
    {
        public string PONumber { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string Ingredient { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Type { get; set; } = "Quantity Short";
        public int Ordered { get; set; }
        public int Received { get; set; }
        public string Status { get; set; } = "Open";
        public string ReportedBy { get; set; } = string.Empty;
        public DateTime ReportedOn { get; set; }

        public int Variance => Received - Ordered;
        public string ReportedDateDisplay => ReportedOn.ToString("yyyy/MM/dd");
        public string StatusDisplay => Status.ToUpperInvariant();
        public string TypeBadgeBg => Type switch
        {
            "Quantity Short" => "#FEF3C7",
            "Quantity Over" => "#FFEDD5",
            "Price Variance" => "#F3E8FF",
            "Quality Issue" => "#FCE7F3",
            "Damaged" => "#FEE2E2",
            "Wrong Item" => "#FEF9C3",
            "Overdue Delivery" => "#FFE4E6",
            _ => "#E5E7EB"
        };

        public string TypeBadgeText => Type switch
        {
            "Quantity Short" => "#92400E",
            "Quantity Over" => "#C2410C",
            "Price Variance" => "#7C3AED",
            "Quality Issue" => "#BE185D",
            "Damaged" => "#B91C1C",
            "Wrong Item" => "#A16207",
            "Overdue Delivery" => "#BE123C",
            _ => "#374151"
        };

        public string StatusBadgeBg => Status switch
        {
            "Open" => "#FEE2E2",
            "Disputed" => "#FEF3C7",
            "Resolved" => "#DCFCE7",
            "Credited" => "#DBEAFE",
            _ => "#E5E7EB"
        };

        public string StatusBadgeText => Status switch
        {
            "Open" => "#991B1B",
            "Disputed" => "#92400E",
            "Resolved" => "#166534",
            "Credited" => "#1D4ED8",
            _ => "#374151"
        };
    }

    public class DiscrepanciesNavigationContext
    {
        public List<DiscrepancyRecord> Records { get; init; } = new();
        public string InitialStatusFilter { get; init; } = "Open";
    }

    public static class ProcurementDiscrepancyStore
    {
        private static readonly List<DiscrepancyRecord> Records = BuildDemoData();
        private static int _nextSequence = 9;

        public static List<DiscrepancyRecord> GetAll()
        {
            return Records.OrderByDescending(r => r.ReportedOn).ToList();
        }

        public static void AddRecord(DiscrepancyRecord record)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));
            Records.Insert(0, record);
        }

        public static string NextPONumber()
        {
            var value = $"PO-2026-{_nextSequence:000}";
            _nextSequence++;
            return value;
        }

        private static List<DiscrepancyRecord> BuildDemoData()
        {
            return new List<DiscrepancyRecord>
            {
                new DiscrepancyRecord
                {
                    PONumber = "PO-2026-003",
                    Supplier = "Beverage World",
                    Ingredient = "Coca-Cola - 300ml",
                    Detail = "2 cases short on delivery - supplier to credit",
                    Type = "Quantity Short",
                    Ordered = 30,
                    Received = 28,
                    Status = "Open",
                    ReportedBy = "James Wilson",
                    ReportedOn = new DateTime(2026, 3, 3)
                },
                new DiscrepancyRecord
                {
                    PONumber = "PO-2026-002",
                    Supplier = "Metro Meat Suppliers",
                    Ingredient = "Beef Mince - Premium",
                    Detail = "Price increased by R5 per kg without notice",
                    Type = "Price Variance",
                    Ordered = 25,
                    Received = 25,
                    Status = "Disputed",
                    ReportedBy = "Sarah Chen",
                    ReportedOn = new DateTime(2026, 3, 2)
                },
                new DiscrepancyRecord
                {
                    PONumber = "PO-2026-004",
                    Supplier = "Fresh Produce Co",
                    Ingredient = "Tomatoes - Roma",
                    Detail = "Received 5kg more than ordered",
                    Type = "Quantity Over",
                    Ordered = 20,
                    Received = 25,
                    Status = "Open",
                    ReportedBy = "Mike Torres",
                    ReportedOn = new DateTime(2026, 3, 4)
                },
                new DiscrepancyRecord
                {
                    PONumber = "PO-2026-005",
                    Supplier = "Dairy Fresh Ltd",
                    Ingredient = "Cheddar Cheese - Block",
                    Detail = "Cheese appears moldy - questionable freshness",
                    Type = "Quality Issue",
                    Ordered = 10,
                    Received = 10,
                    Status = "Open",
                    ReportedBy = "James Wilson",
                    ReportedOn = new DateTime(2026, 3, 5)
                },
                new DiscrepancyRecord
                {
                    PONumber = "PO-2026-006",
                    Supplier = "Beverage World",
                    Ingredient = "Orange Juice - 1L",
                    Detail = "3 bottles broken during transit - packaging inadequate",
                    Type = "Damaged",
                    Ordered = 24,
                    Received = 24,
                    Status = "Open",
                    ReportedBy = "Sarah Chen",
                    ReportedOn = new DateTime(2026, 3, 6)
                },
                new DiscrepancyRecord
                {
                    PONumber = "PO-2026-007",
                    Supplier = "Metro Meat Suppliers",
                    Ingredient = "Chicken Breast - Skinless",
                    Detail = "Received chicken thighs instead of breast",
                    Type = "Wrong Item",
                    Ordered = 15,
                    Received = 15,
                    Status = "Open",
                    ReportedBy = "Mike Torres",
                    ReportedOn = new DateTime(2026, 3, 7)
                },
                new DiscrepancyRecord
                {
                    PONumber = "PO-2026-008",
                    Supplier = "Fresh Produce Co",
                    Ingredient = "Lettuce - Iceberg",
                    Detail = "Delivery was due 2026-03-05, still not received",
                    Type = "Overdue Delivery",
                    Ordered = 30,
                    Received = 0,
                    Status = "Open",
                    ReportedBy = "James Wilson",
                    ReportedOn = new DateTime(2026, 3, 8)
                }
            };
        }
    }

    // ===== Control =====

    public partial class ProcurementControl : UserControl
    {
        private readonly Action<DiscrepanciesNavigationContext>? _onViewDiscrepancies;

        public ProcurementControl(Action<DiscrepanciesNavigationContext>? onViewDiscrepancies = null)
        {
            InitializeComponent();
            _onViewDiscrepancies = onViewDiscrepancies;
            RecentPOList.ItemsSource = GetDemoRecentOrders();
            BindAttentionRequired();
        }

        // ===== Event handlers =====

        private void CreateNewOrder_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to PO creation — wired up by parent
        }

        private void ViewAlertDetails_Click(object sender, RoutedEventArgs e)
        {
            _onViewDiscrepancies?.Invoke(new DiscrepanciesNavigationContext
            {
                Records = ProcurementDiscrepancyStore.GetAll(),
                InitialStatusFilter = "Open"
            });
        }

        // ===== Demo data =====

        public static List<RecentPurchaseOrder> GetDemoRecentOrders() => new()
        {
            new RecentPurchaseOrder
            {
                PONumber = "PO-2026-005",
                Status = "Draft",
                SupplierName = "FreshPro Distributors",
                OrderDate = new DateTime(2026, 3, 22),
                ItemCount = 1,
                Amount = 1_569.75m,
                InvoiceDate = null
            },
            new RecentPurchaseOrder
            {
                PONumber = "PO-2026-002",
                Status = "Sent",
                SupplierName = "Metro Meat Suppliers",
                OrderDate = new DateTime(2026, 3, 12),
                ItemCount = 2,
                Amount = 7_360.00m,
                InvoiceDate = null
            },
            new RecentPurchaseOrder
            {
                PONumber = "PO-2026-001",
                Status = "Pending Invoice",
                SupplierName = "FreshPro Distributors",
                OrderDate = new DateTime(2026, 3, 1),
                ItemCount = 2,
                Amount = 5_907.25m,
                InvoiceDate = null
            },
            new RecentPurchaseOrder
            {
                PONumber = "PO-2026-004",
                Status = "Partial Shipment",
                SupplierName = "Beverages World",
                OrderDate = new DateTime(2026, 2, 28),
                ItemCount = 1,
                Amount = 1_277.10m,
                InvoiceDate = new DateTime(2026, 3, 10)
            }
        };

        private void BindAttentionRequired()
        {
            var records = ProcurementDiscrepancyStore.GetAll();
            var openCount = records.Count(r => r.Status == "Open");
            var disputedCount = records.Count(r => r.Status == "Disputed");

            OverdueDeliveryText.Text = $"• {Math.Max(openCount, 1)} overdue delivery expected";
            DiscrepanciesPendingText.Text = $"• {openCount + disputedCount} discrepancies pending review";
        }
    }
}
