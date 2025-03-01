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

namespace CtrInvoice.Services;

public class ComparisonLogic
{
    public void CompareDatabase(string dbFilePath)
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
                List<DwgEntry> dwgData = GetDwgEntries(connection);
                List<BowEntry> bowData = GetBowEntries(connection);
                CheckReferences(dwgData, bowData);
                UpdateDatabase(connection, dwgData, bowData);
                Console.WriteLine($"Cable schedule to drawings comparison done.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing database: {ex.Message}");
        }
    }
    private void CheckReferences(List<DwgEntry> dwgData, List<BowEntry> bowData)
         {
             foreach (BowEntry bowEntry in bowData)
             {
                 int fromRefCount = 0;
                 int toRefCount = 0;
                 int matchesFound = 0;
                 
                 foreach (DwgEntry dwgEntry in dwgData)
                 {
                     if (dwgEntry.CableTag == bowEntry.CableTag)
                     {
                         matchesFound++;
                         
                         if (dwgEntry.DwgRef == bowEntry.FromRef)
                         {
                             dwgEntry.CableTagColorFlag = 1;
                             fromRefCount ++;
                         }
                         else if (dwgEntry.DwgRef == bowEntry.ToRef)
                         {
                             dwgEntry.CableTagColorFlag = 1;
                             toRefCount ++;
                         }
                         else
                         {
                             dwgEntry.CableTagColorFlag = 3;
                         }
                     }
                 }
                 
                 if (matchesFound == 0)
                 {
                     bowEntry.CableTagColorFlag = 3;
                     bowEntry.FromRefColorFlag = 3;
                     bowEntry.ToRefColorFlag = 3;
                   
                 }
                 if (matchesFound == 1)
                 {
                     bowEntry.CableTagColorFlag = 1;
                     
                     if (fromRefCount == 1 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 1;
                         bowEntry.ToRefColorFlag = 3;
                     }
                     if (fromRefCount == 0 && toRefCount == 1)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 1;
                     }
                     if (fromRefCount == 0 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 3;
                     }
                     
                 }
                 if (matchesFound == 2)
                 {
                     bowEntry.CableTagColorFlag = 1;
                     
                     if (fromRefCount == 1 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 1;
                         bowEntry.ToRefColorFlag = 3;
                     }
                     if (fromRefCount == 0 && toRefCount == 1)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 1;
                     }
                     if (fromRefCount == 1 && toRefCount == 1)
                     {
                         bowEntry.FromRefColorFlag = 1;
                         bowEntry.ToRefColorFlag = 1;
                     }
                     if (fromRefCount == 0 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 3;
                     }
                     if (fromRefCount == 2 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 4;
                         bowEntry.ToRefColorFlag = 3;
                         bowEntry.CableTagColorFlag = 4;
                     }
                     if (fromRefCount == 0 && toRefCount == 2)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 4;
                         bowEntry.CableTagColorFlag = 4;
                     }
                     
                 }
                 if (matchesFound > 2)
                 {
                     if (fromRefCount == 1 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 1;
                         bowEntry.ToRefColorFlag = 3;
                         bowEntry.CableTagColorFlag = 1;
                     }
                     if (fromRefCount == 0 && toRefCount == 1)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 1;
                         bowEntry.CableTagColorFlag = 1;
                     }
                     if (fromRefCount == 1 && toRefCount == 1)
                     {
                         bowEntry.FromRefColorFlag = 1;
                         bowEntry.ToRefColorFlag = 1;
                         bowEntry.CableTagColorFlag = 1;
                     }
                     if (fromRefCount == 0 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 3;
                         bowEntry.CableTagColorFlag = 4;
                     }
                     if (fromRefCount >= 2 && toRefCount == 0)
                     {
                         bowEntry.FromRefColorFlag = 4;
                         bowEntry.ToRefColorFlag = 3;
                         bowEntry.CableTagColorFlag = 4;
                     }
                     if (fromRefCount == 0 && toRefCount >= 2)
                     {
                         bowEntry.FromRefColorFlag = 3;
                         bowEntry.ToRefColorFlag = 4;
                         bowEntry.CableTagColorFlag = 4;
                     }
                     if (fromRefCount >= 2 && toRefCount >= 2)
                     {
                         bowEntry.FromRefColorFlag = 4;
                         bowEntry.ToRefColorFlag = 4;
                         bowEntry.CableTagColorFlag = 4;
                     }
                     
                 }
                 
                 
             }
             
         }
    private List<DwgEntry> GetDwgEntries(SQLiteConnection connection)
    {
        int currentSheet = -1;
        int currentItem = -1;
        string currentCableTag = null;
        string currentFromRef = null;
        string currentToRef = null;
        string currentDwgRef = null;
        
        // Fetch DWG_table data
        var dwgQuery = @"SELECT Word, Sheet, Tag, ColorFlag FROM DWG_table ORDER BY Sheet, Tag DESC";
        List<DwgEntry> dwgData = new List<DwgEntry>();

        using (var cmd = new SQLiteCommand(dwgQuery, connection))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (Convert.ToInt32(reader["ColorFlag"]) != 0)
                    continue;

                int sheet = Convert.ToInt32(reader["Sheet"]);
                string tag = reader["Tag"].ToString();
                string word = reader["Word"].ToString();


                // If we move to a new sheet, save the previous entry
                if (sheet != currentSheet)
                {
                    currentSheet = sheet;
                    currentCableTag = null;
                    currentDwgRef = null;
                }

                if (tag == "full_dwg_number")
                {
                    currentDwgRef = word;
                    continue;
                }

                if (tag == "cable_tag")
                {
                    currentCableTag = word;

                    dwgData.Add(new DwgEntry
                    {
                        CableTag = currentCableTag,
                        DwgRef = currentDwgRef,
                        Sheet = currentSheet,
                        CableTagColorFlag = 0
                    });
                }

            }
        }
        return dwgData;
    }
    private List<BowEntry> GetBowEntries(SQLiteConnection connection)
    {
        int currentSheet = -1;
        int currentItem = -1;
        string currentCableTag = null;
        string currentFromRef = null;
        string currentToRef = null;
        string currentDwgRef = null;
        

        // Fetch BOW_table data
        string bowQuery = @"SELECT Word, Sheet, Tag, Item, ColorFlag FROM BOW_table ORDER BY Sheet, Item";
        List<BowEntry> bowData = new List<BowEntry>();
        
        using (var cmd = new SQLiteCommand(bowQuery, connection))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (Convert.ToInt32(reader["ColorFlag"]) != 0) 
                    continue;
        
                int sheet = Convert.ToInt32(reader["Sheet"]);
                int item = Convert.ToInt32(reader["Item"]);
                string tag = reader["Tag"].ToString();
                string word = reader["Word"].ToString().Trim();
        
                // If we move to a new sheet or item, save the previous entry
                if (sheet != currentSheet || item != currentItem)
                {
                    if (currentSheet != -1) // Avoid saving empty first iteration
                    {
                        bowData.Add(new BowEntry
                        {
                            CableTag = currentCableTag,
                            FromRef = currentFromRef,
                            ToRef = currentToRef,
                            Sheet = currentSheet,
                            Item = currentItem,
                            CableTagColorFlag = 0,
                            FromRefColorFlag = 0,
                            ToRefColorFlag = 0
                        });
                    }
        
                    // Update tracking variables
                    currentSheet = sheet;
                    currentItem = item;
                    currentCableTag = null;
                    currentFromRef = null;
                    currentToRef = null;
                }
        
                // Assign values based on tag type
                switch (tag)
                {
                    case "cable_tag":
                        currentCableTag += "" + word;
                        break;
                    case "from_ref":
                        currentFromRef = word.Replace("(", "").Replace(")", "");
                        break;
                    case "to_ref":
                        currentToRef = word.Replace("(", "").Replace(")", "");
                        break;
                }
            }
        
            // Add last entry after loop ends
            if (currentSheet != -1)
            {
                bowData.Add(new BowEntry
                {
                    CableTag = currentCableTag,
                    FromRef = currentFromRef,
                    ToRef = currentToRef,
                    Sheet = currentSheet,
                    Item = currentItem,
                    CableTagColorFlag = 0,
                    FromRefColorFlag = 0,
                    ToRefColorFlag = 0
                });
                
            }
        }
        return bowData;
    }
    private void UpdateDatabase(SQLiteConnection connection, List<DwgEntry> dwgData, List<BowEntry> bowData)
    {
        using (var transaction = connection.BeginTransaction())
        {
            try
            {
                using (var cmd = new SQLiteCommand(connection))
                {
                    // Update DWG_table
                    cmd.CommandText = @"UPDATE DWG_table SET ColorFlag = @ColorFlag WHERE Sheet = @Sheet AND Tag = 'cable_tag' AND Word = @CableTag";
                    cmd.Parameters.Add(new SQLiteParameter("@ColorFlag"));
                    cmd.Parameters.Add(new SQLiteParameter("@Sheet"));
                    cmd.Parameters.Add(new SQLiteParameter("@CableTag"));

                    foreach (var dwgEntry in dwgData)
                    {
                        cmd.Parameters["@ColorFlag"].Value = dwgEntry.CableTagColorFlag;
                        cmd.Parameters["@Sheet"].Value = dwgEntry.Sheet;
                        cmd.Parameters["@CableTag"].Value = dwgEntry.CableTag;
                        cmd.ExecuteNonQuery();
                    }

                    // Update BOW_table
                    cmd.CommandText = @"UPDATE BOW_table SET ColorFlag = @ColorFlag WHERE Sheet = @Sheet AND Item = @Item AND Tag = @Tag AND Word IS NOT NULL AND Word <> '';";
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(new SQLiteParameter("@ColorFlag"));
                    cmd.Parameters.Add(new SQLiteParameter("@Sheet"));
                    cmd.Parameters.Add(new SQLiteParameter("@Item"));
                    cmd.Parameters.Add(new SQLiteParameter("@Tag"));

                    foreach (var bowEntry in bowData)
                    {
                        // Update CableTag ColorFlag
                        cmd.Parameters["@ColorFlag"].Value = bowEntry.CableTagColorFlag;
                        cmd.Parameters["@Sheet"].Value = bowEntry.Sheet;
                        cmd.Parameters["@Item"].Value = bowEntry.Item;
                        cmd.Parameters["@Tag"].Value = "cable_tag";
                        cmd.ExecuteNonQuery();

                        // Update FromRef ColorFlag
                        cmd.Parameters["@ColorFlag"].Value = bowEntry.FromRefColorFlag;
                        cmd.Parameters["@Sheet"].Value = bowEntry.Sheet;
                        cmd.Parameters["@Item"].Value = bowEntry.Item;
                        cmd.Parameters["@Tag"].Value = "from_ref";
                        cmd.ExecuteNonQuery();

                        // Update ToRef ColorFlag
                        cmd.Parameters["@ColorFlag"].Value = bowEntry.ToRefColorFlag;
                        cmd.Parameters["@Sheet"].Value = bowEntry.Sheet;
                        cmd.Parameters["@Item"].Value = bowEntry.Item;
                        cmd.Parameters["@Tag"].Value = "to_ref";
                        cmd.ExecuteNonQuery();
                    }
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error updating database: {ex.Message}");
            }
        }
    }
}  

public class BowEntry
{
    public string CableTag { get; set; }
    public string FromRef { get; set; }
    public string ToRef { get; set; }
    public int Sheet { get; set; }
    public int Item { get; set; }
    public int CableTagColorFlag { get; set; }
    public int FromRefColorFlag { get; set; }
    public int ToRefColorFlag { get; set; }
}
public class DwgEntry
{
    public string CableTag { get; set; }
    public string DwgRef { get; set; }
    public int Sheet { get; set; }
    public int CableTagColorFlag { get; set; }
}
