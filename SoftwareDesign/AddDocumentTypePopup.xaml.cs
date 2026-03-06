using Microsoft.Maui.Controls;
using SoftwareDesign.Models;
using SoftwareDesign.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SoftwareDesign
{
    public partial class AddDocumentTypePopup : ContentPage
    {
        private DatabaseService _databaseService;
        private List<AvailableField> _allFields;
        private List<string> _selectedFields = new List<string>();
        private DocumentType _editingType;
        private bool _isEditMode;
        private bool _fieldsLoaded = false;

        public AddDocumentTypePopup(DocumentType existingType = null)
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _editingType = existingType;
            _isEditMode = existingType != null;

            LoadAvailableFields();

            if (_isEditMode)
            {
                PopulateExistingData();
                PageTitle.Text = "Edit Document Type";
                SaveButton.Text = "Update";
            }
        }

        private async void LoadAvailableFields()
        {
            if (_fieldsLoaded) return;
            _fieldsLoaded = true;

            try
            {
                _allFields = await _databaseService.GetAllFieldsAsync();
                AvailableFieldsCollectionView.ItemsSource = _allFields;

                if (_isEditMode && !string.IsNullOrEmpty(_editingType.Required_Fields_Json))
                {
                    _selectedFields = JsonSerializer.Deserialize<List<string>>(_editingType.Required_Fields_Json);
                    UpdateSelectedFieldsDisplay();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load fields: {ex.Message}", "OK");
            }
        }

        private void PopulateExistingData()
        {
            TypeNameEntry.Text = _editingType.Type_Name;
            DescriptionEntry.Text = _editingType.Description;

            if (!string.IsNullOrEmpty(_editingType.Required_Fields_Json))
            {
                _selectedFields = JsonSerializer.Deserialize<List<string>>(_editingType.Required_Fields_Json);
            }
        }

        private void OnFieldSelectionChanged(object sender, CheckedChangedEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            var field = (AvailableField)checkBox.BindingContext;

            if (e.Value)
            {
                if (!_selectedFields.Contains(field.Field_Name))
                    _selectedFields.Add(field.Field_Name);
            }
            else
            {
                _selectedFields.Remove(field.Field_Name);
            }

            UpdateSelectedFieldsDisplay();
            ValidateForm();
        }

        private void UpdateSelectedFieldsDisplay()
        {
            SelectedFieldsLabel.Text = _selectedFields.Count > 0
                ? string.Join(", ", _selectedFields)
                : "No fields selected";
        }

        private void ValidateForm()
        {
            bool isValid = !string.IsNullOrWhiteSpace(TypeNameEntry.Text)
                          && _selectedFields.Count > 0;

            SaveButton.IsEnabled = isValid;
        }

        private void OnTypeNameChanged(object sender, TextChangedEventArgs e)
        {
            ValidateForm();
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TypeNameEntry.Text))
            {
                await DisplayAlert("Validation Error", "Please enter a document type name.", "OK");
                return;
            }

            if (_selectedFields.Count == 0)
            {
                await DisplayAlert("Validation Error", "Please select at least one field.", "OK");
                return;
            }

            try
            {
                var documentType = _isEditMode ? _editingType : new DocumentType();

                documentType.Type_Name = TypeNameEntry.Text.Trim();
                documentType.Description = DescriptionEntry.Text?.Trim();
                documentType.Required_Fields_Json = JsonSerializer.Serialize(_selectedFields);
                documentType.Is_System_Type = false;

                if (_isEditMode)
                {
                    await _databaseService.UpdateDocumentTypeAsync(documentType);
                    await DisplayAlert("Success", "Document type updated successfully!", "OK");
                }
                else
                {
                    var existing = await _databaseService.GetDocumentTypeByNameAsync(documentType.Type_Name);
                    if (existing != null)
                    {
                        await DisplayAlert("Error", "A document type with this name already exists.", "OK");
                        return;
                    }

                    await _databaseService.AddDocumentTypeAsync(documentType);
                    await DisplayAlert("Success", "Document type created successfully!", "OK");
                }

                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
            }
        }

        // 🆕 YENİ EKLENEN: Manage Document Types
        private async void OnManageDocumentTypesClicked(object sender, EventArgs e)
        {
            var manageDocTypesPage = new ManageDocumentTypesPage();
            manageDocTypesPage.Disappearing += (s, args) =>
            {
                // Sayfa kapandığında bir şey yapmanıza gerek yok
                // Çünkü bu popup'tan sadece silme/düzenleme yapıyorsunuz
            };
            await Navigation.PushModalAsync(manageDocTypesPage);
        }

        // İsmi değişti: Manage Fields -> Manage Attributes
        private async void OnManageFieldsClicked(object sender, EventArgs e)
        {
            var manageFieldsPage = new ManageFieldsPage();
            manageFieldsPage.Disappearing += (s, args) =>
            {
                _fieldsLoaded = false;
                LoadAvailableFields();
            };
            await Navigation.PushModalAsync(manageFieldsPage);
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}