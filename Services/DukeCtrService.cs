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


using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ClosedXML.Excel;
using Dapper;

namespace CtrInvoice.Services
{
    public class DukeCtrService
    {
        public void GenerateCtr(string filePath, string databasePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Excel file not found!");
                return;
            }
            
            string connectionString = $"Data Source={databasePath};Version=3;";

            FileInfo originalFile = new FileInfo(filePath);
            string newFilePath = Path.Combine(originalFile.DirectoryName, "updated_CTR.xlsx");

            try
            {
                using (var workbook = new XLWorkbook(filePath))
                using (var connection = new SQLiteConnection(connectionString))
                {
                    var worksheet = workbook.Worksheets.First(); // Get the first worksheet
                    connection.Open();

                    // Fetch Data using Dapper
                    string query = "SELECT Word, Tag, Sheet FROM INVOICE_table";
                    
                    using (var cmd = new SQLiteCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string word = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
                            string tag = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                            int sheet = reader.GetInt32(2);
                            
                            
                            //Update CTR sheet
                            if (tag == "invoice_number")
                            {
                                worksheet.Cell($"F4").Value = word;
                            }
                            if (tag == "client_contract")
                            {
                                worksheet.Cell($"D4").Value = word;
                            }
                            if (tag == "state")
                            {
                                string cellValue = word switch
                                {
                                    "IN" => "TD-IN",
                                    "KY" or "OH" => "TD-KY-OH",
                                    "NC" or "SC" => "TD-NC-SC",
                                    "FL" => "TD-FL",
                                    _ => "" 
                                };

                                worksheet.Cell("A4").Value = cellValue;
                            }
                            if (tag == "invoice_end_date")
                            {
                                DateTime parsedDate = DateTime.ParseExact(word, "dd-MMM-yyyy", 
                                    System.Globalization.CultureInfo.InvariantCulture);
                                string formattedDate = parsedDate.ToString("MM.dd.yyyy");
                                
                                worksheet.Cell($"B4").Value = formattedDate;
                            }

                            
                        }
                    }

                    // Save the updated file
                    workbook.SaveAs(newFilePath);
                    Console.WriteLine($"File saved as: {newFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
    
}

