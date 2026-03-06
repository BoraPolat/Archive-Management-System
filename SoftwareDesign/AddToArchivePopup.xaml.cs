using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using SoftwareDesign.Models;
using SoftwareDesign.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SoftwareDesign
{
    public partial class AddToArchivePopup : ContentPage
    {
        private DatabaseService _databaseService;
        private Dictionary<string, Picker> dynamicPickers = new Dictionary<string, Picker>();
        private Dictionary<string, DatePicker> dynamicDatePickers = new Dictionary<string, DatePicker>();
        private List<DocumentType> _documentTypes;
        private DocumentType _selectedDocumentType;
        private bool _typesLoaded = false;

        public AddToArchivePopup()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            LoadDocumentTypes();
        }

        private async void LoadDocumentTypes()
        {
            if (_typesLoaded) return;
            _typesLoaded = true;

            try
            {
                _documentTypes = await _databaseService.GetAllDocumentTypesAsync();
                FileTypePickerPopup.ItemsSource = _documentTypes.Select(t => t.Type_Name).ToList();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load document types: {ex.Message}", "OK");
            }
        }

        private async void OnCreateNewTypeClicked(object sender, EventArgs e)
        {
            var popup = new AddDocumentTypePopup();
            popup.Disappearing += async (s, args) =>
            {
                await Task.Delay(300);
                _typesLoaded = false;
                LoadDocumentTypes();
            };
            await Navigation.PushModalAsync(popup);
        }

        private async void OnFileTypeSelectedPopup(object sender, EventArgs e)
        {
            var picker = (Picker)sender;
            var selectedFileType = picker.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(selectedFileType))
            {
                DynamicGrid.IsVisible = false;
                SaveButton.IsEnabled = false;
                return;
            }

            _selectedDocumentType = _documentTypes.FirstOrDefault(t => t.Type_Name == selectedFileType);
            if (_selectedDocumentType != null)
            {
                await CreateDynamicFields(_selectedDocumentType);
                DynamicGrid.IsVisible = true;
            }
        }

        private async Task CreateDynamicFields(DocumentType documentType)
        {
            DynamicGrid.Children.Clear();
            DynamicGrid.RowDefinitions.Clear();
            DynamicGrid.ColumnDefinitions.Clear();
            dynamicPickers.Clear();
            dynamicDatePickers.Clear();

            if (string.IsNullOrEmpty(documentType.Required_Fields_Json)) return;

            var fieldNames = JsonSerializer.Deserialize<List<string>>(documentType.Required_Fields_Json);
            var fields = new List<AvailableField>();

            foreach (var fieldName in fieldNames)
            {
                var field = await _databaseService.GetFieldByNameAsync(fieldName);
                if (field != null) fields.Add(field);
            }

            // ✅ Tarih alanlarını en sona taşı (MainPage mantığı)
            var dateFields = fields.Where(f => f.Field_Type == "Date" || f.Field_Name == "Archive_Date").ToList();
            var nonDateFields = fields.Where(f => f.Field_Type != "Date" && f.Field_Name != "Archive_Date").ToList();
            fields = nonDateFields.Concat(dateFields).ToList();

            int columnCount = fields.Count;
            DynamicGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DynamicGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Sütunları eşit dağıt
            for (int i = 0; i < columnCount; i++)
            {
                DynamicGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            }

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];

                // Bu alanın tarih olup olmadığını baştan belirliyoruz
                bool isDateField = field.Field_Type == "Date" || field.Field_Name == "Archive_Date";

                // Label
                var label = new Label
                {
                    Text = $"{field.Display_Name} *",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    // ✅ GÜNCELLENDİ: Tarih ise etiketi ortala, değilse sola yasla
                    HorizontalOptions = isDateField ? LayoutOptions.Center : LayoutOptions.Start
                };
                Grid.SetRow(label, 0);
                Grid.SetColumn(label, i);
                DynamicGrid.Children.Add(label);

                View inputControl;

                if (isDateField)
                {
                    // ✅ DatePicker (Önceki ayarlar korundu + Ortalamalar)
                    var border = new Border
                    {
                        Stroke = Color.FromArgb("#E5E7EB"),
                        StrokeThickness = 1,
                        BackgroundColor = Colors.White,
                        StrokeShape = new RoundRectangle { CornerRadius = 4 },
                        Padding = new Thickness(10, 4),
                        HorizontalOptions = LayoutOptions.Center, // Kutuyu ortalar
                        VerticalOptions = LayoutOptions.Center,
                        MinimumWidthRequest = 130
                    };

                    var datePicker = new DatePicker
                    {
                        Format = "yyyy-MM-dd",
                        FontSize = 12,
                        BackgroundColor = Colors.Transparent,
                        TextColor = Color.FromArgb("#374151"),
                        HorizontalOptions = LayoutOptions.Center, // Tarih yazısını kutu içinde ortalar
                        VerticalOptions = LayoutOptions.Center
                    };

                    border.Content = datePicker;
                    dynamicDatePickers[field.Field_Name] = datePicker;
                    inputControl = border;
                }
                else
                {
                    // Normal Picker
                    var picker = new Picker
                    {
                        Title = "Select...",
                        FontSize = 12,
                        BackgroundColor = Colors.White,
                        TextColor = Colors.Black
                    };
                    picker.SelectedIndexChanged += OnAnyPickerChanged;
                    await LoadPickerData(picker, field);

                    dynamicPickers[field.Field_Name] = picker;
                    inputControl = picker;
                }

                Grid.SetRow((BindableObject)inputControl, 1);
                Grid.SetColumn((BindableObject)inputControl, i);
                DynamicGrid.Children.Add(inputControl);
            }

            CheckAllFieldsFilled();
        }

        private async Task LoadPickerData(Picker picker, AvailableField field)
        {
            if (field.Field_Type == "Picker" && !string.IsNullOrEmpty(field.Picker_Options_Json))
            {
                try
                {
                    var options = JsonSerializer.Deserialize<List<string>>(field.Picker_Options_Json);
                    picker.ItemsSource = options;
                }
                catch { }
            }
        }

        private void OnAnyPickerChanged(object sender, EventArgs e)
        {
            CheckAllFieldsFilled();
        }

        private void CheckAllFieldsFilled()
        {
            bool allFilled = true;

            if (FileTypePickerPopup.SelectedItem == null) allFilled = false;

            foreach (var kvp in dynamicPickers)
            {
                if (kvp.Value.SelectedItem == null) allFilled = false;
            }

            SaveButton.IsEnabled = allFilled;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (!ValidateAllFields())
            {
                await DisplayAlert("Error", "Please fill all required fields", "OK");
                return;
            }

            var data = CollectFormData();

            try
            {
                var document = new ArchiveRecord
                {
                    Document_Type = _selectedDocumentType.Type_Name,
                    Fields_Json = JsonSerializer.Serialize(data)
                };

                if (data.ContainsKey("Course_ID"))
                    document.Course_ID = data["Course_ID"];
                if (data.ContainsKey("Teacher_Name"))
                    document.Teacher_Name = data["Teacher_Name"];
                if (data.ContainsKey("Year"))
                    document.Year = data["Year"];
                if (data.ContainsKey("Semester"))
                    document.Semester = data["Semester"];
                if (data.ContainsKey("Archive_Date"))
                    document.Archive_Date = data["Archive_Date"];
                if (data.ContainsKey("Location"))
                    document.Location = data["Location"];

                await _databaseService.AddDocumentAsync(document);

                string message = $"Document Type: {_selectedDocumentType.Type_Name}\n\n";
                foreach (var kvp in data)
                {
                    message += $"{kvp.Key}: {kvp.Value}\n";
                }

                await DisplayAlert("✅ Success",
                    $"Document added to archive successfully!\n\n{message}",
                    "OK");

                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private bool ValidateAllFields()
        {
            if (FileTypePickerPopup.SelectedItem == null) return false;

            foreach (var kvp in dynamicPickers)
            {
                if (kvp.Value.SelectedItem == null) return false;
            }

            foreach (var kvp in dynamicDatePickers)
            {
                if (kvp.Value == null) return false;
            }

            return true;
        }

        private Dictionary<string, string> CollectFormData()
        {
            var data = new Dictionary<string, string>();

            foreach (var kvp in dynamicPickers)
            {
                data[kvp.Key] = kvp.Value.SelectedItem?.ToString();
            }

            foreach (var kvp in dynamicDatePickers)
            {
                data[kvp.Key] = kvp.Value.Date.ToString("yyyy-MM-dd");
            }

            return data;
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Cancel",
                "Are you sure you want to cancel?\nAll entered data will be lost.",
                "Yes", "No");

            if (confirm)
                await Navigation.PopModalAsync();
        }
    }
}