using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class ProcessCostRulesForm : Form
    {
        private readonly DataGridView _grid;
        private readonly DataGridView _sourceGrid;
        private readonly Button _batchScanButton;
        private readonly Button _stopScanButton;
        private readonly ProgressBar _scanProgressBar;
        private readonly Label _scanStatusLabel;
        private volatile bool _cancelBatchScan;

        public ProcessCostRulesForm() : this(null)
        {
        }

        public ProcessCostRulesForm(DataGridView sourceGrid)
        {
            _sourceGrid = sourceGrid;
            Text = "工艺费用规则";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(860, 560);
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            root.Controls.Add(toolbar, 0, 0);

            var save = new Button { Text = "保存", Width = 86, Height = 30 };
            save.Click += OnSave;
            toolbar.Controls.Add(save);

            var add = new Button { Text = "新增规则", Width = 96, Height = 30 };
            add.Click += (_, __) => _grid.Rows.Add();
            toolbar.Controls.Add(add);

            var batchEdit = new Button { Text = "批量编辑", Width = 96, Height = 30 };
            batchEdit.Click += OnBatchEdit;
            toolbar.Controls.Add(batchEdit);

            var scan = new Button { Text = "扫描填入", Width = 96, Height = 30 };
            scan.Click += OnScanFromCurrentAnalysis;
            toolbar.Controls.Add(scan);

            _batchScanButton = new Button { Text = "批量扫描报价单", Width = 126, Height = 30 };
            _batchScanButton.Click += OnBatchScanFolder;
            toolbar.Controls.Add(_batchScanButton);

            _stopScanButton = new Button { Text = "停止扫描", Width = 96, Height = 30, Enabled = false };
            _stopScanButton.Click += (_, __) =>
            {
                _cancelBatchScan = true;
                _stopScanButton.Enabled = false;
                _scanStatusLabel.Text = "正在停止，当前文件读取完成后会停下...";
            };
            toolbar.Controls.Add(_stopScanButton);

            var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            root.Controls.Add(statusPanel, 0, 1);

            _scanStatusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "就绪",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(92, 99, 112)
            };
            statusPanel.Controls.Add(_scanStatusLabel, 0, 0);

            _scanProgressBar = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Value = 0 };
            statusPanel.Controls.Add(_scanProgressBar, 1, 0);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            root.Controls.Add(_grid, 0, 2);

            AddColumn("Keyword", "关键词");
            AddColumn("CostType", "费用类型");
            AddColumn("Amount", "金额");
            AddColumn("IsEnabled", "启用");
            AddColumn("Remark", "备注");
            LoadRules();
        }

        private void OnScanFromCurrentAnalysis(object sender, EventArgs e)
        {
            var added = ScanFromCurrentAnalysis();
            MessageBox.Show(this, added == 0 ? "没有扫描到新的工艺关键词。" : "已从当前成本分析表扫描新增工艺规则 " + added + " 条。", "扫描填入", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnBatchScanFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择要扫描的报价单文件夹";
                var defaultFolder = @"D:\1\LB报价单";
                if (Directory.Exists(defaultFolder))
                {
                    dialog.SelectedPath = defaultFolder;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    BeginBatchScan(dialog.SelectedPath);
                }
            }
        }

        private void BeginBatchScan(string folderPath)
        {
            var files = GetQuoteFiles(folderPath);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "这个文件夹里没有可扫描的 .xls/.xlsx 报价单。", "批量扫描报价单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _cancelBatchScan = false;
            _batchScanButton.Enabled = false;
            _stopScanButton.Enabled = true;
            _scanProgressBar.Value = 0;
            _scanStatusLabel.Text = "准备扫描 " + files.Count + " 个报价单...";

            var worker = new BackgroundWorker { WorkerReportsProgress = true };
            var existing = BuildExistingKeywordSet();
            worker.DoWork += (s, args) => args.Result = ScanQuoteFiles(files, existing, worker);
            worker.ProgressChanged += (s, args) =>
            {
                _scanProgressBar.Value = Math.Max(0, Math.Min(100, args.ProgressPercentage));
                _scanStatusLabel.Text = Convert.ToString(args.UserState);
            };
            worker.RunWorkerCompleted += (s, args) =>
            {
                _batchScanButton.Enabled = true;
                _stopScanButton.Enabled = false;

                if (args.Error != null)
                {
                    _scanStatusLabel.Text = "扫描失败";
                    MessageBox.Show(this, args.Error.Message, "批量扫描报价单", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var stats = args.Result as BatchScanStats ?? new BatchScanStats();
                foreach (var candidate in stats.Rules)
                {
                    AddRuleRow(candidate.Keyword, candidate.CostType, candidate.Remark);
                }

                _scanProgressBar.Value = 100;
                _scanStatusLabel.Text = stats.Cancelled ? "已停止，新增 " + stats.AddedCount + " 条工艺规则候选。" : "扫描完成，新增 " + stats.AddedCount + " 条工艺规则候选。";
                MessageBox.Show(
                    this,
                    "扫描文件：" + stats.FileCount + " 个\r\n成功读取：" + stats.SuccessCount + " 个\r\n新增规则：" + stats.AddedCount + " 条\r\n失败：" + stats.FailedCount + " 个" + (stats.Cancelled ? "\r\n状态：已停止" : string.Empty),
                    "批量扫描报价单",
                    MessageBoxButtons.OK,
                    stats.AddedCount > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            };
            worker.RunWorkerAsync();
        }

        private BatchScanStats ScanQuoteFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return new BatchScanStats();
            }

            var stats = ScanQuoteFiles(GetQuoteFiles(folderPath), BuildExistingKeywordSet(), null);
            foreach (var candidate in stats.Rules)
            {
                AddRuleRow(candidate.Keyword, candidate.CostType, candidate.Remark);
            }

            return stats;
        }

        private BatchScanStats ScanQuoteFiles(List<string> files, HashSet<string> existing, BackgroundWorker worker)
        {
            var stats = new BatchScanStats();
            var service = new ExcelQuoteImportService();
            foreach (var file in files)
            {
                if (_cancelBatchScan)
                {
                    stats.Cancelled = true;
                    break;
                }

                stats.FileCount++;
                ReportScanProgress(worker, stats.FileCount, files.Count, "工艺规则 - 正在扫描 " + stats.FileCount + "/" + files.Count + "：" + Path.GetFileName(file));

                try
                {
                    var preview = service.Import(file);
                    stats.SuccessCount++;
                    if (preview == null || preview.Items == null)
                    {
                        continue;
                    }

                    foreach (var item in preview.Items)
                    {
                        var text = (item.MaterialProcess ?? string.Empty) + "；" + (item.MaterialNameExtracted ?? string.Empty);
                        foreach (var candidate in ExtractProcessKeywords(text))
                        {
                            var key = Normalize(candidate.Keyword);
                            if (string.IsNullOrWhiteSpace(key) || existing.Contains(key))
                            {
                                continue;
                            }

                            existing.Add(key);
                            stats.Rules.Add(new BatchRuleCandidate(candidate.Keyword, candidate.CostType, "批量扫描：" + Path.GetFileName(file)));
                            stats.AddedCount++;
                        }
                    }
                }
                catch
                {
                    stats.FailedCount++;
                }
            }

            ReportScanProgress(worker, files.Count, files.Count, stats.Cancelled ? "已停止" : "扫描完成");
            return stats;
        }

        private static void ReportScanProgress(BackgroundWorker worker, int current, int total, string message)
        {
            if (worker == null || total <= 0)
            {
                return;
            }

            worker.ReportProgress((int)Math.Round(current * 100.0 / total), message);
        }

        private void OnBatchEdit(object sender, EventArgs e)
        {
            var rows = GetTargetRows();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "没有可批量编辑的工艺规则行。", "批量编辑", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var fields = new[]
            {
                new BatchEditField("CostType", "费用类型"),
                new BatchEditField("Amount", "金额"),
                new BatchEditField("IsEnabled", "启用"),
                new BatchEditField("Remark", "备注")
            };
            using (var form = new BatchGridEditForm("工艺规则批量编辑", fields))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var changed = ApplyBatchEdit(rows, form.ColumnName, form.NewValue, form.OnlyEmpty);
                MessageBox.Show(this, "已更新 " + changed + " 个单元格。", "批量编辑", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private int ApplyBatchEdit(List<DataGridViewRow> rows, string columnName, string value, bool onlyEmpty)
        {
            var changed = 0;
            foreach (var row in rows)
            {
                if (!_grid.Columns.Contains(columnName))
                {
                    continue;
                }

                var current = ReadCell(row, columnName);
                if (onlyEmpty && !string.IsNullOrWhiteSpace(current))
                {
                    continue;
                }

                row.Cells[columnName].Value = value;
                row.DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 255);
                changed++;
            }

            return changed;
        }

        private List<DataGridViewRow> GetTargetRows()
        {
            var rows = new List<DataGridViewRow>();
            var seen = new HashSet<int>();
            foreach (DataGridViewCell cell in _grid.SelectedCells)
            {
                if (cell.RowIndex >= 0 && seen.Add(cell.RowIndex))
                {
                    var row = _grid.Rows[cell.RowIndex];
                    if (!row.IsNewRow)
                    {
                        rows.Add(row);
                    }
                }
            }

            if (rows.Count > 0)
            {
                return rows;
            }

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private void AddRuleRow(string keyword, string costType, string remark)
        {
            var rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Cells["Keyword"].Value = keyword;
            row.Cells["CostType"].Value = ToDisplayCostType(costType);
            row.Cells["IsEnabled"].Value = "是";
            row.Cells["Remark"].Value = remark;
            row.DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 255);
        }

        private int ScanFromCurrentAnalysis()
        {
            if (_sourceGrid == null)
            {
                return 0;
            }

            var existing = BuildExistingKeywordSet();
            var added = 0;
            foreach (DataGridViewRow sourceRow in _sourceGrid.Rows)
            {
                if (sourceRow.IsNewRow)
                {
                    continue;
                }

                var text = ReadSourceCell(sourceRow, "MaterialDescription") + "；" + ReadSourceCell(sourceRow, "BaseMaterialName");
                foreach (var candidate in ExtractProcessKeywords(text))
                {
                    var key = Normalize(candidate.Keyword);
                    if (string.IsNullOrWhiteSpace(key) || existing.Contains(key))
                    {
                        continue;
                    }

                    existing.Add(key);
                    AddRuleRow(candidate.Keyword, candidate.CostType, "从当前成本分析表扫描");
                    added++;
                }
            }

            return added;
        }

        private HashSet<string> BuildExistingKeywordSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in _grid.Rows)
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

        private static IEnumerable<ProcessKeywordCandidate> ExtractProcessKeywords(string text)
        {
            var keywords = new[]
            {
                new ProcessKeywordCandidate("4C", "PrintingCost"),
                new ProcessKeywordCandidate("专色", "PrintingCost"),
                new ProcessKeywordCandidate("印刷", "PrintingCost"),
                new ProcessKeywordCandidate("UV", "PostProcessCost"),
                new ProcessKeywordCandidate("局部UV", "PostProcessCost"),
                new ProcessKeywordCandidate("哑胶", "PostProcessCost"),
                new ProcessKeywordCandidate("光胶", "PostProcessCost"),
                new ProcessKeywordCandidate("覆膜", "PostProcessCost"),
                new ProcessKeywordCandidate("过胶", "PostProcessCost"),
                new ProcessKeywordCandidate("啤", "PostProcessCost"),
                new ProcessKeywordCandidate("啤形", "PostProcessCost"),
                new ProcessKeywordCandidate("V槽", "PostProcessCost"),
                new ProcessKeywordCandidate("烫金", "PostProcessCost"),
                new ProcessKeywordCandidate("击凸", "PostProcessCost"),
                new ProcessKeywordCandidate("粘", "PostProcessCost"),
                new ProcessKeywordCandidate("礼盒成型", "PostProcessCost"),
                new ProcessKeywordCandidate("磁铁", "OtherCost"),
                new ProcessKeywordCandidate("装箱", "OtherCost"),
                new ProcessKeywordCandidate("运输", "OtherCost")
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

        private void LoadRules()
        {
            _grid.Rows.Clear();
            foreach (var rule in new ProcessCostRuleRepository().GetAll())
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Keyword"].Value = rule.Keyword;
                row.Cells["CostType"].Value = ToDisplayCostType(rule.CostType);
                row.Cells["Amount"].Value = rule.Amount.HasValue ? rule.Amount.Value.ToString("0.####") : string.Empty;
                row.Cells["IsEnabled"].Value = rule.IsEnabled ? "是" : string.Empty;
                row.Cells["Remark"].Value = rule.Remark;
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            try
            {
                new ProcessCostRuleRepository().SaveAll(ReadRules());
                MessageBox.Show(this, "工艺费用规则已保存。", "工艺费用规则", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private List<ProcessCostRule> ReadRules()
        {
            var list = new List<ProcessCostRule>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var keyword = ReadCell(row, "Keyword");
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                decimal amount;
                list.Add(new ProcessCostRule
                {
                    Keyword = keyword,
                    CostType = NormalizeCostType(ReadCell(row, "CostType")),
                    Amount = decimal.TryParse(ReadCell(row, "Amount"), out amount) ? amount : (decimal?)null,
                    IsEnabled = ReadCell(row, "IsEnabled") == "是" || ReadCell(row, "IsEnabled") == "1",
                    Remark = ReadCell(row, "Remark")
                });
            }

            return list;
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
            var value = row.Cells[columnName].Value;
            return value == null ? string.Empty : Convert.ToString(value).Trim();
        }

        private static string ReadSourceCell(DataGridViewRow row, string columnName)
        {
            if (row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            var value = row.Cells[columnName].Value;
            return value == null ? string.Empty : Convert.ToString(value).Trim();
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }

        private static List<string> GetQuoteFiles(string folderPath)
        {
            var files = new List<string>();
            foreach (var file in Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var extension = Path.GetExtension(file);
                if (string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(file);
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private void AddColumn(string name, string header)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, SortMode = DataGridViewColumnSortMode.NotSortable });
        }

        private sealed class BatchScanStats
        {
            public BatchScanStats()
            {
                Rules = new List<BatchRuleCandidate>();
            }

            public int FileCount { get; set; }
            public int SuccessCount { get; set; }
            public int AddedCount { get; set; }
            public int FailedCount { get; set; }
            public bool Cancelled { get; set; }
            public List<BatchRuleCandidate> Rules { get; private set; }
        }

        private sealed class BatchRuleCandidate
        {
            public BatchRuleCandidate(string keyword, string costType, string remark)
            {
                Keyword = keyword;
                CostType = costType;
                Remark = remark;
            }

            public string Keyword { get; private set; }
            public string CostType { get; private set; }
            public string Remark { get; private set; }
        }

        private sealed class ProcessKeywordCandidate
        {
            public ProcessKeywordCandidate(string keyword, string costType)
            {
                Keyword = keyword;
                CostType = costType;
            }

            public string Keyword { get; private set; }
            public string CostType { get; private set; }
        }
    }
}
