
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
