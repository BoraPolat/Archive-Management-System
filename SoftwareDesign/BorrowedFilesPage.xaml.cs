using SoftwareDesign.Services;
using SoftwareDesign.Models;
using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace SoftwareDesign
{
    public partial class BorrowedFilesPage : ContentPage
    {
        private DatabaseService _databaseService;

        public BorrowedFilesPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadBorrowedFiles();
        }

        private async Task LoadBorrowedFiles()
        {
            try
            {
                // Aktif ödünç kayıtlarını al (Is_Returned = false)
                var activeBorrowedFiles = await _databaseService.GetActiveBorrowedDocumentsAsync();

                // İstatistikleri al
                var stats = await _databaseService.GetBorrowingStatisticsAsync();

                // İstatistikleri güncelle
                TotalCountLabel.Text = stats["Total_Borrowed"].ToString();
                ActiveCountLabel.Text = stats["Currently_Borrowed"].ToString();

                // Liste boş mu kontrolü
                if (activeBorrowedFiles.Count == 0)
                {
                    BorrowedList.IsVisible = false;
                    EmptyView.IsVisible = true;
                }
                else
                {
                    BorrowedList.IsVisible = true;
                    EmptyView.IsVisible = false;
                    BorrowedList.ItemsSource = activeBorrowedFiles;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to load borrowed files: {ex.Message}", "OK");
            }
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

        // ✅ YENİ: Total Borrowed kartına tıklandığında History sayfasını aç
        private async void OnTotalBorrowedTapped(object sender, EventArgs e)
        {
            var historyPage = new BorrowingHistoryPage();
            await Navigation.PushModalAsync(historyPage);
        }

        // ✅ Button için doğru signature (EventArgs)
        private async void OnReturnClicked(object sender, EventArgs e)
        {
            try
            {
                // Button'dan CommandParameter'ı al
                var button = sender as Button;
                if (button == null)
                    return;

                var record = button.CommandParameter as BorrowedRecord;

                if (record == null)
                    return;

                // Onay mesajı
                string message = $"Are you sure you want to return this document?\n\n" +
                                $"📋 {record.Document_Type}\n" +
                                $"👤 Borrowed by: {record.Borrower_Name}\n" +
                                $"⏱️ {record.Duration_Counter}";

                bool confirm = await DisplayAlert(
                    "↩️ Return Document",
                    message,
                    "Yes, Return",
                    "Cancel"
                );

                if (!confirm)
                    return;

                // İade işlemi
                await _databaseService.ReturnDocumentAsync(record);

                // Başarı mesajı
                await DisplayAlert(
                    "✅ Success!",
                    $"Document '{record.Document_Type}' has been returned successfully.",
                    "OK"
                );

                // Listeyi yenile
                await LoadBorrowedFiles();
            }
            catch (InvalidOperationException ex)
            {
                await DisplayAlert("⚠️ Already Returned", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to return document: {ex.Message}", "OK");
            }
        }
    }

    // ===============================================
    // 🔧 HELPER CONVERTER (XAML için gerekli)
    // ===============================================

    /// <summary>
    /// String boş değilse true döndürür (Visibility için)
    /// </summary>
    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}