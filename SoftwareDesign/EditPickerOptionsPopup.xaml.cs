using Microsoft.Maui.Controls;
using SoftwareDesign.Models;
using SoftwareDesign.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SoftwareDesign
{
    public partial class EditPickerOptionsPopup : ContentPage
    {
        private DatabaseService _databaseService;
        private AvailableField _field;
        private List<string> _options;

        public EditPickerOptionsPopup(AvailableField field)
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _field = field;

            FieldNameLabel.Text = $"Editing: {field.Display_Name}";
            LoadOptions();
        }

        private void LoadOptions()
        {
            _options = new List<string>();

            if (!string.IsNullOrEmpty(_field.Picker_Options_Json))
            {
                try
                {
                    _options = JsonSerializer.Deserialize<List<string>>(_field.Picker_Options_Json);
                }
                catch { }
            }

            RefreshOptionsList();
        }

        private void RefreshOptionsList()
        {
            OptionsCollectionView.ItemsSource = null;
            OptionsCollectionView.ItemsSource = _options.Select((opt, index) => new
            {
                Index = index,
                Value = opt
            }).ToList();

            OptionsCountLabel.Text = _options.Count.ToString();
        }

        private async void OnAddOptionClicked(object sender, EventArgs e)
        {
            string newOption = await DisplayPromptAsync("Add Option", "Enter new option value:");

            if (string.IsNullOrWhiteSpace(newOption))
                return;

            if (_options.Contains(newOption.Trim()))
            {
                await DisplayAlert("Duplicate", "This option already exists.", "OK");
                return;
            }

            _options.Add(newOption.Trim());
            RefreshOptionsList();
        }

        private async void OnDeleteOptionClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var item = button.BindingContext;
            var indexProperty = item.GetType().GetProperty("Index");
            var valueProperty = item.GetType().GetProperty("Value");

            if (indexProperty != null && valueProperty != null)
            {
                int index = (int)indexProperty.GetValue(item);
                string value = (string)valueProperty.GetValue(item);

                bool confirm = await DisplayAlert("Confirm Delete",
                    $"Delete option: {value}?",
                    "Delete", "Cancel");

                if (confirm)
                {
                    _options.RemoveAt(index);
                    RefreshOptionsList();
                }
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                _field.Picker_Options_Json = JsonSerializer.Serialize(_options);
                await _databaseService.UpdateFieldAsync(_field);

                await DisplayAlert("Success", "Options saved successfully!", "OK");
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
            }
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}