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

namespace CtrInvoice.Services
{
    public class BmcdInvoiceService
    {
        private static readonly List<string> RequiredTypes = new()
        {
            "invoice_number", "client_contract", "client_dpn", "state", "zip", "invoice_end_date"
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
                    Console.WriteLine($"BMCD invoice tags assigned in database");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing database: {ex.Message}");
            }
        }

        private void UpdateRowsBasedOnConditions(SQLiteConnection connection)
        {
            // Gather min X1 and max Y1 for each sheet in advance
            var sheetBounds = new Dictionary<int, (double MinX, double MaxY)>();

            string boundsQuery = @"
                SELECT
                    Sheet,
                    MIN(X1) AS MinX,
                    MAX(Y1) AS MaxY
                FROM INVOICE_table
                GROUP BY Sheet
            ";

            using (var boundsCmd = new SQLiteCommand(boundsQuery, connection))
            using (var boundsReader = boundsCmd.ExecuteReader())
            {
                while (boundsReader.Read())
                {
                    int sheetNumber = boundsReader.GetInt32(0);
                    double minX = boundsReader.IsDBNull(1) ? 0 : boundsReader.GetDouble(1);
                    double maxY = boundsReader.IsDBNull(2) ? 0 : boundsReader.GetDouble(2);

                    sheetBounds[sheetNumber] = (minX, maxY);
                    
                }
            }
            
            string selectQuery = @"
                SELECT rowid, X1, Y1, X2, Y2, Sheet, Word
                FROM INVOICE_table
                ORDER BY Sheet, Y1 DESC;";
            
            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = -1;
                int currentItemNumber = 1;
                
                double y1_current = 0;
                double x1_current = 0;
                bool y1_current_set = false;
                string siteId = string.Empty;
                
                // This set will track all unique “Type” entries for the current ItemNumber
                var currentItemTags = new HashSet<string>();
                
                using (var transaction = connection.BeginTransaction())
                using (var updateCmd =
                       new SQLiteCommand(
                           "UPDATE INVOICE_table SET Tag = @WordTag WHERE rowid = @RowId;",
                           connection, transaction))
                {
                    updateCmd.Parameters.Add(new SQLiteParameter("@WordTag"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@RowId"));

                    while (reader.Read())
                    {
                        int rowId = reader.GetInt32(0);
                        double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        double y1 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        double x2 = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                        double y2 = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
                        int sheetNumber = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                        string wordValue = reader.IsDBNull(6) ? string.Empty : reader.GetString(6).Trim();
                        
                        if (lastSheetNumber == -1 || sheetNumber != lastSheetNumber)
                        {
                            if (sheetBounds.TryGetValue(sheetNumber, out var bounds))
                            {
                                x1_current = bounds.MinX;
                                y1_current = bounds.MaxY;
                            }
                            lastSheetNumber = sheetNumber;
                            y1_current_set = false;
                            currentItemTags.Clear();
                        }
                        
                        string tag = IsValidTag(x1, y1, x2, y2, 
                            ref x1_current, ref y1_current, sheetNumber, wordValue);

                        if (!string.IsNullOrEmpty(tag))
                        {
                            
                            updateCmd.Parameters["@WordTag"].Value = tag;
                            updateCmd.Parameters["@RowId"].Value = rowId;
                            updateCmd.ExecuteNonQuery();
                            
                            // Add to our set of encountered tags
                            currentItemTags.Add(tag);
                        }
                    }
                    transaction.Commit();
                }
            }
        }
        private string IsValidTag(double x1, double y1, double x2, double y2, 
            ref double x1_current, ref double y1_current, int sheetNumber, string wordValue)
        {
            double x1_relative = Math.Abs(x1_current - x1);
            double y1_relative = Math.Abs(y1_current - y1);

            if (sheetNumber == 1)
            {
                // Extract invoice and project details
                if (x1_relative.IsBetween(440, 444))
                {
                    if (y1_relative.IsBetween(95, 99)) return "invoice_number";
                    if (y1_relative.IsBetween(104, 108)) return "federal_id";
                    if (y1_relative.IsBetween(113, 117)) return "client_contract";
                    if (y1_relative.IsBetween(122, 126)) return "client_dpn";
                }
                
                // Extract invoice end date
                if (x1 > x1_current + 100
                    && x2 < x1_current + 160
                    && y1 > y1_current - 290
                    && y2 < y1_current - 265)
                {
                    
                    return "invoice_end_date";            
                }
                
                // Extract site ID from address in the invoice
                if (x1 > x1_current 
                    && x2 < x1_current + 270 
                    && y1 > y1_current - 233 
                    && y2 < y1_current - 153)
                {
                    if (wordValue == "IN" 
                        || wordValue == "KY"
                        || wordValue == "OH"
                        || wordValue == "NC"
                        || wordValue == "SC"
                        || wordValue == "FL"
                        )
                    {
                        return "state"; 
                    }
                }
            }
            return string.Empty;
        }
        
        private void DeleteNullRows(SQLiteConnection connection)
        {
            var deleteQuery = @"
                DELETE FROM INVOICE_table
                WHERE Tag = 'NA' ;";
        
            using var cmd = new SQLiteCommand(deleteQuery, connection);
            cmd.ExecuteNonQuery();
            
        }
        
        private void UpdateColorFlag(SQLiteConnection connection)
        {
            string updateQuery = @"
            UPDATE INVOICE_table
            SET ColorFlag = 2
            WHERE (Word IS NULL OR TRIM(Word) = '');";

            using var cmd = new SQLiteCommand(updateQuery, connection);
            int affectedRows = cmd.ExecuteNonQuery();
        }

        
    }
}
