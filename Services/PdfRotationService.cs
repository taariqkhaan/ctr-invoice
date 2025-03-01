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


using iText.Kernel.Pdf;
using System.IO;

namespace CtrInvoice.Services
{
    public class PdfRotationService
    {
        public void RotatePdfPages(string filePath, List<int> verticalPageList)
        {
            string pdfPath = System.IO.Path.Combine( System.IO.Path.GetDirectoryName(filePath)!, "highlighted_DWG.pdf");
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("Drawing PDF file not found.");
                return;
            }
            
            string tempFilePath = pdfPath + ".tmp";

            using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(pdfPath), new PdfWriter(tempFilePath)))
            {
                foreach (int pageNumber in verticalPageList)
                {
                    PdfPage page = pdfDoc.GetPage(pageNumber);
                    page.SetRotation(90);
                }
            }

            // Overwrite the original file
            File.Delete(pdfPath);
            File.Move(tempFilePath, pdfPath);
            Console.WriteLine($"Vertical pages rotated");
        }
        
        public void RevertRotations(string filePath)
        {
            string pdfPath = Path.Combine(Path.GetDirectoryName(filePath)!, "highlighted_DWG.pdf");
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("Drawing PDF file not found.");
                return;
            }

            string tempFilePath = pdfPath + ".tmp";

            using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(pdfPath), new PdfWriter(tempFilePath)))
            {
                int totalPages = pdfDoc.GetNumberOfPages();
                for (int i = 1; i <= totalPages; i++)
                {
                    PdfPage page = pdfDoc.GetPage(i);
                    page.SetRotation(0);
                }
            }
            // Overwrite the original file
            File.Delete(pdfPath);
            File.Move(tempFilePath, pdfPath);
            Console.WriteLine($"Vertical pages rotation reverted");
        }
    }
}