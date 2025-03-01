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


namespace CtrInvoice.Models;

public class PdfTextModel
{
    public string PageWord { get; set; }
    public double BottomLeftX { get; set; }
    public double BottomLeftY { get; set; }
    public double TopRightX { get; set; }
    public double TopRightY { get; set; }
    public int PageNumber { get; set; }
    public int PageRotation { get; set; }
    public int WordRotation { get; set; }
    public string WordTag { get; set; }
    public int ItemNumber { get; set; }
    public int ColorFlag { get; set; }

    public PdfTextModel(string pageWord, 
        double bottomLeftX, 
        double bottomLeftY, 
        double topRightX, 
        double topRightY, 
        int pageNumber, 
        int pageRotation, 
        int wordRotation,
        string wordTag, 
        int itemNumber,
        int colorFlag)
    {
        PageWord = pageWord;
        BottomLeftX = bottomLeftX;
        BottomLeftY = bottomLeftY;
        TopRightX = topRightX;
        TopRightY = topRightY;
        PageNumber = pageNumber;
        PageRotation = pageRotation;
        WordRotation = wordRotation;
        WordTag = wordTag;
        ItemNumber = itemNumber;
        ColorFlag = colorFlag;
    }
}