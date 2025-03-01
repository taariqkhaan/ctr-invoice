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


using UglyToad.PdfPig.Core;

namespace CtrInvoice.Services
{
    public class PdfRegionService
    {
        // Gets the full page size as a rectangle.
        public PdfRectangle GetFullPageRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(0, 0, pageWidth, pageHeight);
        }
        
        // Gets search region for Southern Company Bill of Wire
        public PdfRectangle GetBowRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(x1_min, y1_min + 10, pageWidth - x1_min, y2_max - 85);
        }
        
        // Gets search region for Southern Company Drawing Title
        public PdfRectangle GetDwgTitleRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(pageWidth - 495, 0, pageWidth, 114);
                
        }
        // Gets search region for Southern Company Drawing
        public PdfRectangle GetDwgRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(0, 95, pageWidth, pageHeight);
        }
        
        // Method to get a function reference based on user input
        public PdfRectangle GetRegionByName(string regionName, double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            var regionMap = new Dictionary<string, Func<double, double, int, double,double,double, double, 
                PdfRectangle>>(StringComparer.OrdinalIgnoreCase)
            {
                { "FULL", GetFullPageRegion },
                { "BOW", GetBowRegion },
                { "TITLE", GetDwgTitleRegion },
                { "DWG", GetDwgRegion }
            };

            if (regionMap.TryGetValue(regionName, out var regionFunc))
            {
                return regionFunc(pageWidth, pageHeight, pageRotation, x1_min, y1_min, x2_max, y2_max);
            }

            throw new ArgumentException($"Invalid region name: {regionName}");
        }
    }
}
