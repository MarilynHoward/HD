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
        public List<ProcurementPurchaseOrderDetail> PurchaseOrders { get; init; } = new();
        public string InitialStatusFilter { get; init; } = "Open";
    }

    public class ProcurementPurchaseOrderItem
    {
        public string ItemCode { get; set; } = string.Empty;
        public string Ingredient { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string PackSize { get; set; } = string.Empty;
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }

        public decimal LineTotal => QuantityOrdered * UnitCost;
        public string UnitCostDisplay => $"R{UnitCost:#,##0.00}";
        public string LineTotalDisplay => $"R{LineTotal:#,##0.00}";
        public string QuantityReceivedBrush => QuantityReceived < QuantityOrdered ? "#D97706" : "#111827";
        public string Note => QuantityReceived < QuantityOrdered ? $"{QuantityOrdered - QuantityReceived} cases short" : string.Empty;
    }

    public class ProcurementPurchaseOrderDetail
    {
        public string PONumber { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierReference { get; set; } = string.Empty;
        public DateTime DeliveryDate { get; set; }
        public string DeliveryLocation { get; set; } = string.Empty;
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime ApprovedOn { get; set; }
        public List<ProcurementPurchaseOrderItem> Items { get; set; } = new();

        public decimal Subtotal => Items.Sum(i => i.LineTotal);
        public decimal Vat => Subtotal * 0.15m;
        public decimal Total => Subtotal + Vat;
        public string CreatedDateDisplay => CreatedOn.ToString("yyyy/MM/dd");
        public string CreatedByLine => $"Created on {CreatedOn:yyyy/MM/dd} by {CreatedBy}";
        public string DeliveryDateDisplay => DeliveryDate.ToString("yyyy/MM/dd");
        public string ApprovedDateDisplay => ApprovedOn.ToString("yyyy/MM/dd, HH:mm:ss");
        public string TotalDisplay => $"R{Total:#,##0.00}";
        public string SubtotalDisplay => $"R{Subtotal:#,##0.00}";
        public string VatDisplay => $"R{Vat:#,##0.00}";
        public string ItemCountDisplay => $"{Items.Count} items";
    }

    public static class ProcurementPurchaseOrderStore
    {
        private static readonly List<ProcurementPurchaseOrderDetail> PurchaseOrders = BuildDemoData();

        public static List<ProcurementPurchaseOrderDetail> GetAll()
        {
            return PurchaseOrders
                .Select(Clone)
                .Select(NormalizePurchaseOrder)
                .ToList();
        }

        public static ProcurementPurchaseOrderDetail? GetByPONumber(string poNumber)
        {
            if (string.IsNullOrWhiteSpace(poNumber))
                return null;

            var record = PurchaseOrders.FirstOrDefault(p =>
                string.Equals(p.PONumber, poNumber, StringComparison.OrdinalIgnoreCase));
            return record == null ? null : NormalizePurchaseOrder(Clone(record));
        }

        private static ProcurementPurchaseOrderDetail Clone(ProcurementPurchaseOrderDetail source)
        {
            return new ProcurementPurchaseOrderDetail
            {
                PONumber = source.PONumber,
                StatusLabel = source.StatusLabel,
                CreatedOn = source.CreatedOn,
                CreatedBy = source.CreatedBy,
                SupplierName = source.SupplierName,
                SupplierReference = source.SupplierReference,
                DeliveryDate = source.DeliveryDate,
                DeliveryLocation = source.DeliveryLocation,
                ApprovedBy = source.ApprovedBy,
                ApprovedOn = source.ApprovedOn,
                Items = source.Items.Select(i => new ProcurementPurchaseOrderItem
                {
                    ItemCode = i.ItemCode,
                    Ingredient = i.Ingredient,
                    Category = i.Category,
                    PackSize = i.PackSize,
                    QuantityOrdered = i.QuantityOrdered,
                    QuantityReceived = i.QuantityReceived,
                    UnitCost = i.UnitCost
                }).ToList()
            };
        }

        private static ProcurementPurchaseOrderDetail NormalizePurchaseOrder(ProcurementPurchaseOrderDetail source)
        {
            foreach (var item in source.Items)
            {
                if (item.QuantityReceived < 0)
                    item.QuantityReceived = 0;

                if (item.QuantityOrdered <= 0)
                    item.QuantityOrdered = Math.Max(item.QuantityReceived, 1);

                if (item.QuantityOrdered < item.QuantityReceived)
                    item.QuantityOrdered = item.QuantityReceived;
            }

            return source;
        }

        private static List<ProcurementPurchaseOrderDetail> BuildDemoData()
        {
            return new List<ProcurementPurchaseOrderDetail>
            {
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-003",
                    StatusLabel = "Partially Received",
                    CreatedOn = new DateTime(2026, 2, 28),
                    CreatedBy = "James Wilson",
                    SupplierName = "Beverage World",
                    SupplierReference = "BW-78901",
                    DeliveryDate = new DateTime(2026, 3, 3),
                    DeliveryLocation = "Main Street Kitchen - Dry Storage",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 2, 28, 14, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "COK-300", Ingredient = "Coca-Cola - 300ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 30, QuantityReceived = 28, UnitCost = 95.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "FAN-330", Ingredient = "Fanta Orange - 330ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 20, QuantityReceived = 20, UnitCost = 88.50m },
                        new ProcurementPurchaseOrderItem { ItemCode = "SPR-500", Ingredient = "Spring Water - 500ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 15, QuantityReceived = 15, UnitCost = 65.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "PEP-330", Ingredient = "Pepsi - 330ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 18, QuantityReceived = 18, UnitCost = 86.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "STY-300", Ingredient = "Sprite - 300ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 16, QuantityReceived = 16, UnitCost = 84.50m },
                        new ProcurementPurchaseOrderItem { ItemCode = "MIN-330", Ingredient = "Mineral Water - 330ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 22, QuantityReceived = 21, UnitCost = 59.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "LEM-1L", Ingredient = "Lemonade - 1L", Category = "Beverage", PackSize = "12 pack", QuantityOrdered = 12, QuantityReceived = 12, UnitCost = 62.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "TON-1L", Ingredient = "Tonic Water - 1L", Category = "Beverage", PackSize = "12 pack", QuantityOrdered = 10, QuantityReceived = 10, UnitCost = 68.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "GIN-200", Ingredient = "Ginger Ale - 200ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 14, QuantityReceived = 14, UnitCost = 72.50m },
                        new ProcurementPurchaseOrderItem { ItemCode = "SOD-1L", Ingredient = "Soda Water - 1L", Category = "Beverage", PackSize = "12 pack", QuantityOrdered = 9, QuantityReceived = 9, UnitCost = 57.50m },
                        new ProcurementPurchaseOrderItem { ItemCode = "ICE-2L", Ingredient = "Iced Tea - 2L", Category = "Beverage", PackSize = "6 pack", QuantityOrdered = 11, QuantityReceived = 11, UnitCost = 74.25m },
                        new ProcurementPurchaseOrderItem { ItemCode = "APP-1L", Ingredient = "Apple Juice - 1L", Category = "Beverage", PackSize = "12 pack", QuantityOrdered = 8, QuantityReceived = 8, UnitCost = 79.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "CRA-1L", Ingredient = "Cranberry Juice - 1L", Category = "Beverage", PackSize = "12 pack", QuantityOrdered = 7, QuantityReceived = 7, UnitCost = 83.40m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-002",
                    StatusLabel = "Sent",
                    CreatedOn = new DateTime(2026, 2, 27),
                    CreatedBy = "Sarah Chen",
                    SupplierName = "Metro Meat Suppliers",
                    SupplierReference = "MMS-24511",
                    DeliveryDate = new DateTime(2026, 3, 2),
                    DeliveryLocation = "Central Kitchen Receiving Bay",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 2, 27, 10, 30, 0),
                    Items = BuildLargeOrderItems()
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-004",
                    StatusLabel = "Open",
                    CreatedOn = new DateTime(2026, 3, 1),
                    CreatedBy = "Mike Torres",
                    SupplierName = "Fresh Produce Co",
                    SupplierReference = "FPC-66102",
                    DeliveryDate = new DateTime(2026, 3, 4),
                    DeliveryLocation = "Main Street Kitchen - Cold Room",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 1, 9, 10, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "TOM-005", Ingredient = "Tomatoes - Roma", Category = "Produce", PackSize = "5kg crate", QuantityOrdered = 20, QuantityReceived = 20, UnitCost = 42.00m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-005",
                    StatusLabel = "Open",
                    CreatedOn = new DateTime(2026, 3, 5),
                    CreatedBy = "James Wilson",
                    SupplierName = "Dairy Fresh Ltd",
                    SupplierReference = "DF-31255",
                    DeliveryDate = new DateTime(2026, 3, 5),
                    DeliveryLocation = "Main Street Kitchen - Cold Room",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 5, 11, 45, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "CHD-010", Ingredient = "Cheddar Cheese - Block", Category = "Dairy", PackSize = "2kg", QuantityOrdered = 10, QuantityReceived = 10, UnitCost = 128.30m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-006",
                    StatusLabel = "Partially Received",
                    CreatedOn = new DateTime(2026, 3, 6),
                    CreatedBy = "Sarah Chen",
                    SupplierName = "Beverage World",
                    SupplierReference = "BW-78962",
                    DeliveryDate = new DateTime(2026, 3, 6),
                    DeliveryLocation = "Main Street Kitchen - Beverage Storage",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 6, 9, 5, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "OJ-1L", Ingredient = "Orange Juice - 1L", Category = "Beverage", PackSize = "12 pack", QuantityOrdered = 24, QuantityReceived = 21, UnitCost = 54.90m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-007",
                    StatusLabel = "Open",
                    CreatedOn = new DateTime(2026, 3, 7),
                    CreatedBy = "Mike Torres",
                    SupplierName = "Metro Meat Suppliers",
                    SupplierReference = "MMS-24610",
                    DeliveryDate = new DateTime(2026, 3, 7),
                    DeliveryLocation = "Central Kitchen Receiving Bay",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 7, 8, 40, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "CHK-BR", Ingredient = "Chicken Breast - Skinless", Category = "Protein", PackSize = "5kg", QuantityOrdered = 15, QuantityReceived = 15, UnitCost = 164.00m },
                        new ProcurementPurchaseOrderItem { ItemCode = "CHK-TH", Ingredient = "Chicken Thighs", Category = "Protein", PackSize = "5kg", QuantityOrdered = 15, QuantityReceived = 15, UnitCost = 132.50m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-008",
                    StatusLabel = "Awaiting Delivery",
                    CreatedOn = new DateTime(2026, 3, 8),
                    CreatedBy = "James Wilson",
                    SupplierName = "Fresh Produce Co",
                    SupplierReference = "FPC-66208",
                    DeliveryDate = new DateTime(2026, 3, 5),
                    DeliveryLocation = "Main Street Kitchen - Prep Area",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 8, 10, 15, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "LET-IC", Ingredient = "Lettuce - Iceberg", Category = "Produce", PackSize = "10 head crate", QuantityOrdered = 30, QuantityReceived = 0, UnitCost = 24.75m }
                    }
                }
            };
        }

        private static List<ProcurementPurchaseOrderItem> BuildLargeOrderItems()
        {
            return new List<ProcurementPurchaseOrderItem>
            {
                new ProcurementPurchaseOrderItem { ItemCode = "BEE-001", Ingredient = "Beef Mince - Premium", Category = "Protein", PackSize = "5kg", QuantityOrdered = 25, QuantityReceived = 25, UnitCost = 189.00m },
                new ProcurementPurchaseOrderItem { ItemCode = "CHK-002", Ingredient = "Chicken Breast - Skinless", Category = "Protein", PackSize = "5kg", QuantityOrdered = 20, QuantityReceived = 20, UnitCost = 164.00m },
                new ProcurementPurchaseOrderItem { ItemCode = "LMB-003", Ingredient = "Lamb Cubes", Category = "Protein", PackSize = "5kg", QuantityOrdered = 10, QuantityReceived = 10, UnitCost = 215.00m },
                new ProcurementPurchaseOrderItem { ItemCode = "POR-004", Ingredient = "Pork Belly", Category = "Protein", PackSize = "5kg", QuantityOrdered = 8, QuantityReceived = 8, UnitCost = 171.50m },
                new ProcurementPurchaseOrderItem { ItemCode = "BAC-005", Ingredient = "Bacon Rashers", Category = "Protein", PackSize = "2kg", QuantityOrdered = 18, QuantityReceived = 18, UnitCost = 132.25m },
                new ProcurementPurchaseOrderItem { ItemCode = "SAU-006", Ingredient = "Beef Sausage", Category = "Protein", PackSize = "2kg", QuantityOrdered = 16, QuantityReceived = 16, UnitCost = 120.00m },
                new ProcurementPurchaseOrderItem { ItemCode = "RIB-007", Ingredient = "Pork Ribs", Category = "Protein", PackSize = "4kg", QuantityOrdered = 9, QuantityReceived = 9, UnitCost = 198.75m },
                new ProcurementPurchaseOrderItem { ItemCode = "STK-008", Ingredient = "Sirloin Steak", Category = "Protein", PackSize = "3kg", QuantityOrdered = 14, QuantityReceived = 14, UnitCost = 226.00m },
                new ProcurementPurchaseOrderItem { ItemCode = "MNC-009", Ingredient = "Chicken Mince", Category = "Protein", PackSize = "3kg", QuantityOrdered = 12, QuantityReceived = 12, UnitCost = 142.80m },
                new ProcurementPurchaseOrderItem { ItemCode = "MEA-010", Ingredient = "Mixed Meatballs", Category = "Protein", PackSize = "3kg", QuantityOrdered = 11, QuantityReceived = 11, UnitCost = 158.10m },
                new ProcurementPurchaseOrderItem { ItemCode = "LIV-011", Ingredient = "Chicken Livers", Category = "Protein", PackSize = "2kg", QuantityOrdered = 13, QuantityReceived = 13, UnitCost = 94.35m },
                new ProcurementPurchaseOrderItem { ItemCode = "HAM-012", Ingredient = "Smoked Ham", Category = "Protein", PackSize = "2kg", QuantityOrdered = 7, QuantityReceived = 7, UnitCost = 145.60m }
            };
        }
    }

    public static class ProcurementDiscrepancyStore
    {
        private static readonly List<DiscrepancyRecord> Records = BuildDemoData();
        private static int _nextSequence = 9;

        public static List<DiscrepancyRecord> GetAll()
        {
            return Records.OrderByDescending(r => r.ReportedOn).ToList();
        }

        public static List<DiscrepancyRecord> GetAllAlignedToPurchaseOrders()
        {
            var purchaseOrders = ProcurementPurchaseOrderStore.GetAll()
                .ToDictionary(po => po.PONumber, StringComparer.OrdinalIgnoreCase);

            return Records
                .Select(CloneRecord)
                .Select(record =>
                {
                    if (!purchaseOrders.TryGetValue(record.PONumber, out var po))
                        return record;

                    record.Ordered = po.Items.Sum(i => i.QuantityOrdered);
                    record.Received = po.Items.Sum(i => i.QuantityReceived);
                    return record;
                })
                .OrderByDescending(r => r.ReportedOn)
                .ToList();
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

        private static DiscrepancyRecord CloneRecord(DiscrepancyRecord source)
        {
            return new DiscrepancyRecord
            {
                PONumber = source.PONumber,
                Supplier = source.Supplier,
                Ingredient = source.Ingredient,
                Detail = source.Detail,
                Type = source.Type,
                Ordered = source.Ordered,
                Received = source.Received,
                Status = source.Status,
                ReportedBy = source.ReportedBy,
                ReportedOn = source.ReportedOn
            };
        }
    }

    // ===== Control =====

    public partial class ProcurementControl : UserControl
    {


        private readonly Action<DiscrepanciesNavigationContext>? _onViewDiscrepancies;
        private ProcurementDiscrepancies? _activeDiscrepanciesOverlay;
        private ProcurementPO? _activePurchaseOrderOverlay;

        public ProcurementControl(Action<DiscrepanciesNavigationContext>? onViewDiscrepancies = null)
        {
            InitializeComponent();
            _onViewDiscrepancies = onViewDiscrepancies;
            RecentPOList.ItemsSource = GetDemoRecentOrders();
            BindAttentionRequired();
        }

        public ProcurementControl() : this(null)
        {
        }

        public bool IsDiscrepanciesOverlayOpen()
        {
            return _activeDiscrepanciesOverlay != null;
        }

        public double GetActiveFooterStripHeight()
        {
            if (_activeDiscrepanciesOverlay == null)
                return 0.0;

            _activeDiscrepanciesOverlay.UpdateLayout();
            return _activeDiscrepanciesOverlay.GetFooterStripHeight();
        }

        private void NotifyDashboardFooterAlignmentChanged()
        {
            if (Window.GetWindow(this) is not DashboardWindow dashboard)
                return;

            dashboard.RefreshFooterAlignHeightForChildOverlay();
        }
        // ===== Event handlers =====

        private void CreateNewOrder_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to PO creation — wired up by parent
        }

        private void ViewAlertDetails_Click(object sender, RoutedEventArgs e)
        {
            OpenDiscrepanciesOverlay(new DiscrepanciesNavigationContext
            {
                Records = ProcurementDiscrepancyStore.GetAllAlignedToPurchaseOrders(),
                PurchaseOrders = ProcurementPurchaseOrderStore.GetAll(),
                InitialStatusFilter = "Open"
            });
        }

        // ===== Overlay open / close =====


        private void OpenDiscrepanciesOverlay(DiscrepanciesNavigationContext context)
        {
            if (context == null)
                return;

            if (_activeDiscrepanciesOverlay != null)
                return;

            _activeDiscrepanciesOverlay = new ProcurementDiscrepancies(context, OpenPurchaseOrderOverlay, CloseDiscrepanciesOverlay)
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch
            };

            if (DiscrepanciesOverlayHost != null)
            {
                DiscrepanciesOverlayHost.Children.Clear();
                DiscrepanciesOverlayHost.Children.Add(_activeDiscrepanciesOverlay);
                DiscrepanciesOverlayHost.Visibility = Visibility.Visible;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void CloseDiscrepanciesOverlay()
        {
            if (_activeDiscrepanciesOverlay == null)
                return;

            ClosePurchaseOrderOverlay();

            if (DiscrepanciesOverlayHost != null)
            {
                if (DiscrepanciesOverlayHost.Children.Contains(_activeDiscrepanciesOverlay))
                    DiscrepanciesOverlayHost.Children.Remove(_activeDiscrepanciesOverlay);

                DiscrepanciesOverlayHost.Children.Clear();
                DiscrepanciesOverlayHost.Visibility = Visibility.Collapsed;
            }

            _activeDiscrepanciesOverlay = null;

            BindAttentionRequired();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void OpenPurchaseOrderOverlay(string poNumber)
        {
            if (string.IsNullOrWhiteSpace(poNumber))
                return;

            if (_activePurchaseOrderOverlay != null)
                return;

            try
            {
                var purchaseOrder = ProcurementPurchaseOrderStore.GetByPONumber(poNumber)
                    ?? BuildFallbackPurchaseOrder(poNumber);
                if (purchaseOrder == null)
                    return;

                _activePurchaseOrderOverlay = new ProcurementPO(purchaseOrder, ClosePurchaseOrderOverlay)
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch
                };

                PurchaseOrderOverlayHost.Children.Clear();
                PurchaseOrderOverlayHost.Children.Add(_activePurchaseOrderOverlay);
                PurchaseOrderOverlayHost.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ClosePurchaseOrderOverlay();
                MessageBox.Show(
                    $"Unable to open purchase order details right now.\n\n{ex.Message}",
                    "Purchase Order",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ClosePurchaseOrderOverlay()
        {
            if (_activePurchaseOrderOverlay == null)
                return;

            if (PurchaseOrderOverlayHost.Children.Contains(_activePurchaseOrderOverlay))
                PurchaseOrderOverlayHost.Children.Remove(_activePurchaseOrderOverlay);

            PurchaseOrderOverlayHost.Children.Clear();
            PurchaseOrderOverlayHost.Visibility = Visibility.Collapsed;
            _activePurchaseOrderOverlay = null;
        }

        private static ProcurementPurchaseOrderDetail? BuildFallbackPurchaseOrder(string poNumber)
        {
            var source = ProcurementDiscrepancyStore.GetAll()
                .FirstOrDefault(r => string.Equals(r.PONumber, poNumber, StringComparison.OrdinalIgnoreCase));
            if (source == null)
                return null;

            return new ProcurementPurchaseOrderDetail
            {
                PONumber = source.PONumber,
                StatusLabel = source.Status,
                CreatedOn = source.ReportedOn,
                CreatedBy = source.ReportedBy,
                SupplierName = source.Supplier,
                SupplierReference = "N/A",
                DeliveryDate = source.ReportedOn,
                DeliveryLocation = "Receiving Area",
                ApprovedBy = source.ReportedBy,
                ApprovedOn = source.ReportedOn,
                Items = new List<ProcurementPurchaseOrderItem>
                {
                    new ProcurementPurchaseOrderItem
                    {
                        ItemCode = "N/A",
                        Ingredient = source.Ingredient,
                        Category = source.Type,
                        PackSize = "N/A",
                        QuantityOrdered = source.Ordered,
                        QuantityReceived = source.Received,
                        UnitCost = 0m
                    }
                }
            };
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