/*
 * [CtrInvoice]
 * Copyright (C) [2025] [Tariq Khan / Burns & McDonnell]
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */


using System.Data.SQLite;
using System.IO;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Font;
using iText.Kernel.Colors;
using iText.IO.Font.Constants;

namespace CtrInvoice.Services
{
    public class CableDetailsService
    {
        private readonly string _socoCablesDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "Resources", "Database", "soco_cables_list.db");
        public void ProcessDatabase(string dbFilePath, string pdfPath)
        {
            if (!File.Exists(dbFilePath) || !File.Exists(_socoCablesDbPath) || !File.Exists(pdfPath))
            {
                Console.WriteLine("One or more required files not found.");
                return;
            }

            // Define the output PDF path in the same folder as the input PDF
            string inputPdfPath = Path.Combine(Path.GetDirectoryName(pdfPath), "highlighted_BOW.pdf");
            string outputPdfPath = inputPdfPath + ".tmp";

            using var conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;");
            conn.Open();

            // Extract cable details from BOW_table
            var bowCablesList = ExtractBowCablesList(conn);

            // Lookup KeyMark from cables_list in soco_cables_list.db
            LookupKeyMarks(bowCablesList);

            // Insert new KeyMark rows into BOW_table
            InsertKeyMarksIntoBowTable(conn, bowCablesList);

            // Overlay keymark values onto the PDF
            OverlayKeymarksOnPdf(conn, inputPdfPath, outputPdfPath);
            
            conn.Close();

            Console.WriteLine($"Output PDF with keymarks saved at: {outputPdfPath}");
        }
        private List<CableEntry> ExtractBowCablesList(SQLiteConnection conn)
        {
            var bowCablesList = new List<CableEntry>();

            string query = @"
                SELECT Word, Tag, Sheet, Item, X1, Y1
                FROM BOW_table 
                WHERE Tag IN ('size', 'insulation', 'conductors', 'parallel_cables')
                ORDER BY Sheet, Item, Tag;";

            using var cmd = new SQLiteCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            CableEntry currentEntry = null;
            double x1 = 0, y1 = 0;  // Store X1, Y1 from the row where Tag = 'size'

            while (reader.Read())
            {
                string word = reader["Word"].ToString();
                string tag = reader["Tag"].ToString();
                int sheet = Convert.ToInt32(reader["Sheet"]);
                int item = Convert.ToInt32(reader["Item"]);
                double currentX1 = Convert.ToDouble(reader["X1"]);
                double currentY1 = Convert.ToDouble(reader["Y1"]);

                // When Item changes, store the previous entry
                if (currentEntry == null || currentEntry.Item != item || currentEntry.Sheet != sheet)
                {
                    if (currentEntry != null)
                        bowCablesList.Add(currentEntry);

                    // Reset and start a new entry
                    currentEntry = new CableEntry { Sheet = sheet, Item = item, X1 = x1, Y1 = y1 };
                }

                // Capture X1, Y1 only from the row where Tag = 'size'
                if (tag == "size")
                {
                    x1 = currentX1;
                    y1 = currentY1;
                    currentEntry.X1 = x1 + 175;
                    currentEntry.Y1 = y1 - 5;
                }

                // Append values for the corresponding tags
                switch (tag)
                {
                    case "size":
                        currentEntry.Size = string.IsNullOrEmpty(currentEntry.Size) 
                            ? word : currentEntry.Size + " " + word;
                        break;
                    case "parallel_cables":
                        currentEntry.ParallelCables = string.IsNullOrEmpty(currentEntry.ParallelCables) 
                            ? word : currentEntry.ParallelCables + " " + word;
                        break;
                    case "conductors":
                        currentEntry.Conductors = string.IsNullOrEmpty(currentEntry.Conductors) 
                            ? word : currentEntry.Conductors + " " + word;
                        break;
                    case "insulation":
                        currentEntry.Insulation = string.IsNullOrEmpty(currentEntry.Insulation) 
                            ? word : currentEntry.Insulation + " " + word;
                        break;
                }
            }

            if (currentEntry != null)
                bowCablesList.Add(currentEntry);

            return bowCablesList;
        }
        private void LookupKeyMarks(List<CableEntry> bowCablesList)
        {
            using var conn = new SQLiteConnection($"Data Source={_socoCablesDbPath};Version=3;");
            conn.Open();

            foreach (var cable in bowCablesList)
            {
                string query = @"
                    SELECT KeyMark FROM cables_list 
                    WHERE Size = @Size 
                    AND ParallelCables = @ParallelCables 
                    AND Conductors = @Conductors 
                    AND Insulation = @Insulation 
                    LIMIT 1;";

                using var cmd = new SQLiteCommand(query, conn);
                cmd.Parameters.AddWithValue("@Size", cable.Size ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@ParallelCables", cable.ParallelCables ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Conductors", cable.Conductors ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Insulation", cable.Insulation ?? (object)DBNull.Value);

                var result = cmd.ExecuteScalar();
                if (result != null)
                    cable.KeyMark = result.ToString();
            }

            conn.Close();
        }
        private void InsertKeyMarksIntoBowTable(SQLiteConnection conn, List<CableEntry> bowCablesList)
        {
            using var transaction = conn.BeginTransaction();

            string insertQuery = @"
                INSERT INTO BOW_table (Word, X1, Y1, X2, Y2, Sheet, PageRotation, WordRotation, Tag, Item, ColorFlag)
                VALUES (@Word, @X1, @Y1, 0, 0, @Sheet, 0, 0, 'keymark', @Item, 0);";

            using var cmd = new SQLiteCommand(insertQuery, conn);

            foreach (var cable in bowCablesList)
            {
                if (string.IsNullOrEmpty(cable.KeyMark))
                    continue;

                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@Word", cable.KeyMark);
                cmd.Parameters.AddWithValue("@X1", cable.X1);
                cmd.Parameters.AddWithValue("@Y1", cable.Y1);
                cmd.Parameters.AddWithValue("@Sheet", cable.Sheet);
                cmd.Parameters.AddWithValue("@Item", cable.Item);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        private void OverlayKeymarksOnPdf(SQLiteConnection conn, string inputPdfPath, string outputPdfPath)
        {
            string query = @"
                SELECT Word, Sheet, X1, Y1 
                FROM BOW_table 
                WHERE Tag = 'keymark'
                ORDER BY Sheet;";

            var keymarks = new List<(string Word, int Sheet, double X1, double Y1)>();

            using var cmd = new SQLiteCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                keymarks.Add((
                    reader["Word"].ToString(),
                    Convert.ToInt32(reader["Sheet"]),
                    Convert.ToDouble(reader["X1"]),
                    Convert.ToDouble(reader["Y1"])
                ));
            }

            using var pdfReader = new PdfReader(inputPdfPath);
            using var pdfWriter = new PdfWriter(outputPdfPath);
            using var pdfDocument = new PdfDocument(pdfReader, pdfWriter);

            PdfFont keymarkFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            
            foreach (var (word, sheet, x1, y1) in keymarks)
            {
                int pageNumber = sheet; // Assuming Sheet corresponds to the PDF page number
                if (pageNumber > 0 && pageNumber <= pdfDocument.GetNumberOfPages())
                {
                    var page = pdfDocument.GetPage(pageNumber);
                    var canvas = new PdfCanvas(page);
                    canvas.BeginText()
                        .SetFontAndSize(keymarkFont, 10)
                        .SetColor(ColorConstants.BLUE, true)
                        .MoveText((float)x1, (float)y1)
                        .ShowText(word)
                        .EndText();
                }
            }

            pdfDocument.Close();
            
            // Overwrite the original file
            File.Delete(inputPdfPath);
            File.Move(outputPdfPath, inputPdfPath);
        }
        
        
        
    }
    public class CableEntry
    {
        public string? KeyMark { get; set; }
        public string? ParallelCables { get; set; }
        public string? Conductors { get; set; }
        public string? Size { get; set; }
        public string? Insulation { get; set; }
        public int Sheet { get; set; }
        public int Item { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
    }
}
