# 成本分析软件 MVP

这是一个面向包装/印刷报价场景的本地成本分析工具。软件用于把供应商报价单中的物料、规格、用量和阶梯价格整理成客户成本分析表，并支持材料库、成本校验、Excel 导出和可选的 DeepSeek AI 辅助识别。

项目当前优先目标是：

- 可在 Windows 7 环境使用。
- 尽量轻量，不使用 Electron/WebView2。
- 数据本地保存，AI 仅作为可选辅助。
- 导出给客户的 Excel 不包含内部追溯字段。

## 当前功能

- 成本分析表编辑
  - 分析单号、客户名称、项目名称、日期、税费说明、运费说明
  - 物料编码、物料名称、物料描述、供应商、材料、成本、采购单价、总用量、总价
  - 勾选行选择，选中行整行高亮
  - 新增明细、删除明细、保存、打开历史单据

- 报价单导入
  - 支持 `.xls` / `.xlsx`
  - 识别常见翔发报价单模板
  - 识别表头行、数量阶梯、物料编码、物料名称、规格描述、尺寸、用量、阶梯价格
  - 导入前进入确认页，用户确认后才写入成本分析表

- 批量预扫描
  - 选择报价单文件夹
  - 扫描 `.xls` / `.xlsx`
  - 显示可导入/未识别、模板、供应商、Sheet、物料数和错误信息
  - 选中可导入报价单后进入导入确认页

- 材料库
  - 维护材料名称、别名、类别、材料厂家、克重规格、计价单位、含税单价、是否含运、备注
  - 导入报价单后按材料名称/别名自动匹配材料信息

- 校验与计算
  - 根据总用量匹配阶梯采购单价
  - 自动计算总价
  - 标记缺物料编码、缺物料名称、缺采购单价、缺总用量
  - 标记成本合计与采购单价不一致、总价计算不一致

- Excel 导出
  - 导出客户成本分析表 `.xlsx`
  - 导出不包含 `选择`、`状态`、来源文件、来源 Sheet、AI 标记等内部字段
  - 使用原生 `.xlsx` ZIP/XML 写入，不依赖 Excel 进程导出

- DeepSeek AI 辅助识别
  - 可选启用
  - 支持 API Key 本地加密保存
  - 默认模型：`deepseek-v4-flash`
  - 可选模型：`deepseek-v4-pro`
  - 支持 JSON Mode
  - AI 返回后先回填导入确认页，不直接导出
  - AI 改动字段会高亮并显示原值到新值
  - 可查看 AI 原始 JSON 返回
  - 可撤销上一次 AI 改动

## 技术栈

- C# WinForms
- .NET Framework 4.8
- SQLite
- Excel COM 读取报价单
- 原生 `.xlsx` 导出
- DeepSeek Chat Completions API，可选

## 目录结构

```text
.
├── docs/
│   └── plans/
│       └── 2026-05-15-cost-analysis-mvp.md
├── src/
│   └── CostAnalysis.App/
│       ├── Data/
│       ├── Domain/
│       ├── Services/
│       ├── UI/
│       ├── Program.cs
│       └── CostAnalysis.App.csproj
├── Apple网页设计特点.md
├── .gitignore
└── README.md
```

## 运行环境

推荐环境：

- Windows 7 或更高版本
- .NET Framework 4.8 Runtime
- Microsoft Excel

说明：

- 当前报价单读取依赖 Excel COM，所以导入 `.xls` / `.xlsx` 时本机需要安装 Excel。
- Excel 导出不依赖 Excel，本程序会直接生成 `.xlsx` 文件。
- AI 功能需要联网和 DeepSeek API Key；不启用 AI 时，软件仍可本地导入、编辑、保存和导出。

## 构建方式

在项目根目录执行：

```powershell
dotnet build .\src\CostAnalysis.App\CostAnalysis.App.csproj -c Release
```

构建产物：

```text
src\CostAnalysis.App\bin\Release\net48\CostAnalysis.App.exe
```

启动：

```powershell
.\src\CostAnalysis.App\bin\Release\net48\CostAnalysis.App.exe
```

## 使用流程

### 1. 导入单个报价单

1. 点击 `导入报价单`
2. 选择 `.xls` 或 `.xlsx` 报价单
3. 在导入确认页检查识别结果
4. 可选：点击 `AI辅助识别`
5. 勾选要加入的物料
6. 点击 `确认加入`

### 2. 批量预扫描报价单

1. 点击 `批量预扫描`
2. 选择报价单文件夹
3. 点击 `开始扫描`
4. 选择状态为 `可导入` 的报价单
5. 点击 `导入选中`
6. 进入导入确认页继续确认

### 3. 编辑成本分析

1. 检查物料编码、物料名称、物料描述、供应商、材料名称、原材料克重、展开尺寸
2. 补充材料费、印刷费、后工序费、其他费用
3. 检查采购单价、总用量和总价
4. 如需删除明细，勾选行后点击 `删除明细`
5. 点击 `保存`

### 4. 导出客户 Excel

1. 点击 `导出 Excel`
2. 选择保存路径
3. 程序生成客户成本分析表

## DeepSeek AI 配置

在软件左侧点击 `系统设置`：

- 勾选 `启用 AI 辅助`
- API 地址默认：

```text
https://api.deepseek.com
```

- 填写 DeepSeek API Key
- 模型建议：

```text
deepseek-v4-flash
```

AI 隐私选项：

- 允许发送客户名称：默认关闭
- 允许发送供应商名称：默认开启
- 允许发送价格：默认开启
- 每次调用前确认：默认开启

API Key 会使用 Windows DPAPI 按当前用户加密保存到本地 SQLite 数据库中，不会写入代码仓库。

## 本地数据

程序运行后会在可执行文件目录下创建本地数据目录和 SQLite 数据库。

数据库文件、构建产物和临时文件已被 `.gitignore` 排除：

```text
bin/
obj/
*.db
*.db-shm
*.db-wal
tmp/
```

## 当前限制

- 报价单读取当前依赖 Excel COM。
- 复杂纸箱核价公式还没有完整支持。
- PDF 自动解析和图片 OCR 暂未进入 MVP。
- AI 只做辅助识别建议，不替用户最终确认成本。
- WinForms UI 已做轻量化优化，但仍是第一版 MVP，后续可继续按 Ant Design Table 的交互体验优化。

## 后续计划

- 增加纯文件读取模式，减少对 Office 的依赖。
- 完善更多报价单模板识别。
- 增强纸箱、贴纸、板贴类报价单支持。
- 增加工艺库和历史成本参考。
- 优化表格交互和快捷操作。
- 增加更完整的导入日志和识别报告。

## 开发说明

核心代码位置：

- 主窗体：`src/CostAnalysis.App/UI/MainForm.cs`
- 报价单导入：`src/CostAnalysis.App/Services/ExcelQuoteImportService.cs`
- 导入确认页：`src/CostAnalysis.App/UI/QuoteImportPreviewForm.cs`
- 批量预扫描：`src/CostAnalysis.App/UI/BatchQuoteScanForm.cs`
- Excel 导出：`src/CostAnalysis.App/Services/ExcelExportService.cs`
- AI 调用：`src/CostAnalysis.App/Services/DeepSeekClient.cs`
- AI 设置：`src/CostAnalysis.App/Data/AiSettingsRepository.cs`
- 材料库：`src/CostAnalysis.App/Data/MaterialRepository.cs`

详细 MVP 计划见：

```text
docs/plans/2026-05-15-cost-analysis-mvp.md
```
