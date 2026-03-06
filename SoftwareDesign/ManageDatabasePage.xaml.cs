using Microsoft.Maui.Controls;
using SoftwareDesign.Models;
using SoftwareDesign.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace SoftwareDesign
{
    public partial class ManageDatabasePage : ContentPage
    {
        private DatabaseService _databaseService;
        private ObservableCollection<SelectableArchiveRecord> _records;
        private List<ArchiveRecord> _allRecords;

        // Pagination
        private const int PAGE_SIZE = 200;
        private int _currentPage = 0;
        private bool _isLoadingMore = false;

        // Filter state
        private string _searchText = "";

        public ManageDatabasePage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            _records = new ObservableCollection<SelectableArchiveRecord>();
            RecordsCollectionView.ItemsSource = _records;

            LoadInitialData();
        }

        private async void LoadInitialData()
        {
            try
            {
                LoadingIndicator.IsVisible = true;

                var borrowedCount = await _databaseService.GetActiveBorrowedDocumentsAsync();
                _allRecords = await _databaseService.GetAllDocumentsAsync();

                TotalRecordsLabel.Text = _allRecords.Count.ToString();
                BorrowedCountLabel.Text = borrowedCount.Count.ToString();

                LoadPage();

                LoadingIndicator.IsVisible = false;
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                await DisplayAlert("Error", $"Failed to load data: {ex.Message}", "OK");
            }
        }

        private void LoadPage()
        {
            try
            {
                var filteredRecords = GetFilteredRecords();
                var startIndex = _currentPage * PAGE_SIZE;
                var recordsToLoad = filteredRecords.Skip(startIndex).Take(PAGE_SIZE).ToList();

                foreach (var record in recordsToLoad)
                {
                    _records.Add(new SelectableArchiveRecord(record));
                }

                UpdatePaginationUI(filteredRecords.Count);
                UpdateSelectedCount();
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", $"Failed to load page: {ex.Message}", "OK");
            }
        }

        private void UpdateEmptyState()
        {
            bool hasRecords = _records.Count > 0;
            bool hasDataInDatabase = _allRecords.Count > 0;
            bool isSearching = !string.IsNullOrWhiteSpace(_searchText);

            EmptyStateContainer.IsVisible = !hasRecords;

            if (!hasRecords)
            {
                if (!hasDataInDatabase)
                {
                    EmptyStateIcon.Text = "📁";
                    EmptyStateTitle.Text = "Database is empty";
                    EmptyStateMessage.Text = "Start by adding some documents";
                }
                else if (isSearching)
                {
                    EmptyStateIcon.Text = "🔍";
                    EmptyStateTitle.Text = "No matching records";
                    EmptyStateMessage.Text = "Try a different search term";
                }
                else
                {
                    EmptyStateIcon.Text = "📄";
                    EmptyStateTitle.Text = "No records to display";
                    EmptyStateMessage.Text = "Load or filter records";
                }
            }
        }

        private List<ArchiveRecord> GetFilteredRecords()
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                return _allRecords;
            }

            // 1. Arama metnini boşluklara göre kelimelere ayır (Örn: "John 2024" -> ["john", "2024"])
            var searchTerms = _searchText.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // 2. Listeyi filtrele
            return _allRecords.Where(r =>
            {
                // Bu kaydın (r) sonuçlarda çıkması için, aranan TÜM kelimelerin (searchTerms)
                // bu kaydın en az bir alanında bulunması gerekir.
                foreach (var term in searchTerms)
                {
                    bool isTermFoundInRecord =
                        (r.Document_Type?.ToLower().Contains(term) ?? false) ||
                        (r.Course_ID?.ToLower().Contains(term) ?? false) ||
                        (r.Teacher_Name?.ToLower().Contains(term) ?? false) ||
                        (r.Year?.ToLower().Contains(term) ?? false) ||
                        (r.Semester?.ToLower().Contains(term) ?? false) ||
                        (r.Location?.ToLower().Contains(term) ?? false) ||
                        (r.Archive_Date?.ToLower().Contains(term) ?? false) || // Tarih alanını da aramaya dahil ettik
                        r.Id.ToString().Contains(term);

                    // Eğer aranan kelimelerden HERHANGİ BİRİ bu kayıtta yoksa, 
                    // bu kaydı ele (false dön).
                    if (!isTermFoundInRecord)
                    {
                        return false;
                    }
                }

                // Döngü tamamlandıysa, aranan tüm kelimeler bu kayıtta bulunmuş demektir.
                return true;
            }).ToList();
        }

        private void UpdatePaginationUI(int totalFilteredCount)
        {
            var loadedCount = _records.Count;
            var hasMore = loadedCount < totalFilteredCount;

            LoadMoreButton.IsVisible = hasMore;

            LoadedCountLabel.Text = $"Showing {loadedCount:N0} of {totalFilteredCount:N0}";
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = e.NewTextValue ?? "";

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(300);
                if (_searchText == e.NewTextValue)
                {
                    ApplyFilter();
                }
            });
        }

        private void OnSearchButtonClicked(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void OnClearSearchClicked(object sender, EventArgs e)
        {
            SearchEntry.Text = "";
            _searchText = "";
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _currentPage = 0;
            _records.Clear();
            LoadPage();
        }

        private void OnLoadMoreClicked(object sender, EventArgs e)
        {
            if (_isLoadingMore) return;

            _isLoadingMore = true;

            _currentPage++;
            LoadPage();

            _isLoadingMore = false;
        }

        private void OnCustomCheckboxTapped(object sender, EventArgs e)
        {
            var tappedEventArgs = e as TappedEventArgs;
            if (tappedEventArgs?.Parameter is SelectableArchiveRecord record)
            {
                record.IsSelected = !record.IsSelected;
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            var selectedCount = _records?.Count(r => r.IsSelected) ?? 0;
            SelectedCountLabel.Text = selectedCount.ToString();

            // Delete button enable/disable
            if (DeleteButton != null)
            {
                DeleteButton.IsEnabled = selectedCount > 0;
            }

            // Select All button text
            if (_records != null && _records.Count > 0 && SelectAllButton != null)
            {
                bool allSelected = _records.All(r => r.IsSelected);
                SelectAllButton.Text = allSelected ? "✗ Deselect All" : "✓ Select All";
            }
        }

        private void OnSelectAllClicked(object sender, EventArgs e)
        {
            if (_records == null || _records.Count == 0) return;

            bool allSelected = _records.All(r => r.IsSelected);

            foreach (var record in _records)
            {
                record.IsSelected = !allSelected;
            }

            UpdateSelectedCount();
        }

        private async void OnSelectAllInDatabaseClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "⚠️ Select All Records",
                $"This will select ALL {_allRecords.Count:N0} records in the database.\n\nAre you sure?",
                "Yes",
                "Cancel"
            );

            if (!confirm) return;

            try
            {
                LoadingIndicator.IsVisible = true;

                _records.Clear();
                _currentPage = 0;

                foreach (var record in _allRecords)
                {
                    _records.Add(new SelectableArchiveRecord(record) { IsSelected = true });
                }

                LoadingIndicator.IsVisible = false;

                UpdatePaginationUI(_allRecords.Count);
                UpdateSelectedCount();

                await DisplayAlert("✅", $"Selected all {_allRecords.Count:N0} records", "OK");
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnDeleteSelectedClicked(object sender, EventArgs e)
        {
            // Eğer button disabled ise çık
            if (DeleteButton?.IsEnabled == false)
            {
                return;
            }

            var selectedRecords = _records.Where(r => r.IsSelected).ToList();

            if (selectedRecords.Count == 0)
            {
                await DisplayAlert("Warning", "No records selected.", "OK");
                return;
            }

            bool confirm = await DisplayAlert(
                "⚠️ Confirm Deletion",
                $"Delete {selectedRecords.Count} record(s)?\n\n⚠️ THIS CANNOT BE UNDONE!",
                "Delete",
                "Cancel"
            );

            if (!confirm) return;

            try
            {
                LoadingIndicator.IsVisible = true;

                int successCount = 0;
                int failCount = 0;

                foreach (var record in selectedRecords)
                {
                    try
                    {
                        await _databaseService.DeleteDocumentAsync(record.Record);
                        successCount++;

                        _allRecords.Remove(record.Record);
                        _records.Remove(record);
                    }
                    catch
                    {
                        failCount++;
                    }
                }

                LoadingIndicator.IsVisible = false;

                await DisplayAlert(
                    "✅ Deletion Complete",
                    $"Deleted: {successCount}" + (failCount > 0 ? $"\nFailed: {failCount}" : ""),
                    "OK"
                );

                TotalRecordsLabel.Text = _allRecords.Count.ToString();
                UpdatePaginationUI(GetFilteredRecords().Count);
                UpdateSelectedCount();
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsVisible = false;
                await DisplayAlert("Error", $"Deletion failed: {ex.Message}", "OK");
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            _currentPage = 0;
            _records.Clear();
            LoadInitialData();
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }

    public class SelectableArchiveRecord : BindableObject
    {
        public ArchiveRecord Record { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public int Id => Record.Id;
        public string Document_Type => Record.Document_Type;
        public string Course_ID => Record.Course_ID;
        public string Teacher_Name => Record.Teacher_Name;
        public string Year => Record.Year;
        public string Semester => Record.Semester;
        public string Location => Record.Location;
        public string Archive_Date => Record.Archive_Date;

        public SelectableArchiveRecord(ArchiveRecord record)
        {
            Record = record;
            IsSelected = false;
        }
    }

    public class BoolToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected ? Color.FromArgb("#007AFF") : Colors.Transparent;
            }
            return Colors.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}