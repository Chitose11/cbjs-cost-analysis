using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
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

            var extension = Path.GetExtension(filePath);
            if (IsPdf(extension))
            {
                return ImportPdfAutoPreview(filePath);
            }

            if (IsImage(extension))
            {
                return ImportImageOcrPreview(filePath);
            }

            Exception xlsxReadException = null;
            if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var xlsxPreview = ImportXlsxWithoutExcel(filePath);
                    if (xlsxPreview != null && (xlsxPreview.Items.Count > 0 || xlsxPreview.RawSheet != null))
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
                if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) && xlsxReadException == null)
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

        private static bool IsPdf(string extension)
        {
            return string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImage(string extension)
        {
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase);
        }

        private static QuoteImportPreview ImportPdfAutoPreview(string filePath)
        {
            var lines = new ExternalTextExtractionService().ExtractPdfTextLines(filePath);
            if (lines.Count == 0)
            {
                lines = ExtractPdfTextLines(filePath);
            }

            if (lines.Count == 0)
            {
                lines.Add("未从 PDF 中提取到可靠文本，当前 PDF 可能是扫描件或使用了无法反解的内嵌字体编码。");
                lines.Add("建议：安装 Poppler 的 pdftotext 后重试；如果是扫描件，请安装 tesseract OCR 或把 OCR 文本保存为同名 .txt 后重新导入。");
            }

            var preview = CreateDocumentPreview(filePath, "PDF自动解析", lines);
            preview.Items = DocumentQuoteTextParser.Parse(lines);
            if (preview.Items.Count > 0)
            {
                preview.Confidence = 0.55;
                preview.TemplateType = "PDF自动解析";
            }

            return preview;
        }

        private static QuoteImportPreview ImportImageOcrPreview(string filePath)
        {
            var lines = new ExternalTextExtractionService().ExtractImageTextLines(filePath);
            if (lines.Count == 0)
            {
                lines.Add("图片文件：" + Path.GetFileName(filePath));
                lines.Add("未检测到可用 OCR 文本。可安装 tesseract OCR，或放置同名 .txt 文本后重新导入。");
            }

            var preview = CreateDocumentPreview(filePath, "图片OCR自动解析", lines);
            preview.Items = DocumentQuoteTextParser.Parse(lines);
            if (preview.Items.Count > 0)
            {
                preview.Confidence = 0.45;
                preview.TemplateType = "图片OCR自动解析";
            }

            return preview;
        }

        private static QuoteImportPreview ImportPdfTextPreview(string filePath)
        {
            var lines = ExtractPdfTextLines(filePath);
            if (lines.Count == 0)
            {
                lines.Add("未从 PDF 中提取到可复制文本。可在导入确认页使用 AI 辅助识别，或后续接入 OCR。");
            }

            return CreateDocumentPreview(filePath, "PDF文本预览", lines);
        }

        private static QuoteImportPreview ImportImagePlaceholder(string filePath)
        {
            return CreateDocumentPreview(filePath, "图片报价单待OCR", new List<string>
            {
                "图片文件：" + Path.GetFileName(filePath),
                "当前轻量版不内置本地 OCR。可保留为待识别文件，后续接入视觉 OCR API 后自动识别。"
            });
        }

        private static QuoteImportPreview CreateDocumentPreview(string filePath, string templateType, List<string> lines)
        {
            var rows = Math.Max(1, Math.Min(80, lines.Count));
            var cells = new string[rows + 1, 2];
            for (var i = 0; i < rows; i++)
            {
                cells[i + 1, 1] = lines[i];
            }

            return new QuoteImportPreview
            {
                FilePath = filePath,
                SheetName = Path.GetFileName(filePath),
                TemplateType = templateType,
                Confidence = 0.1,
                HeaderRow = 0,
                QuantityRow = 0,
                DataStartRow = 0,
                RawSheet = QuoteRawSheetPreview.FromCells(cells, rows, 1),
                Items = new List<QuoteImportItem>()
            };
        }

        private static List<string> ExtractPdfTextLines(string filePath)
        {
            var result = new List<string>();
            var bytes = File.ReadAllBytes(filePath);
            var content = Encoding.GetEncoding(28591).GetString(bytes);
            foreach (Match match in Regex.Matches(content, @"<<(?:.|\n|\r)*?/FlateDecode(?:.|\n|\r)*?>>\s*stream\r?\n(?<data>.*?)\r?\nendstream", RegexOptions.Singleline))
            {
                var streamText = TryInflatePdfStream(Encoding.GetEncoding(28591).GetBytes(match.Groups["data"].Value));
                AddPdfTextFragments(result, streamText);
                if (result.Count >= 80)
                {
                    break;
                }
            }

            if (result.Count == 0)
            {
                AddPdfTextFragments(result, content);
            }

            return NormalizePdfLines(result);
        }

        private static string TryInflatePdfStream(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            var attempts = new List<byte[]> { data };
            if (data.Length > 2)
            {
                var trimmed = new byte[data.Length - 2];
                Buffer.BlockCopy(data, 2, trimmed, 0, trimmed.Length);
                attempts.Add(trimmed);
            }

            foreach (var attempt in attempts)
            {
                try
                {
                    using (var input = new MemoryStream(attempt))
                    using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        deflate.CopyTo(output);
                        return Encoding.GetEncoding(28591).GetString(output.ToArray());
                    }
                }
                catch
                {
                    // Try the next stream shape.
                }
            }

            return string.Empty;
        }

        private static void AddPdfTextFragments(List<string> lines, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (Match match in Regex.Matches(text, @"\((?<text>(?:\\.|[^\\)])*)\)"))
            {
                var value = DecodePdfLiteral(match.Groups["text"].Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    lines.Add(value);
                }
            }

            foreach (Match match in Regex.Matches(text, @"<(?<hex>(?:FEFF)?[0-9A-Fa-f]{4,})>"))
            {
                var value = DecodePdfHex(match.Groups["hex"].Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    lines.Add(value);
                }
            }
        }

        private static string DecodePdfLiteral(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\(", "(")
                .Replace("\\)", ")")
                .Replace("\\\\", "\\")
                .Replace("\\r", " ")
                .Replace("\\n", " ")
                .Trim();
        }

        private static string DecodePdfHex(string hex)
        {
            try
            {
                if (hex.StartsWith("FEFF", StringComparison.OrdinalIgnoreCase))
                {
                    hex = hex.Substring(4);
                    var bytes = HexToBytes(hex);
                    return Encoding.BigEndianUnicode.GetString(bytes).Trim();
                }

                return Encoding.ASCII.GetString(HexToBytes(hex)).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 == 1)
            {
                hex += "0";
            }

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private static List<string> NormalizePdfLines(List<string> fragments)
        {
            var result = new List<string>();
            foreach (var fragment in fragments)
            {
                var cleaned = Clean(fragment);
                if (string.IsNullOrWhiteSpace(cleaned) ||
                    cleaned.Length <= 2 ||
                    LooksLikeEncodedGlyph(cleaned) ||
                    LooksLikePdfMetadata(cleaned) ||
                    LooksLikePdfEncodedNoise(cleaned) ||
                    !HasReadableText(cleaned))
                {
                    continue;
                }

                result.Add(cleaned);
                if (result.Count >= 80)
                {
                    break;
                }
            }

            return result;
        }

        private static bool HasReadableText(string value)
        {
            var readable = 0;
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch) || IsChinese(ch) || "：:，,。./*-+（）()[]【】 ".IndexOf(ch) >= 0)
                {
                    readable++;
                }
            }

            return readable >= Math.Max(2, value.Length / 2);
        }

        private static bool LooksLikeEncodedGlyph(string value)
        {
            return Regex.IsMatch(value ?? string.Empty, @"^[0-9A-Fa-f]{2,6}$");
        }

        private static bool LooksLikePdfEncodedNoise(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var controlOrReplacement = 0;
            var latinExtended = 0;
            var symbol = 0;
            var chinese = 0;
            var digit = 0;
            foreach (var ch in value)
            {
                if (char.IsControl(ch) || ch == '\ufffd')
                {
                    controlOrReplacement++;
                }

                if (ch >= 0x00c0 && ch <= 0x024f)
                {
                    latinExtended++;
                }

                if (!char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch) && !IsChinese(ch))
                {
                    symbol++;
                }

                if (IsChinese(ch))
                {
                    chinese++;
                }

                if (char.IsDigit(ch))
                {
                    digit++;
                }
            }

            var length = Math.Max(1, value.Length);
            if (controlOrReplacement > 0)
            {
                return true;
            }

            if (chinese == 0 && digit == 0 && latinExtended * 1.0 / length > 0.15)
            {
                return true;
            }

            if (chinese == 0 && digit == 0 && symbol * 1.0 / length > 0.35)
            {
                return true;
            }

            return false;
        }

        private static bool LooksLikePdfMetadata(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.StartsWith("D:", StringComparison.OrdinalIgnoreCase) &&
                Regex.IsMatch(text, @"^D:\d{8,}"))
            {
                return true;
            }

            if (text.IndexOf("KONICA MINOLTA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("bizhub", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Acrobat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static bool IsChinese(char ch)
        {
            return ch >= 0x4e00 && ch <= 0x9fff;
        }

        private static QuoteImportPreview ImportXlsxWithoutExcel(string filePath)
        {
            var preview = CreateEmptyPreview(filePath);
            var snapshots = new LightweightXlsxReader().ReadSheets(filePath);
            foreach (var snapshot in snapshots)
            {
                var candidate = AnalyzeCellsAsPreview(snapshot.Name, snapshot.Cells, snapshot.Rows, snapshot.Columns);
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
                return AnalyzeCellsAsPreview(Convert.ToString(sheet.Name), cells, rows, cols);
            }
            finally
            {
                if (usedRange != null)
                {
                    ReleaseCom(usedRange);
                }
            }
        }

        private static QuoteImportPreview AnalyzeCellsAsPreview(string sheetName, string[,] cells, int rows, int cols)
        {
            return ReadCellsAsPreview(sheetName, cells, rows, cols) ?? CreateUnrecognizedSheetPreview(sheetName, cells, rows, cols);
        }

        private static QuoteImportPreview CreateUnrecognizedSheetPreview(string sheetName, string[,] cells, int rows, int cols)
        {
            return new QuoteImportPreview
            {
                SheetName = sheetName,
                Supplier = FindSupplier(cells, rows, cols),
                TemplateType = "待识别",
                Confidence = CalculateRawSheetConfidence(cells, rows, cols),
                HeaderRow = 0,
                QuantityRow = 0,
                DataStartRow = 0,
                RawSheet = QuoteRawSheetPreview.FromCells(cells, rows, cols),
                Items = new List<QuoteImportItem>()
            };
        }

        private static double CalculateRawSheetConfidence(string[,] cells, int rows, int cols)
        {
            var score = 0;
            for (var row = 1; row <= Math.Min(rows, 30); row++)
            {
                for (var col = 1; col <= Math.Min(cols, 20); col++)
                {
                    var text = Get(cells, row, col);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    if (ContainsAny(text, "料号", "物料编码", "产品型号")) score += 2;
                    if (ContainsAny(text, "名称", "产品名称", "物料名称")) score += 2;
                    if (ContainsAny(text, "总长", "展开总长")) score += 2;
                    if (ContainsAny(text, "总宽", "展开总宽")) score += 2;
                    if (ContainsAny(text, "基价")) score += 2;
                    if (ContainsAny(text, "成品单价", "单价（元）", "单价(元)")) score += 2;
                    if (ContainsAny(text, "材质", "规格", "纸箱", "纸盒", "核价", "计价公式")) score++;
                }
            }

            return Math.Min(0.2, 0.01 + score * 0.005);
        }

        private static QuoteImportPreview ReadCellsAsPreview(string sheetName, string[,] cells, int rows, int cols)
        {
            var learnedPreview = new LearnedQuoteTemplateService().TryApply(sheetName, cells, rows, cols);
            if (learnedPreview != null && learnedPreview.Items.Count > 0)
            {
                return learnedPreview;
            }

            var headers = FindHeaders(cells, rows, cols);
            if (headers.Count == 0)
            {
                return null;
            }

            QuoteImportPreview fallback = null;
            foreach (var header in headers)
            {
                var preview = ReadCellsAsPreview(sheetName, cells, rows, cols, header);
                if (preview.Items.Count > 0)
                {
                    return preview;
                }

                if (fallback == null)
                {
                    fallback = preview;
                }
            }

            return fallback ?? CreateUnrecognizedSheetPreview(sheetName, cells, rows, cols);
        }

        private static QuoteImportPreview ReadCellsAsPreview(string sheetName, string[,] cells, int rows, int cols, HeaderCandidate header)
        {
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
                if (header.CodeColumn > 0)
                {
                    split.Code = Get(cells, row, header.CodeColumn);
                    split.Name = nameText;
                }
                else if (header.IsPaperboardBaseTemplate)
                {
                    split.Code = Get(cells, row, header.MaterialTypeColumn);
                    split.Name = nameText;
                }

                var process = Get(cells, row, header.ProcessColumn);
                if (string.IsNullOrWhiteSpace(process) && header.IsCartonTemplate)
                {
                    process = BuildCartonProcess(cells, row, header);
                }
                else if (!string.IsNullOrWhiteSpace(process) && header.IsCartonTemplate)
                {
                    var cartonProcess = BuildCartonProcess(cells, row, header);
                    if (!string.IsNullOrWhiteSpace(cartonProcess) &&
                        process.IndexOf(cartonProcess, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        process = process + "；" + cartonProcess;
                    }
                }
                else if (string.IsNullOrWhiteSpace(process) && header.IsPaperboardBaseTemplate)
                {
                    process = BuildPaperboardProcess(cells, row, header);
                }

                var size = Get(cells, row, header.SizeColumn);
                if (string.IsNullOrWhiteSpace(size) && header.IsCartonTemplate)
                {
                    size = BuildCartonSize(cells, row, header);
                }

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
                    MaterialNameExtracted = header.IsPaperboardBaseTemplate ? BuildPaperboardMaterialName(cells, row, header) : ExtractMaterialName(process),
                    GramWeight = header.IsPaperboardBaseTemplate ? Get(cells, row, header.PaperWeightColumn) : ExtractGramWeight(process),
                    UsageQuantity = ParseDecimal(Get(cells, row, header.UsageColumn)),
                    PriceTiers = new List<PriceTier>()
                };

                foreach (var priceColumn in header.PriceColumns)
                {
                    decimal? unitPrice = ParseDecimal(Get(cells, row, priceColumn.Column));
                    if (!unitPrice.HasValue && header.IsCartonTemplate)
                    {
                        unitPrice = CalculateCartonUnitPrice(cells, row, header);
                    }

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

                if (item.PriceTiers.Count > 0 || header.IsPaperboardBaseTemplate)
                {
                    preview.Items.Add(item);
                }
            }

            return preview;
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
            var headers = FindHeaders(cells, rows, cols);
            return headers.Count == 0 ? null : headers[0];
        }

        private static List<HeaderCandidate> FindHeaders(string[,] cells, int rows, int cols)
        {
            var headers = new List<HeaderCandidate>();
            for (var row = 1; row <= Math.Min(rows, 30); row++)
            {
                var candidate = ScoreHeader(cells, row, cols);
                if (candidate != null && candidate.Score >= 4)
                {
                    headers.Add(candidate);
                }
            }

            headers.Sort((a, b) => b.Score.CompareTo(a.Score));
            return headers;
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
                else if (ContainsAny(text, "产品型号", "料号及名称", "物料名称", "产品名称", "名称", "品名"))
                {
                    candidate.NameColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "料号", "物料编码"))
                {
                    candidate.CodeColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "成品尺寸", "规格尺寸", "尺寸"))
                {
                    candidate.SizeColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "总长", "展开总长"))
                {
                    candidate.TotalLengthColumn = col;
                    candidate.TotalLengthExtraInch = ExtractExtraInch(text);
                    candidate.TotalLengthInInch = IsInchHeader(text);
                    candidate.Score++;
                }
                else if (ContainsAny(text, "总宽", "展开总宽"))
                {
                    candidate.TotalWidthColumn = col;
                    candidate.TotalWidthExtraInch = ExtractExtraInch(text);
                    candidate.TotalWidthInInch = IsInchHeader(text);
                    candidate.Score++;
                }
                else if (ContainsAny(text, "基价"))
                {
                    candidate.BasePriceColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "材质类型"))
                {
                    candidate.MaterialTypeColumn = col;
                    candidate.Score++;
                }
                else if (ContainsAny(text, "成品单价", "单价（元）", "单价(元)", "单价"))
                {
                    candidate.CartonUnitPriceColumn = col;
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
                else if (ContainsAny(text, "公式", "计价公式", "计算公式"))
                {
                    candidate.FormulaColumn = col;
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
            if (candidate.BasePriceColumn > 0 &&
                candidate.NameColumn > 0 &&
                candidate.MaterialTypeColumn > 0 &&
                candidate.TotalLengthColumn == 0 &&
                candidate.TotalWidthColumn == 0)
            {
                DetectPaperboardSubHeaders(cells, row + 1, cols, candidate);
                candidate.IsPaperboardBaseTemplate = true;
                candidate.PriceColumns = new List<PriceColumn>
                {
                    new PriceColumn
                    {
                        Column = candidate.BasePriceColumn,
                        Label = "基价(元/千平方英寸)",
                        LabelSourceRow = row
                    }
                };
                candidate.QuantityRow = row;
                candidate.DataStartRow = FindFirstDataRow(cells, row + 1, candidate.NoColumn, candidate.NameColumn);
                candidate.TemplateType = "纸板基价表";
                candidate.Score += 3;
                return candidate;
            }

            if (candidate.CartonUnitPriceColumn > 0 &&
                (candidate.TotalLengthColumn > 0 || candidate.TotalWidthColumn > 0 || candidate.BasePriceColumn > 0))
            {
                candidate.IsCartonTemplate = true;
                candidate.PriceColumns = new List<PriceColumn>
                {
                    new PriceColumn
                    {
                        Column = candidate.CartonUnitPriceColumn,
                        Label = "成品单价",
                        LabelSourceRow = row
                    }
                };
                candidate.QuantityRow = row;
                candidate.DataStartRow = FindFirstDataRow(cells, row + 1, candidate.NoColumn, candidate.NameColumn);
                candidate.TemplateType = "纸箱/外箱核价报价";
                candidate.Score += 3;
                return candidate;
            }

            if (priceColumns.Count == 0)
            {
                return null;
            }

            candidate.PriceColumns = priceColumns;
            var quantityOnHeader = priceColumns.Exists(x => x.LabelSourceRow == row);
            candidate.QuantityRow = quantityOnHeader ? row : row + 1;
            candidate.DataStartRow = quantityOnHeader ? row + 1 : row + 2;
            var isStickerTemplate = RowContainsStickerKeywords(cells, row, cols) || RowContainsStickerKeywords(cells, row + 1, cols);
            candidate.TemplateType = candidate.ProcessColumn > 0 ? "普通报价单" : "新版报价单";
            if (isStickerTemplate)
            {
                candidate.TemplateType = "\u8d34\u7eb8/\u677f\u8d34\u62a5\u4ef7\u5355";
                candidate.Score++;
            }

            candidate.Score += Math.Min(priceColumns.Count, 2);
            return candidate;
        }

        private static bool RowContainsStickerKeywords(string[,] cells, int row, int cols)
        {
            if (cells == null || row <= 0 || row >= cells.GetLength(0))
            {
                return false;
            }

            for (var col = 1; col <= cols; col++)
            {
                var text = Get(cells, row, col);
                if (ContainsAny(text, "\u8d34\u7eb8", "\u6807\u7b7e", "\u6807\u8d34", "\u677f\u8d34", "\u80f6\u8d34", "PET", "\u4e0d\u5e72\u80f6"))
                {
                    return true;
                }
            }

            return false;
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

        private static int FindFirstDataRow(string[,] cells, int startRow, int noColumn, int nameColumn)
        {
            for (var row = startRow; row < cells.GetLength(0); row++)
            {
                var noText = Get(cells, row, noColumn);
                var nameText = Get(cells, row, nameColumn);
                if (!string.IsNullOrWhiteSpace(nameText) && (noColumn <= 0 || string.IsNullOrWhiteSpace(noText) || IsSequenceCell(noText)))
                {
                    return row;
                }
            }

            return startRow;
        }

        private static void DetectPaperboardSubHeaders(string[,] cells, int row, int cols, HeaderCandidate candidate)
        {
            if (row <= 0 || row >= cells.GetLength(0))
            {
                return;
            }

            for (var col = 1; col <= cols; col++)
            {
                var text = Get(cells, row, col);
                if (ContainsAny(text, "品牌"))
                {
                    candidate.BrandColumn = col;
                }
                else if (ContainsAny(text, "克重"))
                {
                    candidate.PaperWeightColumn = col;
                }
                else if (ContainsAny(text, "耐破"))
                {
                    candidate.BurstColumn = col;
                }
                else if (ContainsAny(text, "边压"))
                {
                    candidate.EdgeColumn = col;
                }
            }
        }

        private static string BuildPaperboardProcess(string[,] cells, int row, HeaderCandidate header)
        {
            var parts = new List<string>();
            var brand = Get(cells, row, header.BrandColumn);
            var weight = Get(cells, row, header.PaperWeightColumn);
            if (!ContainsDigit(weight) && ContainsDigit(brand))
            {
                var temp = brand;
                brand = weight;
                weight = temp;
            }

            AddPart(parts, "材质类型", Get(cells, row, header.MaterialTypeColumn));
            AddPart(parts, "品牌", brand);
            AddPart(parts, "克重", weight);
            AddPart(parts, "耐破", Get(cells, row, header.BurstColumn));
            AddPart(parts, "边压", Get(cells, row, header.EdgeColumn));
            AddPart(parts, "基价", Get(cells, row, header.BasePriceColumn));

            return string.Join("；", parts.ToArray());
        }

        private static string BuildPaperboardMaterialName(string[,] cells, int row, HeaderCandidate header)
        {
            var materialType = Get(cells, row, header.MaterialTypeColumn);
            var name = Get(cells, row, header.NameColumn);
            if (string.IsNullOrWhiteSpace(materialType))
            {
                return name;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return materialType;
            }

            return materialType + "；" + name;
        }

        private static void AddPart(List<string> parts, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(label + " " + value);
            }
        }

        private static bool ContainsDigit(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (var ch in value)
            {
                if (char.IsDigit(ch))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildCartonSize(string[,] cells, int row, HeaderCandidate header)
        {
            var length = Get(cells, row, header.TotalLengthColumn);
            var width = Get(cells, row, header.TotalWidthColumn);
            if (string.IsNullOrWhiteSpace(length) || string.IsNullOrWhiteSpace(width))
            {
                return string.Empty;
            }

            var unit = header.TotalLengthInInch || header.TotalWidthInInch ? "inch" : "MM";
            return length + "*" + width + unit;
        }

        private static string BuildCartonProcess(string[,] cells, int row, HeaderCandidate header)
        {
            var parts = new List<string>();
            var basePrice = Get(cells, row, header.BasePriceColumn);
            if (string.IsNullOrWhiteSpace(basePrice))
            {
                var lookedUpBasePrice = LookupCartonBasePrice(cells, row, header);
                if (lookedUpBasePrice.HasValue)
                {
                    basePrice = lookedUpBasePrice.Value.ToString("0.####");
                }
            }

            if (!string.IsNullOrWhiteSpace(basePrice))
            {
                parts.Add("基价 " + basePrice + " 元/千平方英寸");
            }

            var length = Get(cells, row, header.TotalLengthColumn);
            var width = Get(cells, row, header.TotalWidthColumn);
            if (!string.IsNullOrWhiteSpace(length) || !string.IsNullOrWhiteSpace(width))
            {
                parts.Add("总长 " + length + "，总宽 " + width);
            }

            if (header.TotalLengthExtraInch != 0 || header.TotalWidthExtraInch != 0)
            {
                parts.Add("公式加放 长+" + header.TotalLengthExtraInch.ToString("0.####") + "英寸，宽+" + header.TotalWidthExtraInch.ToString("0.####") + "英寸");
            }

            AddCartonFormulaParts(parts, cells, row, header);

            return string.Join("；", parts.ToArray());
        }

        private static void AddCartonFormulaParts(List<string> parts, string[,] cells, int row, HeaderCandidate header)
        {
            var formula = Get(cells, row, header.FormulaColumn);
            if (!string.IsNullOrWhiteSpace(formula))
            {
                parts.Add("计价公式 " + formula);
            }

            var calculated = CalculateCartonUnitPrice(cells, row, header);
            if (calculated.HasValue)
            {
                parts.Add("公式核价 " + calculated.Value.ToString("0.####"));
            }
        }

        private static decimal? CalculateCartonUnitPrice(string[,] cells, int row, HeaderCandidate header)
        {
            var lengthMm = ParseDecimal(Get(cells, row, header.TotalLengthColumn));
            var widthMm = ParseDecimal(Get(cells, row, header.TotalWidthColumn));
            var basePrice = ParseDecimal(Get(cells, row, header.BasePriceColumn));
            if (!basePrice.HasValue)
            {
                basePrice = LookupCartonBasePrice(cells, row, header);
            }

            if (!lengthMm.HasValue || !widthMm.HasValue || !basePrice.HasValue)
            {
                return null;
            }

            var lengthInch = ConvertCartonLengthToInch(lengthMm.Value, header.TotalLengthInInch) + header.TotalLengthExtraInch;
            var widthInch = ConvertCartonLengthToInch(widthMm.Value, header.TotalWidthInInch) + header.TotalWidthExtraInch;
            if (lengthInch <= 0 || widthInch <= 0)
            {
                return null;
            }

            return Math.Round(lengthInch * widthInch * basePrice.Value / 1000M, 4);
        }

        private static decimal ConvertCartonLengthToInch(decimal value, bool alreadyInInch)
        {
            return alreadyInInch ? value : value / 25.4M;
        }

        private static decimal? LookupCartonBasePrice(string[,] cells, int row, HeaderCandidate header)
        {
            var materialType = Get(cells, row, header.ProcessColumn);
            if (string.IsNullOrWhiteSpace(materialType))
            {
                materialType = Get(cells, row, header.MaterialTypeColumn);
            }

            return LookupBasePriceByMaterialType(cells, materialType, header.Row);
        }

        private static decimal? LookupBasePriceByMaterialType(string[,] cells, string materialType, int beforeRow)
        {
            var normalizedMaterial = NormalizeForMatch(materialType);
            if (string.IsNullOrWhiteSpace(normalizedMaterial))
            {
                return null;
            }

            var maxRow = Math.Min(beforeRow <= 0 ? cells.GetLength(0) - 1 : beforeRow - 1, cells.GetLength(0) - 1);
            for (var row = 1; row <= maxRow; row++)
            {
                for (var col = 1; col < cells.GetLength(1); col++)
                {
                    if (NormalizeForMatch(Get(cells, row, col)) != normalizedMaterial)
                    {
                        continue;
                    }

                    for (var priceCol = col + 1; priceCol < cells.GetLength(1) && priceCol <= col + 5; priceCol++)
                    {
                        var price = ParseDecimal(Get(cells, row, priceCol));
                        if (price.HasValue && price.Value > 0)
                        {
                            return price;
                        }
                    }
                }
            }

            return null;
        }

        private static string NormalizeForMatch(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
        }

        private static bool IsInchHeader(string headerText)
        {
            return ContainsAny(headerText, "鑻卞", "英寸", "inch", "(in");
        }

        private static decimal ExtractExtraInch(string headerText)
        {
            var match = Regex.Match(headerText ?? string.Empty, @"\+(?<extra>\d+(?:\.\d+)?)\s*(?:英寸|inch|in)?", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return 0;
            }

            decimal extra;
            return decimal.TryParse(match.Groups["extra"].Value, out extra) ? extra : 0;
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

            var materials = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parts = Regex.Split(text, @"[+＋,，;；/、]");
            foreach (var rawPart in parts)
            {
                var material = NormalizeMaterialPart(rawPart);
                if (string.IsNullOrWhiteSpace(material) || !LooksLikeMaterial(material))
                {
                    continue;
                }

                if (seen.Add(material))
                {
                    materials.Add(material);
                }
            }

            if (materials.Count > 0)
            {
                return string.Join("；", materials.ToArray());
            }

            var first = text.Split(new[] { '+', '＋', ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
            return first.Length == 0 ? string.Empty : NormalizeMaterialPart(first[0]);
        }

        private static string ExtractGramWeight(string text)
        {
            var weights = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in GramWeightRegex.Matches(text ?? string.Empty))
            {
                var weight = match.Groups["weight"].Value.Replace(" ", string.Empty);
                if (seen.Add(weight))
                {
                    weights.Add(weight);
                }
            }

            return string.Join("；", weights.ToArray());
        }

        private static string NormalizeMaterialPart(string value)
        {
            var text = Clean(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            text = Regex.Replace(text, @"^(材质/工艺|材质|工艺|规格|尺寸)[:：]\s*", string.Empty);
            text = Regex.Replace(text, @"^(单|双)?[面面]?", string.Empty);
            text = Regex.Replace(text, @"^(啤|粘|装箱|运输|礼盒成型|局部UV|UV|哑胶|光胶|印刷|过胶).*$", string.Empty);
            text = Regex.Replace(text, @"\s+", string.Empty);
            return text.Trim('，', ',', '。', '.', ':', '：', '-', '_');
        }

        private static bool LooksLikeMaterial(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (ContainsAny(value, "纸", "板", "卡", "灰", "铜", "胶", "PET", "PVC", "EVA", "海绵", "坑", "楞"))
            {
                return true;
            }

            return Regex.IsMatch(value, @"\d+(?:\.\d+)?\s*(?:g|G|克|#)");
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
            public int CodeColumn { get; set; }
            public int NameColumn { get; set; }
            public int SizeColumn { get; set; }
            public int UsageColumn { get; set; }
            public int ProcessColumn { get; set; }
            public int TotalLengthColumn { get; set; }
            public int TotalWidthColumn { get; set; }
            public decimal TotalLengthExtraInch { get; set; }
            public decimal TotalWidthExtraInch { get; set; }
            public bool TotalLengthInInch { get; set; }
            public bool TotalWidthInInch { get; set; }
            public int BasePriceColumn { get; set; }
            public int FormulaColumn { get; set; }
            public int CartonUnitPriceColumn { get; set; }
            public int MaterialTypeColumn { get; set; }
            public int BrandColumn { get; set; }
            public int PaperWeightColumn { get; set; }
            public int BurstColumn { get; set; }
            public int EdgeColumn { get; set; }
            public bool IsCartonTemplate { get; set; }
            public bool IsPaperboardBaseTemplate { get; set; }
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

        public static QuoteRawSheetPreview FromLines(IList<string> lines)
        {
            var rowCount = Math.Max(1, Math.Min(200, lines == null ? 0 : lines.Count));
            var cells = new string[rowCount + 1, 2];
            for (var row = 1; row <= rowCount; row++)
            {
                cells[row, 1] = lines != null && row - 1 < lines.Count ? lines[row - 1] : string.Empty;
            }

            return FromCells(cells, rowCount, 1);
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
