using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Forms;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class ExcelExportService
    {
        public void ExportGrid(string filePath, DataGridView grid)
        {
            ExportGrid(filePath, null, grid);
        }

        public void ExportGrid(string filePath, CostAnalysisHeader header, DataGridView grid)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("请选择导出文件路径。", "filePath");
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var columns = GetExportColumns(grid);
            var rows = GetExportRows(grid, columns);

            using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", ContentTypesXml());
                WriteEntry(archive, "_rels/.rels", RootRelsXml());
                WriteEntry(archive, "xl/workbook.xml", WorkbookXml());
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
                WriteEntry(archive, "xl/styles.xml", StylesXml());
                WriteEntry(archive, "xl/worksheets/sheet1.xml", WorksheetXml(header, columns, rows));
                WriteEntry(archive, "docProps/core.xml", CorePropsXml());
                WriteEntry(archive, "docProps/app.xml", AppPropsXml());
            }
        }

        private static List<ExportColumn> GetExportColumns(DataGridView grid)
        {
            var columns = new List<ExportColumn>();
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible || column.Name == "ValidationStatus" || column.Name == "Selected")
                {
                    continue;
                }

                columns.Add(new ExportColumn
                {
                    Name = column.Name,
                    Header = column.HeaderText
                });
            }

            return columns;
        }

        private static List<List<string>> GetExportRows(DataGridView grid, List<ExportColumn> columns)
        {
            var rows = new List<List<string>>();
            foreach (DataGridViewRow gridRow in grid.Rows)
            {
                if (gridRow.IsNewRow)
                {
                    continue;
                }

                var row = new List<string>();
                var hasValue = false;
                foreach (var column in columns)
                {
                    var value = gridRow.Cells[column.Name].Value;
                    var text = value == null ? string.Empty : Convert.ToString(value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        hasValue = true;
                    }

                    row.Add(text);
                }

                if (hasValue)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static string WorksheetXml(CostAnalysisHeader header, List<ExportColumn> columns, List<List<string>> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.AppendLine("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.AppendLine("<sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>");
            sb.AppendLine("<sheetFormatPr defaultRowHeight=\"18\"/>");
            sb.AppendLine("<cols>");
            for (var i = 1; i <= columns.Count; i++)
            {
                var width = EstimateWidth(columns[i - 1].Header);
                sb.AppendFormat("<col min=\"{0}\" max=\"{0}\" width=\"{1}\" customWidth=\"1\"/>", i, width);
                sb.AppendLine();
            }
            sb.AppendLine("</cols>");
            sb.AppendLine("<sheetData>");

            var headerOffset = 0;
            if (header != null)
            {
                WriteHeaderRows(sb, header);
                headerOffset = 4;
            }

            var tableHeaderRow = headerOffset + 1;
            sb.AppendFormat("<row r=\"{0}\" ht=\"22\" customHeight=\"1\">", tableHeaderRow);
            sb.AppendLine();
            for (var col = 0; col < columns.Count; col++)
            {
                WriteInlineCell(sb, tableHeaderRow, col + 1, columns[col].Header, 1);
            }
            sb.AppendLine("</row>");

            for (var row = 0; row < rows.Count; row++)
            {
                var excelRow = row + headerOffset + 2;
                sb.AppendFormat("<row r=\"{0}\">", excelRow);
                sb.AppendLine();
                for (var col = 0; col < columns.Count; col++)
                {
                    WriteInlineCell(sb, excelRow, col + 1, rows[row][col], 0);
                }
                sb.AppendLine("</row>");
            }

            sb.AppendLine("</sheetData>");
            sb.AppendLine("<pageMargins left=\"0.3\" right=\"0.3\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>");
            sb.AppendLine("</worksheet>");
            return sb.ToString();
        }

        private static void WriteHeaderRows(StringBuilder sb, CostAnalysisHeader header)
        {
            sb.AppendLine("<row r=\"1\" ht=\"24\" customHeight=\"1\">");
            WriteInlineCell(sb, 1, 1, "成本分析", 1);
            sb.AppendLine("</row>");

            sb.AppendLine("<row r=\"2\">");
            WriteInlineCell(sb, 2, 1, "分析单号", 1);
            WriteInlineCell(sb, 2, 2, header.AnalysisNo, 0);
            WriteInlineCell(sb, 2, 4, "客户名称", 1);
            WriteInlineCell(sb, 2, 5, header.CustomerName, 0);
            WriteInlineCell(sb, 2, 7, "项目名称", 1);
            WriteInlineCell(sb, 2, 8, header.ProjectName, 0);
            sb.AppendLine("</row>");

            sb.AppendLine("<row r=\"3\">");
            WriteInlineCell(sb, 3, 1, "分析日期", 1);
            WriteInlineCell(sb, 3, 2, header.AnalysisDate, 0);
            WriteInlineCell(sb, 3, 4, "税费说明", 1);
            WriteInlineCell(sb, 3, 5, header.TaxNote, 0);
            WriteInlineCell(sb, 3, 7, "运费说明", 1);
            WriteInlineCell(sb, 3, 8, header.FreightNote, 0);
            sb.AppendLine("</row>");

            sb.AppendLine("<row r=\"4\"/>");
        }

        private static void WriteInlineCell(StringBuilder sb, int row, int column, string text, int style)
        {
            var reference = ColumnName(column) + row;
            sb.AppendFormat("<c r=\"{0}\" t=\"inlineStr\" s=\"{1}\"><is><t>{2}</t></is></c>", reference, style, Escape(text));
            sb.AppendLine();
        }

        private static double EstimateWidth(string header)
        {
            if (header == "物料描述")
            {
                return 42;
            }

            if (header == "物料编码")
            {
                return 20;
            }

            if (header == "供应商")
            {
                return 24;
            }

            return Math.Max(10, Math.Min(18, (header ?? string.Empty).Length * 2.2));
        }

        private static string ColumnName(int index)
        {
            var name = string.Empty;
            while (index > 0)
            {
                index--;
                name = (char)('A' + index % 26) + name;
                index /= 26;
            }

            return name;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string ContentTypesXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
  <Override PartName=""/docProps/core.xml"" ContentType=""application/vnd.openxmlformats-package.core-properties+xml""/>
  <Override PartName=""/docProps/app.xml"" ContentType=""application/vnd.openxmlformats-officedocument.extended-properties+xml""/>
</Types>";
        }

        private static string RootRelsXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"" Target=""docProps/core.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"" Target=""docProps/app.xml""/>
</Relationships>";
        }

        private static string WorkbookXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""成本分析"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>";
        }

        private static string WorkbookRelsXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>";
        }

        private static string StylesXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""2"">
    <font><sz val=""10""/><name val=""Microsoft YaHei UI""/></font>
    <font><b/><sz val=""10""/><name val=""Microsoft YaHei UI""/><color rgb=""FF1D1D1F""/></font>
  </fonts>
  <fills count=""3"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FFF5F5F7""/><bgColor indexed=""64""/></patternFill></fill>
  </fills>
  <borders count=""2"">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border><left style=""thin""><color rgb=""FFD2D2D7""/></left><right style=""thin""><color rgb=""FFD2D2D7""/></right><top style=""thin""><color rgb=""FFD2D2D7""/></top><bottom style=""thin""><color rgb=""FFD2D2D7""/></bottom><diagonal/></border>
  </borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""2"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0"" applyBorder=""1""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1""/>
  </cellXfs>
</styleSheet>";
        }

        private static string CorePropsXml()
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/metadata/core-properties"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:dcterms=""http://purl.org/dc/terms/"" xmlns:dcmitype=""http://purl.org/dc/dcmitype/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <dc:title>成本分析</dc:title>
  <dc:creator>成本分析软件</dc:creator>
  <cp:lastModifiedBy>成本分析软件</cp:lastModifiedBy>
  <dcterms:created xsi:type=""dcterms:W3CDTF"">" + now + @"</dcterms:created>
  <dcterms:modified xsi:type=""dcterms:W3CDTF"">" + now + @"</dcterms:modified>
</cp:coreProperties>";
        }

        private static string AppPropsXml()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"" xmlns:vt=""http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"">
  <Application>成本分析软件</Application>
</Properties>";
        }

        private sealed class ExportColumn
        {
            public string Name { get; set; }
            public string Header { get; set; }
        }
    }
}
