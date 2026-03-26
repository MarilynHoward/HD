using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf
{
    // ===== Models, constants, and records (co-located per convention) =====

    public class DocumentModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.Now;
        public string Notes { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public static class DocumentCategories
    {
        public const string Compliance = "Compliance";
        public const string Legal = "Legal";
        public const string Banking = "Banking";

        public static readonly IReadOnlyList<string> All = new[] { Compliance, Legal, Banking };
    }

    public record SummaryCardData(string Title, int Count, string ColorKey);

    // ===== Control =====

    public partial class DocumentRepositoryControl : UserControl
    {
        private readonly Action<Action<DocumentModel>> _onRequestUploadDialog;
        private readonly List<DocumentModel> _documents;

        public DocumentRepositoryControl(
            Action<Action<DocumentModel>> onRequestUploadDialog)
        {
            _onRequestUploadDialog = onRequestUploadDialog ?? throw new ArgumentNullException(nameof(onRequestUploadDialog));

            InitializeComponent();

            _documents = GetDemoDocuments();
            DocumentList.ItemsSource = _documents;
            RefreshSummaries();
        }

        // ===== Summary computation =====

        public List<SummaryCardData> ComputeSummaries(List<DocumentModel> documents) => new()
        {
            new("Total Documents", documents.Count, "TotalColor"),
            new("Compliance", documents.Count(d => d.Category == DocumentCategories.Compliance), "ComplianceColor"),
            new("Legal", documents.Count(d => d.Category == DocumentCategories.Legal), "LegalColor"),
            new("Banking", documents.Count(d => d.Category == DocumentCategories.Banking), "BankingColor"),
        };

        private void RefreshSummaries()
        {
            var summaries = ComputeSummaries(_documents);
            TotalCount.Text = summaries[0].Count.ToString();
            ComplianceCount.Text = summaries[1].Count.ToString();
            LegalCount.Text = summaries[2].Count.ToString();
            BankingCount.Text = summaries[3].Count.ToString();

            EmptyState.Visibility = _documents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DocumentList.Visibility = _documents.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ===== Event handlers =====

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            _onRequestUploadDialog(OnDocumentUploaded);
        }

        private void OnDocumentUploaded(DocumentModel newDocument)
        {
            _documents.Add(newDocument);
            DocumentList.Items.Refresh();
            RefreshSummaries();
        }

        // ===== Demo data =====

        public static List<DocumentModel> GetDemoDocuments() => new()
        {
            new DocumentModel
            {
                Name = "Annual Compliance Report 2024",
                Category = DocumentCategories.Compliance,
                FileType = "PDF",
                FileSize = 2_450_000,
                UploadDate = new DateTime(2024, 3, 15),
                Notes = "Reviewed and approved by compliance team.",
                FileName = "annual_compliance_2024.pdf"
            },
            new DocumentModel
            {
                Name = "Health & Safety Audit",
                Category = DocumentCategories.Compliance,
                FileType = "DOCX",
                FileSize = 1_200_000,
                UploadDate = new DateTime(2024, 5, 10),
                Notes = "Quarterly audit results.",
                FileName = "health_safety_audit_q2.docx"
            },
            new DocumentModel
            {
                Name = "Vendor Agreement - Fresh Foods Inc",
                Category = DocumentCategories.Legal,
                FileType = "PDF",
                FileSize = 890_000,
                UploadDate = new DateTime(2024, 2, 20),
                Notes = "Signed contract for 2024 supply.",
                FileName = "vendor_agreement_freshfoods.pdf"
            },
            new DocumentModel
            {
                Name = "Employment Contract Template",
                Category = DocumentCategories.Legal,
                FileType = "DOCX",
                FileSize = 340_000,
                UploadDate = new DateTime(2024, 1, 8),
                Notes = "Updated template with new clauses.",
                FileName = "employment_contract_template.docx"
            },
            new DocumentModel
            {
                Name = "Q1 Financial Statement",
                Category = DocumentCategories.Banking,
                FileType = "XLSX",
                FileSize = 1_780_000,
                UploadDate = new DateTime(2024, 4, 5),
                Notes = "Includes all revenue and expense details.",
                FileName = "q1_financial_statement.xlsx"
            },
            new DocumentModel
            {
                Name = "Loan Application Documents",
                Category = DocumentCategories.Banking,
                FileType = "PDF",
                FileSize = 3_100_000,
                UploadDate = new DateTime(2024, 6, 1),
                Notes = "Equipment financing application.",
                FileName = "loan_application_docs.pdf"
            },
        };
    }
}
