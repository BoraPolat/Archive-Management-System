using Microsoft.Maui.Controls;
using SoftwareDesign.Services;
using SoftwareDesign.Models; // AvailableField modeli için
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SoftwareDesign
{
    public partial class PopulateLocationsPage : ContentPage
    {
        private DatabaseService _databaseService;

        public PopulateLocationsPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnGenerateClicked(object sender, EventArgs e)
        {
            try
            {
                StatusLabel.Text = "Calculating...";

                // 1. Girdileri Kontrol Et
                if (!int.TryParse(RowCountEntry.Text, out int rowCount) ||
                    !int.TryParse(ShelfHeightEntry.Text, out int shelfHeight))
                {
                    await DisplayAlert("Error", "Please enter valid numbers for counts.", "OK");
                    return;
                }

                var sides = SidesEntry.Text.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                var sections = SectionsEntry.Text.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

                // 2. Yeni Kombinasyonları Oluştur (Hafızada)
                List<string> newLocations = new List<string>();

                for (int i = 1; i <= rowCount; i++)
                {
                    foreach (var side in sides)
                    {
                        for (int j = 1; j <= shelfHeight; j++)
                        {
                            foreach (var section in sections)
                            {
                                newLocations.Add($"{i}{side}-{j}{section}");
                            }
                        }
                    }
                }

                // =========================================================
                // 🛡️ GÜVENLİK KONTROLÜ VE VERİ SİLME UYARISI
                // =========================================================

                // Veritabanındaki tüm belgeleri getir
                var allDocuments = await _databaseService.GetAllDocumentsAsync();

                // Yeni listede OLMAYAN konumlara sahip belgeleri bul
                // (Yani: Location alanı dolu olup da, yeni üretilen listede bulunmayanlar)
                var documentsToDelete = allDocuments
                    .Where(doc => !string.IsNullOrEmpty(doc.Location) && !newLocations.Contains(doc.Location))
                    .ToList();

                if (documentsToDelete.Count > 0)
                {
                    // Kullanıcıya gösterilecek uyarı mesajını hazırla
                    string warningMsg = $"⚠️ WARNING: There are {documentsToDelete.Count} documents in locations that are not in your new list.\n\n";

                    // İlk 3 belgenin adını örnek olarak göster
                    foreach (var doc in documentsToDelete.Take(3))
                    {
                        warningMsg += $"• {doc.Document_Type} ({doc.Location})\n";
                    }

                    if (documentsToDelete.Count > 3)
                        warningMsg += $"...and {documentsToDelete.Count - 3} more.\n";

                    warningMsg += "\nIf you proceed, THESE DOCUMENTS WILL BE PERMANENTLY DELETED along with the old locations.\n\nAre you sure?";

                    // Onay iste
                    bool confirm = await DisplayAlert("⚠️ DATA LOSS WARNING", warningMsg, "Yes, Delete & Populate", "Cancel");

                    if (!confirm)
                    {
                        StatusLabel.Text = "Operation cancelled.";
                        return; // İşlemi iptal et
                    }

                    // Kullanıcı onayladıysa, uyumsuz belgeleri sil
                    foreach (var doc in documentsToDelete)
                    {
                        await _databaseService.DeleteDocumentAsync(doc);
                    }
                }

                // =========================================================
                // 3. VERİTABANINI GÜNCELLE (Mevcut İşlem)
                // =========================================================

                var fields = await _databaseService.GetAllFieldsAsync();
                var locationField = fields.FirstOrDefault(f => f.Field_Name == "Location");

                if (locationField != null)
                {
                    locationField.Picker_Options_Json = JsonSerializer.Serialize(newLocations);

                    await _databaseService.UpdateFieldAsync(locationField);

                    string successMsg = $"✅ Success! {newLocations.Count} locations generated.";

                    if (documentsToDelete.Count > 0)
                        successMsg += $"\n🗑️ {documentsToDelete.Count} old documents were deleted.";

                    StatusLabel.Text = successMsg;
                    await DisplayAlert("Success", successMsg, "OK");
                }
                else
                {
                    StatusLabel.Text = "❌ Error: 'Location' field not found.";
                    await DisplayAlert("Error", "Could not find 'Location' field.", "OK");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ Error occurred";
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }
}