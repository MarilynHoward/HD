using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace RestaurantPosWpf
{
    // ===== Validation record (co-located per convention) =====

    public record ValidationResult(bool IsValid, string ErrorMessage = "");

    // ===== Allowed file types =====

    public static class AllowedDocumentTypes
    {
        public static readonly IReadOnlyList<string> Extensions = new[]
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png"
        };

        public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        public static string FileDialogFilter =>
            "Documents (*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.jpg;*.png)|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.jpg;*.png|All Files (*.*)|*.*";

        public static bool IsAllowed(string extension) =>
            Extensions.Contains(extension.ToLowerInvariant());
    }

    // ===== Control =====

    public partial class UploadDocumentDialog : UserControl
    {
        private readonly Action<DocumentModel> _onDocumentUploaded;
        private readonly Action _onCancel;
        private readonly List<string> _categories;

        private string? _selectedFilePath;
        private string? _selectedFileName;
        private long _selectedFileSize;

        public UploadDocumentDialog(
            IEnumerable<string> categories,
            Action<DocumentModel> onDocumentUploaded,
            Action onCancel)
        {
            _categories = (categories ?? throw new ArgumentNullException(nameof(categories))).ToList();
            _onDocumentUploaded = onDocumentUploaded ?? throw new ArgumentNullException(nameof(onDocumentUploaded));
            _onCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));

            InitializeComponent();

            CategoryComboBox.ItemsSource = _categories;
            if (_categories.Count > 0)
                CategoryComboBox.SelectedIndex = 0;
        }

        // ===== Validation =====

        public ValidationResult ValidateForm()
        {
            var name = DocumentNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(name))
                return new ValidationResult(false, "Document Name is required.");

            if (string.IsNullOrEmpty(_selectedFilePath))
                return new ValidationResult(false, "Please select a file to upload.");

            return new ValidationResult(true);
        }

        // ===== Event handlers =====

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Document",
                Filter = AllowedDocumentTypes.FileDialogFilter,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            var filePath = dialog.FileName;
            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension.ToLowerInvariant();

            // Validate file type
            if (!AllowedDocumentTypes.IsAllowed(extension))
            {
                FileValidationMessage.Text = $"Unsupported file type: {extension}";
                FileValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            // Validate file size
            if (fileInfo.Length > AllowedDocumentTypes.MaxFileSizeBytes)
            {
                FileValidationMessage.Text = "File exceeds the 10 MB size limit.";
                FileValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            // Store selected file info
            _selectedFilePath = filePath;
            _selectedFileName = fileInfo.Name;
            _selectedFileSize = fileInfo.Length;

            // Update browse button text to show selected file
            FileValidationMessage.Visibility = Visibility.Collapsed;
            UpdateBrowseButtonText(_selectedFileName);

            // Pre-populate document name from filename (without extension) if empty
            if (string.IsNullOrWhiteSpace(DocumentNameTextBox.Text))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(_selectedFileName);
                // Convert underscores/hyphens to spaces for readability
                nameWithoutExt = nameWithoutExt.Replace('_', ' ').Replace('-', ' ');
                DocumentNameTextBox.Text = nameWithoutExt;
            }
        }

        private void UpdateBrowseButtonText(string fileName)
        {
            // Access the Run element inside the BrowseButton template
            if (BrowseButton.Template.FindName("FileStatusText", BrowseButton) is System.Windows.Documents.Run fileStatusRun)
            {
                fileStatusRun.Text = fileName;
            }
        }

        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var result = ValidateForm();
            ValidationMessage.Text = result.ErrorMessage;
            ValidationMessage.Visibility = result.IsValid ? Visibility.Collapsed : Visibility.Visible;

            if (!result.IsValid)
                return;

            var fileExtension = Path.GetExtension(_selectedFileName ?? "").TrimStart('.').ToUpperInvariant();

            var document = new DocumentModel
            {
                Name = DocumentNameTextBox.Text.Trim(),
                Category = CategoryComboBox.SelectedItem as string ?? _categories[0],
                FileType = fileExtension,
                FileSize = _selectedFileSize,
                UploadDate = DateTime.Now,
                Notes = NotesTextBox.Text,
                FileName = _selectedFileName ?? ""
            };

            _onDocumentUploaded(document);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _onCancel();
        }
    }
}
