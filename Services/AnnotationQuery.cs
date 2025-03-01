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


namespace CtrInvoice.Services;

public class AnnotationQuery
{
    private static readonly Dictionary<string, List<string>> Filters = new()
    {
        { "BOW", new List<string> { "cable_tag", "from_desc", "to_desc", "function", "size", "insulation",
            "from_ref", "to_ref", "voltage", "conductors", "length" }},
        { "TITLE", new List<string> { "facility_name", "facility_id", "dwg_title1", "dwg_title2", "dwg_scale", 
            "dwg_size", "dwg_number", "dwg_sheet", "dwg_rev", "dwg_type" }},
        { "CUSTOM", new List<string> { "facility_name" }}
    };
    
    public List<string> GetFilters(string key)
    {
        return Filters.TryGetValue(key, out var values) ? values : new List<string>();
    }
}