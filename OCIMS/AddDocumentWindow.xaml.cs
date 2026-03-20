using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using OCIMS.Data;
using OCIMS.Models;

namespace OCIMS
{
    public partial class AddDocumentWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        private DocumentRepository _repo = new DocumentRepository();
        private List<DocumentType> _docTypes;
        private string _selectedFilePath = "";

        public AddDocumentWindow(List<DocumentType> docTypes)
        {
            InitializeComponent();
            _docTypes = docTypes;

            // Populate document type combo
            CmbDocType.Items.Clear();
            foreach (var t in _docTypes)
                CmbDocType.Items.Add(new ComboBoxItem
                {
                    Content = t.TypeName,
                    Tag = t.DocTypeId
                });
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select File",
                Filter = "All Files (*.*)|*.*|PDF (*.pdf)|*.pdf|Word (*.docx)|*.docx|Images (*.png;*.jpg)|*.png;*.jpg"
            };
            if (dlg.ShowDialog() == true)
            {
                _selectedFilePath = dlg.FileName;
                TxtFilePath.Text = dlg.FileName;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            { ShowError("Please enter the document title."); return; }

            if (CmbDocType.SelectedItem == null)
            { ShowError("Please select a document type."); return; }

            if (string.IsNullOrWhiteSpace(TxtClientId.Text))
            { ShowError("Please enter the client ID."); return; }

            int docTypeId = (int)(CmbDocType.SelectedItem as ComboBoxItem).Tag;

            string fileName = "";
            string fileSize = "";
            if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
            {
                var info = new FileInfo(_selectedFilePath);
                fileName = info.Name;
                double kb = info.Length / 1024.0;
                fileSize = kb >= 1024
                    ? (kb / 1024).ToString("0.#") + " MB"
                    : kb.ToString("0.#") + " KB";
            }

            var doc = new Document
            {
                ClientId = TxtClientId.Text.Trim(),
                DocTypeId = docTypeId,
                DocTitle = TxtTitle.Text.Trim(),
                FileName = fileName,
                FilePath = _selectedFilePath,
                FileSize = fileSize,
                Remarks = TxtRemarks.Text.Trim()
            };

            if (_repo.Save(doc))
            {
                IsSaved = true;
                MessageBox.Show("✔ Document '" + doc.DocTitle + "' saved successfully!",
                    "OCIMS — Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowError(string msg)
        {
            ErrorMsg.Text = "⚠ " + msg;
            ErrorMsg.Visibility = Visibility.Visible;
        }
    }
}
