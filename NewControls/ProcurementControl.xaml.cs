using System.Globalization;
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
            "Awaiting Approval" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")),
            "Awaiting Delivery" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")),
            "Fully Received" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")),
            "Discrepancy" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6"))
        };

        public Brush StatusBadgeText => Status switch
        {
            "Draft" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
            "Sent" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF")),
            "Pending Invoice" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")),
            "Partial Shipment" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9D174D")),
            "Awaiting Approval" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")),
            "Awaiting Delivery" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF")),
            "Fully Received" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534")),
            "Discrepancy" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
        };

        public static RecentPurchaseOrder FromPurchaseOrderDetail(ProcurementPurchaseOrderDetail po)
        {
            var displayStatus = ProcurementPurchaseOrderDisplay.MapToRecentTableStatus(po.StatusLabel);
            return new RecentPurchaseOrder
            {
                PONumber = po.PONumber,
                Status = displayStatus,
                SupplierName = po.SupplierName,
                OrderDate = po.CreatedOn,
                ItemCount = po.Items.Count,
                Amount = po.Total,
                InvoiceDate = null
            };
        }
    }

    /// <summary>
    /// Row model for the full Purchase Orders list (View All) grid.
    /// </summary>
    public sealed class PurchaseOrderListRow
    {
        public string PONumber { get; init; } = string.Empty;
        public bool ShowOverdueBadge { get; init; }
        public string SupplierName { get; init; } = string.Empty;
        public string BranchName { get; init; } = string.Empty;
        public string OrderDateDisplay { get; init; } = string.Empty;
        public string ExpectedDeliveryDisplay { get; init; } = string.Empty;
        public string DaysWaitingText { get; init; } = string.Empty;
        public Brush DaysWaitingForeground { get; init; } = Brushes.Black;
        public int ItemsCount { get; init; }
        public string StatusDisplay { get; init; } = string.Empty;
        public Brush StatusBadgeBackground { get; init; } = Brushes.LightGray;
        public Brush StatusBadgeForeground { get; init; } = Brushes.Black;
        public string TotalDisplay { get; init; } = string.Empty;

        public Visibility OverdueBadgeVisibility => ShowOverdueBadge ? Visibility.Visible : Visibility.Collapsed;
    }

    public static class ProcurementPurchaseOrderDisplay
    {
        public static string MapToRecentTableStatus(string statusLabel)
        {
            return statusLabel switch
            {
                "Open" => "Draft",
                "Partially Received" => "Partial Shipment",
                _ => statusLabel
            };
        }

        public static string MapStatusLabelToGridDisplay(string statusLabel)
        {
            return statusLabel switch
            {
                "Open" => "Awaiting Approval",
                "Draft" => "Draft",
                "Awaiting Approval" => "Awaiting Approval",
                "Sent" => "Sent",
                "Awaiting Delivery" => "Awaiting Delivery",
                "Confirmed" => "Confirmed",
                "Partially Received" => "Partially Received",
                "Partial Shipment" => "Partial Shipment",
                "Fully Received" => "Fully Received",
                "Delivered" => "Delivered",
                "Pending Invoice" => "Pending Invoice",
                "Invoice Issues" => "Invoice Issues",
                "Payments Pending" => "Payments Pending",
                "Discrepancy" => "Discrepancy",
                _ => statusLabel
            };
        }

        public static bool IsPurchaseOrderOverdue(ProcurementPurchaseOrderDetail po)
        {
            var notFullyReceived = po.Items.Any(i => i.QuantityReceived < i.QuantityOrdered);
            return notFullyReceived && po.DeliveryDate.Date < DateTime.Today;
        }

        public static PurchaseOrderListRow ToListRow(ProcurementPurchaseOrderDetail po)
        {
            var daysWaiting = Math.Max(0, (DateTime.Today - po.CreatedOn.Date).Days);
            Brush daysBrush = daysWaiting > 14
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")!)
                : daysWaiting > 7
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706")!)
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")!);

            var overdue = IsPurchaseOrderOverdue(po);

            var statusDisplay = MapStatusLabelToGridDisplay(po.StatusLabel);
            var (bg, fg) = GetStatusBadgeBrushes(statusDisplay);

            return new PurchaseOrderListRow
            {
                PONumber = po.PONumber,
                ShowOverdueBadge = overdue,
                SupplierName = po.SupplierName,
                BranchName = po.DeliveryLocation,
                OrderDateDisplay = po.CreatedOn.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                ExpectedDeliveryDisplay = po.DeliveryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                DaysWaitingText = $"{daysWaiting} days",
                DaysWaitingForeground = daysBrush,
                ItemsCount = po.Items.Count,
                StatusDisplay = statusDisplay,
                StatusBadgeBackground = bg,
                StatusBadgeForeground = fg,
                TotalDisplay = po.TotalDisplay
            };
        }

        private static (Brush bg, Brush fg) GetStatusBadgeBrushes(string display)
        {
            return display switch
            {
                "Awaiting Approval" or "Draft" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")!)),
                "Sent" or "Confirmed" or "Awaiting Delivery" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DBEAFE")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF")!)),
                "Partially Received" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEDD5")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2410C")!)),
                "Fully Received" or "Delivered" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534")!)),
                "Pending Invoice" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#92400E")!)),
                "Invoice Issues" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCE7F3")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9D174D")!)),
                "Payments Pending" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E8FF")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B21A8")!)),
                "Discrepancy" => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B")!)),
                _ => (
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")!),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")!))
            };
        }

        public static OrdersInProgressSummary SummarizeOrdersInProgress(IReadOnlyList<ProcurementPurchaseOrderDetail> orders)
        {
            decimal a = 0, d = 0, p = 0, del = 0, inv = 0, pay = 0;
            int ca = 0, cd = 0, cp = 0, cdel = 0, cinv = 0, cpay = 0;

            foreach (var po in orders)
            {
                var bucket = GetInProgressBucket(po.StatusLabel);
                if (bucket == null)
                    continue;

                switch (bucket.Value)
                {
                    case OrdersInProgressBucket.AwaitingApproval:
                        ca++;
                        a += po.Total;
                        break;
                    case OrdersInProgressBucket.AwaitingDelivery:
                        cd++;
                        d += po.Total;
                        break;
                    case OrdersInProgressBucket.PartiallyDelivered:
                        cp++;
                        p += po.Total;
                        break;
                    case OrdersInProgressBucket.Delivered:
                        cdel++;
                        del += po.Total;
                        break;
                    case OrdersInProgressBucket.InvoiceIssues:
                        cinv++;
                        inv += po.Total;
                        break;
                    case OrdersInProgressBucket.PaymentsPending:
                        cpay++;
                        pay += po.Total;
                        break;
                }
            }

            return new OrdersInProgressSummary(
                a, ca,
                d, cd,
                p, cp,
                del, cdel,
                inv, cinv,
                pay, cpay);
        }

        private static OrdersInProgressBucket? GetInProgressBucket(string? statusLabel)
        {
            var s = statusLabel?.Trim() ?? string.Empty;
            return s switch
            {
                "Draft" or "Open" or "Awaiting Approval" => OrdersInProgressBucket.AwaitingApproval,
                "Sent" or "Awaiting Delivery" or "Confirmed" or "Discrepancy" => OrdersInProgressBucket.AwaitingDelivery,
                "Partially Received" or "Partial Shipment" => OrdersInProgressBucket.PartiallyDelivered,
                "Fully Received" or "Delivered" => OrdersInProgressBucket.Delivered,
                "Pending Invoice" or "Invoice Issues" => OrdersInProgressBucket.InvoiceIssues,
                "Payments Pending" => OrdersInProgressBucket.PaymentsPending,
                _ => null
            };
        }

        private enum OrdersInProgressBucket
        {
            AwaitingApproval,
            AwaitingDelivery,
            PartiallyDelivered,
            Delivered,
            InvoiceIssues,
            PaymentsPending
        }
    }

    public sealed record OrdersInProgressSummary(
        decimal AwaitingApprovalValue,
        int AwaitingApprovalCount,
        decimal AwaitingDeliveryValue,
        int AwaitingDeliveryCount,
        decimal PartialValue,
        int PartialCount,
        decimal DeliveredValue,
        int DeliveredCount,
        decimal InvoiceIssuesValue,
        int InvoiceIssuesCount,
        decimal PaymentsPendingValue,
        int PaymentsPendingCount);

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

        public string DisputeReason { get; set; } = string.Empty;
        public string DisputeContactPerson { get; set; } = string.Empty;
        public string DisputeContactEmail { get; set; } = string.Empty;
        public string ResolveCreditNoteNumber { get; set; } = string.Empty;
        public string ResolveDetails { get; set; } = string.Empty;

        public string DisputeSubtitleLine => $"Provide details to dispute {PONumber}";
        public string ResolveSubtitleLine => $"Provide resolution details for {PONumber}";

        public int Variance => Received - Ordered;
        public string VarianceForegroundHex => Variance < 0 ? "#DC2626" : "#111827";
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

        /// <summary>
        /// True when this PO is flagged for discrepancy workflows: explicit <c>Discrepancy</c> status
        /// or an active (Open/Disputed) discrepancy record exists for this PO number.
        /// </summary>
        public bool HasDiscrepancy { get; set; }
    }

    public static class ProcurementPurchaseOrderStore
    {
        private static readonly List<ProcurementPurchaseOrderDetail> PurchaseOrders = BuildDemoData();

        public static List<ProcurementPurchaseOrderDetail> GetAll()
        {
            var list = PurchaseOrders
                .Select(Clone)
                .Select(NormalizePurchaseOrder)
                .ToList();
            ApplyPurchaseOrderMetadata(list);
            return list;
        }

        public static ProcurementPurchaseOrderDetail? GetByPONumber(string poNumber)
        {
            if (string.IsNullOrWhiteSpace(poNumber))
                return null;

            var record = PurchaseOrders.FirstOrDefault(p =>
                string.Equals(p.PONumber, poNumber, StringComparison.OrdinalIgnoreCase));
            if (record == null)
                return null;

            var po = NormalizePurchaseOrder(Clone(record));
            ApplyPurchaseOrderMetadata(new List<ProcurementPurchaseOrderDetail> { po });
            return po;
        }

        private static void ApplyPurchaseOrderMetadata(IReadOnlyList<ProcurementPurchaseOrderDetail> list)
        {
            var activeDiscrepancyPoNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in ProcurementDiscrepancyStore.GetAll())
            {
                if (r.Status is "Open" or "Disputed")
                    activeDiscrepancyPoNumbers.Add(r.PONumber);
            }

            foreach (var po in list)
            {
                po.HasDiscrepancy = activeDiscrepancyPoNumbers.Contains(po.PONumber)
                    || string.Equals(po.StatusLabel, "Discrepancy", StringComparison.OrdinalIgnoreCase);
            }
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
                HasDiscrepancy = source.HasDiscrepancy,
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
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-001",
                    StatusLabel = "Discrepancy",
                    CreatedOn = new DateTime(2026, 2, 20),
                    CreatedBy = "James Wilson",
                    SupplierName = "FreshPro Distributors",
                    SupplierReference = "FPD-10001",
                    DeliveryDate = new DateTime(2026, 2, 25),
                    DeliveryLocation = "Main Street Kitchen - Dry Storage",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 2, 20, 8, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "RICE-5", Ingredient = "Basmati Rice - 5kg", Category = "Dry Goods", PackSize = "5kg", QuantityOrdered = 40, QuantityReceived = 40, UnitCost = 89.50m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-009",
                    StatusLabel = "Awaiting Approval",
                    CreatedOn = new DateTime(2026, 3, 9),
                    CreatedBy = "Sarah Chen",
                    SupplierName = "Coastal Seafood Ltd",
                    SupplierReference = "CS-44021",
                    DeliveryDate = new DateTime(2026, 3, 12),
                    DeliveryLocation = "Riverside Bistro - Cold Room",
                    ApprovedBy = string.Empty,
                    ApprovedOn = default,
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "FSH-001", Ingredient = "Salmon Fillet", Category = "Protein", PackSize = "3kg", QuantityOrdered = 12, QuantityReceived = 0, UnitCost = 420.00m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-010",
                    StatusLabel = "Pending Invoice",
                    CreatedOn = new DateTime(2026, 3, 4),
                    CreatedBy = "Mike Torres",
                    SupplierName = "Beverage World",
                    SupplierReference = "BW-77990",
                    DeliveryDate = new DateTime(2026, 3, 5),
                    DeliveryLocation = "Main Street Kitchen - Beverage Storage",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 4, 11, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "WAT-500", Ingredient = "Spring Water - 500ml", Category = "Beverage", PackSize = "24 pack", QuantityOrdered = 50, QuantityReceived = 50, UnitCost = 65.00m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-011",
                    StatusLabel = "Invoice Issues",
                    CreatedOn = new DateTime(2026, 3, 2),
                    CreatedBy = "James Wilson",
                    SupplierName = "Metro Meat Suppliers",
                    SupplierReference = "MMS-24490",
                    DeliveryDate = new DateTime(2026, 3, 3),
                    DeliveryLocation = "Central Kitchen Receiving Bay",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 2, 9, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "POR-004", Ingredient = "Pork Belly", Category = "Protein", PackSize = "5kg", QuantityOrdered = 8, QuantityReceived = 8, UnitCost = 171.50m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-012",
                    StatusLabel = "Payments Pending",
                    CreatedOn = new DateTime(2026, 2, 25),
                    CreatedBy = "Sarah Chen",
                    SupplierName = "Fresh Produce Co",
                    SupplierReference = "FPC-66001",
                    DeliveryDate = new DateTime(2026, 2, 27),
                    DeliveryLocation = "Main Street Kitchen - Prep Area",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 2, 25, 14, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "ONI-01", Ingredient = "Brown Onions - 10kg", Category = "Produce", PackSize = "bag", QuantityOrdered = 20, QuantityReceived = 20, UnitCost = 38.20m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-013",
                    StatusLabel = "Fully Received",
                    CreatedOn = new DateTime(2026, 2, 18),
                    CreatedBy = "Mike Torres",
                    SupplierName = "Dairy Fresh Ltd",
                    SupplierReference = "DF-30990",
                    DeliveryDate = new DateTime(2026, 2, 20),
                    DeliveryLocation = "Main Street Kitchen - Cold Room",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 2, 18, 10, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "MLK-2L", Ingredient = "Full Cream Milk - 2L", Category = "Dairy", PackSize = "6 pack", QuantityOrdered = 60, QuantityReceived = 60, UnitCost = 72.40m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-014",
                    StatusLabel = "Confirmed",
                    CreatedOn = new DateTime(2026, 3, 10),
                    CreatedBy = "James Wilson",
                    SupplierName = "Beverage World",
                    SupplierReference = "BW-78001",
                    DeliveryDate = new DateTime(2026, 3, 14),
                    DeliveryLocation = "Riverside Bistro - Dry Storage",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 10, 8, 30, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "TEA-1L", Ingredient = "Iced Tea - 1L", Category = "Beverage", PackSize = "12 pack", QuantityOrdered = 24, QuantityReceived = 0, UnitCost = 74.25m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-015",
                    StatusLabel = "Draft",
                    CreatedOn = new DateTime(2026, 3, 11),
                    CreatedBy = "Sarah Chen",
                    SupplierName = "FreshPro Distributors",
                    SupplierReference = "FPD-10120",
                    DeliveryDate = new DateTime(2026, 3, 18),
                    DeliveryLocation = "Main Street Kitchen - Dry Storage",
                    ApprovedBy = string.Empty,
                    ApprovedOn = default,
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "PST-500", Ingredient = "Pasta - Penne 500g", Category = "Dry Goods", PackSize = "case", QuantityOrdered = 48, QuantityReceived = 0, UnitCost = 32.10m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-016",
                    StatusLabel = "Delivered",
                    CreatedOn = new DateTime(2026, 2, 10),
                    CreatedBy = "Mike Torres",
                    SupplierName = "Metro Meat Suppliers",
                    SupplierReference = "MMS-24300",
                    DeliveryDate = new DateTime(2026, 2, 12),
                    DeliveryLocation = "Central Kitchen Receiving Bay",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 2, 10, 9, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "BEEF-5", Ingredient = "Beef Mince - Premium", Category = "Protein", PackSize = "5kg", QuantityOrdered = 20, QuantityReceived = 20, UnitCost = 189.00m }
                    }
                },
                new ProcurementPurchaseOrderDetail
                {
                    PONumber = "PO-2026-017",
                    StatusLabel = "Partially Received",
                    CreatedOn = new DateTime(2026, 3, 3),
                    CreatedBy = "James Wilson",
                    SupplierName = "Coastal Seafood Ltd",
                    SupplierReference = "CS-44100",
                    DeliveryDate = new DateTime(2026, 3, 6),
                    DeliveryLocation = "Riverside Bistro - Cold Room",
                    ApprovedBy = "Michael Roberts",
                    ApprovedOn = new DateTime(2026, 3, 3, 11, 0, 0),
                    Items = new List<ProcurementPurchaseOrderItem>
                    {
                        new ProcurementPurchaseOrderItem { ItemCode = "PRW-01", Ingredient = "Prawns - 2kg", Category = "Seafood", PackSize = "2kg", QuantityOrdered = 15, QuantityReceived = 10, UnitCost = 265.00m }
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
                    ReportedOn = new DateTime(2026, 3, 2),
                    DisputeReason = "Price was raised by R5 per kg without written notice or an updated quote; we are disputing the variance against the agreed PO pricing.",
                    DisputeContactPerson = "Sarah Chen",
                    DisputeContactEmail = "sarah.chen@example.com"
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
                ReportedOn = source.ReportedOn,
                DisputeReason = source.DisputeReason,
                DisputeContactPerson = source.DisputeContactPerson,
                DisputeContactEmail = source.DisputeContactEmail,
                ResolveCreditNoteNumber = source.ResolveCreditNoteNumber,
                ResolveDetails = source.ResolveDetails
            };
        }

        public static bool TryApplyDispute(
            string poNumber,
            string ingredient,
            string reason,
            string contactPerson,
            string contactEmail)
        {
            if (string.IsNullOrWhiteSpace(poNumber) || string.IsNullOrWhiteSpace(ingredient))
                return false;

            var r = Records.FirstOrDefault(x =>
                string.Equals(x.PONumber, poNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Ingredient, ingredient, StringComparison.Ordinal));

            if (r == null)
                return false;

            r.Status = "Disputed";
            r.DisputeReason = reason;
            r.DisputeContactPerson = contactPerson;
            r.DisputeContactEmail = contactEmail;
            return true;
        }

        public static bool TryApplyResolve(
            string poNumber,
            string ingredient,
            string creditNoteNumber,
            string resolutionDetails)
        {
            if (string.IsNullOrWhiteSpace(poNumber) || string.IsNullOrWhiteSpace(ingredient))
                return false;

            var r = Records.FirstOrDefault(x =>
                string.Equals(x.PONumber, poNumber, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Ingredient, ingredient, StringComparison.Ordinal));

            if (r == null)
                return false;

            r.Status = "Resolved";
            r.ResolveCreditNoteNumber = creditNoteNumber;
            r.ResolveDetails = resolutionDetails;
            return true;
        }
    }

    // ===== Control =====

    public partial class ProcurementControl : UserControl
    {


        private readonly Action<DiscrepanciesNavigationContext>? _onViewDiscrepancies;
        private ProcurementDiscrepancies? _activeDiscrepanciesOverlay;
        private ProcurementPOrders? _activePurchaseOrdersListOverlay;
        private ProcurementPO? _activePurchaseOrderOverlay;
        private ProcurementDispute? _activeDisputeOverlay;
        private ProcurementResolve? _activeResolveOverlay;

        public ProcurementControl(Action<DiscrepanciesNavigationContext>? onViewDiscrepancies = null)
        {
            InitializeComponent();
            _onViewDiscrepancies = onViewDiscrepancies;
            BindRecentPurchaseOrders();
            BindOrdersInProgress();
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
            if (_activeDiscrepanciesOverlay != null)
            {
                _activeDiscrepanciesOverlay.UpdateLayout();
                return _activeDiscrepanciesOverlay.GetFooterStripHeight();
            }

            if (_activePurchaseOrdersListOverlay != null)
            {
                _activePurchaseOrdersListOverlay.UpdateLayout();
                return _activePurchaseOrdersListOverlay.GetFooterStripHeight();
            }

            return 0.0;
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
            OpenDiscrepanciesOverlay(BuildDiscrepanciesNavigationContextForDashboard());
        }

        private void ViewAllPurchaseOrders_Click(object sender, RoutedEventArgs e)
        {
            OpenPurchaseOrdersListOverlay();
        }

        private static DiscrepanciesNavigationContext BuildDiscrepanciesNavigationContextForDashboard()
        {
            var aligned = ProcurementDiscrepancyStore.GetAllAlignedToPurchaseOrders();
            var filteredRecords = aligned
                .Where(r => ProcurementPurchaseOrderStore.GetByPONumber(r.PONumber)?.HasDiscrepancy == true)
                .ToList();
            var purchaseOrders = ProcurementPurchaseOrderStore.GetAll()
                .Where(p => p.HasDiscrepancy)
                .ToList();

            return new DiscrepanciesNavigationContext
            {
                Records = filteredRecords,
                PurchaseOrders = purchaseOrders,
                InitialStatusFilter = "Open"
            };
        }

        // ===== Overlay open / close =====


        private void OpenDiscrepanciesOverlay(DiscrepanciesNavigationContext context)
        {
            if (context == null)
                return;

            if (_activeDiscrepanciesOverlay != null)
                return;

            ClosePurchaseOrdersListOverlay();

            _activeDiscrepanciesOverlay = new ProcurementDiscrepancies(
                context,
                OpenPurchaseOrderOverlay,
                OpenDisputeOverlay,
                CloseDiscrepanciesOverlay)
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

        private void OpenPurchaseOrdersListOverlay()
        {
            if (_activePurchaseOrdersListOverlay != null)
                return;

            CloseDiscrepanciesOverlay();

            _activePurchaseOrdersListOverlay = new ProcurementPOrders(
                OpenPurchaseOrderOverlay,
                ClosePurchaseOrdersListOverlay)
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch
            };

            if (PurchaseOrdersListOverlayHost != null)
            {
                PurchaseOrdersListOverlayHost.Children.Clear();
                PurchaseOrdersListOverlayHost.Children.Add(_activePurchaseOrdersListOverlay);
                PurchaseOrdersListOverlayHost.Visibility = Visibility.Visible;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void ClosePurchaseOrdersListOverlay()
        {
            if (_activePurchaseOrdersListOverlay == null)
                return;

            ClosePurchaseOrderOverlay();

            if (PurchaseOrdersListOverlayHost != null)
            {
                if (PurchaseOrdersListOverlayHost.Children.Contains(_activePurchaseOrdersListOverlay))
                    PurchaseOrdersListOverlayHost.Children.Remove(_activePurchaseOrdersListOverlay);

                PurchaseOrdersListOverlayHost.Children.Clear();
                PurchaseOrdersListOverlayHost.Visibility = Visibility.Collapsed;
            }

            _activePurchaseOrdersListOverlay = null;

            BindRecentPurchaseOrders();
            BindOrdersInProgress();
            BindAttentionRequired();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void CloseDiscrepanciesOverlay()
        {
            if (_activeDiscrepanciesOverlay == null)
                return;

            CloseDisputeOverlay();
            CloseResolveOverlay();
            ClosePurchaseOrderOverlay();

            if (DiscrepanciesOverlayHost != null)
            {
                if (DiscrepanciesOverlayHost.Children.Contains(_activeDiscrepanciesOverlay))
                    DiscrepanciesOverlayHost.Children.Remove(_activeDiscrepanciesOverlay);

                DiscrepanciesOverlayHost.Children.Clear();
                DiscrepanciesOverlayHost.Visibility = Visibility.Collapsed;
            }

            _activeDiscrepanciesOverlay = null;

            BindRecentPurchaseOrders();
            BindOrdersInProgress();
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

        private void OpenDisputeOverlay(DiscrepancyRecord record)
        {
            if (record is null)
                return;

            if (_activeDisputeOverlay != null || _activeResolveOverlay != null)
                return;

            try
            {
                _activeDisputeOverlay = new ProcurementDispute(record, CloseDisputeOverlay, ConfirmDispute)
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch
                };

                if (DisputeOverlayHost != null)
                {
                    DisputeOverlayHost.Children.Clear();
                    DisputeOverlayHost.Children.Add(_activeDisputeOverlay);
                    DisputeOverlayHost.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                CloseDisputeOverlay();
                MessageBox.Show(
                    $"Unable to open dispute form right now.\n\n{ex.Message}",
                    "Dispute Discrepancy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void CloseDisputeOverlay()
        {
            if (_activeDisputeOverlay == null)
                return;

            if (DisputeOverlayHost != null)
            {
                if (DisputeOverlayHost.Children.Contains(_activeDisputeOverlay))
                    DisputeOverlayHost.Children.Remove(_activeDisputeOverlay);

                DisputeOverlayHost.Children.Clear();
                DisputeOverlayHost.Visibility = Visibility.Collapsed;
            }

            _activeDisputeOverlay = null;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void OpenResolveOverlay(DiscrepancyRecord record)
        {
            if (record is null)
                return;

            if (_activeResolveOverlay != null || _activeDisputeOverlay != null)
                return;

            try
            {
                var resolveSubtitleText = "Provide resolution details for " + (record.PONumber ?? string.Empty);
                _activeResolveOverlay = new ProcurementResolve(record, CloseResolveOverlay, ConfirmResolve, resolveSubtitleText)
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch
                };

                if (DisputeOverlayHost != null)
                {
                    DisputeOverlayHost.Children.Clear();
                    DisputeOverlayHost.Children.Add(_activeResolveOverlay);
                    DisputeOverlayHost.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                CloseResolveOverlay();
                MessageBox.Show(
                    $"Unable to open resolve form right now.\n\n{ex.Message}",
                    "Resolve Discrepancy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void CloseResolveOverlay()
        {
            if (_activeResolveOverlay == null)
                return;

            if (DisputeOverlayHost != null)
            {
                if (DisputeOverlayHost.Children.Contains(_activeResolveOverlay))
                    DisputeOverlayHost.Children.Remove(_activeResolveOverlay);

                DisputeOverlayHost.Children.Clear();
                DisputeOverlayHost.Visibility = Visibility.Collapsed;
            }

            _activeResolveOverlay = null;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void ConfirmDispute(DiscrepancyRecord record, string reason, string contactPerson, string contactEmail)
        {
            if (!ProcurementDiscrepancyStore.TryApplyDispute(
                    record.PONumber,
                    record.Ingredient,
                    reason,
                    contactPerson,
                    contactEmail))
            {
                MessageBox.Show(
                    "Could not save this dispute. The discrepancy may have been removed.",
                    "Dispute Discrepancy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            record.Status = "Disputed";
            record.DisputeReason = reason;
            record.DisputeContactPerson = contactPerson;
            record.DisputeContactEmail = contactEmail;

            _activeDiscrepanciesOverlay?.RefreshView();
            CloseDisputeOverlay();
            BindRecentPurchaseOrders();
            BindOrdersInProgress();
            BindAttentionRequired();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void ConfirmResolve(DiscrepancyRecord record, string creditNoteNumber, string resolutionDetails)
        {
            if (!ProcurementDiscrepancyStore.TryApplyResolve(
                    record.PONumber,
                    record.Ingredient,
                    creditNoteNumber,
                    resolutionDetails))
            {
                MessageBox.Show(
                    "Could not save this resolution. The discrepancy may have been removed.",
                    "Resolve Discrepancy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            record.Status = "Resolved";
            record.ResolveCreditNoteNumber = creditNoteNumber;
            record.ResolveDetails = resolutionDetails;

            _activeDiscrepanciesOverlay?.RefreshView();
            CloseResolveOverlay();
            BindRecentPurchaseOrders();
            BindOrdersInProgress();
            BindAttentionRequired();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                NotifyDashboardFooterAlignmentChanged();
            }), System.Windows.Threading.DispatcherPriority.Render);
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

        private void BindRecentPurchaseOrders()
        {
            var recent = ProcurementPurchaseOrderStore.GetAll()
                .OrderByDescending(p => p.CreatedOn)
                .Take(5)
                .Select(RecentPurchaseOrder.FromPurchaseOrderDetail)
                .ToList();

            RecentPOList.ItemsSource = recent;
        }

        private void BindOrdersInProgress()
        {
            var all = ProcurementPurchaseOrderStore.GetAll();
            var s = ProcurementPurchaseOrderDisplay.SummarizeOrdersInProgress(all);

            ApprovalValue.Text = $"R{s.AwaitingApprovalValue:#,##0.00}";
            ApprovalCount.Text = s.AwaitingApprovalCount.ToString(CultureInfo.InvariantCulture);

            DeliveryValue.Text = $"R{s.AwaitingDeliveryValue:#,##0.00}";
            DeliveryCount.Text = s.AwaitingDeliveryCount.ToString(CultureInfo.InvariantCulture);

            PartialValue.Text = $"R{s.PartialValue:#,##0.00}";
            PartialCount.Text = s.PartialCount.ToString(CultureInfo.InvariantCulture);

            DeliveredValue.Text = $"R{s.DeliveredValue:#,##0.00}";
            DeliveredCount.Text = s.DeliveredCount.ToString(CultureInfo.InvariantCulture);

            InvoiceIssuesValue.Text = $"R{s.InvoiceIssuesValue:#,##0.00}";
            InvoiceIssuesCount.Text = s.InvoiceIssuesCount.ToString(CultureInfo.InvariantCulture);

            PaymentsPendingValue.Text = $"R{s.PaymentsPendingValue:#,##0.00}";
            PaymentsPendingCount.Text = s.PaymentsPendingCount.ToString(CultureInfo.InvariantCulture);
        }

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
