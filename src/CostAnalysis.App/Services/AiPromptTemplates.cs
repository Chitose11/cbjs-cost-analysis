namespace CostAnalysis.App.Services
{
    internal static class AiPromptTemplates
    {
        public static string QuoteRecognitionSystemPrompt
        {
            get
            {
                return "你是报价单结构化识别助手。只输出严格 JSON，不输出 Markdown 或自然语言解释。";
            }
        }

        public static string QuoteRecognitionTaskInstruction
        {
            get
            {
                return @"任务：
1. 识别报价单类型。
2. 判断表头行、数量阶梯行、数据起始行。
3. 将报价单字段映射到标准成本分析字段。
4. 提取物料编码、物料名称、规格描述、尺寸、材质、工艺、数量阶梯、单价等信息。
5. 从混合文本中拆分物料编码和物料名称。
6. 从“材质/工艺/规格描述”中提取材料名称、克重、尺寸、工艺关键词。
7. 对不确定字段标记 requires_review，不要猜测成确定结果。

限制：
1. 不要替用户最终决定采购单价、总用量、总价。
2. 不要把推测值当作确定值。
3. 不要编造报价单中不存在的数据。
4. 不要生成最终客户成本分析表，只生成结构化识别建议。";
            }
        }

        public static string QuoteRecognitionSchemaInstruction
        {
            get
            {
                return @"请按以下 JSON 结构返回：
{
  ""template_type"": """",
  ""confidence"": 0,
  ""supplier"": """",
  ""quote_date"": """",
  ""quote_no"": """",
  ""header_row"": null,
  ""quantity_row"": null,
  ""data_start_row"": null,
  ""field_map"": {
    ""No"": """",
    ""物料编码"": """",
    ""物料名称"": """",
    ""物料描述"": """",
    ""供应商"": """",
    ""材料名称"": """",
    ""材料厂家"": """",
    ""材料单价"": """",
    ""原材料克重"": """",
    ""展开尺寸"": """",
    ""材料费"": """",
    ""印刷费"": """",
    ""后工序费"": """",
    ""其他"": """",
    ""采购单价"": """",
    ""总用量"": """",
    ""总价"": """"
  },
  ""items"": [
    {
      ""index"": 1,
      ""raw_name"": """",
      ""material_code"": """",
      ""material_name"": """",
      ""finished_size"": """",
      ""material_process"": """",
      ""material_name_extracted"": """",
      ""gram_weight"": """",
      ""requires_review"": false,
      ""warnings"": []
    }
  ],
  ""warnings"": []
}";
            }
        }

    }
}
