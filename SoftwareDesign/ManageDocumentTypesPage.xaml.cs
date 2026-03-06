using Microsoft.Maui.Controls;
using SoftwareDesign.Models;
using SoftwareDesign.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SoftwareDesign
{
    public partial class ManageDocumentTypesPage : ContentPage
    {
        private DatabaseService _databaseService;
        private List<DocumentType> _documentTypes;

        public ManageDocumentTypesPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            LoadDocumentTypes();
        }

        private async void LoadDocumentTypes()
        {
            try
            {
                _documentTypes = await _databaseService.GetAllDocumentTypesAsync();
                DocumentTypesCollectionView.ItemsSource = _documentTypes;
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load document types: {ex.Message}", "OK");
            }
        }

        private void UpdateStatistics()
        {
            TotalTypesLabel.Text = _documentTypes.Count.ToString();
            SystemTypesLabel.Text = _documentTypes.Count(t => t.Is_System_Type).ToString();
            CustomTypesLabel.Text = _documentTypes.Count(t => !t.Is_System_Type).ToString();
        }

        private async void OnAddNewTypeClicked(object sender, EventArgs e)
        {
            var popup = new AddDocumentTypePopup();
            popup.Disappearing += (s, args) => LoadDocumentTypes();
            await Navigation.PushModalAsync(popup);
        }

        // 🆕 ENHANCED: Edit Type with Rename + Field Management
        private async void OnEditTypeClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var documentType = (DocumentType)button.BindingContext;

            string action = await DisplayActionSheet(
                $"Edit '{documentType.Type_Name}'",
                "Cancel",
                null,
                "✏️ Rename Type",
                "📋 Edit Required Fields",
                "📝 Edit Description"
            );

            switch (action)
            {
                case "✏️ Rename Type":
                    await RenameDocumentType(documentType);
                    break;
                case "📋 Edit Required Fields":
                    await EditRequiredFields(documentType);
                    break;
                case "📝 Edit Description":
                    await EditDescription(documentType);
                    break;
            }
        }

        // 🆕 NEW: Rename Document Type
        private async Task RenameDocumentType(DocumentType documentType)
        {
            if (documentType.Is_System_Type)
            {
                await DisplayAlert("Warning", "System types cannot be renamed.", "OK");
                return;
            }

            string newName = await DisplayPromptAsync(
                "Rename Document Type",
                "Enter new name:",
                initialValue: documentType.Type_Name,
                placeholder: "Type Name"
            );

            if (string.IsNullOrWhiteSpace(newName) || newName == documentType.Type_Name)
                return;

            // Check if name already exists
            var existing = await _databaseService.GetDocumentTypeByNameAsync(newName);
            if (existing != null)
            {
                await DisplayAlert("Error", "A type with this name already exists.", "OK");
                return;
            }

            try
            {
                // Update all related documents
                var relatedDocs = await _databaseService.SearchDocumentsAsync(documentType.Type_Name);
                foreach (var doc in relatedDocs)
                {
                    doc.Document_Type = newName;
                    await _databaseService.UpdateDocumentAsync(doc);
                }

                // Update type
                documentType.Type_Name = newName;
                await _databaseService.UpdateDocumentTypeAsync(documentType);

                await DisplayAlert("✅ Success", $"Type renamed to '{newName}'", "OK");
                LoadDocumentTypes();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to rename: {ex.Message}", "OK");
            }
        }

        // 🆕 NEW: Edit Required Fields
        private async Task EditRequiredFields(DocumentType documentType)
        {
            var allFields = await _databaseService.GetAllFieldsAsync();
            var currentFields = JsonSerializer.Deserialize<List<string>>(documentType.Required_Fields_Json ?? "[]");

            var fieldChoices = allFields.Select(f => f.Field_Name).ToList();
            var selectedFields = new List<string>(currentFields);

            // Show multi-select popup
            var popup = new MultiSelectFieldsPopup(fieldChoices, selectedFields);
            popup.OnFieldsSelected = async (fields) =>
            {
                documentType.Required_Fields_Json = JsonSerializer.Serialize(fields);
                await _databaseService.UpdateDocumentTypeAsync(documentType);
                await DisplayAlert("✅ Success", "Required fields updated!", "OK");
                LoadDocumentTypes();
            };

            await Navigation.PushModalAsync(popup);
        }

        // 🆕 NEW: Edit Description
        private async Task EditDescription(DocumentType documentType)
        {
            string newDesc = await DisplayPromptAsync(
                "Edit Description",
                "Enter description:",
                initialValue: documentType.Description,
                placeholder: "Description",
                maxLength: 200
            );

            if (newDesc == null) return;

            try
            {
                documentType.Description = newDesc;
                await _databaseService.UpdateDocumentTypeAsync(documentType);
                await DisplayAlert("✅ Success", "Description updated!", "OK");
                LoadDocumentTypes();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update: {ex.Message}", "OK");
            }
        }

        private async void OnDeleteTypeClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var documentType = (DocumentType)button.BindingContext;

            if (documentType.Is_System_Type)
            {
                await DisplayAlert("Warning", "System document types cannot be deleted.", "OK");
                return;
            }

            bool confirm = await DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete '{documentType.Type_Name}'?\n\nAll documents of this type will also be deleted!",
                "Delete", "Cancel");

            if (confirm)
            {
                try
                {
                    await _databaseService.DeleteDocumentTypeAsync(documentType);
                    await DisplayAlert("Success", "Document type deleted successfully.", "OK");
                    LoadDocumentTypes();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to delete: {ex.Message}", "OK");
                }
            }
        }

        private async void OnViewDocumentsClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var documentType = (DocumentType)button.BindingContext;

            var criteria = new Dictionary<string, string>
            {
                { "File_Type", documentType.Type_Name }
            };

            var results = await _databaseService.SearchDocumentsAsync(documentType.Type_Name);
            var resultsPopup = new SearchResultsPopup(results, criteria);
            await Navigation.PushModalAsync(resultsPopup);
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}