using Microsoft.Maui.Controls;
using SoftwareDesign.Services;
using System;
using Microsoft.Maui.Storage;
using System.IO;

namespace SoftwareDesign
{
    public partial class SettingsPage : ContentPage
    {
        private readonly AuthService _authService;

        public SettingsPage()
        {
            InitializeComponent();
            _authService = new AuthService();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            CurrentUsernameLabel.Text = _authService.GetCurrentUsername();
            DbPathEntry.Text = DatabaseService.GetDatabasePath();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        // ==========================================
        // ✅ MOBILE-COMPATIBLE DB SELECTION
        // ==========================================

        private async void OnChoosePathClicked(object sender, EventArgs e)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.database", "public.data" } },
                        { DevicePlatform.Android, new[] { "application/x-sqlite3", "application/octet-stream", "*/*" } },
                        { DevicePlatform.WinUI, new[] { ".db", ".sqlite" } },
                    });

                var options = new PickOptions
                {
                    PickerTitle = "Select Database File (.db)",
                    FileTypes = customFileType,
                };

                var result = await FilePicker.Default.PickAsync(options);

                if (result != null)
                {
                    // Mobile platformlar için dosyayı app dizinine kopyala
                    var targetPath = await CopyDatabaseToAppDirectory(result);
                    DbPathEntry.Text = targetPath;

                    await DisplayAlert("✅ File Selected",
                        $"Database copied to app directory.\n\nTap 'Set Path' to use this database.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not pick file: {ex.Message}", "OK");
            }
        }

        private async Task<string> CopyDatabaseToAppDirectory(FileResult pickedFile)
        {
            try
            {
                var appDataPath = FileSystem.AppDataDirectory;
                var archiveFolder = Path.Combine(appDataPath, "Archive_System");

                if (!Directory.Exists(archiveFolder))
                    Directory.CreateDirectory(archiveFolder);

                var fileName = Path.GetFileName(pickedFile.FullPath);
                var targetPath = Path.Combine(archiveFolder, fileName);

                // Dosya zaten varsa benzersiz isim oluştur
                if (File.Exists(targetPath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    fileName = $"{nameWithoutExt}_{timestamp}{ext}";
                    targetPath = Path.Combine(archiveFolder, fileName);
                }

                // Dosyayı kopyala
                using (var sourceStream = await pickedFile.OpenReadAsync())
                using (var targetStream = File.Create(targetPath))
                {
                    await sourceStream.CopyToAsync(targetStream);
                }

                return targetPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to copy database: {ex.Message}", ex);
            }
        }

        private async void OnResetPathClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Reset Database",
                "This will disconnect from the current database and create/connect to the default 'ArchiveManagement.db'.\n\nAre you sure?",
                "Yes, Reset",
                "Cancel");

            if (!confirm) return;

            try
            {
                await DatabaseService.ResetToDefaultDatabaseAsync();
                DbPathEntry.Text = DatabaseService.GetDatabasePath();
                await DisplayAlert("✅ Success", "Database connection reset to default.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to reset: {ex.Message}", "OK");
            }
        }

        private async void OnSetPathClicked(object sender, EventArgs e)
        {
            string newPath = DbPathEntry.Text;

            if (string.IsNullOrWhiteSpace(newPath))
            {
                await DisplayAlert("Warning", "Please select a database file first.", "OK");
                return;
            }

            if (!File.Exists(newPath))
            {
                await DisplayAlert("Error", "Selected database file does not exist.", "OK");
                return;
            }

            try
            {
                await DatabaseService.SetCustomDatabasePathAsync(newPath);
                await DisplayAlert("✅ Path Saved", "Database connection updated successfully.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to connect: {ex.Message}", "OK");
            }
        }

        // ==========================================
        // EXISTING FUNCTIONS (UNCHANGED)
        // ==========================================

        private async void OnChangeUsernameClicked(object sender, EventArgs e)
        {
            string currentPassword = await DisplayPromptAsync(
                "Change Username",
                "Enter your current password:",
                placeholder: "Current password",
                maxLength: 50,
                keyboard: Keyboard.Default
            );

            if (string.IsNullOrEmpty(currentPassword))
                return;

            string newUsername = await DisplayPromptAsync(
                "Change Username",
                "Enter new username:",
                placeholder: "New username",
                maxLength: 50,
                keyboard: Keyboard.Default
            );

            if (string.IsNullOrEmpty(newUsername))
                return;

            try
            {
                bool success = await _authService.ChangeCredentialsAsync(
                    currentPassword,
                    newUsername,
                    null
                );

                if (success)
                {
                    await DisplayAlert("✅ Success", "Username changed successfully!", "OK");
                    CurrentUsernameLabel.Text = _authService.GetCurrentUsername();
                }
                else
                {
                    await DisplayAlert("❌ Error", "Current password is incorrect.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to change username: {ex.Message}", "OK");
            }
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            string currentPassword = await DisplayPromptAsync(
                "Change Password",
                "Enter your current password:",
                placeholder: "Current password",
                maxLength: 50,
                keyboard: Keyboard.Default
            );

            if (string.IsNullOrEmpty(currentPassword))
                return;

            string newPassword = await DisplayPromptAsync(
                "Change Password",
                "Enter new password:",
                placeholder: "New password (min 3 chars)",
                maxLength: 50,
                keyboard: Keyboard.Default
            );

            if (string.IsNullOrEmpty(newPassword))
                return;

            if (newPassword.Length < 3)
            {
                await DisplayAlert("❌ Error", "Password must be at least 3 characters long.", "OK");
                return;
            }

            string confirmPassword = await DisplayPromptAsync(
                "Change Password",
                "Confirm new password:",
                placeholder: "Confirm password",
                maxLength: 50,
                keyboard: Keyboard.Default
            );

            if (newPassword != confirmPassword)
            {
                await DisplayAlert("❌ Error", "Passwords do not match.", "OK");
                return;
            }

            try
            {
                bool success = await _authService.ChangeCredentialsAsync(
                    currentPassword,
                    null,
                    newPassword
                );

                if (success)
                {
                    await DisplayAlert("✅ Success", "Password changed successfully!", "OK");
                }
                else
                {
                    await DisplayAlert("❌ Error", "Current password is incorrect.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ Error", $"Failed to change password: {ex.Message}", "OK");
            }
        }

        private async void OnManageDatabaseClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new ManageDatabasePage());
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert(
                "About GAU Archive System",
                "Version: 1.0\n\n" +
                "A document management and archiving system.\n\n" +
                "Developed for GAU University\n" +
                "© 2025 All Rights Reserved",
                "OK"
            );
        }

        private async void OnResetToDefaultClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "⚠️ Reset to Default",
                "This will reset your username and password to:\n\n" +
                "Username: GAU\n" +
                "Password: GAU\n\n" +
                "Are you sure?",
                "Yes, Reset",
                "Cancel"
            );

            if (!confirm)
                return;

            bool doubleConfirm = await DisplayAlert(
                "⚠️ Final Confirmation",
                "This action cannot be undone. Continue?",
                "Yes",
                "No"
            );

            if (doubleConfirm)
            {
                try
                {
                    await _authService.ResetToDefaultAsync();
                    await DisplayAlert("✅ Success", "Credentials reset to default (GAU/GAU).", "OK");
                    CurrentUsernameLabel.Text = _authService.GetCurrentUsername();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("❌ Error", $"Failed to reset: {ex.Message}", "OK");
                }
            }
        }

        private async void OnPopulateLocationsClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new PopulateLocationsPage());
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Logout",
                "Are you sure you want to logout?",
                "Yes",
                "Cancel"
            );

            if (confirm)
            {
                Application.Current.MainPage = new NavigationPage(new LoginPage());
            }
        }
    }
}