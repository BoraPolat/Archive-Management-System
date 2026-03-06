using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SoftwareDesign.Models;
using SoftwareDesign.Services;

namespace SoftwareDesign
{
    public partial class SearchResultsPopup : ContentPage
    {
        private List<ArchiveRecord> _results;
        private Dictionary<string, string> _searchCriteria;
        private DatabaseService _databaseService;

        public SearchResultsPopup(List<ArchiveRecord> results, Dictionary<string, string> searchCriteria)
        {
            InitializeComponent();
            _results = results;
            _searchCriteria = searchCriteria;
            _databaseService = new DatabaseService();
            DisplayResults();
        }

        private void DisplayResults()
        {
            var criteriaText = "";
            foreach (var kvp in _searchCriteria)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    criteriaText += string.IsNullOrEmpty(criteriaText)
                        ? $"{kvp.Key}: {kvp.Value}"
                        : $" | {kvp.Key}: {kvp.Value}";
                }
            }
            SearchCriteriaLabel.Text = criteriaText;

            ResultCountLabel.Text = _results.Count.ToString();

            if (_results.Count == 0)
            {
                ResultsCollectionView.IsVisible = false;
                NoResultsView.IsVisible = true;
            }
            else
            {
                var displayResults = _results.Select(doc => ConvertToDisplayItem(doc)).ToList();
                ResultsCollectionView.ItemsSource = displayResults;
                ResultsCollectionView.IsVisible = true;
                NoResultsView.IsVisible = false;
            }
        }

        private DisplayItem ConvertToDisplayItem(ArchiveRecord record)
        {
            var displayItem = new DisplayItem
            {
                Id = record.Id.ToString(),
                ActualId = record.Id,
                Document_Type = record.Document_Type,
                Course_ID = record.Course_ID,
                Teacher_Name = record.Teacher_Name,
                Year = record.Year,
                Semester = record.Semester,
                Archive_Date = record.Archive_Date,
                Location = record.Location,
                Created_At = record.Created_At.ToString("dd/MM/yyyy HH:mm")
            };

            if (!string.IsNullOrEmpty(record.Fields_Json))
            {
                try
                {
                    var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(record.Fields_Json);
                    displayItem.AdditionalFields = fields;
                }
                catch { }
            }

            return displayItem;
        }

        private async void OnResultSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() == null)
                return;

            var selectedItem = e.CurrentSelection.FirstOrDefault() as DisplayItem;

            // Ödünç durumunu kontrol et
            bool isCurrentlyBorrowed = await _databaseService.IsDocumentCurrentlyBorrowedAsync(selectedItem.ActualId);

            string details = GetItemDetails(selectedItem, isCurrentlyBorrowed);

            // Buton metni ödünç durumuna göre değişir
            string actionButton = isCurrentlyBorrowed ? "View Borrow Info" : "📤 Borrow!";

            bool userWantsToBorrow = await DisplayAlert("📄 Record Details", details, actionButton, "Close");

            if (userWantsToBorrow)
            {
                if (isCurrentlyBorrowed)
                {
                    // Mevcut ödünç bilgisini göster
                    await ShowCurrentBorrowInfo(selectedItem.ActualId);
                }
                else
                {
                    // Ödünç alma formunu göster
                    await ShowBorrowDialog(selectedItem);
                }
            }

            ((CollectionView)sender).SelectedItem = null;
        }

        private async Task ShowCurrentBorrowInfo(int archiveId)
        {
            try
            {
                var borrowRecord = await _databaseService.GetActiveBorrowRecordAsync(archiveId);

                if (borrowRecord != null)
                {
                    string info = $"📋 Currently Borrowed\n\n" +
                                 $"👤 Borrower: {borrowRecord.Borrower_Name}\n" +
                                 $"📅 Borrowed: {borrowRecord.Borrow_Date:dd/MM/yyyy}\n" +
                                 $"⏱️ Duration: {borrowRecord.Duration_Counter}";

                    await DisplayAlert("📤 Borrow Information", info, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to load borrow info: {ex.Message}", "OK");
            }
        }

        private async Task ShowBorrowDialog(DisplayItem item)
        {
            try
            {
                // Sadece kullanıcı adını al (süre sınırı YOK)
                string borrowerName = await DisplayPromptAsync(
                    "📤 Borrow Document",
                    "Enter borrower's name:",
                    placeholder: "e.g., John Doe",
                    maxLength: 100
                );

                if (string.IsNullOrWhiteSpace(borrowerName))
                    return; // İptal edildi

                // Onay
                bool confirm = await DisplayAlert(
                    "✅ Confirm Borrow",
                    $"📋 Document: {item.Document_Type}\n" +
                    $"👤 Borrower: {borrowerName}\n\n" +
                    $"Proceed with borrowing?",
                    "Yes, Borrow",
                    "Cancel"
                );

                if (!confirm)
                    return;

                // Ödünç verme işlemi (süre parametresi YOK)
                var borrowRecord = await _databaseService.BorrowDocumentAsync(
                    item.ActualId,
                    borrowerName
                );

                await DisplayAlert(
                    "✅ Success!",
                    $"Document successfully borrowed!\n\n" +
                    $"📋 {item.Document_Type}\n" +
                    $"👤 Borrower: {borrowerName}\n" +
                    $"📅 Date: {DateTime.Now:dd/MM/yyyy}",
                    "OK"
                );

                // Listeyi güncelle (opsiyonel)
                DisplayResults();
            }
            catch (ArgumentException ex)
            {
                await DisplayAlert("⚠️ Validation Error", ex.Message, "OK");
            }
            catch (InvalidOperationException ex)
            {
                await DisplayAlert("❌ Cannot Borrow", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"An error occurred: {ex.Message}", "OK");
            }
        }

        private string GetItemDetails(DisplayItem item, bool isCurrentlyBorrowed)
        {
            var details = $"ID: {item.Id}\n\n";
            details += $"Type: {item.Document_Type}\n\n";

            if (!string.IsNullOrEmpty(item.Course_ID))
                details += $"Course ID: {item.Course_ID}\n";
            if (!string.IsNullOrEmpty(item.Teacher_Name))
                details += $"Teacher: {item.Teacher_Name}\n";
            if (!string.IsNullOrEmpty(item.Year))
                details += $"Year: {item.Year}\n";
            if (!string.IsNullOrEmpty(item.Semester))
                details += $"Semester: {item.Semester}\n";
            if (!string.IsNullOrEmpty(item.Archive_Date))
                details += $"Archive Date: {item.Archive_Date}\n";
            if (!string.IsNullOrEmpty(item.Location))
                details += $"Location: {item.Location}\n";

            // Custom fields - sistem fieldlarını filtrele
            if (item.AdditionalFields != null && item.AdditionalFields.Any())
            {
                var systemFields = new HashSet<string>
                {
                    "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location"
                };

                var customFields = item.AdditionalFields.Where(f => !systemFields.Contains(f.Key)).ToList();

                if (customFields.Any())
                {
                    details += "\n";
                    foreach (var kvp in customFields)
                    {
                        details += $"{kvp.Key}: {kvp.Value}\n";
                    }
                }
            }

            if (!string.IsNullOrEmpty(item.Created_At))
                details += $"\nCreated: {item.Created_At}\n";

            if (isCurrentlyBorrowed)
            {
                details += "\nSTATUS: Currently Borrowed";
            }
            else
            {
                details += "\nSTATUS: Available";
            }

            return details;
        }

        private async void OnNewSearchClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        public class DisplayItem
        {
            public string Id { get; set; }
            public int ActualId { get; set; } // Database ID (int)
            public string Document_Type { get; set; }
            public string Course_ID { get; set; }
            public string Teacher_Name { get; set; }
            public string Year { get; set; }
            public string Semester { get; set; }
            public string Archive_Date { get; set; }
            public string Location { get; set; }
            public string Created_At { get; set; }
            public Dictionary<string, string> AdditionalFields { get; set; }

            public string Summary
            {
                get
                {
                    var parts = new List<string>();

                    if (!string.IsNullOrEmpty(Document_Type))
                        parts.Add($"📋 {Document_Type}");
                    if (!string.IsNullOrEmpty(Course_ID))
                        parts.Add($"📚 {Course_ID}");
                    if (!string.IsNullOrEmpty(Teacher_Name))
                        parts.Add($"👨‍🏫 {Teacher_Name}");
                    if (!string.IsNullOrEmpty(Year))
                        parts.Add($"📅 {Year}");

                    return string.Join(" | ", parts);
                }
            }

            public string DetailLine
            {
                get
                {
                    var parts = new List<string>();

                    if (!string.IsNullOrEmpty(Semester))
                        parts.Add($"📆 {Semester}");
                    if (!string.IsNullOrEmpty(Archive_Date))
                        parts.Add($"📁 {Archive_Date}");
                    if (!string.IsNullOrEmpty(Location))
                        parts.Add($"📍 {Location}");

                    return string.Join(" | ", parts);
                }
            }

            public string FullDetails
            {
                get
                {
                    var parts = new List<string>();

                    if (!string.IsNullOrEmpty(Document_Type))
                        parts.Add($"Type: {Document_Type}");
                    if (!string.IsNullOrEmpty(Course_ID))
                        parts.Add($"Course: {Course_ID}");
                    if (!string.IsNullOrEmpty(Teacher_Name))
                        parts.Add($"Teacher: {Teacher_Name}");
                    if (!string.IsNullOrEmpty(Year))
                        parts.Add($"Year: {Year}");
                    if (!string.IsNullOrEmpty(Semester))
                        parts.Add($"Semester: {Semester}");
                    if (!string.IsNullOrEmpty(Archive_Date))
                        parts.Add($"Archive: {Archive_Date}");
                    if (!string.IsNullOrEmpty(Location))
                        parts.Add($"Location: {Location}");

                    // Custom fields ekle - sistem fieldlarını filtrele
                    if (AdditionalFields != null && AdditionalFields.Count > 0)
                    {
                        var systemFields = new HashSet<string>
                        {
                            "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location"
                        };

                        foreach (var field in AdditionalFields)
                        {
                            if (!systemFields.Contains(field.Key))
                            {
                                parts.Add($"{field.Key}: {field.Value}");
                            }
                        }
                    }

                    return string.Join(" | ", parts);
                }
            }
        }
    }
}