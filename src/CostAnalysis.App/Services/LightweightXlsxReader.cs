using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace CostAnalysis.App.Services
{
    internal sealed class LightweightXlsxReader
    {
        private const int MaxRows = 80;
        private const int MaxColumns = 30;

        public List<XlsxSheetSnapshot> ReadSheets(string filePath)
        {
            var snapshots = new List<XlsxSheetSnapshot>();
            using (var archive = ZipFile.OpenRead(filePath))
            {
                var sharedStrings = ReadSharedStrings(archive);
                var rels = ReadWorkbookRelationships(archive);
                foreach (var sheetInfo in ReadWorkbookSheets(archive, rels))
                {
                    var entry = archive.GetEntry(sheetInfo.Path);
                    if (entry == null)
                    {
                        continue;
                    }

                    snapshots.Add(ReadWorksheet(entry, sheetInfo.Name, sharedStrings));
                }
            }

            return snapshots;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var result = new List<string>();
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return result;
            }

            var document = LoadXml(entry);
            var namespaceManager = CreateNamespaceManager(document);
            foreach (XmlNode node in document.SelectNodes("//x:si", namespaceManager))
            {
                result.Add(CollectText(node, namespaceManager));
            }

            return result;
        }

        private static Dictionary<string, string> ReadWorkbookRelationships(ZipArchive archive)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var entry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (entry == null)
            {
                return result;
            }

            var document = LoadXml(entry);
            foreach (XmlNode node in document.GetElementsByTagName("Relationship"))
            {
                var id = GetAttribute(node, "Id");
                var target = GetAttribute(node, "Target");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                result[id] = NormalizeWorkbookTarget(target);
            }

            return result;
        }

        private static List<WorkbookSheetInfo> ReadWorkbookSheets(ZipArchive archive, Dictionary<string, string> relationships)
        {
            var result = new List<WorkbookSheetInfo>();
            var entry = archive.GetEntry("xl/workbook.xml");
            if (entry == null)
            {
                return result;
            }

            var document = LoadXml(entry);
            var namespaceManager = CreateNamespaceManager(document);
            foreach (XmlNode node in document.SelectNodes("//x:sheets/x:sheet", namespaceManager))
            {
                var name = GetAttribute(node, "name");
                var relId = GetAttribute(node, "r:id");
                string path;
                if (string.IsNullOrWhiteSpace(relId) || !relationships.TryGetValue(relId, out path))
                {
                    continue;
                }

                result.Add(new WorkbookSheetInfo { Name = name, Path = path });
            }

            return result;
        }

        private static XlsxSheetSnapshot ReadWorksheet(ZipArchiveEntry entry, string sheetName, List<string> sharedStrings)
        {
            var cells = new string[MaxRows + 1, MaxColumns + 1];
            var document = LoadXml(entry);
            var namespaceManager = CreateNamespaceManager(document);
            foreach (XmlNode cellNode in document.SelectNodes("//x:sheetData/x:row/x:c", namespaceManager))
            {
                int row;
                int column;
                if (!TryParseCellAddress(GetAttribute(cellNode, "r"), out row, out column))
                {
                    continue;
                }

                if (row < 1 || row > MaxRows || column < 1 || column > MaxColumns)
                {
                    continue;
                }

                cells[row, column] = ReadCellValue(cellNode, namespaceManager, sharedStrings);
            }

            return new XlsxSheetSnapshot
            {
                Name = sheetName,
                Cells = cells,
                Rows = MaxRows,
                Columns = MaxColumns
            };
        }

        private static string ReadCellValue(XmlNode cellNode, XmlNamespaceManager namespaceManager, List<string> sharedStrings)
        {
            var type = GetAttribute(cellNode, "t");
            if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            {
                var inlineNode = cellNode.SelectSingleNode("x:is", namespaceManager);
                return Clean(CollectText(inlineNode, namespaceManager));
            }

            var valueNode = cellNode.SelectSingleNode("x:v", namespaceManager);
            var rawValue = valueNode == null ? string.Empty : valueNode.InnerText;
            if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                    index >= 0 &&
                    index < sharedStrings.Count)
                {
                    return Clean(sharedStrings[index]);
                }

                return string.Empty;
            }

            if (string.Equals(type, "b", StringComparison.OrdinalIgnoreCase))
            {
                return rawValue == "1" ? "TRUE" : "FALSE";
            }

            return Clean(rawValue);
        }

        private static string CollectText(XmlNode node, XmlNamespaceManager namespaceManager)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (XmlNode textNode in node.SelectNodes(".//x:t", namespaceManager))
            {
                parts.Add(textNode.InnerText);
            }

            return string.Join(string.Empty, parts.ToArray());
        }

        private static bool TryParseCellAddress(string address, out int row, out int column)
        {
            row = 0;
            column = 0;
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            var index = 0;
            while (index < address.Length && char.IsLetter(address[index]))
            {
                column = column * 26 + (char.ToUpperInvariant(address[index]) - 'A' + 1);
                index++;
            }

            if (column == 0 || index >= address.Length)
            {
                return false;
            }

            return int.TryParse(address.Substring(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out row);
        }

        private static XmlDocument LoadXml(ZipArchiveEntry entry)
        {
            var document = new XmlDocument();
            document.PreserveWhitespace = false;
            using (var stream = entry.Open())
            {
                document.Load(stream);
            }

            return document;
        }

        private static XmlNamespaceManager CreateNamespaceManager(XmlDocument document)
        {
            var manager = new XmlNamespaceManager(document.NameTable);
            manager.AddNamespace("x", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            manager.AddNamespace("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            return manager;
        }

        private static string NormalizeWorkbookTarget(string target)
        {
            var normalized = target.Replace('\\', '/').TrimStart('/');
            if (normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return "xl/" + normalized;
        }

        private static string GetAttribute(XmlNode node, string name)
        {
            if (node == null || node.Attributes == null)
            {
                return string.Empty;
            }

            var attribute = node.Attributes[name];
            if (attribute != null)
            {
                return attribute.Value;
            }

            if (name.IndexOf(':') > 0)
            {
                var localName = name.Substring(name.IndexOf(':') + 1);
                attribute = node.Attributes[localName, "http://schemas.openxmlformats.org/officeDocument/2006/relationships"];
                return attribute == null ? string.Empty : attribute.Value;
            }

            return string.Empty;
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private sealed class WorkbookSheetInfo
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }
    }

    internal sealed class XlsxSheetSnapshot
    {
        public string Name { get; set; }
        public string[,] Cells { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
    }
}
