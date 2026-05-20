using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.Services
{
    internal sealed class DeepSeekClient
    {
        public string BuildQuoteRecognitionRequestJson(AiSettings settings, QuoteImportPreview preview)
        {
            var model = string.IsNullOrWhiteSpace(settings.ModelName) ? "deepseek-v4-flash" : settings.ModelName;
            var userPrompt = BuildQuoteRecognitionUserPrompt(settings, preview);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            WriteJsonProperty(sb, "model", model, 1, true);
            sb.AppendLine("  \"messages\": [");
            sb.AppendLine("    {");
            WriteJsonProperty(sb, "role", "system", 3, true);
            WriteJsonProperty(sb, "content", AiPromptTemplates.QuoteRecognitionSystemPrompt, 3, false);
            sb.AppendLine("    },");
            sb.AppendLine("    {");
            WriteJsonProperty(sb, "role", "user", 3, true);
            WriteJsonProperty(sb, "content", userPrompt, 3, false);
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"response_format\": { \"type\": \"json_object\" },");
            sb.AppendLine("  \"temperature\": 0.1,");
            sb.AppendLine("  \"max_tokens\": 2000");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public AiQuoteRecognitionResult RecognizeQuote(AiSettings settings, QuoteImportPreview preview)
        {
            ValidateSettings(settings);

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var requestJson = BuildQuoteRecognitionRequestJson(settings, preview);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            var request = (HttpWebRequest)WebRequest.Create(BuildEndpoint(settings.ApiUrl));
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("Authorization", "Bearer " + settings.ApiKey);
            request.Timeout = Math.Max(10, settings.TimeoutSeconds) * 1000;
            request.ReadWriteTimeout = request.Timeout;
            request.ContentLength = requestBytes.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(requestBytes, 0, requestBytes.Length);
            }

            string responseText;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    responseText = reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                throw new InvalidOperationException("DeepSeek 调用失败：" + ReadWebException(ex), ex);
            }

            return ParseChatCompletion(responseText);
        }

        public string BuildCostCompletionRequestJson(AiSettings settings, List<AiCostCompletionInput> rows)
        {
            var model = string.IsNullOrWhiteSpace(settings.ModelName) ? "deepseek-v4-flash" : settings.ModelName;
            var userPrompt = BuildCostCompletionUserPrompt(settings, rows);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            WriteJsonProperty(sb, "model", model, 1, true);
            sb.AppendLine("  \"messages\": [");
            sb.AppendLine("    {");
            WriteJsonProperty(sb, "role", "system", 3, true);
            WriteJsonProperty(sb, "content", AiPromptTemplates.CostCompletionSystemPrompt, 3, false);
            sb.AppendLine("    },");
            sb.AppendLine("    {");
            WriteJsonProperty(sb, "role", "user", 3, true);
            WriteJsonProperty(sb, "content", userPrompt, 3, false);
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"response_format\": { \"type\": \"json_object\" },");
            sb.AppendLine("  \"temperature\": 0.1,");
            sb.AppendLine("  \"max_tokens\": 2000");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public AiCostCompletionResult SuggestCosts(AiSettings settings, List<AiCostCompletionInput> rows)
        {
            ValidateSettings(settings);
            if (rows == null || rows.Count == 0)
            {
                throw new InvalidOperationException("没有可发送给 AI 的成本明细。");
            }

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var requestJson = BuildCostCompletionRequestJson(settings, rows);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            var request = (HttpWebRequest)WebRequest.Create(BuildEndpoint(settings.ApiUrl));
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Headers.Add("Authorization", "Bearer " + settings.ApiKey);
            request.Timeout = Math.Max(10, settings.TimeoutSeconds) * 1000;
            request.ReadWriteTimeout = request.Timeout;
            request.ContentLength = requestBytes.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(requestBytes, 0, requestBytes.Length);
            }

            string responseText;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    responseText = reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                throw new InvalidOperationException("DeepSeek 调用失败：" + ReadWebException(ex), ex);
            }

            return ParseCostCompletionChatCompletion(responseText);
        }

        private static void ValidateSettings(AiSettings settings)
        {
            if (settings == null)
            {
                throw new InvalidOperationException("AI 设置为空，请先打开系统设置。");
            }

            if (string.IsNullOrWhiteSpace(settings.ApiUrl))
            {
                throw new InvalidOperationException("请先在系统设置中填写 DeepSeek API 地址。");
            }

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new InvalidOperationException("请先在系统设置中填写 DeepSeek API Key。");
            }
        }

        private static string BuildEndpoint(string apiUrl)
        {
            var url = (apiUrl ?? string.Empty).Trim().TrimEnd('/');
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            return url + "/chat/completions";
        }

        private static string ReadWebException(WebException ex)
        {
            if (ex.Response == null)
            {
                return ex.Message;
            }

            using (var response = (HttpWebResponse)ex.Response)
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var body = reader.ReadToEnd();
                return string.Format("{0} {1} {2}", (int)response.StatusCode, response.StatusDescription, body);
            }
        }

        private static AiQuoteRecognitionResult ParseChatCompletion(string responseText)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 * 8 };
            var root = serializer.DeserializeObject(responseText) as Dictionary<string, object>;
            var choices = GetArray(root, "choices");
            if (choices.Count == 0)
            {
                throw new InvalidOperationException("DeepSeek 返回内容中没有 choices。");
            }

            var firstChoice = choices[0] as Dictionary<string, object>;
            var message = GetDictionary(firstChoice, "message");
            var content = GetString(message, "content");
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("DeepSeek 返回内容为空。");
            }

            Dictionary<string, object> data;
            try
            {
                data = serializer.DeserializeObject(content) as Dictionary<string, object>;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("DeepSeek 返回的不是有效 JSON：" + content, ex);
            }

            var result = new AiQuoteRecognitionResult
            {
                RawContent = content,
                TemplateType = GetString(data, "template_type"),
                Supplier = GetString(data, "supplier"),
                QuoteDate = GetString(data, "quote_date"),
                QuoteNo = GetString(data, "quote_no"),
                Confidence = GetDouble(data, "confidence"),
                HeaderRow = GetNullableInt(data, "header_row"),
                QuantityRow = GetNullableInt(data, "quantity_row"),
                DataStartRow = GetNullableInt(data, "data_start_row"),
                Items = new List<AiQuoteRecognitionItem>(),
                Warnings = GetStringList(data, "warnings")
            };

            foreach (var itemObject in GetArray(data, "items"))
            {
                var itemData = itemObject as Dictionary<string, object>;
                if (itemData == null)
                {
                    continue;
                }

                result.Items.Add(new AiQuoteRecognitionItem
                {
                    Index = GetNullableIntAny(itemData, "index", "no", "序号"),
                    RawName = GetStringAny(itemData, "raw_name", "原始名称"),
                    MaterialCode = GetStringAny(itemData, "material_code", "物料编码"),
                    MaterialName = GetStringAny(itemData, "material_name", "物料名称"),
                    FinishedSize = GetStringAny(itemData, "finished_size", "成品尺寸", "展开尺寸"),
                    MaterialProcess = GetStringAny(itemData, "material_process", "材质/工艺", "工艺"),
                    MaterialNameExtracted = GetStringAny(itemData, "material_name_extracted", "base_material_name", "材料名称"),
                    GramWeight = GetStringAny(itemData, "gram_weight", "原材料克重", "克重"),
                    RequiresReview = GetBool(itemData, "requires_review"),
                    Warnings = GetStringList(itemData, "warnings")
                });
            }

            return result;
        }

        private static AiCostCompletionResult ParseCostCompletionChatCompletion(string responseText)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 * 8 };
            var root = serializer.DeserializeObject(responseText) as Dictionary<string, object>;
            var choices = GetArray(root, "choices");
            if (choices.Count == 0)
            {
                throw new InvalidOperationException("DeepSeek 返回内容中没有 choices。");
            }

            var firstChoice = choices[0] as Dictionary<string, object>;
            var message = GetDictionary(firstChoice, "message");
            var content = GetString(message, "content");
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("DeepSeek 返回内容为空。");
            }

            Dictionary<string, object> data;
            try
            {
                data = serializer.DeserializeObject(content) as Dictionary<string, object>;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("DeepSeek 返回的不是有效 JSON：" + content, ex);
            }

            var result = new AiCostCompletionResult
            {
                RawContent = content,
                Items = new List<AiCostCompletionSuggestion>(),
                Warnings = GetStringList(data, "warnings")
            };

            foreach (var itemObject in GetArray(data, "items"))
            {
                var itemData = itemObject as Dictionary<string, object>;
                if (itemData == null)
                {
                    continue;
                }

                result.Items.Add(new AiCostCompletionSuggestion
                {
                    Index = GetNullableIntAny(itemData, "index", "no", "序号"),
                    MaterialCost = GetNullableDecimal(itemData, "material_cost"),
                    PrintingCost = GetNullableDecimal(itemData, "printing_cost"),
                    PostProcessCost = GetNullableDecimal(itemData, "post_process_cost"),
                    OtherCost = GetNullableDecimal(itemData, "other_cost"),
                    PurchaseUnitPrice = GetNullableDecimal(itemData, "purchase_unit_price"),
                    Confidence = GetDouble(itemData, "confidence"),
                    RequiresReview = GetBool(itemData, "requires_review"),
                    Reason = GetString(itemData, "reason"),
                    Warnings = GetStringList(itemData, "warnings")
                });
            }

            return result;
        }

        private static string BuildQuoteRecognitionUserPrompt(AiSettings settings, QuoteImportPreview preview)
        {
            var sb = new StringBuilder();
            sb.AppendLine("请根据以下报价单识别结果进行复核和结构化补全。");
            sb.AppendLine("注意：这是本地程序初步识别结果，可能存在错误。请只输出 JSON。");
            sb.AppendLine("如果不能确定，请保留原值并设置 requires_review=true 或在 warnings 中说明。");
            sb.AppendLine();
            sb.AppendLine(AiPromptTemplates.QuoteRecognitionSchemaInstruction);
            var templateHint = new QuoteTemplateRepository().BuildAiTemplateHint(preview);
            if (!string.IsNullOrWhiteSpace(templateHint))
            {
                sb.AppendLine();
                sb.AppendLine(templateHint);
            }
            sb.AppendLine();
            sb.AppendLine("报价单基本信息：");
            sb.AppendLine("供应商：" + (settings.AllowSupplierName ? Safe(preview.Supplier) : "[已按设置隐藏]"));
            sb.AppendLine("报价日期：" + Safe(preview.QuoteDate));
            sb.AppendLine("报价单号：" + Safe(preview.QuoteNo));
            sb.AppendLine("Sheet：" + Safe(preview.SheetName));
            sb.AppendLine("模板类型：" + Safe(preview.TemplateType));
            sb.AppendLine("表头行：" + preview.HeaderRow);
            sb.AppendLine("数量行：" + preview.QuantityRow);
            sb.AppendLine("数据起始行：" + preview.DataStartRow);
            sb.AppendLine();
            sb.AppendLine("原始预览文本：");
            AppendRawSheetPreview(sb, preview.RawSheet);
            sb.AppendLine();
            sb.AppendLine("物料初步识别：");

            if (preview.Items != null)
            {
                for (var i = 0; i < preview.Items.Count; i++)
                {
                    var item = preview.Items[i];
                    sb.AppendLine("- index：" + (i + 1));
                    sb.AppendLine("  原始名称：" + Safe(item.RawName));
                    sb.AppendLine("  物料编码：" + Safe(item.MaterialCode));
                    sb.AppendLine("  物料名称：" + Safe(item.MaterialName));
                    sb.AppendLine("  成品尺寸：" + Safe(item.FinishedSize));
                    sb.AppendLine("  材质/工艺：" + Safe(item.MaterialProcess));
                    sb.AppendLine("  材料名称：" + Safe(item.MaterialNameExtracted));
                    sb.AppendLine("  克重：" + Safe(item.GramWeight));
                    sb.AppendLine("  阶梯价格：" + (settings.AllowPrice ? FormatTiers(item) : "[已按设置隐藏]"));
                }
            }

            return sb.ToString();
        }

        private static void AppendRawSheetPreview(StringBuilder sb, QuoteRawSheetPreview rawSheet)
        {
            if (rawSheet == null || rawSheet.Cells == null)
            {
                sb.AppendLine("[无原始预览]");
                return;
            }

            var maxRows = Math.Min(rawSheet.Rows, 40);
            var maxColumns = Math.Min(rawSheet.Columns, 12);
            for (var row = 1; row <= maxRows; row++)
            {
                var line = new StringBuilder();
                for (var column = 1; column <= maxColumns; column++)
                {
                    var value = rawSheet.Cells[row, column];
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (line.Length > 0)
                    {
                        line.Append(" | ");
                    }

                    line.Append("C");
                    line.Append(column);
                    line.Append("=");
                    line.Append(value);
                }

                if (line.Length > 0)
                {
                    sb.AppendLine("R" + row + " " + line);
                }
            }
        }

        private static string BuildCostCompletionUserPrompt(AiSettings settings, List<AiCostCompletionInput> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("请根据以下成本分析明细，给出谨慎的成本金额补全建议。");
            sb.AppendLine("只输出 JSON。不要解释，不要输出 Markdown。");
            sb.AppendLine(AiPromptTemplates.CostCompletionSchemaInstruction);
            sb.AppendLine();
            sb.AppendLine("明细：");
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                sb.AppendLine("- index：" + row.Index);
                sb.AppendLine("  物料编码：" + Safe(row.MaterialCode));
                sb.AppendLine("  物料名称：" + Safe(row.MaterialName));
                sb.AppendLine("  物料描述：" + Safe(row.MaterialDescription));
                sb.AppendLine("  供应商：" + (settings.AllowSupplierName ? Safe(row.Supplier) : "[已按设置隐藏]"));
                sb.AppendLine("  材料名称：" + Safe(row.BaseMaterialName));
                sb.AppendLine("  材料厂家：" + Safe(row.MaterialVendor));
                sb.AppendLine("  材料单价：" + Safe(row.MaterialUnitPrice));
                sb.AppendLine("  原材料克重：" + Safe(row.GramWeight));
                sb.AppendLine("  展开尺寸：" + Safe(row.ExpandedSize));
                sb.AppendLine("  当前材料费：" + Safe(row.MaterialCost));
                sb.AppendLine("  当前印刷费：" + Safe(row.PrintingCost));
                sb.AppendLine("  当前后工序费：" + Safe(row.PostProcessCost));
                sb.AppendLine("  当前其他：" + Safe(row.OtherCost));
                sb.AppendLine("  当前采购单价：" + (settings.AllowPrice ? Safe(row.PurchaseUnitPrice) : "[已按设置隐藏]"));
                sb.AppendLine("  总用量：" + Safe(row.TotalQuantity));
                sb.AppendLine("  本地历史参考：" + Safe(row.HistorySummary));
                sb.AppendLine("  本地工艺规则匹配：" + Safe(row.ProcessRuleSummary));
            }

            return sb.ToString();
        }

        private static ArrayList GetArray(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null)
            {
                return new ArrayList();
            }

            var arrayList = data[key] as ArrayList;
            if (arrayList != null)
            {
                return arrayList;
            }

            var objectArray = data[key] as object[];
            if (objectArray != null)
            {
                return new ArrayList(objectArray);
            }

            return new ArrayList();
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key))
            {
                return new Dictionary<string, object>();
            }

            return data[key] as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null)
            {
                return string.Empty;
            }

            return Convert.ToString(data[key]);
        }

        private static string GetStringAny(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = GetString(data, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static double GetDouble(Dictionary<string, object> data, string key)
        {
            var value = GetString(data, key);
            double number;
            return double.TryParse(value, out number) ? number : 0;
        }

        private static decimal? GetNullableDecimal(Dictionary<string, object> data, string key)
        {
            var value = GetString(data, key);
            decimal number;
            return decimal.TryParse(value, out number) ? number : (decimal?)null;
        }

        private static int? GetNullableInt(Dictionary<string, object> data, string key)
        {
            var value = GetString(data, key);
            int number;
            return int.TryParse(value, out number) ? number : (int?)null;
        }

        private static int? GetNullableIntAny(Dictionary<string, object> data, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = GetNullableInt(data, key);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private static bool GetBool(Dictionary<string, object> data, string key)
        {
            var value = GetString(data, key);
            bool boolean;
            if (bool.TryParse(value, out boolean))
            {
                return boolean;
            }

            return value == "1" || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> GetStringList(Dictionary<string, object> data, string key)
        {
            var result = new List<string>();
            foreach (var item in GetArray(data, key))
            {
                if (item != null && !string.IsNullOrWhiteSpace(Convert.ToString(item)))
                {
                    result.Add(Convert.ToString(item));
                }
            }

            return result;
        }

        private static string FormatTiers(QuoteImportItem item)
        {
            if (item.PriceTiers == null || item.PriceTiers.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            foreach (var tier in item.PriceTiers)
            {
                if (sb.Length > 0)
                {
                    sb.Append("；");
                }

                sb.Append(Safe(tier.Label));
                sb.Append("=");
                sb.Append(tier.UnitPrice.HasValue ? tier.UnitPrice.Value.ToString("0.####") : "");
            }

            return sb.ToString();
        }

        private static void WriteJsonProperty(StringBuilder sb, string name, string value, int indent, bool comma)
        {
            sb.Append(new string(' ', indent * 2));
            sb.Append("\"");
            sb.Append(EscapeJson(name));
            sb.Append("\": \"");
            sb.Append(EscapeJson(value));
            sb.Append("\"");
            if (comma)
            {
                sb.Append(",");
            }
            sb.AppendLine();
        }

        private static string Safe(string value)
        {
            return value ?? string.Empty;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }

    internal sealed class AiQuoteRecognitionResult
    {
        public string RawContent { get; set; }
        public string TemplateType { get; set; }
        public string Supplier { get; set; }
        public string QuoteDate { get; set; }
        public string QuoteNo { get; set; }
        public double Confidence { get; set; }
        public int? HeaderRow { get; set; }
        public int? QuantityRow { get; set; }
        public int? DataStartRow { get; set; }
        public List<AiQuoteRecognitionItem> Items { get; set; }
        public List<string> Warnings { get; set; }
    }

    internal sealed class AiQuoteRecognitionItem
    {
        public int? Index { get; set; }
        public string RawName { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string FinishedSize { get; set; }
        public string MaterialProcess { get; set; }
        public string MaterialNameExtracted { get; set; }
        public string GramWeight { get; set; }
        public bool RequiresReview { get; set; }
        public List<string> Warnings { get; set; }
    }

    internal sealed class AiCostCompletionInput
    {
        public int Index { get; set; }
        public string MaterialCode { get; set; }
        public string MaterialName { get; set; }
        public string MaterialDescription { get; set; }
        public string Supplier { get; set; }
        public string BaseMaterialName { get; set; }
        public string MaterialVendor { get; set; }
        public string MaterialUnitPrice { get; set; }
        public string GramWeight { get; set; }
        public string ExpandedSize { get; set; }
        public string MaterialCost { get; set; }
        public string PrintingCost { get; set; }
        public string PostProcessCost { get; set; }
        public string OtherCost { get; set; }
        public string PurchaseUnitPrice { get; set; }
        public string TotalQuantity { get; set; }
        public string HistorySummary { get; set; }
        public string ProcessRuleSummary { get; set; }
    }

    internal sealed class AiCostCompletionResult
    {
        public string RawContent { get; set; }
        public List<AiCostCompletionSuggestion> Items { get; set; }
        public List<string> Warnings { get; set; }
    }

    internal sealed class AiCostCompletionSuggestion
    {
        public int? Index { get; set; }
        public decimal? MaterialCost { get; set; }
        public decimal? PrintingCost { get; set; }
        public decimal? PostProcessCost { get; set; }
        public decimal? OtherCost { get; set; }
        public decimal? PurchaseUnitPrice { get; set; }
        public double Confidence { get; set; }
        public bool RequiresReview { get; set; }
        public string Reason { get; set; }
        public List<string> Warnings { get; set; }
    }
}
