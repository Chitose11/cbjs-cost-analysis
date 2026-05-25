using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class BatchQuoteScanForm : Form
    {
        private readonly TextBox _folderTextBox;
        private readonly Button _browseButton;
        private readonly Button _scanButton;
        private readonly Button _cancelScanButton;
        private readonly Button _aiCleanButton;
        private readonly Button _importButton;
        private readonly Button _selectReviewableButton;
        private readonly Button _invertSelectionButton;
        private readonly Button _clearSelectionButton;
        private readonly Button _saveMaterialsButton;
        private readonly Button _saveRulesButton;
        private readonly DataGridView _grid;
        private readonly DataGridView _materialsGrid;
        private readonly DataGridView _rulesGrid;
        private readonly Label _statusLabel;
        private readonly Label _learningSummaryLabel;
        private readonly ProgressBar _progressBar;

        private volatile bool _cancelRequested;
        private Thread _scanThread;
        private int _successCount;
        private int _failureCount;

        public BatchQuoteScanForm(string initialFolder)
        {
            Text = "AI学习识别";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1200, 720);
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(920, 560);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            var folderPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            root.Controls.Add(folderPanel, 0, 0);

            _folderTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = initialFolder ?? string.Empty,
                Margin = new Padding(0, 6, 8, 6)
            };
            folderPanel.Controls.Add(_folderTextBox, 0, 0);

            _browseButton = new Button { Text = "选择文件夹", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 8, 4) };
            _browseButton.Click += OnBrowseFolder;
            folderPanel.Controls.Add(_browseButton, 1, 0);

            _scanButton = new Button { Text = "开始学习", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 8, 4) };
            _scanButton.Click += OnStartScan;
            folderPanel.Controls.Add(_scanButton, 2, 0);

            _cancelScanButton = new Button { Text = "停止", Dock = DockStyle.Fill, Enabled = false, Margin = new Padding(0, 4, 0, 4) };
            _cancelScanButton.Click += (_, __) => _cancelRequested = true;
            folderPanel.Controls.Add(_cancelScanButton, 3, 0);

            _grid = BuildGrid();
            _materialsGrid = BuildMaterialsGrid();
            _rulesGrid = BuildRulesGrid();
            root.Controls.Add(BuildTabs(), 0, 1);

            var statusPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            root.Controls.Add(statusPanel, 0, 2);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "请选择报价单文件夹后开始学习识别。",
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusPanel.Controls.Add(_statusLabel, 0, 0);

            _learningSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "学习质量：待扫描",
                ForeColor = Color.FromArgb(70, 95, 130),
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusPanel.Controls.Add(_learningSummaryLabel, 1, 0);

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Margin = new Padding(8, 7, 0, 7)
            };
            statusPanel.Controls.Add(_progressBar, 2, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(buttons, 0, 3);

            _importButton = new Button { Text = "识别校对", Width = 96, Height = 32 };
            _importButton.Click += OnImportSelected;
            buttons.Controls.Add(_importButton);

            var closeButton = new Button { Text = "关闭", Width = 82, Height = 32 };
            closeButton.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(closeButton);

            _aiCleanButton = new Button { Text = "AI清洗未知", Width = 108, Height = 32 };
            _aiCleanButton.Click += OnAiCleanUnknown;
            buttons.Controls.Add(_aiCleanButton);

            _clearSelectionButton = new Button { Text = "取消选择", Width = 92, Height = 32 };
            _clearSelectionButton.Click += (_, __) => SetReviewSelection(false, false);
            buttons.Controls.Add(_clearSelectionButton);

            _invertSelectionButton = new Button { Text = "反选", Width = 72, Height = 32 };
            _invertSelectionButton.Click += (_, __) => InvertReviewSelection();
            buttons.Controls.Add(_invertSelectionButton);

            _selectReviewableButton = new Button { Text = "全选可复核", Width = 104, Height = 32 };
            _selectReviewableButton.Click += (_, __) => SetReviewSelection(true, true);
            buttons.Controls.Add(_selectReviewableButton);

            _saveRulesButton = new Button { Text = "保存工艺候选", Width = 116, Height = 32 };
            _saveRulesButton.Click += OnSaveRuleCandidates;
            buttons.Controls.Add(_saveRulesButton);

            _saveMaterialsButton = new Button { Text = "保存材料候选", Width = 116, Height = 32 };
            _saveMaterialsButton.Click += OnSaveMaterialCandidates;
            buttons.Controls.Add(_saveMaterialsButton);

            FormClosing += (_, __) => _cancelRequested = true;
        }

        private Control BuildTabs()
        {
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            tabs.TabPages.Add(BuildTabPage("报价单模板", _grid));
            tabs.TabPages.Add(BuildTabPage("材料候选", _materialsGrid));
            tabs.TabPages.Add(BuildTabPage("工艺候选", _rulesGrid));
            return tabs;
        }

        private static TabPage BuildTabPage(string title, Control content)
        {
            var page = new TabPage(title) { Padding = new Padding(0) };
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            return page;
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 252) },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(230, 244, 255),
                    SelectionForeColor = Color.FromArgb(29, 29, 31)
                },
                RowTemplate = { Height = 30 }
            };
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(29, 29, 31);
            grid.CurrentCellDirtyStateChanged += OnGridCurrentCellDirtyStateChanged;
            grid.CellValueChanged += OnGridCellValueChanged;
            grid.CellDoubleClick += OnGridCellDoubleClick;

            AddCheckColumn(grid, "ReviewSelected", "选择");
            AddColumn(grid, "Status", "状态");
            AddColumn(grid, "FileName", "文件名");
            AddColumn(grid, "TemplateType", "模板");
            AddColumn(grid, "Supplier", "供应商");
            AddColumn(grid, "SheetName", "Sheet");
            AddColumn(grid, "ItemCount", "物料数");
            AddColumn(grid, "LearningStatus", "学习状态");
            AddColumn(grid, "LocalRuleStatus", "本地验证");
            AddColumn(grid, "QualityScore", "质量");
            AddColumn(grid, "ReuseRate", "复用率");
            AddColumn(grid, "MaterialCandidates", "材料候选");
            AddColumn(grid, "RuleCandidates", "工艺候选");
            AddColumn(grid, "FilePath", "完整路径");
            AddColumn(grid, "Error", "错误信息");
            grid.Columns["FilePath"].Visible = false;
            ConfigureScanGridColumns(grid);
            return grid;
        }

        private DataGridView BuildMaterialsGrid()
        {
            var grid = BuildCandidateGrid();
            AddCheckColumn(grid, "Selected", "加入");
            AddColumn(grid, "Name", "材料名称");
            AddColumn(grid, "Category", "类别");
            AddColumn(grid, "Vendor", "材料厂家");
            AddColumn(grid, "Spec", "克重规格");
            AddColumn(grid, "Unit", "计价单位");
            AddColumn(grid, "IncludesFreight", "含运");
            AddColumn(grid, "Source", "来源");
            AddColumn(grid, "Existing", "状态");
            SetColumn(grid, "Selected", 54, false);
            SetColumn(grid, "Name", 180, true);
            SetColumn(grid, "Category", 80, false);
            SetColumn(grid, "Vendor", 180, true);
            SetColumn(grid, "Spec", 110, true);
            SetColumn(grid, "Unit", 78, false);
            SetColumn(grid, "IncludesFreight", 64, false);
            SetColumn(grid, "Source", 220, true);
            SetColumn(grid, "Existing", 100, false);
            return grid;
        }

        private DataGridView BuildRulesGrid()
        {
            var grid = BuildCandidateGrid();
            AddCheckColumn(grid, "Selected", "加入");
            AddColumn(grid, "Keyword", "关键词");
            AddColumn(grid, "CostType", "费用类型");
            AddColumn(grid, "Amount", "金额");
            AddColumn(grid, "IsEnabled", "启用");
            AddColumn(grid, "Remark", "备注");
            AddColumn(grid, "Source", "来源");
            AddColumn(grid, "Existing", "状态");
            SetColumn(grid, "Selected", 54, false);
            SetColumn(grid, "Keyword", 120, true);
            SetColumn(grid, "CostType", 90, false);
            SetColumn(grid, "Amount", 80, false);
            SetColumn(grid, "IsEnabled", 64, false);
            SetColumn(grid, "Remark", 260, true);
            SetColumn(grid, "Source", 220, true);
            SetColumn(grid, "Existing", 100, false);
            return grid;
        }

        private static DataGridView BuildCandidateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 252) },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(230, 244, 255),
                    SelectionForeColor = Color.FromArgb(29, 29, 31)
                },
                RowTemplate = { Height = 30 }
            };
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(29, 29, 31);
            return grid;
        }

        private void OnBrowseFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择报价单文件夹";
                if (Directory.Exists(_folderTextBox.Text))
                {
                    dialog.SelectedPath = _folderTextBox.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _folderTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void OnStartScan(object sender, EventArgs e)
        {
            var folder = _folderTextBox.Text.Trim();
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "请选择有效的文件夹。", "AI学习识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var files = FindExcelFiles(folder);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "该文件夹下没有找到 .xls 或 .xlsx 报价单。", "AI学习识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _grid.Rows.Clear();
            _materialsGrid.Rows.Clear();
            _rulesGrid.Rows.Clear();
            _successCount = 0;
            _failureCount = 0;
            _cancelRequested = false;
            SetProgress(0, files.Count);
            UpdateLearningSummary();

            for (var i = 0; i < files.Count; i++)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["ReviewSelected"].Value = false;
                row.Cells["Status"].Value = "等待";
                row.Cells["LearningStatus"].Value = "待学习";
                row.Cells["LocalRuleStatus"].Value = "-";
                row.Cells["QualityScore"].Value = "-";
                row.Cells["ReuseRate"].Value = "-";
                row.Cells["FileName"].Value = Path.GetFileName(files[i]);
                row.Cells["FilePath"].Value = files[i];
            }

            SetScanningState(true);
            _statusLabel.Text = "开始学习识别，共 " + files.Count + " 个文件。";

            _scanThread = new Thread(() => ScanFiles(files));
            _scanThread.IsBackground = true;
            _scanThread.SetApartmentState(ApartmentState.STA);
            _scanThread.Start();
        }

        private static List<string> FindExcelFiles(string folder)
        {
            var result = new List<string>();
            foreach (var file in Directory.GetFiles(folder))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var ext = Path.GetExtension(file);
                if (string.Equals(ext, ".xls", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(file);
                }
            }

            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        private void ScanFiles(List<string> files)
        {
            for (var i = 0; i < files.Count; i++)
            {
                if (_cancelRequested)
                {
                    break;
                }

                var rowIndex = i;
                var file = files[i];
                RunOnUi(() =>
                {
                    _grid.Rows[rowIndex].Cells["Status"].Value = "扫描中";
                    _statusLabel.Text = string.Format("正在扫描 {0}/{1}：{2}", rowIndex + 1, files.Count, Path.GetFileName(file));
                });

                try
                {
                    var preview = new ExcelQuoteImportService().Import(file);
                    if (preview.Items == null || preview.Items.Count == 0)
                    {
                        throw new InvalidOperationException("未识别到可校对物料。");
                    }

                    var result = new BatchQuoteScanResult { Preview = preview };
                    _successCount++;
                    RunOnUi(() => ApplyScanSuccess(rowIndex, result));
                }
                catch (Exception ex)
                {
                    _failureCount++;
                    var message = ex.Message;
                    RunOnUi(() => ApplyScanFailure(rowIndex, message));
                }
            }

            RunOnUi(() =>
            {
                SetScanningState(false);
                SetProgress(_successCount + _failureCount, _grid.Rows.Count);
                _statusLabel.Text = _cancelRequested
                    ? string.Format("已停止。成功 {0} 个，失败 {1} 个。", _successCount, _failureCount)
                    : string.Format("扫描完成。成功 {0} 个，失败 {1} 个。", _successCount, _failureCount);
            });
        }

        private void ApplyScanSuccess(int rowIndex, BatchQuoteScanResult result)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var preview = result.Preview;
            var row = _grid.Rows[rowIndex];
            row.Tag = result;
            row.Cells["Status"].Value = "可校对";
            row.Cells["TemplateType"].Value = preview.TemplateType;
            row.Cells["Supplier"].Value = preview.Supplier;
            row.Cells["SheetName"].Value = preview.SheetName;
            row.Cells["ItemCount"].Value = preview.Items == null ? 0 : preview.Items.Count;
            row.Cells["Error"].Value = string.Empty;
            SetProgress(_successCount + _failureCount, _grid.Rows.Count);
            new QuoteTemplateRepository().SaveLearnedTemplate(
                result.Preview,
                Convert.ToString(row.Cells["FileName"].Value),
                result.Preview.Items == null ? 0 : result.Preview.Items.Count);
            var validation = ValidateLearnedLocalRule(preview);
            row.Cells["LearningStatus"].Value = BuildLearningStatus(preview);
            row.Cells["LocalRuleStatus"].Value = validation.StatusText;
            row.Cells["QualityScore"].Value = BuildQualityScore(preview, validation);
            row.Cells["ReuseRate"].Value = validation.SourceCount <= 0 ? "-" : validation.AppliedCount + "/" + validation.SourceCount;
            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["Error"].Value)))
            {
                row.Cells["Error"].Value = "已学习为本地规则";
            }

            var counts = AddCandidatesFromPreview(preview, Convert.ToString(row.Cells["FileName"].Value));
            row.Cells["MaterialCandidates"].Value = counts.MaterialCount;
            row.Cells["RuleCandidates"].Value = counts.RuleCount;
            ApplyScanRowVisualState(row);
            UpdateLearningSummary();
        }

        private void ApplyScanFailure(int rowIndex, string message)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var row = _grid.Rows[rowIndex];
            row.Cells["Status"].Value = "未识别";
            row.Cells["LearningStatus"].Value = "未学习";
            row.Cells["LocalRuleStatus"].Value = "-";
            row.Cells["QualityScore"].Value = "-";
            row.Cells["ReuseRate"].Value = "-";
            row.Cells["Error"].Value = message;
            row.Cells["Error"].ToolTipText = message;
            SetProgress(_successCount + _failureCount, _grid.Rows.Count);
            ApplyScanRowVisualState(row);
            UpdateLearningSummary();
        }

        private void OnAiCleanUnknown(object sender, EventArgs e)
        {
            var rows = GetAiCleanTargetRows();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "没有可 AI 清洗的未知单据。请先进行 AI 学习扫描，或选择未识别的行。", "AI清洗未知", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var settings = new AiSettingsRepository().Get();
            if (!settings.IsEnabled || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                MessageBox.Show(this, "AI 功能尚未启用或未配置 DeepSeek API Key。请先到系统设置中启用 AI。", "AI清洗未知", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this, "将对 " + rows.Count + " 个未知单据调用 DeepSeek 批量清洗，结果仍需进入识别校对窗口确认。继续吗？", "AI清洗未知", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            _cancelRequested = false;
            SetScanningState(true);
            var targets = BuildAiCleanTargets(rows);
            SetProgress(0, targets.Count);
            _scanThread = new Thread(() => AiCleanRows(targets, settings));
            _scanThread.IsBackground = true;
            _scanThread.SetApartmentState(ApartmentState.STA);
            _scanThread.Start();
        }

        private List<AiCleanTarget> BuildAiCleanTargets(List<int> rowIndexes)
        {
            var targets = new List<AiCleanTarget>();
            foreach (var rowIndex in rowIndexes)
            {
                if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                {
                    continue;
                }

                var row = _grid.Rows[rowIndex];
                var result = row.Tag as BatchQuoteScanResult;
                targets.Add(new AiCleanTarget
                {
                    RowIndex = rowIndex,
                    FilePath = Convert.ToString(row.Cells["FilePath"].Value),
                    FileName = Convert.ToString(row.Cells["FileName"].Value),
                    Result = result
                });
            }

            return targets;
        }

        private List<int> GetAiCleanTargetRows()
        {
            var result = new List<int>();
            if (_grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in _grid.SelectedRows)
                {
                    if (!row.IsNewRow && IsUnknownRow(row))
                    {
                        result.Add(row.Index);
                    }
                }
            }

            if (result.Count > 0)
            {
                result.Sort();
                return result;
            }

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow && IsUnknownRow(row))
                {
                    result.Add(row.Index);
                }
            }

            return result;
        }

        private static bool IsUnknownRow(DataGridViewRow row)
        {
            var itemCountText = Convert.ToString(row.Cells["ItemCount"].Value);
            int itemCount;
            if (int.TryParse(itemCountText, out itemCount) && itemCount > 0)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["FilePath"].Value));
        }

        private void AiCleanRows(List<AiCleanTarget> targets, AiSettings settings)
        {
            var client = new DeepSeekClient();
            var success = 0;
            var failure = 0;
            for (var i = 0; i < targets.Count; i++)
            {
                if (_cancelRequested)
                {
                    break;
                }

                var target = targets[i];
                var rowIndex = target.RowIndex;
                var scanResult = target.Result;
                RunOnUi(() =>
                {
                    if (rowIndex >= 0 && rowIndex < _grid.Rows.Count)
                    {
                        var row = _grid.Rows[rowIndex];
                        row.Cells["Status"].Value = "AI清洗中";
                        _statusLabel.Text = "AI 清洗 " + (i + 1) + "/" + targets.Count + "：" + target.FileName;
                    }
                });

                try
                {
                    var preview = scanResult == null ? null : scanResult.Preview;
                    if (preview == null)
                    {
                        preview = new ExcelQuoteImportService().Import(target.FilePath);
                        scanResult = new BatchQuoteScanResult { Preview = preview };
                    }

                    var aiResult = client.RecognizeQuote(settings, preview);
                    var added = ApplyAiResultToPreview(preview, aiResult);
                    if (added <= 0)
                    {
                        throw new InvalidOperationException("AI 未返回可校对物料。");
                    }

                    success++;
                    RunOnUi(() => ApplyAiCleanSuccess(rowIndex, scanResult, aiResult));
                }
                catch (Exception ex)
                {
                    failure++;
                    var message = ex.Message;
                    RunOnUi(() => ApplyAiCleanFailure(rowIndex, message));
                }
            }

            RunOnUi(() =>
            {
                SetScanningState(false);
                SetProgress(success + failure, targets.Count);
                _statusLabel.Text = _cancelRequested
                    ? "AI 清洗已停止。成功 " + success + " 个，失败 " + failure + " 个。"
                    : "AI 清洗完成。成功 " + success + " 个，失败 " + failure + " 个。";
            });
        }

        private void ApplyAiCleanSuccess(int rowIndex, BatchQuoteScanResult result, AiQuoteRecognitionResult aiResult)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var row = _grid.Rows[rowIndex];
            row.Tag = result;
            row.Cells["Status"].Value = "AI已识别";
            row.Cells["TemplateType"].Value = result.Preview.TemplateType;
            row.Cells["Supplier"].Value = result.Preview.Supplier;
            row.Cells["SheetName"].Value = result.Preview.SheetName;
            row.Cells["ItemCount"].Value = result.Preview.Items == null ? 0 : result.Preview.Items.Count;
            row.Cells["Error"].Value = aiResult == null || aiResult.Warnings == null ? string.Empty : string.Join("；", aiResult.Warnings.ToArray());
            new QuoteTemplateRepository().SaveLearnedTemplate(
                result.Preview,
                Convert.ToString(row.Cells["FileName"].Value),
                result.Preview.Items == null ? 0 : result.Preview.Items.Count);
            var validation = ValidateLearnedLocalRule(result.Preview);
            row.Cells["LearningStatus"].Value = BuildLearningStatus(result.Preview);
            row.Cells["LocalRuleStatus"].Value = validation.StatusText;
            row.Cells["QualityScore"].Value = BuildQualityScore(result.Preview, validation);
            row.Cells["ReuseRate"].Value = validation.SourceCount <= 0 ? "-" : validation.AppliedCount + "/" + validation.SourceCount;
            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["Error"].Value)))
            {
                row.Cells["Error"].Value = "AI清洗并学习为本地规则";
            }

            var counts = AddCandidatesFromPreview(result.Preview, Convert.ToString(row.Cells["FileName"].Value));
            row.Cells["MaterialCandidates"].Value = counts.MaterialCount;
            row.Cells["RuleCandidates"].Value = counts.RuleCount;
            ApplyScanRowVisualState(row);
            UpdateLearningSummary();
        }

        private void ApplyAiCleanFailure(int rowIndex, string message)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var row = _grid.Rows[rowIndex];
            row.Cells["Status"].Value = "AI失败";
            row.Cells["LearningStatus"].Value = "未学习";
            row.Cells["LocalRuleStatus"].Value = "-";
            row.Cells["QualityScore"].Value = "-";
            row.Cells["ReuseRate"].Value = "-";
            row.Cells["Error"].Value = message;
            ApplyScanRowVisualState(row);
            UpdateLearningSummary();
        }

        private static int ApplyAiResultToPreview(QuoteImportPreview preview, AiQuoteRecognitionResult result)
        {
            if (preview == null || result == null || result.Items == null)
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(result.TemplateType)) preview.TemplateType = result.TemplateType;
            if (!string.IsNullOrWhiteSpace(result.Supplier)) preview.Supplier = result.Supplier;
            if (!string.IsNullOrWhiteSpace(result.QuoteDate)) preview.QuoteDate = result.QuoteDate;
            if (!string.IsNullOrWhiteSpace(result.QuoteNo)) preview.QuoteNo = result.QuoteNo;
            if (result.HeaderRow.HasValue) preview.HeaderRow = result.HeaderRow.Value;
            if (result.QuantityRow.HasValue) preview.QuantityRow = result.QuantityRow.Value;
            if (result.DataStartRow.HasValue) preview.DataStartRow = result.DataStartRow.Value;

            preview.Items = new List<QuoteImportItem>();
            foreach (var aiItem in result.Items)
            {
                if (aiItem == null)
                {
                    continue;
                }

                preview.Items.Add(new QuoteImportItem
                {
                    RawName = aiItem.RawName,
                    MaterialCode = aiItem.MaterialCode,
                    MaterialName = aiItem.MaterialName,
                    FinishedSize = aiItem.FinishedSize,
                    MaterialProcess = aiItem.MaterialProcess,
                    MaterialNameExtracted = aiItem.MaterialNameExtracted,
                    GramWeight = aiItem.GramWeight,
                    PriceTiers = new List<PriceTier>()
                });
            }

            return preview.Items.Count;
        }

        private void OnImportSelected(object sender, EventArgs e)
        {
            var row = GetSingleReviewRow();
            if (row == null)
            {
                return;
            }

            var result = row.Tag as BatchQuoteScanResult;
            if (result == null || result.Preview == null)
            {
                MessageBox.Show(this, "选中的文件还没有可校对结果。", "识别校对", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new AiRecognitionReviewForm(result.Preview, Convert.ToString(row.Cells["FilePath"].Value), Convert.ToString(row.Cells["FileName"].Value)))
            {
                form.ShowDialog(this);
                result.Preview = form.Preview;
                RefreshScanRowAfterReview(row, result);
            }
        }

        private DataGridViewRow GetSingleReviewRow()
        {
            var rows = GetCheckedReviewRows();
            if (rows.Count == 0 && _grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in _grid.SelectedRows)
                {
                    if (!row.IsNewRow && row.Tag is BatchQuoteScanResult)
                    {
                        rows.Add(row);
                    }
                }
            }

            if (rows.Count == 0)
            {
                MessageBox.Show(this, "请先勾选一条需要识别校对的扫描结果。", "识别校对", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            if (rows.Count > 1)
            {
                MessageBox.Show(this, "识别校对一次只能打开一条报价单。请只勾选一条，或双击要校对的行。", "识别校对", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            return rows[0];
        }

        private void RefreshScanRowAfterReview(DataGridViewRow row, BatchQuoteScanResult result)
        {
            if (row == null || result == null || result.Preview == null)
            {
                return;
            }

            var preview = result.Preview;
            row.Tag = result;
            row.Cells["Status"].Value = "已校对";
            row.Cells["TemplateType"].Value = preview.TemplateType;
            row.Cells["Supplier"].Value = preview.Supplier;
            row.Cells["SheetName"].Value = preview.SheetName;
            row.Cells["ItemCount"].Value = preview.Items == null ? 0 : preview.Items.Count;
            var validation = ValidateLearnedLocalRule(preview);
            row.Cells["LearningStatus"].Value = BuildLearningStatus(preview);
            row.Cells["LocalRuleStatus"].Value = validation.StatusText;
            row.Cells["QualityScore"].Value = BuildQualityScore(preview, validation);
            row.Cells["ReuseRate"].Value = validation.SourceCount <= 0 ? "-" : validation.AppliedCount + "/" + validation.SourceCount;
            row.Cells["Error"].Value = "已完成识别校对";
            ApplyScanRowVisualState(row);
            UpdateLearningSummary();
        }

        private List<DataGridViewRow> GetCheckedReviewRows()
        {
            var rows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow && IsRowChecked(row, "ReviewSelected"))
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private void SetScanningState(bool scanning)
        {
            _browseButton.Enabled = !scanning;
            _scanButton.Enabled = !scanning;
            _cancelScanButton.Enabled = scanning;
            _importButton.Enabled = !scanning;
            _aiCleanButton.Enabled = !scanning;
            _selectReviewableButton.Enabled = !scanning;
            _invertSelectionButton.Enabled = !scanning;
            _clearSelectionButton.Enabled = !scanning;
            _saveMaterialsButton.Enabled = !scanning;
            _saveRulesButton.Enabled = !scanning;
            Cursor = scanning ? Cursors.WaitCursor : Cursors.Default;
        }

        private void SetProgress(int value, int maximum)
        {
            if (_progressBar == null)
            {
                return;
            }

            var safeMaximum = Math.Max(1, maximum);
            _progressBar.Maximum = safeMaximum;
            _progressBar.Value = Math.Max(0, Math.Min(value, safeMaximum));
        }

        private void RunOnUi(Action action)
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    if (!IsDisposed)
                    {
                        action();
                    }
                }));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnGridCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void OnGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "ReviewSelected")
            {
                ApplyScanRowVisualState(_grid.Rows[e.RowIndex]);
                UpdateLearningSummary();
            }
        }

        private void OnGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var row = _grid.Rows[e.RowIndex];
            if (!(row.Tag is BatchQuoteScanResult))
            {
                return;
            }

            SetReviewSelection(false, false);
            row.Cells["ReviewSelected"].Value = true;
            ApplyScanRowVisualState(row);
            OnImportSelected(sender, EventArgs.Empty);
        }

        private void SetReviewSelection(bool selected, bool onlyReviewable)
        {
            var changed = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                if (onlyReviewable && !(row.Tag is BatchQuoteScanResult))
                {
                    continue;
                }

                row.Cells["ReviewSelected"].Value = selected;
                ApplyScanRowVisualState(row);
                changed++;
            }

            _statusLabel.Text = selected
                ? "已选择可复核报价单 " + changed + " 条。"
                : "已取消选择。";
            UpdateLearningSummary();
        }

        private void InvertReviewSelection()
        {
            var changed = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow || !(row.Tag is BatchQuoteScanResult))
                {
                    continue;
                }

                row.Cells["ReviewSelected"].Value = !IsRowChecked(row, "ReviewSelected");
                ApplyScanRowVisualState(row);
                changed++;
            }

            _statusLabel.Text = "已反选可复核报价单 " + changed + " 条。";
            UpdateLearningSummary();
        }

        private void ApplyScanRowVisualState(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow)
            {
                return;
            }

            if (IsRowChecked(row, "ReviewSelected"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(204, 232, 255);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(24, 144, 255);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
                return;
            }

            var status = ReadCell(row, "Status");
            if (status == "未识别")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 219);
            }
            else if (status == "AI失败")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 241, 240);
            }
            else if (status == "可校对" || status == "AI已识别" || status == "已校对")
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(238, 247, 255);
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.White;
            }

            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 244, 255);
            row.DefaultCellStyle.SelectionForeColor = Color.FromArgb(29, 29, 31);
        }

        private static void AddColumn(DataGridView grid, string name, string header)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private static void AddCheckColumn(DataGridView grid, string name, string header)
        {
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private static void ConfigureScanGridColumns(DataGridView grid)
        {
            SetColumn(grid, "ReviewSelected", 58, false);
            SetColumn(grid, "Status", 92, false);
            SetColumn(grid, "FileName", 260, true);
            SetColumn(grid, "TemplateType", 150, true);
            SetColumn(grid, "Supplier", 160, true);
            SetColumn(grid, "SheetName", 130, false);
            SetColumn(grid, "ItemCount", 78, false);
            SetColumn(grid, "LearningStatus", 128, true);
            SetColumn(grid, "LocalRuleStatus", 150, true);
            SetColumn(grid, "QualityScore", 78, false);
            SetColumn(grid, "ReuseRate", 78, false);
            SetColumn(grid, "MaterialCandidates", 82, false);
            SetColumn(grid, "RuleCandidates", 82, false);
            SetColumn(grid, "Error", 260, true);
        }

        private static void SetColumn(DataGridView grid, string name, int width, bool wrap)
        {
            if (!grid.Columns.Contains(name))
            {
                return;
            }

            var column = grid.Columns[name];
            column.Width = width;
            column.MinimumWidth = Math.Min(width, 60);
            column.DefaultCellStyle.WrapMode = wrap ? DataGridViewTriState.True : DataGridViewTriState.False;
        }

        private static string BuildLearningStatus(QuoteImportPreview preview)
        {
            if (preview == null || preview.Items == null || preview.Items.Count == 0)
            {
                return "无可学习物料";
            }

            var parts = new List<string> { "已生成本地规则" };
            if (preview.HeaderRow > 0)
            {
                parts.Add("表头" + preview.HeaderRow);
            }

            if (preview.DataStartRow > 0)
            {
                parts.Add("数据" + preview.DataStartRow);
            }

            return string.Join("；", parts.ToArray());
        }

        private static LocalRuleValidationResult ValidateLearnedLocalRule(QuoteImportPreview preview)
        {
            if (preview == null || preview.RawSheet == null || preview.RawSheet.Cells == null || preview.Items == null || preview.Items.Count == 0)
            {
                return new LocalRuleValidationResult { StatusText = "缺少原始预览" };
            }

            try
            {
                var applied = new LearnedQuoteTemplateService().TryApplyForValidation(
                    preview.SheetName,
                    preview.RawSheet.Cells,
                    preview.RawSheet.Rows,
                    preview.RawSheet.Columns);
                var sourceCount = preview.Items.Count;
                if (applied == null || applied.Items == null || applied.Items.Count == 0)
                {
                    return new LocalRuleValidationResult
                    {
                        StatusText = "需复核",
                        SourceCount = sourceCount
                    };
                }

                var appliedCount = applied.Items.Count;
                if (appliedCount >= sourceCount)
                {
                    return new LocalRuleValidationResult
                    {
                        StatusText = "可本地复用：" + appliedCount + "条",
                        SourceCount = sourceCount,
                        AppliedCount = appliedCount
                    };
                }

                return new LocalRuleValidationResult
                {
                    StatusText = "部分复用：" + appliedCount + "/" + sourceCount,
                    SourceCount = sourceCount,
                    AppliedCount = appliedCount
                };
            }
            catch (Exception ex)
            {
                return new LocalRuleValidationResult { StatusText = "验证失败：" + ex.Message };
            }
        }

        private static string BuildQualityScore(QuoteImportPreview preview, LocalRuleValidationResult validation)
        {
            if (preview == null || preview.Items == null || preview.Items.Count == 0)
            {
                return "-";
            }

            var confidence = Math.Max(0, Math.Min(1, preview.Confidence));
            var reuse = validation == null || validation.SourceCount <= 0
                ? 0
                : Math.Max(0, Math.Min(1, validation.AppliedCount / (double)validation.SourceCount));
            var score = (int)Math.Round((confidence * 0.45 + reuse * 0.55) * 100);
            return score + "%";
        }

        private void UpdateLearningSummary()
        {
            if (_learningSummaryLabel == null)
            {
                return;
            }

            var learned = 0;
            var reusable = 0;
            var partial = 0;
            var pending = 0;
            var selected = 0;
            var qualityTotal = 0;
            var qualityCount = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var learning = ReadCell(row, "LearningStatus");
                var local = ReadCell(row, "LocalRuleStatus");
                if (learning.StartsWith("已", StringComparison.OrdinalIgnoreCase))
                {
                    learned++;
                }

                if (IsRowChecked(row, "ReviewSelected"))
                {
                    selected++;
                }

                if (local.StartsWith("可本地复用", StringComparison.OrdinalIgnoreCase))
                {
                    reusable++;
                }
                else if (local.StartsWith("部分复用", StringComparison.OrdinalIgnoreCase))
                {
                    partial++;
                }
                else if (local == "需复核")
                {
                    pending++;
                }

                var quality = ReadCell(row, "QualityScore").TrimEnd('%');
                int parsed;
                if (int.TryParse(quality, out parsed))
                {
                    qualityTotal += parsed;
                    qualityCount++;
                }
            }

            var avg = qualityCount == 0 ? "-" : (qualityTotal / qualityCount) + "%";
            _learningSummaryLabel.Text = string.Format(
                "学习质量：已学{0} 可复用{1} 部分{2} 待确认{3} 已选{4} 平均{5}",
                learned,
                reusable,
                partial,
                pending,
                selected,
                avg);
        }

        private CandidateCounts AddCandidatesFromPreview(QuoteImportPreview preview, string sourceFileName)
        {
            var counts = new CandidateCounts();
            if (preview == null || preview.Items == null)
            {
                return counts;
            }

            var existingMaterials = BuildMaterialCandidateSet();
            var existingRules = BuildRuleCandidateSet();
            var source = string.IsNullOrWhiteSpace(sourceFileName) ? preview.SheetName : sourceFileName;
            foreach (var item in preview.Items)
            {
                foreach (var material in ExtractMaterialCandidates(preview, item, source))
                {
                    var key = Normalize(material.Name);
                    if (string.IsNullOrWhiteSpace(key) || existingMaterials.Contains(key))
                    {
                        continue;
                    }

                    existingMaterials.Add(key);
                    AddMaterialCandidateRow(material);
                    counts.MaterialCount++;
                }

                foreach (var rule in ExtractRuleCandidates(item, source))
                {
                    var key = Normalize(rule.Keyword);
                    if (string.IsNullOrWhiteSpace(key) || existingRules.Contains(key))
                    {
                        continue;
                    }

                    existingRules.Add(key);
                    AddRuleCandidateRow(rule);
                    counts.RuleCount++;
                }
            }

            return counts;
        }

        private HashSet<string> BuildMaterialCandidateSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var material in new MaterialRepository().GetAll())
            {
                if (!string.IsNullOrWhiteSpace(material.Name))
                {
                    set.Add(Normalize(material.Name));
                }
            }

            foreach (DataGridViewRow row in _materialsGrid.Rows)
            {
                if (!row.IsNewRow)
                {
                    var name = ReadCell(row, "Name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        set.Add(Normalize(name));
                    }
                }
            }

            return set;
        }

        private HashSet<string> BuildRuleCandidateSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in new ProcessCostRuleRepository().GetAll())
            {
                if (!string.IsNullOrWhiteSpace(rule.Keyword))
                {
                    set.Add(Normalize(rule.Keyword));
                }
            }

            foreach (DataGridViewRow row in _rulesGrid.Rows)
            {
                if (!row.IsNewRow)
                {
                    var keyword = ReadCell(row, "Keyword");
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        set.Add(Normalize(keyword));
                    }
                }
            }

            return set;
        }

        private void AddMaterialCandidateRow(MaterialCandidate candidate)
        {
            var rowIndex = _materialsGrid.Rows.Add();
            var row = _materialsGrid.Rows[rowIndex];
            row.Cells["Selected"].Value = true;
            row.Cells["Name"].Value = candidate.Name;
            row.Cells["Category"].Value = GuessMaterialCategory(candidate.Name);
            row.Cells["Vendor"].Value = candidate.Vendor;
            row.Cells["Spec"].Value = candidate.Spec;
            row.Cells["Unit"].Value = "张";
            row.Cells["IncludesFreight"].Value = "是";
            row.Cells["Source"].Value = candidate.Source;
            row.Cells["Existing"].Value = "新候选";
            row.DefaultCellStyle.BackColor = Color.FromArgb(238, 247, 255);
        }

        private void AddRuleCandidateRow(RuleCandidate candidate)
        {
            var rowIndex = _rulesGrid.Rows.Add();
            var row = _rulesGrid.Rows[rowIndex];
            row.Cells["Selected"].Value = true;
            row.Cells["Keyword"].Value = candidate.Keyword;
            row.Cells["CostType"].Value = ToDisplayCostType(candidate.CostType);
            row.Cells["Amount"].Value = string.Empty;
            row.Cells["IsEnabled"].Value = "是";
            row.Cells["Remark"].Value = BuildRuleRemark(candidate, candidate.Source);
            row.Cells["Source"].Value = candidate.Source;
            row.Cells["Existing"].Value = "新候选";
            row.DefaultCellStyle.BackColor = Color.FromArgb(238, 247, 255);
        }

        private void OnSaveMaterialCandidates(object sender, EventArgs e)
        {
            var existing = new MaterialRepository().GetAll();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var material in existing)
            {
                names.Add(Normalize(material.Name));
            }

            var added = 0;
            foreach (DataGridViewRow row in _materialsGrid.Rows)
            {
                if (row.IsNewRow || !IsRowChecked(row, "Selected"))
                {
                    continue;
                }

                var name = ReadCell(row, "Name");
                var key = Normalize(name);
                if (string.IsNullOrWhiteSpace(key) || names.Contains(key))
                {
                    continue;
                }

                names.Add(key);
                existing.Add(new MaterialRecord
                {
                    Name = name,
                    Category = ReadCell(row, "Category"),
                    Vendor = ReadCell(row, "Vendor"),
                    Spec = ReadCell(row, "Spec"),
                    Unit = string.IsNullOrWhiteSpace(ReadCell(row, "Unit")) ? "张" : ReadCell(row, "Unit"),
                    IncludesFreight = ReadCell(row, "IncludesFreight") == "是" || ReadCell(row, "IncludesFreight") == "1",
                    Remark = "AI学习识别：" + ReadCell(row, "Source")
                });
                row.Cells["Existing"].Value = "已入库";
                row.Cells["Selected"].Value = false;
                added++;
            }

            if (added > 0)
            {
                new MaterialRepository().SaveAll(existing);
            }

            MessageBox.Show(this, added == 0 ? "没有新的材料候选需要保存。" : "已保存材料候选 " + added + " 条。", "保存材料候选", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnSaveRuleCandidates(object sender, EventArgs e)
        {
            var existing = new ProcessCostRuleRepository().GetAll();
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in existing)
            {
                keywords.Add(Normalize(rule.Keyword));
            }

            var added = 0;
            foreach (DataGridViewRow row in _rulesGrid.Rows)
            {
                if (row.IsNewRow || !IsRowChecked(row, "Selected"))
                {
                    continue;
                }

                var keyword = ReadCell(row, "Keyword");
                var key = Normalize(keyword);
                if (string.IsNullOrWhiteSpace(key) || keywords.Contains(key))
                {
                    continue;
                }

                decimal amount;
                keywords.Add(key);
                existing.Add(new ProcessCostRule
                {
                    Keyword = keyword,
                    CostType = NormalizeCostType(ReadCell(row, "CostType")),
                    Amount = decimal.TryParse(ReadCell(row, "Amount"), out amount) ? amount : (decimal?)null,
                    IsEnabled = ReadCell(row, "IsEnabled") == "是" || ReadCell(row, "IsEnabled") == "1",
                    Remark = ReadCell(row, "Remark")
                });
                row.Cells["Existing"].Value = "已入库";
                row.Cells["Selected"].Value = false;
                added++;
            }

            if (added > 0)
            {
                new ProcessCostRuleRepository().SaveAll(existing);
            }

            MessageBox.Show(this, added == 0 ? "没有新的工艺候选需要保存。" : "已保存工艺候选 " + added + " 条。", "保存工艺候选", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static IEnumerable<MaterialCandidate> ExtractMaterialCandidates(QuoteImportPreview preview, QuoteImportItem item, string source)
        {
            var materialText = item == null ? string.Empty : item.MaterialNameExtracted;
            foreach (var name in SplitMaterials(materialText))
            {
                var trimmed = name.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                yield return new MaterialCandidate
                {
                    Name = trimmed,
                    Vendor = preview == null ? string.Empty : preview.Supplier,
                    Spec = PickSpecForMaterial(trimmed, item == null ? string.Empty : item.GramWeight),
                    Source = source
                };
            }
        }

        private static IEnumerable<RuleCandidate> ExtractRuleCandidates(QuoteImportItem item, string source)
        {
            var text = ((item == null ? string.Empty : item.MaterialProcess) ?? string.Empty) + "；" +
                       ((item == null ? string.Empty : item.MaterialNameExtracted) ?? string.Empty);
            foreach (var candidate in ExtractProcessKeywords(text))
            {
                candidate.Source = source;
                yield return candidate;
            }
        }

        private static IEnumerable<RuleCandidate> ExtractProcessKeywords(string text)
        {
            var keywords = new[]
            {
                new RuleCandidate("4C", "PrintingCost"),
                new RuleCandidate("专色", "PrintingCost"),
                new RuleCandidate("印刷", "PrintingCost"),
                new RuleCandidate("UV", "PostProcessCost", "面积*0.02 最低0.5"),
                new RuleCandidate("局部UV", "PostProcessCost", "面积*0.02 最低0.5"),
                new RuleCandidate("哑胶", "PostProcessCost", "面积*0.01 最低0.3"),
                new RuleCandidate("光胶", "PostProcessCost", "面积*0.01 最低0.3"),
                new RuleCandidate("覆膜", "PostProcessCost", "面积*0.01 最低0.3"),
                new RuleCandidate("过胶", "PostProcessCost"),
                new RuleCandidate("啤", "PostProcessCost", "周长*1 最低0.5"),
                new RuleCandidate("啤形", "PostProcessCost", "周长*1 最低0.5"),
                new RuleCandidate("V槽", "PostProcessCost", "周长*1.5 最低0.5"),
                new RuleCandidate("烫金", "PostProcessCost"),
                new RuleCandidate("击凸", "PostProcessCost"),
                new RuleCandidate("粘", "PostProcessCost"),
                new RuleCandidate("礼盒成型", "PostProcessCost"),
                new RuleCandidate("磁铁", "OtherCost"),
                new RuleCandidate("装箱", "OtherCost"),
                new RuleCandidate("运输", "OtherCost")
            };

            var normalizedText = Normalize(text);
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyword in keywords)
            {
                if (normalizedText.IndexOf(Normalize(keyword.Keyword), StringComparison.OrdinalIgnoreCase) >= 0 &&
                    emitted.Add(Normalize(keyword.Keyword)))
                {
                    yield return keyword;
                }
            }
        }

        private static IEnumerable<string> SplitMaterials(string value)
        {
            return (value ?? string.Empty).Split(new[] { ';', '；', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string PickSpecForMaterial(string materialName, string specText)
        {
            if (string.IsNullOrWhiteSpace(specText))
            {
                return string.Empty;
            }

            var specs = specText.Split(new[] { ';', '；', ',', '，', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var spec in specs)
            {
                var trimmed = spec.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && materialName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return trimmed;
                }
            }

            return specs.Length == 1 ? specs[0].Trim() : specText;
        }

        private static string GuessMaterialCategory(string materialName)
        {
            if (ContainsAny(materialName, "灰板", "双灰", "纸板", "坑", "楞")) return "纸板";
            if (ContainsAny(materialName, "纸", "双铜", "双胶", "白卡", "黑卡", "牛卡")) return "纸张";
            if (ContainsAny(materialName, "PET", "PVC", "胶", "膜", "磁铁", "海绵", "EVA")) return "辅料";
            return string.Empty;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if ((text ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildRuleRemark(RuleCandidate candidate, string source)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.FormulaHint))
            {
                return "AI学习识别：" + source;
            }

            return candidate.FormulaHint + "；AI学习识别：" + source;
        }

        private static string NormalizeCostType(string value)
        {
            if ((value ?? string.Empty).Contains("印刷")) return "PrintingCost";
            if ((value ?? string.Empty).Contains("其他")) return "OtherCost";
            if ((value ?? string.Empty).Contains("材料")) return "MaterialCost";
            if (value == "PrintingCost" || value == "OtherCost" || value == "MaterialCost" || value == "PostProcessCost") return value;
            return "PostProcessCost";
        }

        private static string ToDisplayCostType(string value)
        {
            if (value == "PrintingCost") return "印刷费";
            if (value == "OtherCost") return "其他";
            if (value == "MaterialCost") return "材料费";
            return "后工序费";
        }

        private static string ReadCell(DataGridViewRow row, string columnName)
        {
            if (row == null || row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            var value = row.Cells[columnName].Value;
            return value == null ? string.Empty : Convert.ToString(value).Trim();
        }

        private static bool IsRowChecked(DataGridViewRow row, string columnName)
        {
            if (row == null || row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return false;
            }

            var value = row.Cells[columnName].Value;
            return value is bool && (bool)value;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Replace("G", "g").Trim().ToLowerInvariant();
        }

        private sealed class BatchQuoteScanResult
        {
            public QuoteImportPreview Preview { get; set; }
        }

        private sealed class CandidateCounts
        {
            public int MaterialCount { get; set; }
            public int RuleCount { get; set; }
        }

        private sealed class LocalRuleValidationResult
        {
            public string StatusText { get; set; }
            public int SourceCount { get; set; }
            public int AppliedCount { get; set; }
        }

        private sealed class MaterialCandidate
        {
            public string Name { get; set; }
            public string Vendor { get; set; }
            public string Spec { get; set; }
            public string Source { get; set; }
        }

        private sealed class RuleCandidate
        {
            public RuleCandidate(string keyword, string costType)
                : this(keyword, costType, string.Empty)
            {
            }

            public RuleCandidate(string keyword, string costType, string formulaHint)
            {
                Keyword = keyword;
                CostType = costType;
                FormulaHint = formulaHint;
            }

            public string Keyword { get; private set; }
            public string CostType { get; private set; }
            public string FormulaHint { get; private set; }
            public string Source { get; set; }
        }

        private sealed class AiCleanTarget
        {
            public int RowIndex { get; set; }
            public string FilePath { get; set; }
            public string FileName { get; set; }
            public BatchQuoteScanResult Result { get; set; }
        }
    }
}
