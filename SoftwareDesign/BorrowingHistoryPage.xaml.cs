using SoftwareDesign.Services;
using SoftwareDesign.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace SoftwareDesign
{
    public partial class BorrowingHistoryPage : ContentPage
    {
        private DatabaseService _databaseService;
        private List<BorrowedRecord> _allRecords;

        public BorrowingHistoryPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            FilterPicker.SelectedIndex = 0; // "All Records"
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadHistory();
        }

        private async Task LoadHistory()
        {
            try
            {
                // Tüm kayıtları yükle (aktif + iade edilmiş)
                _allRecords = await _databaseService.GetAllBorrowedDocumentsAsync();

                // İstatistikleri güncelle
                var stats = await _databaseService.GetBorrowingStatisticsAsync();
                TotalRecordsLabel.Text = $"{stats["Total_Borrowed"]} Total Records";
                ReturnedCountLabel.Text = $"{stats["Returned"]} Returned";
                ActiveCountLabel.Text = $"{stats["Currently_Borrowed"]} Active";

                // Listeyi göster
                ApplyFilter();
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to load history: {ex.Message}", "OK");
            }
        }

        private void OnFilterChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_allRecords == null)
                return;

            List<BorrowedRecordViewModel> filteredRecords;

            switch (FilterPicker.SelectedIndex)
            {
                case 0: // All Records
                    filteredRecords = _allRecords
                        .OrderByDescending(r => r.Borrow_Date)
                        .Select(r => new BorrowedRecordViewModel(r))
                        .ToList();
                    break;

                case 1: // Active Only
                    filteredRecords = _allRecords
                        .Where(r => !r.Is_Returned)
                        .OrderByDescending(r => r.Borrow_Date)
                        .Select(r => new BorrowedRecordViewModel(r))
                        .ToList();
                    break;

                case 2: // Returned Only
                    filteredRecords = _allRecords
                        .Where(r => r.Is_Returned)
                        .OrderByDescending(r => r.Return_Date ?? r.Borrow_Date)
                        .Select(r => new BorrowedRecordViewModel(r))
                        .ToList();
                    break;

                default:
                    filteredRecords = new List<BorrowedRecordViewModel>();
                    break;
            }

            // Liste boş mu kontrolü
            if (filteredRecords.Count == 0)
            {
                HistoryList.IsVisible = false;
                EmptyView.IsVisible = true;
                RecordCountLabel.Text = "0 records";
            }
            else
            {
                HistoryList.IsVisible = true;
                EmptyView.IsVisible = false;
                HistoryList.ItemsSource = filteredRecords;
                RecordCountLabel.Text = $"{filteredRecords.Count} records";
            }
        }

        private async void OnRecordTapped(object sender, TappedEventArgs e)
        {
            var viewModel = e.Parameter as BorrowedRecordViewModel;
            if (viewModel == null)
                return;

            await ShowRecordDetails(viewModel);
        }

        private async Task ShowRecordDetails(BorrowedRecordViewModel viewModel)
        {
            string details = $"📋 Document: {viewModel.Document_Type}\n\n" +
                           $"👤 Borrower: {viewModel.Borrower_Name}\n" +
                           $"📚 Course: {viewModel.Course_ID}\n" +
                           $"👨‍🏫 Teacher: {viewModel.Teacher_Name}\n" +
                           $"📅 Borrowed: {viewModel.Borrow_Date:dd/MM/yyyy}\n";

            if (viewModel.Is_Returned && viewModel.Return_Date.HasValue)
            {
                details += $"✅ Returned: {viewModel.Return_Date.Value:dd/MM/yyyy}\n";
                details += $"⏱️ Total Duration: {(viewModel.Return_Date.Value - viewModel.Borrow_Date).Days} days";
            }
            else
            {
                details += $"⏱️ Duration: {viewModel.Duration_Counter}\n";
                details += $"📊 Status: Active";
            }

            await DisplayAlert("📄 Record Details", details, "OK");
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PopModalAsync();
            }
            catch
            {
                try
                {
                    await Navigation.PopAsync();
                }
                catch
                {
                    // Ignore if can't pop
                }
            }
        }
    }

    // ===============================================
    // 🎨 VIEW MODEL (UI için ek özellikler)
    // ===============================================
    public class BorrowedRecordViewModel
    {
        private readonly BorrowedRecord _record;

        public BorrowedRecordViewModel(BorrowedRecord record)
        {
            _record = record;
        }

        // Original properties
        public int Id => _record.Id;
        public int ArchiveRecordId => _record.ArchiveRecordId;
        public string Document_Type => _record.Document_Type;
        public string Course_ID => _record.Course_ID ?? "-";
        public string Teacher_Name => _record.Teacher_Name ?? "-";
        public string Borrower_Name => _record.Borrower_Name;
        public DateTime Borrow_Date => _record.Borrow_Date;
        public bool Is_Returned => _record.Is_Returned;
        public DateTime? Return_Date => _record.Return_Date;
        public string Duration_Counter => _record.Duration_Counter;

        // UI-specific properties
        public string StatusIcon => Is_Returned ? "✅" : "⏱️";

        public string StatusText => Is_Returned ? "Returned" : "Active";

        public string StatusColor => Is_Returned ? "#34C759" : "#007AFF";

        public string DurationText
        {
            get
            {
                if (Is_Returned && Return_Date.HasValue)
                {
                    int days = (Return_Date.Value - Borrow_Date).Days;
                    return $"{days}d";
                }
                else
                {
                    int days = (DateTime.Now - Borrow_Date).Days;
                    return $"{days}d";
                }
            }
        }
    }
}