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
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Colors;
using iText.Kernel.Geom;

namespace CtrInvoice.Services;

public class AnnotationService
{
    public void AnnotatePdf(string pdfPath, string dbPath, string key)
    {
        if (!File.Exists(pdfPath))
        {
            Console.WriteLine("PDF file not found.");
            return;
        }
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine("Database file not found.");
            return;
        }

        string outputPdfPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(pdfPath), 
            $"highlighted_{key}.pdf");
        
        string tableName = key + "_table";

        using PdfReader reader = new(pdfPath);
        using PdfWriter writer = new(outputPdfPath);
        using PdfDocument pdfDoc = new(reader, writer);
        
        string connectionString = $"Data Source={dbPath};Version=3;";
        using SQLiteConnection connection = new(connectionString);
        connection.Open();
        
        // Get the list of items to be annotated
        AnnotationQuery annotationQuery = new AnnotationQuery();
        List<string> userTags = annotationQuery.GetFilters(key);
        
        // Default case if no tags are provided
        string whereClause = userTags.Any()
            ? $"WHERE Tag IN ({string.Join(", ", userTags.Select(tag => $"'{tag}'"))})"
            : ""; // If no filter is needed, leave it blank

        const int batchSize = 500;
        int offset = 0;
        bool hasMoreRecords = true;

        while (hasMoreRecords)
        {
            string query = $@"
                SELECT Word, X1, Y1, X2, Y2, Sheet, WordRotation, Tag, Item, ColorFlag
                FROM {tableName}
                {whereClause}
                ORDER BY Sheet ASC, Item ASC
                LIMIT {batchSize} OFFSET {offset}";

            using SQLiteCommand command = new(query, connection);
            using SQLiteDataReader readerDb = command.ExecuteReader();

            hasMoreRecords = readerDb.HasRows;
            if (!hasMoreRecords) break;

            while (readerDb.Read())
            {
                string wordValue = readerDb.IsDBNull(0) ? string.Empty : readerDb.GetString(0).Trim();
                double real_x1 = readerDb.GetDouble(1);
                double real_y1 = readerDb.GetDouble(2);
                double real_x2 = readerDb.GetDouble(3);
                double real_y2 = readerDb.GetDouble(4);
                int pageIndex = readerDb.GetInt32(5) - 1;
                int wordRotation = readerDb.GetInt32(6);
                string tagValue = readerDb.IsDBNull(7) ? string.Empty : readerDb.GetString(7).Trim();
                int colorFlag = readerDb.GetInt32(9);

                if (pageIndex < 0 || pageIndex >= pdfDoc.GetNumberOfPages())
                    continue;

                PdfPage page = pdfDoc.GetPage(pageIndex + 1);
                
                int pageRotation = page.GetRotation();
                float pageWidth = page.GetPageSize().GetWidth();
                float pageHeight = page.GetPageSize().GetHeight();
                Rectangle annotationRect;
                double newX1 = 0;
                double newY1 = 0;
                double newX2 = 0;
                double newY2 = 0;
                double rectWidth = 0;
                double rectHeight = 0;
                
                (double x1, double y1, double x2, double y2) = AdjustCoordinates(wordRotation, wordValue, real_x1, real_y1, real_x2, real_y2);

                if (pageRotation == 90)
                {
                    newX1 = pageWidth - y2;
                    newY1 = x1;
                    newX2 = pageWidth - y1;
                    newY2 = x2;

                    rectWidth = newX2 - newX1;
                    rectHeight = newY2 - newY1;
                }
                else if (pageRotation == 0)
                {
                    newX1 = x1;
                    newY1 = y1;
                    newY2 = y2;
                    
                    rectWidth = x2 - x1;
                    rectHeight = newY2 - newY1;
                }
                
                annotationRect = new Rectangle((float)newX1 - 1, (float)newY1 - 1, (float)rectWidth + 2, (float)rectHeight + 2);
                
                bool missingValue = string.IsNullOrWhiteSpace(wordValue);
                float opacity = 0.6f;
                float[] colorComponents = [];

                if (colorFlag != 0) // Default: not relevant for annotation
                {
                    if (colorFlag == 1) //Correct: Yellow
                    {
                        colorComponents = new [] { 1f, 0.9f, 0f };
                    }
                    else if (colorFlag == 2) // Missing Value: Red
                    {
                        colorComponents = new [] { 1f, 0f, 0f };
                    }
                    else if (colorFlag == 3) // Not found: magenta
                    {
                        colorComponents = new [] { 1f, 0f, 1f };
                    }
                    else if (colorFlag == 4) // More than 2 instances found: cyan
                    {
                        colorComponents = new [] { 0.28f, 0.82f, 0.8f };
                    }
                    
                    PdfSquareAnnotation annotation = new PdfSquareAnnotation(annotationRect);
                    annotation.SetColor(new DeviceRgb(colorComponents[0], colorComponents[1], colorComponents[2]));
                    annotation.SetInteriorColor(new PdfArray(colorComponents));
                    annotation.SetTitle(new PdfString("Annotation"));
                    annotation.SetContents(missingValue ? tagValue + "?" : tagValue);
                    annotation.SetBorder(new PdfArray(new float[] { 0.1f, 0.1f, 0.1f }));
                    annotation.Put(PdfName.CA, new PdfNumber(opacity)); // Opacity (40%)
                
                    page.AddAnnotation(annotation);
                }
                
            }

            offset += batchSize;
        }

        Console.WriteLine($"Annotated PDF saved at: {outputPdfPath}");
    }

    private (double, double, double, double) AdjustCoordinates(int wordRotation,
        string textValue, double real_x1, double real_y1, double real_x2, double real_y2)
    {
        if (string.IsNullOrEmpty(textValue))
        {
            return (real_x1, real_y1, real_x2, real_y2);
        }

        char firstChar = textValue[0];
        char lastChar = textValue[^1];
        double bottomLeftX = real_x1;
        double bottomLeftY = real_y1;
        double topRightX = real_x2;
        double topRightY = real_y2;

        switch (wordRotation)
        {
            case 0:
                if (".,_".Contains(lastChar))
                    topRightY += 5;
                if ("-+=".Contains(firstChar))
                    bottomLeftY -= 2.5;
                if ("-+=".Contains(lastChar))
                    topRightY += 4;
                if ("^`'\"".Contains(firstChar))
                    bottomLeftY -= 2;
                break;
        
            case 90:
                topRightY -= 4;
                bottomLeftY += 4;
                if (".,_".Contains(lastChar))
                    topRightX += 9;
                if ("-+=".Contains(firstChar))
                    bottomLeftX -= 2.5;
                if ("-+=".Contains(lastChar))
                    topRightX += 4;
                if ("^`'\"".Contains(firstChar))
                    bottomLeftX -= 5;
                break;
        
            case 180:
                (bottomLeftX, topRightX) = (topRightX, bottomLeftX);
                (bottomLeftY, topRightY) = (topRightY, bottomLeftY);
                if (".,_".Contains(lastChar))
                    bottomLeftY -= 9;
                if ("-+=".Contains(firstChar))
                    topRightY += 4;
                if ("-+=".Contains(lastChar))
                    bottomLeftY -= 5;
                if ("^`'\"".Contains(firstChar))
                    topRightY += 4;
                break;
            
            case 270:
                bottomLeftX += 2;
                topRightX -= 2;

                if (".,_".Contains(firstChar))
                    bottomLeftX += 2;

                if (".,_".Contains(lastChar))
                    topRightX -= 4;

                if ("-+=".Contains(lastChar))
                    topRightX -= 7;

                if ("^`'\"".Contains(firstChar))
                    bottomLeftX += 10;
                break;
        }
        return (bottomLeftX, bottomLeftY, topRightX, topRightY);
    }
    
    
}
