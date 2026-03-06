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
    public partial class MainPage : ContentPage
    {
        private DatabaseService _databaseService;
        private Dictionary<string, Picker> dynamicPickers = new Dictionary<string, Picker>();
        private Dictionary<string, CheckBox> dateCheckBoxes = new Dictionary<string, CheckBox>();
        private Dictionary<string, DatePicker> dynamicDatePickers = new Dictionary<string, DatePicker>();
        private List<DocumentType> _documentTypes;
        private string _currentSelectedType;
        private bool _isInitialized = false;

        public MainPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (!_isInitialized)
            {
                _isInitialized = true;
                LoadDocumentTypes();
            }
            else
            {
                RefreshData();
            }
        }

        private async void RefreshData()
        {
            try
            {
                _documentTypes = await _databaseService.GetAllDocumentTypesAsync();
                FileTypePicker.ItemsSource = _documentTypes.Select(t => t.Type_Name).ToList();

                if (!string.IsNullOrEmpty(_currentSelectedType))
                {
                    var documentType = _documentTypes.FirstOrDefault(t => t.Type_Name == _currentSelectedType);
                    if (documentType != null)
                    {
                        await CreateDynamicFields(documentType);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to refresh data: {ex.Message}", "OK");
            }
        }

        private async void LoadDocumentTypes()
        {
            try
            {
                _documentTypes = await _databaseService.GetAllDocumentTypesAsync();
                FileTypePicker.ItemsSource = _documentTypes.Select(t => t.Type_Name).ToList();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to load document types: {ex.Message}", "OK");
            }
        }

        private async void OnFileTypeSelected(object sender, EventArgs e)
        {
            var picker = (Picker)sender;
            var selectedFileType = picker.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(selectedFileType))
            {
                DynamicGrid.IsVisible = false;
                SearchButton.IsEnabled = false;
                _currentSelectedType = null;
                return;
            }

            _currentSelectedType = selectedFileType;

            var documentType = _documentTypes.FirstOrDefault(t => t.Type_Name == selectedFileType);
            if (documentType != null)
            {
                await CreateDynamicFields(documentType);
                DynamicGrid.IsVisible = true;
                SearchButton.IsEnabled = true;
            }
        }

        private async Task CreateDynamicFields(DocumentType documentType)
        {
            DynamicGrid.Children.Clear();
            DynamicGrid.RowDefinitions.Clear();
            DynamicGrid.ColumnDefinitions.Clear();
            dynamicPickers.Clear();
            dynamicDatePickers.Clear();
            dateCheckBoxes.Clear();

            if (string.IsNullOrEmpty(documentType.Required_Fields_Json)) return;

            var fieldNames = JsonSerializer.Deserialize<List<string>>(documentType.Required_Fields_Json);
            var fields = new List<AvailableField>();

            foreach (var fieldName in fieldNames)
            {
                var field = await _databaseService.GetFieldByNameAsync(fieldName);
                if (field != null) fields.Add(field);
            }

            var dateFields = fields.Where(f => f.Field_Type == "Date" || f.Field_Name == "Archive_Date").ToList();
            var nonDateFields = fields.Where(f => f.Field_Type != "Date" && f.Field_Name != "Archive_Date").ToList();
            fields = nonDateFields.Concat(dateFields).ToList();

            int columnCount = fields.Count;

            DynamicGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DynamicGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < columnCount; i++)
            {
                DynamicGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            }

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                bool isDateField = field.Field_Type == "Date" || field.Field_Name == "Archive_Date";
                CheckBox currentCheckBox = null;

                // ============================================================
                // 1. SATIR (HEADER): Label ve CheckBox
                // ============================================================

                if (isDateField)
                {
                    var headerStack = new HorizontalStackLayout
                    {
                        Spacing = 0,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.End
                    };

                    currentCheckBox = new CheckBox
                    {
                        IsChecked = false,
                        Color = Colors.White,
                        VerticalOptions = LayoutOptions.Center,
                        Scale = 0.8,
                        Margin = new Thickness(-20, 0, 0, 0)
                    };

                    var label = new Label
                    {
                        Text = field.Display_Name,
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(-6, 0, 0, 0)
                    };

                    headerStack.Children.Add(currentCheckBox);
                    headerStack.Children.Add(label);

                    Grid.SetRow(headerStack, 0);
                    Grid.SetColumn(headerStack, i);
                    DynamicGrid.Children.Add(headerStack);

                    dateCheckBoxes[field.Field_Name] = currentCheckBox;
                }
                else
                {
                    var label = new Label
                    {
                        Text = field.Display_Name,
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.End
                    };
                    Grid.SetRow(label, 0);
                    Grid.SetColumn(label, i);
                    DynamicGrid.Children.Add(label);
                }

                // ============================================================
                // 2. SATIR (INPUT): Kutu
                // ============================================================

                View inputControl;

                if (isDateField)
                {
                    // ✅ DATE KUTUSU: AddToArchive boyutlarına getirildi
                    var border = new Border
                    {
                        Stroke = Color.FromArgb("#E5E7EB"),
                        StrokeThickness = 1,
                        BackgroundColor = Colors.White,
                        StrokeShape = new RoundRectangle { CornerRadius = 4 },

                        // ✅ AddToArchive ile birebir aynı Padding (Boyutu büyütür)
                        Padding = new Thickness(10, 4),

                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,

                        // ✅ AddToArchive ile birebir aynı Genişlik
                        MinimumWidthRequest = 130,

                        // Konum ayarınız (Bunu koruduk ki hizalama bozulmasın)
                        Margin = new Thickness(0, 8, 0, 0)
                    };

                    var centerGrid = new Grid
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill
                    };

                    var datePicker = new DatePicker
                    {
                        Format = "yyyy-MM-dd",
                        FontSize = 12,
                        BackgroundColor = Colors.Transparent,
                        TextColor = Color.FromArgb("#374151"),
                        IsEnabled = false,
                        Opacity = 0.5,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    };

                    centerGrid.Children.Add(datePicker);
                    border.Content = centerGrid;

                    if (currentCheckBox != null)
                    {
                        currentCheckBox.CheckedChanged += (s, e) =>
                        {
                            datePicker.IsEnabled = e.Value;
                            datePicker.Opacity = e.Value ? 1.0 : 0.5;
                        };
                    }

                    dynamicDatePickers[field.Field_Name] = datePicker;
                    inputControl = border;
                }
                else
                {
                    var picker = new Picker
                    {
                        Title = "Select...",
                        FontSize = 12,
                        BackgroundColor = Colors.White,
                        TextColor = Colors.Black,
                        VerticalOptions = LayoutOptions.Center
                    };
                    await LoadPickerData(picker, field);
                    dynamicPickers[field.Field_Name] = picker;
                    inputControl = picker;
                }

                Grid.SetRow((BindableObject)inputControl, 1);
                Grid.SetColumn((BindableObject)inputControl, i);
                DynamicGrid.Children.Add(inputControl);
            }
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

        private async void OnSearchClicked(object sender, EventArgs e)
        {
            try
            {
                var selectedValues = GetSelectedValues();
                var fileType = selectedValues["File_Type"];

                if (string.IsNullOrEmpty(fileType))
                {
                    await DisplayAlert("⚠️ Warning", "Please select a File Type first.", "OK");
                    return;
                }

                selectedValues.Remove("File_Type");
                var results = await _databaseService.SearchDocumentsAsync(fileType, selectedValues);

                var resultsPopup = new SearchResultsPopup(results, selectedValues);
                await Navigation.PushModalAsync(resultsPopup);
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Search failed: {ex.Message}", "OK");
            }
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            var settingsPage = new SettingsPage();
            await Navigation.PushModalAsync(settingsPage);
        }

        private async void OnAddToArchiveClicked(object sender, EventArgs e)
        {
            var popup = new AddToArchivePopup();
            await Navigation.PushModalAsync(popup);
        }

        private async void OnManageDocumentTypesClicked(object sender, EventArgs e)
        {
            var managePage = new ManageDocumentTypesPage();
            await Navigation.PushModalAsync(managePage);
        }

        private async void OnManageFieldsClicked(object sender, EventArgs e)
        {
            var manageFieldsPage = new ManageFieldsPage();
            await Navigation.PushModalAsync(manageFieldsPage);
        }

        // MainPage.xaml.cs dosyası

        private async void OnArchiveMapClicked(object sender, EventArgs e)
        {
            // Eski kod: var archiveMapPage = new ArchiveMap();
            // Eski kod: await Navigation.PushModalAsync(archiveMapPage);

            // YENİ KOD: Hazırladığımız rehber sayfasını açar
            await Navigation.PushModalAsync(new LocationGuidePage());
        }

        private async void OnBorrowedFilesClicked(object sender, EventArgs e)
        {
            var borrowedFilesPage = new BorrowedFilesPage();
            await Navigation.PushModalAsync(borrowedFilesPage);
        }

        public Dictionary<string, string> GetSelectedValues()
        {
            var values = new Dictionary<string, string>();
            values["File_Type"] = FileTypePicker.SelectedItem?.ToString();

            foreach (var kvp in dynamicPickers)
            {
                values[kvp.Key] = kvp.Value.SelectedItem?.ToString();
            }

            foreach (var kvp in dynamicDatePickers)
            {
                if (dateCheckBoxes.ContainsKey(kvp.Key) && dateCheckBoxes[kvp.Key].IsChecked)
                {
                    values[kvp.Key] = kvp.Value.Date.ToString("yyyy-MM-dd");
                }
            }

            return values;
        }
    }
}