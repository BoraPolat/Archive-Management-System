using Microsoft.Maui.Storage;
using SoftwareDesign.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SoftwareDesign.Services;

namespace SoftwareDesign.Services
{
    public class DatabaseService
    {
        private static SQLiteAsyncConnection _database;
        private static string _databasePath;
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        static DatabaseService()
        {
            InitializePath();
        }

        // ==========================================
        // 🛠️ YENİ PATH VE BAŞLATMA MANTIĞI
        // ==========================================

        // 🆕 Aktif veritabanı dosyasının tam yolunu döner
        public static string GetDatabasePath()
        {
            return _databasePath;
        }

        // 🆕 Varsayılan veritabanı dosyasının yolunu döner (AppData/Archive_System/ArchiveManagement.db)
        public static string GetDefaultDatabasePath()
        {
            return Path.Combine(FileSystem.AppDataDirectory, "Archive_System", "ArchiveManagement.db");
        }

        private static void InitializePath()
        {
            // 1. Hafızadan "CustomDbFullPath" anahtarıyla kayıtlı TAM DOSYA yolunu getirmeyi dene
            string savedFullPath = Preferences.Get("CustomDbFullPath", null);

            // 2. Eğer kayıtlı bir dosya yolu varsa VE bu dosya diskte fiziksel olarak duruyorsa
            if (!string.IsNullOrEmpty(savedFullPath) && File.Exists(savedFullPath))
            {
                _databasePath = savedFullPath;
                System.Diagnostics.Debug.WriteLine($"=== DATABASE RESTORED: {_databasePath} ===");
            }
            else
            {
                // 3. Kayıt yoksa veya dosya silinmişse VARSAYILAN yolu kullan
                _databasePath = GetDefaultDatabasePath();

                // Klasörün var olduğundan emin ol (Dosya veritabanı ilk açıldığında oluşur)
                string folder = Path.GetDirectoryName(_databasePath);
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                System.Diagnostics.Debug.WriteLine($"=== DATABASE SET TO DEFAULT: {_databasePath} ===");
            }
        }

        // 🆕 KULLANICI ÖZEL BİR .DB DOSYASI SEÇTİĞİNDE ÇAĞRILIR
        public static async Task SetCustomDatabasePathAsync(string fullFilePath)
        {
            // Dosya var mı kontrolü
            if (!File.Exists(fullFilePath))
                throw new FileNotFoundException("Selected database file does not exist.");

            // Uzantı kontrolü (.db)
            if (Path.GetExtension(fullFilePath).ToLower() != ".db")
                throw new ArgumentException("Selected file must be a .db file.");

            // Mevcut bağlantıyı kapat
            if (_database != null)
            {
                await _database.CloseAsync();
                _database = null;
            }

            // ÖNEMLİ: Tam dosya yolunu kalıcı hafızaya kaydet
            Preferences.Set("CustomDbFullPath", fullFilePath);

            _initialized = false;
            _databasePath = fullFilePath; // Yolu güncelle

            await EnsureInitializedAsync(); // Yeni DB'yi başlat/bağlan
                                            // 🛠️ MEVCUT VERİTABANINI GÜNCELLEME YAMASI
                                            // Eğer Archive_Date hala "Picker" ise "Date"e çevir.
            var archiveDateField = await _database.Table<AvailableField>()
                .Where(x => x.Field_Name == "Archive_Date")
                .FirstOrDefaultAsync();

            if (archiveDateField != null && archiveDateField.Field_Type == "Picker")
            {
                archiveDateField.Field_Type = "Date";
                archiveDateField.Picker_Options_Json = null; // Eski seçenekleri temizle
                await _database.UpdateAsync(archiveDateField);
                System.Diagnostics.Debug.WriteLine("=== Archive_Date migrated to DatePicker type ===");
            }
        }

        // 🆕 VARSAYILAN AYARLARA VE KONUMA SIFIRLAMA (DEFAULT BUTONU İÇİN)
        public static async Task ResetToDefaultDatabaseAsync()
        {
            if (_database != null)
            {
                await _database.CloseAsync();
                _database = null;
            }

            // Kayıtlı özel yolu sil (Böylece InitializePath varsayılanı seçecek)
            Preferences.Remove("CustomDbFullPath");

            _initialized = false;
            InitializePath(); // Varsayılan yolu tekrar yükle
            await EnsureInitializedAsync(); // DB'yi başlat
        }

        // 🆕 UI uyumluluğu için eski metot adı yönlendirmesi
        public static string GetDatabaseFolderPath()
        {
            return Path.GetDirectoryName(_databasePath);
        }

        private static async Task EnsureInitializedAsync()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;
            }

            try
            {
                _database = new SQLiteAsyncConnection(_databasePath);

                await _database.CreateTableAsync<ArchiveRecord>();
                await _database.CreateTableAsync<DocumentType>();
                await _database.CreateTableAsync<AvailableField>();
                await _database.CreateTableAsync<BorrowedRecord>();

                await InitializeDefaultData();

                lock (_initLock)
                {
                    _initialized = true;
                }

                System.Diagnostics.Debug.WriteLine("=== DATABASE INITIALIZED SUCCESSFULLY ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== DATABASE ERROR: {ex.Message} ===");
                System.Diagnostics.Debug.WriteLine($"=== STACK TRACE: {ex.StackTrace} ===");
                throw;
            }
        }

        private static async Task InitializeDefaultData()
        {
            await InitializeDefaultFields();
            await InitializeDefaultTypes();
        }

        private static async Task InitializeDefaultFields()
        {
            var defaultFields = new List<AvailableField>
            {
                new AvailableField {
                    Field_Name = "Course_ID",
                    Display_Name = "Course ID",
                    Field_Type = "Picker",
                    Is_System_Field = true,
                    Picker_Options_Json = JsonSerializer.Serialize(new[] { "CS101", "CS102", "CS201", "CS202", "MATH101", "PHYS101" })
                },
                new AvailableField {
                    Field_Name = "Teacher_Name",
                    Display_Name = "Teacher Name",
                    Field_Type = "Picker",
                    Is_System_Field = true,
                    Picker_Options_Json = JsonSerializer.Serialize(new[] { "Dr. Smith", "Dr. Johnson", "Prof. Williams", "Dr. Brown", "Prof. Davis" })
                },
                new AvailableField {
                    Field_Name = "Year",
                    Display_Name = "Year",
                    Field_Type = "Picker",
                    Is_System_Field = true,
                    Picker_Options_Json = JsonSerializer.Serialize(new[] { "2023", "2024", "2025" })
                },
                new AvailableField {
                    Field_Name = "Semester",
                    Display_Name = "Semester",
                    Field_Type = "Picker",
                    Is_System_Field = true,
                    Picker_Options_Json = JsonSerializer.Serialize(new[] { "Fall", "Spring", "Summer" })
                },
                new AvailableField {
                    Field_Name = "Archive_Date",
                    Display_Name = "Archive Date",
                    Field_Type = "Date",
                    Is_System_Field = true,
                    Picker_Options_Json = JsonSerializer.Serialize(new[] { "2024-01-15", "2024-06-20", "2024-09-10", "2024-12-15" })
                },
                new AvailableField {
                    Field_Name = "Location",
                    Display_Name = "Location",
                    Field_Type = "Picker",
                    Is_System_Field = true,
                    Picker_Options_Json = JsonSerializer.Serialize(new[] { "Building A - Room 101", "Building B - Room 202", "Archive Room", "Storage Room" })
                }
            };

            foreach (var field in defaultFields)
            {
                var existing = await _database.Table<AvailableField>()
                    .Where(x => x.Field_Name == field.Field_Name)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    field.Created_At = DateTime.Now;
                    await _database.InsertAsync(field);
                }
            }
        }

        private static async Task InitializeDefaultTypes()
        {
            var defaultTypes = new List<DocumentType>
            {
                new DocumentType {
                    Type_Name = "Midterms",
                    Is_System_Type = true,
                    Description = "Midterm exams",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location" })
                },
                new DocumentType {
                    Type_Name = "Finals",
                    Is_System_Type = true,
                    Description = "Final exams",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location" })
                },
                new DocumentType {
                    Type_Name = "Quiz1",
                    Is_System_Type = true,
                    Description = "First quiz",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location" })
                },
                new DocumentType {
                    Type_Name = "Quiz2",
                    Is_System_Type = true,
                    Description = "Second quiz",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location" })
                },
                new DocumentType {
                    Type_Name = "Graduation_Projects",
                    Is_System_Type = true,
                    Description = "Graduation projects",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Year", "Semester", "Archive_Date", "Location" })
                },
                new DocumentType {
                    Type_Name = "Summer_Training_Reports",
                    Is_System_Type = true,
                    Description = "Summer training reports",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Year", "Archive_Date", "Location" })
                },
                new DocumentType {
                    Type_Name = "Homeworks",
                    Is_System_Type = true,
                    Description = "Homework assignments",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location" })
                },
                new DocumentType {
                    Type_Name = "Letters",
                    Is_System_Type = true,
                    Description = "Official letters",
                    Required_Fields_Json = JsonSerializer.Serialize(new[] { "Year", "Semester", "Archive_Date", "Location" })
                }
            };

            foreach (var type in defaultTypes)
            {
                var existing = await _database.Table<DocumentType>()
                    .Where(x => x.Type_Name == type.Type_Name)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    type.Created_At = DateTime.Now;
                    await _database.InsertAsync(type);
                }
            }
        }

        // ============ BELGE İŞLEMLERİ ============

        public async Task<int> AddDocumentAsync(ArchiveRecord document)
        {
            await EnsureInitializedAsync();
            document.Created_At = DateTime.Now;
            document.Updated_At = DateTime.Now;
            return await _database.InsertAsync(document);
        }

        public async Task<List<ArchiveRecord>> GetAllDocumentsAsync()
        {
            await EnsureInitializedAsync();
            return await _database.Table<ArchiveRecord>().ToListAsync();
        }

        public async Task<ArchiveRecord> GetDocumentByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _database.Table<ArchiveRecord>()
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ArchiveRecord>> SearchDocumentsAsync(string documentType = null,
            Dictionary<string, string> criteria = null)
        {
            await EnsureInitializedAsync();
            var query = _database.Table<ArchiveRecord>();

            if (!string.IsNullOrEmpty(documentType))
                query = query.Where(x => x.Document_Type == documentType);

            var results = await query.ToListAsync();

            if (criteria != null && criteria.Any())
            {
                results = results.Where(record =>
                {
                    foreach (var kvp in criteria)
                    {
                        if (string.IsNullOrEmpty(kvp.Value)) continue;

                        switch (kvp.Key)
                        {
                            case "Course_ID":
                                if (record.Course_ID != kvp.Value) return false;
                                break;
                            case "Teacher_Name":
                                if (record.Teacher_Name != kvp.Value) return false;
                                break;
                            case "Year":
                                if (record.Year != kvp.Value) return false;
                                break;
                            case "Semester":
                                if (record.Semester != kvp.Value) return false;
                                break;
                            case "Archive_Date":
                                if (record.Archive_Date != kvp.Value) return false;
                                break;
                            case "Location":
                                if (record.Location != kvp.Value) return false;
                                break;
                        }
                    }
                    return true;
                }).ToList();
            }

            return results;
        }

        public async Task<int> DeleteDocumentAsync(ArchiveRecord document)
        {
            await EnsureInitializedAsync();
            return await _database.DeleteAsync(document);
        }

        public async Task<int> UpdateDocumentAsync(ArchiveRecord document)
        {
            await EnsureInitializedAsync();
            document.Updated_At = DateTime.Now;
            return await _database.UpdateAsync(document);
        }

        // ============ BELGE TÜRLERİ İŞLEMLERİ ============

        public async Task<List<DocumentType>> GetAllDocumentTypesAsync()
        {
            await EnsureInitializedAsync();
            return await _database.Table<DocumentType>().ToListAsync();
        }

        public async Task<DocumentType> GetDocumentTypeByNameAsync(string typeName)
        {
            await EnsureInitializedAsync();
            return await _database.Table<DocumentType>()
                .Where(x => x.Type_Name == typeName)
                .FirstOrDefaultAsync();
        }

        public async Task<int> AddDocumentTypeAsync(DocumentType documentType)
        {
            await EnsureInitializedAsync();
            documentType.Created_At = DateTime.Now;
            return await _database.InsertAsync(documentType);
        }

        public async Task<int> UpdateDocumentTypeAsync(DocumentType documentType)
        {
            await EnsureInitializedAsync();
            return await _database.UpdateAsync(documentType);
        }

        public async Task<int> DeleteDocumentTypeAsync(DocumentType documentType)
        {
            await EnsureInitializedAsync();

            var documents = await _database.Table<ArchiveRecord>()
                .Where(x => x.Document_Type == documentType.Type_Name)
                .ToListAsync();

            foreach (var record in documents)
            {
                await _database.DeleteAsync(record);
            }

            return await _database.DeleteAsync(documentType);
        }

        // ============ ALAN İŞLEMLERİ ============

        public async Task<List<AvailableField>> GetAllFieldsAsync()
        {
            await EnsureInitializedAsync();
            return await _database.Table<AvailableField>().ToListAsync();
        }

        public async Task<AvailableField> GetFieldByNameAsync(string fieldName)
        {
            await EnsureInitializedAsync();
            return await _database.Table<AvailableField>()
                .Where(x => x.Field_Name == fieldName)
                .FirstOrDefaultAsync();
        }

        public async Task<int> AddFieldAsync(AvailableField field)
        {
            await EnsureInitializedAsync();
            field.Created_At = DateTime.Now;
            return await _database.InsertAsync(field);
        }

        public async Task<int> UpdateFieldAsync(AvailableField field)
        {
            await EnsureInitializedAsync();
            return await _database.UpdateAsync(field);
        }

        public async Task<int> DeleteFieldAsync(AvailableField field)
        {
            await EnsureInitializedAsync();
            return await _database.DeleteAsync(field);
        }

        // ============ İSTATİSTİKLER ============

        public async Task<Dictionary<string, int>> GetDocumentCountByTypeAsync()
        {
            await EnsureInitializedAsync();
            var documents = await GetAllDocumentsAsync();
            return documents.GroupBy(record => record.Document_Type)
                           .ToDictionary(g => g.Key, g => g.Count());
        }

        public async Task<int> GetTotalDocumentCountAsync()
        {
            await EnsureInitializedAsync();
            return await _database.Table<ArchiveRecord>().CountAsync();
        }

        // ============ ✅ ÖDÜNÇ ALMA SİSTEMİ ============

        public async Task<List<BorrowedRecord>> GetActiveBorrowedDocumentsAsync()
        {
            await EnsureInitializedAsync();
            return await _database.Table<BorrowedRecord>()
                .Where(b => !b.Is_Returned)
                .ToListAsync();
        }

        public async Task<List<BorrowedRecord>> GetAllBorrowedDocumentsAsync()
        {
            await EnsureInitializedAsync();
            return await _database.Table<BorrowedRecord>().ToListAsync();
        }

        public async Task<List<BorrowedRecord>> GetReturnedDocumentsAsync()
        {
            await EnsureInitializedAsync();
            return await _database.Table<BorrowedRecord>()
                .Where(b => b.Is_Returned)
                .ToListAsync();
        }

        public async Task<bool> IsDocumentCurrentlyBorrowedAsync(int archiveId)
        {
            await EnsureInitializedAsync();
            var count = await _database.Table<BorrowedRecord>()
                .Where(b => b.ArchiveRecordId == archiveId && !b.Is_Returned)
                .CountAsync();
            return count > 0;
        }

        public async Task<BorrowedRecord> GetActiveBorrowRecordAsync(int archiveId)
        {
            await EnsureInitializedAsync();
            return await _database.Table<BorrowedRecord>()
                .Where(b => b.ArchiveRecordId == archiveId && !b.Is_Returned)
                .FirstOrDefaultAsync();
        }

        public async Task<BorrowedRecord> BorrowDocumentAsync(int archiveId, string borrowerName)
        {
            await EnsureInitializedAsync();

            if (string.IsNullOrWhiteSpace(borrowerName))
                throw new ArgumentException("Borrower name cannot be empty!");

            var document = await GetDocumentByIdAsync(archiveId);
            if (document == null)
                throw new InvalidOperationException("Document not found in archive!");

            var isAlreadyBorrowed = await IsDocumentCurrentlyBorrowedAsync(archiveId);
            if (isAlreadyBorrowed)
            {
                var existingRecord = await GetActiveBorrowRecordAsync(archiveId);
                throw new InvalidOperationException(
                    $"This document is already borrowed by '{existingRecord.Borrower_Name}' " +
                    $"since {existingRecord.Borrow_Date:dd/MM/yyyy}."
                );
            }

            var borrowRecord = new BorrowedRecord
            {
                ArchiveRecordId = document.Id,
                Document_Type = document.Document_Type,
                Course_ID = document.Course_ID,
                Teacher_Name = document.Teacher_Name,
                Year = document.Year,
                Semester = document.Semester,
                Archive_Date = document.Archive_Date,
                Location = document.Location,
                Fields_Json = document.Fields_Json,
                Borrower_Name = borrowerName.Trim(),
                Borrow_Date = DateTime.Now,
                Is_Returned = false
            };

            await _database.InsertAsync(borrowRecord);
            return borrowRecord;
        }

        public async Task ReturnDocumentAsync(BorrowedRecord record)
        {
            await EnsureInitializedAsync();

            if (record.Is_Returned)
                throw new InvalidOperationException("This document has already been returned!");

            record.Is_Returned = true;
            record.Return_Date = DateTime.Now;

            await _database.UpdateAsync(record);
        }

        public async Task<List<BorrowedRecord>> GetBorrowedDocumentsByPersonAsync(string borrowerName)
        {
            await EnsureInitializedAsync();
            return await _database.Table<BorrowedRecord>()
                .Where(b => b.Borrower_Name == borrowerName && !b.Is_Returned)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetBorrowingStatisticsAsync()
        {
            await EnsureInitializedAsync();

            var allRecords = await GetAllBorrowedDocumentsAsync();
            var activeRecords = allRecords.Where(b => !b.Is_Returned).ToList();

            return new Dictionary<string, int>
            {
                { "Total_Borrowed", allRecords.Count },
                { "Currently_Borrowed", activeRecords.Count },
                { "Returned", allRecords.Count(b => b.Is_Returned) }
            };
        }
    }
}