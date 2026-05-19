using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CostAnalysis.App.Services
{
    internal sealed class ExcelQuoteImportService
    {
        private static readonly Regex MaterialCodeRegex =
            new Regex(@"(?<code>(?:\d+-)?\d{2}\.\d{2}\.[A-Za-z0-9xX]{4,}(?:[-.][A-Za-z0-9]+)*)", RegexOptions.Compiled);

        private static readonly Regex DateRegex =
            new Regex(@"日期[:：]?\s*(?<date>\d{4}[-/.]\d{1,2}[-/.]\d{1,2})", RegexOptions.Compiled);

        private static readonly Regex QuoteNoRegex =
            new Regex(@"报价单号[:：]?\s*(?<no>[A-Za-z0-9/._-]+)", RegexOptions.Compiled);

        private static readonly Regex GramWeightRegex =
            new Regex(@"(?<weight>\d+(?:\.\d+)?\s*(?:g|G|克|#))", RegexOptions.Compiled);

        private static readonly Regex SizeRegex =
            new Regex(@"(?:尺寸|规格|成品尺寸)?[:：]?\s*(?<size>\d+(?:\.\d+)?\s*[*xX×]\s*\d+(?:\.\d+)?(?:\s*[*xX×]\s*\d+(?:\.\d+)?)?\s*(?:mm|MM|毫米)?)", RegexOptions.Compiled);

        public QuoteImportPreview Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("请选择报价单文件。", "filePath");
            }

            Exception xlsxReadException = null;
            if (string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var xlsxPreview = ImportXlsxWithoutExcel(filePath);
                    if (xlsxPreview != null && xlsxPreview.Items.Count > 0)
                    {
                        return xlsxPreview;
                    }
                }
                catch (Exception ex)
                {
                    xlsxReadException = ex;
                }
            }

            var excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                if (string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase) && xlsxReadException == null)
                {
                    return CreateEmptyPreview(filePath);
                }

                throw new InvalidOperationException("当前系统没有可用的 Excel COM，老版 .xls 报价单仍需要通过 Excel 兼容读取。", xlsxReadException);
            }

            return ImportWithExcelCom(filePath, excelType);
        }

        private static QuoteImportPreview CreateEmptyPreview(string filePath)
        {
            return new QuoteImportPreview
            {
                FilePath = filePath,
                TemplateType = "待识别",
                Confidence = 0,
                Items = new List<QuoteImportItem>()
            };
        }

        private static QuoteImportPreview ImportXlsxWithoutExcel(string filePath)
        {
            var preview = CreateEmptyPreview(filePath);
            var snapshots = new LightweightXlsxReader().ReadSheets(filePath);
            foreach (var snapshot in snapshots)
            {
                var candidate = ReadCellsAsPreview(snapshot.Name, snapshot.Cells, snapshot.Rows, snapshot.Columns);
                if (candidate != null && candidate.Confidence > preview.Confidence)
                {
                    candidate.FilePath = filePath;
                    preview = candidate;
                }
            }

            return preview;
        }

        private static QuoteImportPreview ImportWithExcelCom(string filePath, Type excelType)
        {
            dynamic excel = null;
            dynamic workbook = null;
            var preview = CreateEmptyPreview(filePath);

            try
            {
                excel = Activator.CreateInstance(excelType);
                excel.Visible = false;
                excel.DisplayAlerts = false;
                workbook = excel.Workbooks.Open(filePath, 0, true);

                for (var sheetIndex = 1; sheetIndex <= workbook.Worksheets.Count; sheetIndex++)
                {
                    dynamic sheet = workbook.Worksheets.Item[sheetIndex];
                    var candidate = ReadSheet(sheet);
                    ReleaseCom(sheet);

                    if (candidate != null && candidate.Confidence > preview.Confidence)
                    {
                        candidate.FilePath = filePath;
                        preview = candidate;
                    }
                }
            }
            finally
            {
                if (workbook != null)
                {
                    workbook.Close(false);
                    ReleaseCom(workbook);
                }

                if (excel != null)
                {
                    excel.Quit();
                    ReleaseCom(excel);
                }
            }

            return preview;
        }

        public static CodeNameSplit SplitCodeAndName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new CodeNameSplit();
            }

            var match = MaterialCodeRegex.Match(text);
            if (!match.Success)
            {
                return new CodeNameSplit { Name = text.Trim() };
            }

            var code = match.Groups["code"].Value.Trim();
            var name = text.Replace(code, string.Empty).Trim(' ', '-', '_', '/', '\\');
            return new CodeNameSplit { Code = code, Name = name };
        }

        public static PriceTier MatchTier(List<PriceTier> tiers, decimal quantity)
        {
            if (tiers == null || tiers.Count == 0)
            {
                return null;
            }

            PriceTier best = null;
            foreach (var tier in tiers)
            {
                if (!tier.MinQuantity.HasValue)
                {
                    continue;
                }

                int min = tier.MinQuantity.Value;
                int? max = tier.MaxQuantity;
                if (max.HasValue && quantity >= min && quantity <= max.Value)
                {
                    return tier;
                }

                if (!max.HasValue && quantity >= min)
                {
                    if (best == null || min > best.MinQuantity.GetValueOrDefault())
                    {
                        best = tier;
                    }
                }
            }

            return best ?? tiers[0];
        }

        private static QuoteImportPreview ReadSheet(dynamic sheet)
        {
            dynamic usedRange = null;
            try
            {
                usedRange = sheet.UsedRange;
                var rows = Math.Min((int)usedRange.Rows.Count, 80);
                var cols = Math.Min((int)usedRange.Columns.Count, 30);
                var cells = ReadCells(sheet, rows, cols);
                return ReadCellsAsPreview(Convert.ToString(sheet.Name), cells, rows, cols);
            }
            finally
            {
                if (usedRange != null)
                {
                    ReleaseCom(usedRange);
                }
            }
        }

        private static QuoteImportPreview ReadCellsAsPreview(string sheetName, string[,] cells, int rows, int cols)
        {
            var header = FindHeader(cells, rows, cols);
            if (header == null)
            {
                return null;
            }

            var preview = new QuoteImportPreview
            {
                SheetName = sheetName,
                Supplier = FindSupplier(cells, rows, cols),
                QuoteDate = FindFirst(DateRegex, cells, rows, cols, "date"),
                QuoteNo = FindFirst(QuoteNoRegex, cells, rows, cols, "no"),
                TemplateType = header.TemplateType,
                Confidence = Math.Min(1, header.Score / 6.0),
                HeaderRow = header.Row,
                QuantityRow = header.QuantityRow,
                DataStartRow = header.DataStartRow,
                RawSheet = QuoteRawSheetPreview.FromCells(cells, rows, cols),
                Items = new List<QuoteImportItem>()
            };

            for (var row = header.DataStartRow; row <= rows; row++)
            {
                var first = Get(cells, row, header.NoColumn);
                var nameText = Get(cells, row, header.NameColumn);
                if (ContainsAny(nameText, "以下空白") || ContainsAny(first, "以下空白"))
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(nameText))
                {
                    continue;
                }

                if (header.NoColumn > 0 && !string.IsNullOrWhiteSpace(first) && !IsSequenceCell(first))
                {
                    continue;
                }

                var split = SplitCodeAndName(nameText);
                var process = Get(cells, row, header.ProcessColumn);
                var size = Get(cells, row, header.SizeColumn);
                if (string.IsNullOrWhiteSpace(size))
                {
                    size = ExtractSize(process);
                }

                var item = new QuoteImportItem
                {
                    RawName = nameText,
                    MaterialCode = split.Code,
                    MaterialName = split.Name,
                    FinishedSize = size,
                    MaterialProcess = process,
                    MaterialNameExtracted = ExtractMaterialName(process),
                    GramWeight = ExtractGramWeight(process),
                    UsageQuantity = ParseDecimal(Get(cells, row, header.UsageColumn)),
                    PriceTiers = new List<PriceTier>()
                };

                foreach (var priceColumn in header.PriceColumns)
                {
                    decimal? unitPrice = ParseDecimal(Get(cells, row, priceColumn.Column));
                    if (!unitPrice.HasValue)
                    {
                        continue;
                    }

                    item.PriceTiers.Add(new PriceTier
                    {
                        Label = priceColumn.Label,
                        MinQuantity = priceColumn.MinQuantity,
                        MaxQuantity = priceColumn.MaxQuantity,
                        UnitPrice = unitPrice
                    });
                }

                if (item.PriceTiers.Count > 0)
                {
                    preview.Items.Add(item);
                }
            }

            return preview.Items.Count > 0 ? preview : null;
        }

        private static string[,] ReadCells(dynamic sheet, int rows, int cols)
        {
            var cells = new string[rows + 1, cols + 1];
            for (var row = 1; row <= rows; row++)
            {
                for (var col = 1; col <= cols; col++)
                {
                    dynamic cell = null;
                    try
                    {
                        cell = sheet.Cells.Item[row, col];
                        cells[row, col] = Clean(Convert.ToString(cell.Text));
                    }
                    finally
                    {
                        if (cell != null)
                        {
                            ReleaseCom(cell);
                        }
                    }
                }
            }

            return cells;
        }

        private static HeaderCandidate FindHeader(string[,] cells, int rows, int cols)
        {
            HeaderCandidate best = null;
            for (var row = 1; row <= Math.Min(rows, 30); row++)
            {
                var candidate = ScoreHeader(cells, row, cols);
                if (candidate != null && (best == null || candidate.Score > best.Score))
                {
                    best = candidate;
                }
            }

            return best != null && best.Score >= 4 ? best : null;
        }

        private static HeaderCandidate ScoreHeader(string[,] cells, int row, int cols)
        {
            var candidate = new HeaderCandidate { Row = row };
            for (var col = 1; col <= cols; col++)
            {
                var text = Get(cells, row, col);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (ContainsAny(text, "序号"))
                {
                    candidate.NoColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "产品型号", "料号及名称", "物料名称", "产品名称", "名称"))
                {
                    candidate.NameColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "成品尺寸", "规格尺寸", "尺寸"))
                {
                    candidate.SizeColumn = col;
                    candidate.Score++;
                }
                else if (IsUsageHeader(text))
                {
                    candidate.UsageColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "材质/工艺", "材质工艺", "规格描述", "材质"))
                {
                    candidate.ProcessColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "数量", "报价", "单价") || LooksLikeQuantityLabel(text))
                {
                    candidate.Score++;
                }
            }

            if (candidate.NameColumn == 0)
            {
                return null;
            }

            var priceColumns = DetectPriceColumns(cells, row, row + 1, cols);
            if (priceColumns.Count == 0)
            {
                return null;
            }

            candidate.PriceColumns = priceColumns;
            var quantityOnHeader = priceColumns.Exists(x => x.LabelSourceRow == row);
            candidate.QuantityRow = quantityOnHeader ? row : row + 1;
            candidate.DataStartRow = quantityOnHeader ? row + 1 : row + 2;
            candidate.TemplateType = candidate.ProcessColumn > 0 ? "普通报价单" : "新版报价单";
            candidate.Score += Math.Min(priceColumns.Count, 2);
            return candidate;
        }

        private static List<PriceColumn> DetectPriceColumns(string[,] cells, int headerRow, int nextRow, int cols)
        {
            var columns = new List<PriceColumn>();
            for (var col = 1; col <= cols; col++)
            {
                var headerText = Get(cells, headerRow, col);
                if (LooksLikeQuantityLabel(headerText))
                {
                    columns.Add(ParsePriceColumn(col, headerText, headerRow));
                }
            }

            if (columns.Count > 0)
            {
                return columns;
            }

            for (var col = 1; col <= cols; col++)
            {
                var nextText = Get(cells, nextRow, col);
                if (LooksLikeQuantityLabel(nextText))
                {
                    columns.Add(ParsePriceColumn(col, nextText, nextRow));
                }
            }

            return columns;
        }

        private static PriceColumn ParsePriceColumn(int column, string label, int sourceRow)
        {
            var result = new PriceColumn { Column = column, Label = label, LabelSourceRow = sourceRow };
            var normalized = label.Replace("MOQ", string.Empty).Replace("个", string.Empty).Trim();
            var match = Regex.Match(normalized, @"(?<min>\d+)\s*[-~至]\s*(?<max>\d+)");
            if (match.Success)
            {
                result.MinQuantity = int.Parse(match.Groups["min"].Value);
                result.MaxQuantity = int.Parse(match.Groups["max"].Value);
                return result;
            }

            match = Regex.Match(normalized, @"(?<qty>\d+)");
            if (match.Success)
            {
                result.MinQuantity = int.Parse(match.Groups["qty"].Value);
            }

            return result;
        }

        private static string FindSupplier(string[,] cells, int rows, int cols)
        {
            for (var row = 1; row <= Math.Min(rows, 8); row++)
            {
                for (var col = 1; col <= Math.Min(cols, 6); col++)
                {
                    var text = Get(cells, row, col);
                    if (text.Contains("有限公司"))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

        private static string FindFirst(Regex regex, string[,] cells, int rows, int cols, string groupName)
        {
            for (var row = 1; row <= Math.Min(rows, 10); row++)
            {
                for (var col = 1; col <= Math.Min(cols, 8); col++)
                {
                    var match = regex.Match(Get(cells, row, col));
                    if (match.Success)
                    {
                        return match.Groups[groupName].Value;
                    }
                }
            }

            return string.Empty;
        }

        private static string ExtractMaterialName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var parts = text.Split(new[] { '+', '＋', ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? string.Empty : parts[0].Trim();
        }

        private static string ExtractGramWeight(string text)
        {
            var match = GramWeightRegex.Match(text ?? string.Empty);
            return match.Success ? match.Groups["weight"].Value.Replace(" ", string.Empty) : string.Empty;
        }

        private static string ExtractSize(string text)
        {
            var match = SizeRegex.Match(text ?? string.Empty);
            return match.Success ? NormalizeSize(match.Groups["size"].Value) : string.Empty;
        }

        private static string NormalizeSize(string value)
        {
            return (value ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("×", "*")
                .Replace("x", "*")
                .Replace("X", "*")
                .Trim('，', ',', '。', '.', ';', '；');
        }

        private static bool IsSequenceCell(string text)
        {
            int number;
            return int.TryParse(text, out number) && number > 0 && number < 10000;
        }

        private static bool LooksLikeQuantityLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = text.Replace(" ", string.Empty);
            return Regex.IsMatch(normalized, @"^(MOQ)?\d+个?$") ||
                   Regex.IsMatch(normalized, @"^\d+\s*[-~至]\s*\d+$");
        }

        private static bool IsUsageHeader(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = Regex.Replace(text, @"\s+", string.Empty);
            return string.Equals(normalized, "用量", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "数量", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalizedText = Regex.Replace(text, @"\s+", string.Empty);
            foreach (var needle in needles)
            {
                var normalizedNeedle = Regex.Replace(needle, @"\s+", string.Empty);
                if (normalizedText.IndexOf(normalizedNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string Get(string[,] cells, int row, int col)
        {
            if (row <= 0 || col <= 0 || row >= cells.GetLength(0) || col >= cells.GetLength(1))
            {
                return string.Empty;
            }

            return cells[row, col] ?? string.Empty;
        }

        private static decimal? ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            decimal number;
            return decimal.TryParse(value.Trim(), out number) ? number : (decimal?)null;
        }

        private static void ReleaseCom(object instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }

        private sealed class HeaderCandidate
        {
            public int Row { get; set; }
            public int QuantityRow { get; set; }
            public int DataStartRow { get; set; }
            public int NoColumn { get; set; }
            public int NameColumn { get; set; }
            public int SizeColumn { get; set; }
            public int UsageColumn { get; set; }
            public int ProcessColumn { get; set; }
            public int Score { get; set; }
            public string TemplateType { get; set; }
            public List<PriceColumn> PriceColumns { get; set; }
        }

        private sealed class PriceColumn
        {
            public int Column { get; set; }
            public string Label { get; set; }
            public int LabelSourceRow { get; set; }
            public int? MinQuantity { get; set; }
            public int? MaxQuantity { get; set; }
        }
    }

    internal sealed class QuoteImportPreview
    {
        public string FilePath { get; set; }
        public string Supplier { get; set; }
        public string QuoteDate { get; set; }
        public string QuoteNo { get; set; }
        public string SheetName { get; set; }
        public string TemplateType { get; set; }
        public double Confidence { get; set; }
        public int HeaderRow { get; set; }
        public int QuantityRow { get; set; }
        public int DataStartRow { get; set; }
        public QuoteRawSheetPreview RawSheet { get; set; }
        public List<QuoteImportItem> Items { get; set; }
    }

    internal sealed class QuoteRawSheetPreview
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public string[,] Cells { get; set; }

        public static QuoteRawSheetPreview FromCells(string[,] cells, int rows, int columns)
        {
            var copy = new string[rows + 1, columns + 1];
            for (var row = 1; row <= rows; row++)
            {
                for (var column = 1; column <= columns; column++)
                {
                    copy[row, column] = cells[row, column];
                }
            }

            return new QuoteRawSheetPreview
            {
                Rows = rows,
                Columns = columns,
                Cells = copy
            };
        }
    }

    internal sealed class QuoteImportItem
    {
        public string RawName { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string FinishedSize { get; set; }
        public string MaterialProcess { get; set; }
        public string MaterialNameExtracted { get; set; }
        public string GramWeight { get; set; }
        public decimal? UsageQuantity { get; set; }
        public List<PriceTier> PriceTiers { get; set; }
    }

    internal sealed class PriceTier
    {
        public string Label { get; set; }
        public int? MinQuantity { get; set; }
        public int? MaxQuantity { get; set; }
        public decimal? UnitPrice { get; set; }
    }

    internal sealed class CodeNameSplit
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }
}
