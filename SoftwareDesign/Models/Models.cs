using SQLite;
using System;

namespace SoftwareDesign.Models
{
    // =========================================================
    // 1. ANA BELGE TABLOSU (Arşivdeki Belgeler)
    // =========================================================
    [Table("Documents")]
    public class ArchiveRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Belge türü (Midterms, Finals, Quiz1, vb.)
        public string Document_Type { get; set; }

        // Dinamik alanlar - JSON formatında saklanacak
        public string Fields_Json { get; set; }

        // Arama için önemli genel alanlar (nullable)
        public string Course_ID { get; set; }
        public string Teacher_Name { get; set; }
        public string Year { get; set; }
        public string Semester { get; set; }
        public string Archive_Date { get; set; }
        public string Location { get; set; }

        public DateTime Created_At { get; set; } = DateTime.Now;
        public DateTime Updated_At { get; set; } = DateTime.Now;
    }

    // =========================================================
    // 2. ÖDÜNÇ VERİLENLER TABLOSU (Snapshot Pattern + History)
    // =========================================================
    [Table("BorrowedRecords")]
    public class BorrowedRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int ArchiveRecordId { get; set; } // Asıl belgeyle bağlantı

        // --- SNAPSHOT (Belge değişse/silinse bile korunur) ---
        public string Document_Type { get; set; }
        public string Course_ID { get; set; }
        public string Teacher_Name { get; set; }
        public string Year { get; set; }
        public string Semester { get; set; }
        public string Archive_Date { get; set; }
        public string Location { get; set; }
        public string Fields_Json { get; set; }

        // --- ÖDÜNÇ BİLGİLERİ ---
        public string Borrower_Name { get; set; }
        public DateTime Borrow_Date { get; set; }

        // --- İADE BİLGİSİ (Geçmiş için) ---
        public bool Is_Returned { get; set; } = false;
        public DateTime? Return_Date { get; set; }

        // --- HESAPLANAN ALANLAR (Veritabanına kaydedilmez) ---

        [Ignore]
        public string Duration_Counter
        {
            get
            {
                if (Is_Returned && Return_Date.HasValue)
                {
                    var totalDays = (Return_Date.Value - Borrow_Date).Days;
                    return $"✅ Returned ({totalDays} days)";
                }

                // Sadece bugüne kadar kaç gün geçtiğini göster
                var currentDays = (DateTime.Now - Borrow_Date).Days;

                if (currentDays == 0)
                    return "🆕 Today";
                else if (currentDays == 1)
                    return "⏱️ 1 Day";
                else
                    return $"⏱️ {currentDays} Days";
            }
        }

        [Ignore]
        public string Display_Summary
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();

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

                // Custom fields ekle (sistem fieldlarını filtrele)
                if (!string.IsNullOrEmpty(Fields_Json))
                {
                    try
                    {
                        var allFields = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(Fields_Json);
                        if (allFields != null)
                        {
                            // Sistem fieldlarını filtrele - sadece custom fieldlar kalsın
                            var systemFields = new System.Collections.Generic.HashSet<string>
                            {
                                "Course_ID", "Teacher_Name", "Year", "Semester", "Archive_Date", "Location"
                            };

                            foreach (var field in allFields)
                            {
                                if (!systemFields.Contains(field.Key))
                                {
                                    parts.Add($"{field.Key}: {field.Value}");
                                }
                            }
                        }
                    }
                    catch { }
                }

                return string.Join(" | ", parts);
            }
        }
    }

    // =========================================================
    // 3. BELGE TÜRÜ TANIMLARI
    // =========================================================
    [Table("DocumentTypes")]
    public class DocumentType
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Type_Name { get; set; }
        public string Required_Fields_Json { get; set; }
        public bool Is_System_Type { get; set; }
        public string Description { get; set; }

        public DateTime Created_At { get; set; } = DateTime.Now;
    }

    // =========================================================
    // 4. ALAN TANIMLARI (Dynamic Fields)
    // =========================================================
    [Table("AvailableFields")]
    public class AvailableField
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Field_Name { get; set; }
        public string Display_Name { get; set; }
        public string Field_Type { get; set; }
        public string Picker_Options_Json { get; set; }
        public bool Is_System_Field { get; set; }

        public DateTime Created_At { get; set; } = DateTime.Now;
    }
}