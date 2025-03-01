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
using CtrInvoice.Models;

namespace CtrInvoice.Services;

public class DwgTitleService
    {
        private static readonly List<string> RequiredTypes = new()
        {
            "facility_name", "facility_id", "dwg_title1","dwg_title2", "dwg_scale", "dwg_size", "dwg_number", "dwg_sheet",
            "dwg_rev", "dwg_type"
        };
        public void ProcessDatabase(string dbFilePath)
        {
            if (!File.Exists(dbFilePath))
            {
                Console.WriteLine("Database file not found.");
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
                {
                    connection.Open();
                    UpdateRowsBasedOnConditions(connection);
                    DeleteNullRows(connection);
                    UpdateColorFlag(connection);
                    Console.WriteLine($"Tags assigned to title block texts in database");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing database: {ex.Message}");
            }
        }
        private void UpdateRowsBasedOnConditions(SQLiteConnection connection)
        {
            
            // Dictionary to store the required MinX and MaxY values per sheet
            var sheetBounds = new Dictionary<int, (double MinX, double MaxY)>();

            // Retrieve all distinct sheets
            string sheetQuery = "SELECT DISTINCT Sheet FROM DWG_table";
            var sheets = new List<int>();

            using (var sheetCmd = new SQLiteCommand(sheetQuery, connection))
            using (var sheetReader = sheetCmd.ExecuteReader())
            {
                while (sheetReader.Read())
                {
                    sheets.Add(sheetReader.GetInt32(0));
                }
            }

            // Process each sheet individually
            foreach (int sheet in sheets)
            {
                string findFacilityQuery = @"
                    SELECT X1, Y1 
                    FROM DWG_table 
                    WHERE Sheet = @Sheet 
                    ORDER BY X1 ASC
                ";

                using (var facilityCmd = new SQLiteCommand(findFacilityQuery, connection))
                {
                    facilityCmd.Parameters.AddWithValue("@Sheet", sheet);

                    using (var facilityReader = facilityCmd.ExecuteReader())
                    {
                        while (facilityReader.Read())
                        {
                            double x1 = facilityReader.IsDBNull(0) ? 0 : facilityReader.GetDouble(0);
                            double y1 = facilityReader.IsDBNull(1) ? 0 : facilityReader.GetDouble(1);

                            // Check if this row has "FACILITY" in the Word column
                            string checkWordQuery = @"
                                SELECT COUNT(*) 
                                FROM DWG_table 
                                WHERE Sheet = @Sheet AND X1 = @X1 AND Word = 'FACILITY'
                            ";

                            using (var checkCmd = new SQLiteCommand(checkWordQuery, connection))
                            {
                                checkCmd.Parameters.AddWithValue("@Sheet", sheet);
                                checkCmd.Parameters.AddWithValue("@X1", x1);

                                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                                if (count > 0)
                                {
                                    // Store MinX and MaxY for this sheet
                                    sheetBounds[sheet] = (x1, y1);
                                    break; // Stop once we find the first "FACILITY"
                                }
                            }
                        }
                    }
                }
            }
            
            // // Gather min X1 and max Y1 for each sheet in advance
            // var sheetBounds = new Dictionary<int, (double MinX, double MaxY)>();
            //
            // string boundsQuery = @"
            //     SELECT
            //         Sheet,
            //         MIN(X1) AS MinX,
            //         MAX(Y1) AS MaxY
            //     FROM DWG_table
            //     GROUP BY Sheet
            // ";
            //
            // using (var boundsCmd = new SQLiteCommand(boundsQuery, connection))
            // using (var boundsReader = boundsCmd.ExecuteReader())
            // {
            //     while (boundsReader.Read())
            //     {
            //         int sheetNumber = boundsReader.GetInt32(0);
            //         double minX = boundsReader.IsDBNull(1) ? 0 : boundsReader.GetDouble(1);
            //         double maxY = boundsReader.IsDBNull(2) ? 0 : boundsReader.GetDouble(2);
            //
            //         sheetBounds[sheetNumber] = (minX, maxY);
            //     }
            // }
            //
            string selectQuery = @"
                SELECT rowid, X1, Y1, Sheet
                FROM DWG_table
                ORDER BY Sheet, Y1 DESC;";
            
            var processedSheets = new HashSet<int>();
            
            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = 0;
                double y1_current = 0;
                double x1_current = 0;
                
                // This set will track all unique “Type” entries for the current ItemNumber
                var currentItemTags = new HashSet<string>();

                using (var transaction = connection.BeginTransaction())
                using (var updateCmd = new SQLiteCommand("UPDATE DWG_table SET Tag = @WordTag WHERE rowid = @RowId;",
                           connection, transaction))
                {
                    updateCmd.Parameters.Add(new SQLiteParameter("@WordTag"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@RowId"));

                    while (reader.Read())
                    {
                        int rowId = reader.GetInt32(0);
                        double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        double y1 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        int sheetNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        
                        if (sheetNumber != lastSheetNumber)
                        {
                            if (lastSheetNumber != 0 )
                            {
                                MissingTag(connection, currentItemTags, lastSheetNumber, x1_current, y1_current);
                                currentItemTags.Clear();
                            }
                            if (sheetBounds.TryGetValue(sheetNumber, out var bounds))
                            {
                                x1_current = bounds.MinX;
                                y1_current = bounds.MaxY;
                            }
                            processedSheets.Add(sheetNumber);
                            lastSheetNumber = sheetNumber;
                        }
                        string tag = IsValidTag(x1, y1, ref x1_current, ref y1_current);

                        if (!string.IsNullOrEmpty(tag))
                        {
                            updateCmd.Parameters["@WordTag"].Value = tag;
                            updateCmd.Parameters["@RowId"].Value = rowId;
                            updateCmd.ExecuteNonQuery();
                            
                            currentItemTags.Add(tag);
                        }
                    }
                    if (lastSheetNumber != 0)
                    {
                        MissingTag(connection, currentItemTags, lastSheetNumber, x1_current, y1_current);
                        currentItemTags.Clear();
                    }
                    transaction.Commit();
                }
            }
            
            foreach (var sheetNumber in sheetBounds.Keys)
            {
                if (!processedSheets.Contains(sheetNumber))
                {
                    var bounds = sheetBounds[sheetNumber];
                    MissingTag(connection, new HashSet<string>(), sheetNumber, bounds.MinX, bounds.MaxY);
                }
            }
        }
        private void MissingTag(SQLiteConnection connection, HashSet<string> existingTags, int sheetNumber,
            double x1_current, double y1_current)
        {
            
            // For convenience, a local function that inserts a row
            void InsertMissingRow(string type, double x1, double y1)
            {
                double width = 0;
                switch (type)
                {
                    case "facility_name":    width = 300; break;
                    case "facility_id":      width = 40; break;
                    case "dwg_title1":       width = 300; break;
                    case "dwg_type":         width = 40; break;
                    case "dwg_scale":        width = 40; break;
                    case "dwg_size":         width = 30; break;
                    case "dwg_number":       width = 40; break;
                    case "dwg_sheet":        width = 40; break;
                    case "dwg_rev":          width = 20; break;
                }
                double x2 = x1 + width;
                double y2 = y1 + 5.77;
        
                // Now insert a new row in DWG_table
                string insertQuery = @"
                    INSERT INTO DWG_table
                    (Sheet, Item, Tag, X1, Y1, X2, Y2, WordRotation, PageRotation, Word, ColorFlag)
                    VALUES
                    (@sheetNumber, @itemNumber, @tag, @x1, @y1, @x2, @y2, @wordRotation, @pageRotation, @word, @colorFlag);
                ";
        
                using var cmd = new SQLiteCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@sheetNumber", sheetNumber);
                cmd.Parameters.AddWithValue("@itemNumber", 0);
                cmd.Parameters.AddWithValue("@tag", type);
                cmd.Parameters.AddWithValue("@x1", x1);
                cmd.Parameters.AddWithValue("@y1", y1);
                cmd.Parameters.AddWithValue("@x2", x2);
                cmd.Parameters.AddWithValue("@y2", y2);
                cmd.Parameters.AddWithValue("@word", string.Empty);
                cmd.Parameters.AddWithValue("@wordRotation", 0);
                cmd.Parameters.AddWithValue("@pageRotation", 0);
                cmd.Parameters.AddWithValue("@colorFlag", 0);
                cmd.ExecuteNonQuery();
            }
        
            // Check each required type; if missing, insert it
            foreach (var requiredTag in RequiredTypes)
            {
                if (!existingTags.Contains(requiredTag))
                {
                    switch (requiredTag)
                    {
                        case "facility_name":
                            InsertMissingRow("facility_name", x1_current + 70, y1_current - 16);
                            break;
                        case "dwg_title1":
                            InsertMissingRow("dwg_title1", x1_current + 40, y1_current - 30);
                            break;
                        case "dwg_scale":
                            InsertMissingRow("dwg_scale", x1_current + 40, y1_current - 64);
                            break;
                        case "dwg_type":
                            InsertMissingRow("dwg_type", x1_current + 40, y1_current - 54);
                            break;
                        case "facility_id":
                            InsertMissingRow("facility_id", x1_current + 145, y1_current - 72);
                            break;
                        case "dwg_number":
                            InsertMissingRow("dwg_number", x1_current + 295, y1_current - 78);
                            break;
                        case "dwg_size":
                            InsertMissingRow("dwg_size", x1_current + 210, y1_current - 78);
                            break;
                        case "dwg_sheet":
                            InsertMissingRow("dwg_sheet", x1_current + 390, y1_current - 78);
                            break;
                        case "dwg_rev":
                            InsertMissingRow("dwg_rev", x1_current + 444, y1_current - 70);
                            break;
                    }
                }
            }
        }
        private string IsValidTag(double x1, double y1, ref double x1_current, ref double y1_current)
    {
        double x1_relative = Math.Abs(x1_current - x1);
        double y1_relative = Math.Abs(y1_current - y1);
        
            return x1 switch
            { 
                 _ when  x1_relative.IsBetween(0, 450) && y1_relative.IsBetween(14, 18) => "facility_name",
                 _ when  x1_relative.IsBetween(30, 450) && y1_relative.IsBetween(28, 34) => "dwg_title1",
                 _ when  x1_relative.IsBetween(30, 450) && y1_relative.IsBetween(40, 48) => "dwg_title2",
                 _ when  x1_relative.IsBetween(30, 450) && y1_relative.IsBetween(62, 68) => "dwg_scale",
                 _ when  x1_relative.IsBetween(30, 130) && y1_relative.IsBetween(52, 56) => "dwg_type",
                 _ when  x1_relative.IsBetween(145, 200) && y1_relative.IsBetween(70, 74) => "facility_id",
                 _ when  x1_relative.IsBetween(255, 375) && y1_relative.IsBetween(76, 80) => "dwg_number",
                 _ when  x1_relative.IsBetween(200, 237) && y1_relative.IsBetween(76, 80) => "dwg_size",
                 _ when  x1_relative.IsBetween(390, 420) && y1_relative.IsBetween(76, 80) => "dwg_sheet",
                 _ when  x1_relative.IsBetween(434, 450) && y1_relative.IsBetween(70, 80) => "dwg_rev",
                 _ => string.Empty
            };
    }
        private void DeleteNullRows(SQLiteConnection connection)
        {
            var deleteQuery = @"
                DELETE FROM DWG_table
                WHERE Tag = 'NA';
            ";

            using var cmd = new SQLiteCommand(deleteQuery, connection);
            cmd.ExecuteNonQuery();
        }
        private void UpdateColorFlag(SQLiteConnection connection)
        {
            string updateQuery = @"
        UPDATE DWG_table
        SET ColorFlag = 2
        WHERE (Word IS NULL OR TRIM(Word) = '');";

            using var cmd = new SQLiteCommand(updateQuery, connection);
            int affectedRows = cmd.ExecuteNonQuery();
        }

    }
    