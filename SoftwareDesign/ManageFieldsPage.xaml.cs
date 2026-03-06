using Microsoft.Maui.Controls;
using SoftwareDesign.Models;
using SoftwareDesign.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SoftwareDesign
{
    public partial class ManageFieldsPage : ContentPage
    {
        private DatabaseService _databaseService;
        private List<AvailableField> _fields;

        public ManageFieldsPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            LoadFields();
        }

        private async void LoadFields()
        {
            try
            {
                _fields = await _databaseService.GetAllFieldsAsync();
                FieldsCollectionView.ItemsSource = _fields;
                TotalFieldsLabel.Text = _fields.Count.ToString();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load fields: {ex.Message}", "OK");
            }
        }

        private async void OnAddNewFieldClicked(object sender, EventArgs e)
        {
            string fieldName = await DisplayPromptAsync("New Field", "Enter field name (e.g., Student_ID):");
            if (string.IsNullOrWhiteSpace(fieldName))
                return;

            string displayName = await DisplayPromptAsync("Display Name", "Enter display name (e.g., Student ID):");
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = fieldName;

            string fieldType = await DisplayActionSheet("Select field type", "Cancel", null, "Text", "Picker", "Date");
            if (fieldType == "Cancel" || string.IsNullOrEmpty(fieldType))
                return;

            try
            {
                var newField = new AvailableField
                {
                    Field_Name = fieldName.Trim().Replace(" ", "_"),
                    Display_Name = displayName.Trim(),
                    Field_Type = fieldType,
                    Is_System_Field = false,
                    Picker_Options_Json = fieldType == "Picker" ? JsonSerializer.Serialize(new List<string>()) : null
                };

                await _databaseService.AddFieldAsync(newField);
                await DisplayAlert("Success", "Field added successfully!", "OK");
                LoadFields();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to add field: {ex.Message}", "OK");
            }
        }

        // 🆕 ENHANCED: Edit Field with more options
        private async void OnEditFieldClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var field = (AvailableField)button.BindingContext;

            string action = await DisplayActionSheet(
                $"Edit '{field.Display_Name}'",
                "Cancel",
                null,
                "✏️ Rename Field",
                "🔄 Change Field Type",
                "📝 Edit Display Name",
                field.Field_Type == "Picker" ? "⚙️ Edit Options" : null
            );

            switch (action)
            {
                case "✏️ Rename Field":
                    await RenameField(field);
                    break;
                case "🔄 Change Field Type":
                    await ChangeFieldType(field);
                    break;
                case "📝 Edit Display Name":
                    await EditDisplayName(field);
                    break;
                case "⚙️ Edit Options":
                    await EditPickerOptions(field);
                    break;
            }
        }

        // 🆕 NEW: Rename Field
        private async Task RenameField(AvailableField field)
        {
            if (field.Is_System_Field)
            {
                await DisplayAlert("Warning", "System fields cannot be renamed.", "OK");
                return;
            }

            string newName = await DisplayPromptAsync(
                "Rename Field",
                "Enter new field name (e.g., Student_ID):",
                initialValue: field.Field_Name
            );

            if (string.IsNullOrWhiteSpace(newName) || newName == field.Field_Name)
                return;

            newName = newName.Trim().Replace(" ", "_");

            // Check if name already exists
            var existing = await _databaseService.GetFieldByNameAsync(newName);
            if (existing != null)
            {
                await DisplayAlert("Error", "A field with this name already exists.", "OK");
                return;
            }

            try
            {
                field.Field_Name = newName;
                await _databaseService.UpdateFieldAsync(field);
                await DisplayAlert("✅ Success", $"Field renamed to '{newName}'", "OK");
                LoadFields();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to rename: {ex.Message}", "OK");
            }
        }

        // 🆕 NEW: Change Field Type
        private async Task ChangeFieldType(AvailableField field)
        {
            if (field.Is_System_Field)
            {
                await DisplayAlert("Warning", "System field types cannot be changed.", "OK");
                return;
            }

            string newType = await DisplayActionSheet(
                "Select new field type",
                "Cancel",
                null,
                "Text",
                "Picker",
                "Date"
            );

            if (newType == "Cancel" || string.IsNullOrEmpty(newType) || newType == field.Field_Type)
                return;

            bool confirm = await DisplayAlert(
                "⚠️ Confirm Change",
                $"Change field type from '{field.Field_Type}' to '{newType}'?\n\n" +
                "This may affect existing documents.",
                "Change",
                "Cancel"
            );

            if (!confirm) return;

            try
            {
                field.Field_Type = newType;

                // Initialize picker options if changing to Picker
                if (newType == "Picker" && string.IsNullOrEmpty(field.Picker_Options_Json))
                {
                    field.Picker_Options_Json = JsonSerializer.Serialize(new List<string>());
                }

                await _databaseService.UpdateFieldAsync(field);
                await DisplayAlert("✅ Success", "Field type changed successfully!", "OK");
                LoadFields();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to change type: {ex.Message}", "OK");
            }
        }

        // 🆕 NEW: Edit Display Name
        private async Task EditDisplayName(AvailableField field)
        {
            string newDisplayName = await DisplayPromptAsync(
                "Edit Display Name",
                "Enter new display name:",
                initialValue: field.Display_Name
            );

            if (string.IsNullOrWhiteSpace(newDisplayName) || newDisplayName == field.Display_Name)
                return;

            try
            {
                field.Display_Name = newDisplayName.Trim();
                await _databaseService.UpdateFieldAsync(field);
                await DisplayAlert("✅ Success", "Display name updated!", "OK");
                LoadFields();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update: {ex.Message}", "OK");
            }
        }

        private async void OnEditOptionsClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var field = (AvailableField)button.BindingContext;

            if (field.Field_Type != "Picker")
            {
                await DisplayAlert("Info", "Only Picker fields have options to edit.", "OK");
                return;
            }

            await EditPickerOptions(field);
        }

        // Edit Picker Options
        private async Task EditPickerOptions(AvailableField field)
        {
            var popup = new EditPickerOptionsPopup(field);
            popup.Disappearing += (s, args) => LoadFields();
            await Navigation.PushModalAsync(popup);
        }

        private async void OnDeleteFieldClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var field = (AvailableField)button.BindingContext;

            if (field.Is_System_Field)
            {
                await DisplayAlert("Warning", "System fields cannot be deleted.", "OK");
                return;
            }

            bool confirm = await DisplayAlert("Confirm Delete",
                $"Are you sure you want to delete the field '{field.Display_Name}'?",
                "Delete", "Cancel");

            if (confirm)
            {
                try
                {
                    await _databaseService.DeleteFieldAsync(field);
                    await DisplayAlert("Success", "Field deleted successfully.", "OK");
                    LoadFields();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to delete: {ex.Message}", "OK");
                }
            }
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}