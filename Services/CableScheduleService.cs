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

namespace CtrInvoice.Services
{
    public class CableScheduleService
    {
        private static readonly List<string> RequiredTypes = new()
        {
            "cable_tag", "from_desc", "to_desc", "function", "size", "insulation",
            "from_ref", "to_ref", "voltage", "conductors", "length", "parallel_cables"
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
                    Console.WriteLine($"BOW Tags assigned to texts in database");
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
                FROM BOW_table
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
                SELECT rowid, X1, Y1, Sheet
                FROM BOW_table
                ORDER BY Sheet, Y1 DESC;";
            

            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = -1;
                int currentItemNumber = 1;
                
                double y1_current = 0;
                double x1_current = 0;
                bool y1_current_set = false;
                
                // This set will track all unique “Type” entries for the current ItemNumber
                var currentItemTags = new HashSet<string>();

                using (var transaction = connection.BeginTransaction())
                using (var updateCmd =
                       new SQLiteCommand(
                           "UPDATE BOW_table SET Tag = @WordTag, Item = @ItemNumber WHERE rowid = @RowId;",
                           connection, transaction))
                {
                    updateCmd.Parameters.Add(new SQLiteParameter("@WordTag"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@ItemNumber"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@RowId"));

                    while (reader.Read())
                    {
                        int rowId = reader.GetInt32(0);
                        double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        double y1 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        int sheetNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);


                        if (lastSheetNumber == -1 || sheetNumber != lastSheetNumber)
                        {
                            
                            if (lastSheetNumber != -1)
                            {
                                MissingTag(connection, currentItemTags, lastSheetNumber, currentItemNumber, x1_current, y1_current);
                                currentItemTags.Clear();
                            }

                            if (sheetBounds.TryGetValue(sheetNumber, out var bounds))
                            {
                                x1_current = bounds.MinX;
                                y1_current = bounds.MaxY;
                            }
                            currentItemNumber = 1;
                            lastSheetNumber = sheetNumber;
                            y1_current_set = false;
                            currentItemTags.Clear();
                        }

                        // filters out all the lines below report but above the cable item
                        if (y1 > y1_current)
                        {
                            continue;
                        }

                        if (!y1_current_set)
                        {
                            y1_current = y1;
                            y1_current_set = true;
                        }

                        // Switches control for Y1 current to the second line of a cable item
                        if (Math.Abs(y1 - y1_current) > 18)
                        {
                            MissingTag(connection, currentItemTags, sheetNumber, currentItemNumber, x1_current, y1_current);
                        
                            // Reset for the new item
                            currentItemTags.Clear();
                            currentItemNumber++;
                            y1_current = y1;
                        }

                        string tag = IsValidTag(x1, y1, ref x1_current, ref y1_current);

                        if (!string.IsNullOrEmpty(tag))
                        {
                            updateCmd.Parameters["@WordTag"].Value = tag;
                            updateCmd.Parameters["@ItemNumber"].Value = currentItemNumber;
                            updateCmd.Parameters["@RowId"].Value = rowId;
                            updateCmd.ExecuteNonQuery();
                            
                            // Add to our set of encountered tags
                            currentItemTags.Add(tag);
                        }
                    }
                    if (lastSheetNumber != -1 && currentItemTags.Count > 0)
                    {
                        MissingTag(connection, currentItemTags, lastSheetNumber, currentItemNumber, x1_current, y1_current);
                        currentItemTags.Clear();
                    }
                    transaction.Commit();
                }
            }
        }
        
        private void MissingTag(SQLiteConnection connection, HashSet<string> existingTags, int sheetNumber,
            int itemNumber, double x1_current, double y1_current)
        {
            double line1Y = y1_current;
            double line2Y = y1_current - 12;
            
        
            // For convenience, a local function that inserts a row
            void InsertMissingRow(string type, double x1, double y1)
            {
                double x2 = 0;
                double y2 = y1 + 5.77; 
                
                double width = 0;
                switch (type)
                {
                    // first-line tags
                    case "cable_tag":      width = 40; break;
                    case "from_desc":      width = 40; break;
                    case "to_desc":        width = 40; break;
                    case "function":       width = 40; break;
                    case "size":           width = 10; break;
                    case "insulation":     width = 40; break;
        
                    // second-line tags
                    case "from_ref":       width = 40; break;
                    case "to_ref":         width = 40; break;
                    case "voltage":        width = 40; break;
                    case "conductors":     width = 10; break;
                    case "length":         width = 10; break;
                }
                x2 = x1 + width;
        
                // Now insert a new row in BOW_table
                string insertQuery = @"
                    INSERT INTO BOW_table 
                    (Sheet, Item, Tag, X1, Y1, X2, Y2, WordRotation, PageRotation, Word, ColorFlag)
                    VALUES
                    (@sheetNumber, @itemNumber, @tag, @x1, @y1, @x2, @y2, @wordRotation, @pageRotation, @word, @colorFlag);
                ";
        
                using var cmd = new SQLiteCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@sheetNumber", sheetNumber);
                cmd.Parameters.AddWithValue("@itemNumber", itemNumber);
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
                        // First line tags
                        case "cable_tag":
                            InsertMissingRow("cable_tag", x1_current, line1Y);
                            break;
                        case "from_desc":
                            InsertMissingRow("from_desc", x1_current + 110, line1Y);
                            break;
                        case "to_desc":
                            InsertMissingRow("to_desc", x1_current + 247, line1Y);
                            break;
                        case "function":
                            InsertMissingRow("function", x1_current + 388, line1Y);
                            break;
                        case "size":
                            InsertMissingRow("size", x1_current + 516, line1Y);
                            break;
                        case "insulation":
                            InsertMissingRow("insulation", x1_current + 570, line1Y);
                            break;
        
                        // Second line tags
                        case "from_ref":
                            InsertMissingRow("from_ref", x1_current + 110, line2Y);
                            break;
                        case "to_ref":
                            InsertMissingRow("to_ref", x1_current + 247, line2Y);
                            break;
                        case "voltage":
                            InsertMissingRow("voltage", x1_current + 424, line2Y);
                            break;
                        case "conductors":
                            InsertMissingRow("conductors", x1_current + 500, line2Y);
                            break;
                        case "length":
                            InsertMissingRow("length", x1_current + 555, line2Y);
                            break;
                    }
                }
            }
        }
        
        private string IsValidTag(double x1, double y1, ref double x1_current, ref double y1_current)
        {
            if (Math.Abs(y1 - y1_current) < 5)
            {
                return x1 switch
                { 
                     _ when x1 > x1_current - 2 && x1 <= x1_current + 95 => "cable_tag",
                     _ when x1 > x1_current + 100 && x1 <= x1_current + 230 => "from_desc",
                     _ when x1 > x1_current + 231 && x1 <= x1_current + 360 => "to_desc",
                     _ when x1 > x1_current + 361 && x1 <= x1_current + 420 => "function",
                     _ when x1 > x1_current + 421 && x1 <= x1_current + 485 => "parallel_cables",
                     _ when x1 > x1_current + 486 && x1 <= x1_current + 530 => "size",
                     _ when x1 > x1_current + 540 && x1 <= x1_current + 700 => "insulation",
                    _ => string.Empty
                };
            }

            if (Math.Abs(y1 - y1_current) > 5)
            {
                return x1 switch
                {
                     _ when x1 > x1_current + 100 && x1 <= x1_current + 230 => "from_ref",
                     _ when x1 > x1_current + 231 && x1 <= x1_current + 360 => "to_ref",
                     _ when x1 > x1_current + 400 && x1 <= x1_current + 450 => "voltage",
                     _ when x1 > x1_current + 450 && x1 <= x1_current + 520 => "conductors",
                     _ when x1 > x1_current + 521 && x1 <= x1_current + 566 => "length", 
                     _ => string.Empty
                };
            }
            
            return string.Empty;
        }
        
        private void DeleteNullRows(SQLiteConnection connection)
        {
            var deleteQuery = @"
                DELETE FROM BOW_table
                WHERE Item = 0 ;";
        
            using var cmd = new SQLiteCommand(deleteQuery, connection);
            cmd.ExecuteNonQuery();
            
        }
        
        private void UpdateColorFlag(SQLiteConnection connection)
        {
            string updateQuery = @"
        UPDATE BOW_table
        SET ColorFlag = 2
        WHERE (Word IS NULL OR TRIM(Word) = '');";

            using var cmd = new SQLiteCommand(updateQuery, connection);
            int affectedRows = cmd.ExecuteNonQuery();
        }

        
    }
}
