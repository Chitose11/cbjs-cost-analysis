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
    internal sealed class MaterialsForm : Form
    {
        private readonly DataGridView _grid;
        private readonly DataGridView _sourceGrid;
        private readonly Button _batchScanButton;
        private readonly Button _stopScanButton;
        private readonly ProgressBar _scanProgressBar;
        private readonly Label _scanStatusLabel;
        private volatile bool _cancelBatchScan;

        public MaterialsForm() : this(null)
        {
        }

        public MaterialsForm(DataGridView sourceGrid)
        {
            _sourceGrid = sourceGrid;
            Text = "材料库";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1080, 600);
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(toolbar, 0, 0);

            var save = new Button { Text = "保存", Width = 86, Height = 30 };
            save.Click += OnSave;
            toolbar.Controls.Add(save);

            var add = new Button { Text = "新增材料", Width = 96, Height = 30 };
            add.Click += (_, __) => _grid.Rows.Add();
            toolbar.Controls.Add(add);

            var delete = new Button { Text = "删除选中", Width = 96, Height = 30 };
            delete.Click += OnDeleteSelectedRows;
            toolbar.Controls.Add(delete);

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

            AddColumn("Name", "材料名称");
            AddColumn("Aliases", "别名");
            AddColumn("Category", "类别");
            AddColumn("Vendor", "材料厂家");
            AddColumn("Spec", "克重规格");
            AddColumn("Unit", "计价单位");
            AddColumn("TaxUnitPrice", "含税单价");
            AddColumn("IncludesFreight", "含运");
            AddColumn("Remark", "备注");

            LoadMaterials();
        }

        private void OnScanFromCurrentAnalysis(object sender, EventArgs e)
        {
            var added = ScanFromCurrentAnalysis();
            MessageBox.Show(this, added == 0 ? "没有扫描到新的材料。" : "已从当前成本分析表扫描新增材料 " + added + " 条。", "扫描填入", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            var existing = BuildExistingMaterialSet();
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
                foreach (var candidate in stats.Materials)
                {
                    AddMaterialRow(candidate.Name, candidate.Vendor, candidate.Spec, candidate.Remark);
                }

                _scanProgressBar.Value = 100;
                _scanStatusLabel.Text = stats.Cancelled ? "已停止，新增 " + stats.AddedCount + " 条材料候选。" : "扫描完成，新增 " + stats.AddedCount + " 条材料候选。";
                MessageBox.Show(
                    this,
                    "扫描文件：" + stats.FileCount + " 个\r\n成功读取：" + stats.SuccessCount + " 个\r\n新增材料：" + stats.AddedCount + " 条\r\n失败：" + stats.FailedCount + " 个" + (stats.Cancelled ? "\r\n状态：已停止" : string.Empty),
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

            var stats = ScanQuoteFiles(GetQuoteFiles(folderPath), BuildExistingMaterialSet(), null);
            foreach (var candidate in stats.Materials)
            {
                AddMaterialRow(candidate.Name, candidate.Vendor, candidate.Spec, candidate.Remark);
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
                ReportScanProgress(worker, stats.FileCount, files.Count, "材料库 - 正在扫描 " + stats.FileCount + "/" + files.Count + "：" + Path.GetFileName(file));

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
                        foreach (var materialName in SplitMaterials(item.MaterialNameExtracted))
                        {
                            var trimmedName = (materialName ?? string.Empty).Trim();
                            var key = Normalize(trimmedName);
                            if (string.IsNullOrWhiteSpace(key) || existing.Contains(key))
                            {
                                continue;
                            }

                            existing.Add(key);
                            stats.Materials.Add(new BatchMaterialCandidate(trimmedName, preview.Supplier, PickSpecForMaterial(trimmedName, item.GramWeight), "批量扫描：" + Path.GetFileName(file)));
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
                MessageBox.Show(this, "没有可批量编辑的材料行。", "批量编辑", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var fields = new[]
            {
                new BatchEditField("Category", "类别"),
                new BatchEditField("Vendor", "材料厂家"),
                new BatchEditField("Spec", "克重规格"),
                new BatchEditField("Unit", "计价单位"),
                new BatchEditField("TaxUnitPrice", "含税单价"),
                new BatchEditField("IncludesFreight", "含运"),
                new BatchEditField("Remark", "备注")
            };
            using (var form = new BatchGridEditForm("材料库批量编辑", fields))
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

        private void OnDeleteSelectedRows(object sender, EventArgs e)
        {
            var rows = GetSelectedRows();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "请先选择要删除的材料行。", "删除材料", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "确定删除选中的 " + rows.Count + " 条材料吗？\r\n删除后需要点击“保存”才会写入材料库。",
                "删除材料",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            rows.Sort((left, right) => right.Index.CompareTo(left.Index));
            foreach (var row in rows)
            {
                if (!row.IsNewRow)
                {
                    _grid.Rows.Remove(row);
                }
            }

            _scanStatusLabel.Text = "已删除 " + rows.Count + " 条材料，请点击“保存”持久化。";
        }

        private List<DataGridViewRow> GetSelectedRows()
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

            return rows;
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

        private void AddMaterialRow(string materialName, string vendor, string spec, string remark)
        {
            var rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Cells["Name"].Value = materialName;
            row.Cells["Category"].Value = GuessCategory(materialName);
            row.Cells["Vendor"].Value = vendor;
            row.Cells["Spec"].Value = spec;
            row.Cells["Unit"].Value = "张";
            row.Cells["IncludesFreight"].Value = "是";
            row.Cells["Remark"].Value = remark;
            row.DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 255);
        }

        private int ScanFromCurrentAnalysis()
        {
            if (_sourceGrid == null)
            {
                return 0;
            }

            var existing = BuildExistingMaterialSet();
            var added = 0;
            foreach (DataGridViewRow sourceRow in _sourceGrid.Rows)
            {
                if (sourceRow.IsNewRow)
                {
                    continue;
                }

                var materialText = ReadSourceCell(sourceRow, "BaseMaterialName");
                var specText = ReadSourceCell(sourceRow, "GramWeight");
                var vendor = ReadSourceCell(sourceRow, "MaterialVendor");
                foreach (var materialName in SplitMaterials(materialText))
                {
                    var trimmedName = materialName.Trim();
                    var key = Normalize(trimmedName);
                    if (string.IsNullOrWhiteSpace(key) || existing.Contains(key))
                    {
                        continue;
                    }

                    existing.Add(key);
                    AddMaterialRow(trimmedName, vendor, PickSpecForMaterial(trimmedName, specText), "从当前成本分析表扫描");
                    added++;
                }
            }

            return added;
        }

        private HashSet<string> BuildExistingMaterialSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in _grid.Rows)
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

        private void LoadMaterials()
        {
            _grid.Rows.Clear();
            foreach (var material in new MaterialRepository().GetAll())
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Name"].Value = material.Name;
                row.Cells["Aliases"].Value = material.Aliases;
                row.Cells["Category"].Value = material.Category;
                row.Cells["Vendor"].Value = material.Vendor;
                row.Cells["Spec"].Value = material.Spec;
                row.Cells["Unit"].Value = material.Unit;
                row.Cells["TaxUnitPrice"].Value = material.TaxUnitPrice.HasValue ? material.TaxUnitPrice.Value.ToString("0.####") : string.Empty;
                row.Cells["IncludesFreight"].Value = material.IncludesFreight ? "是" : string.Empty;
                row.Cells["Remark"].Value = material.Remark;
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            try
            {
                new MaterialRepository().SaveAll(ReadMaterials());
                MessageBox.Show(this, "材料库已保存。", "材料库", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private List<MaterialRecord> ReadMaterials()
        {
            var list = new List<MaterialRecord>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var name = ReadCell(row, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                decimal price;
                var hasPrice = decimal.TryParse(ReadCell(row, "TaxUnitPrice"), out price);
                list.Add(new MaterialRecord
                {
                    Name = name,
                    Aliases = ReadCell(row, "Aliases"),
                    Category = ReadCell(row, "Category"),
                    Vendor = ReadCell(row, "Vendor"),
                    Spec = ReadCell(row, "Spec"),
                    Unit = ReadCell(row, "Unit"),
                    TaxUnitPrice = hasPrice ? price : (decimal?)null,
                    IncludesFreight = ReadCell(row, "IncludesFreight") == "是" || ReadCell(row, "IncludesFreight") == "1",
                    Remark = ReadCell(row, "Remark")
                });
            }

            return list;
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

        private static string GuessCategory(string materialName)
        {
            if (ContainsAny(materialName, "灰板", "双灰", "纸板", "坑", "楞"))
            {
                return "纸板";
            }

            if (ContainsAny(materialName, "纸", "双铜", "双胶", "白卡", "黑卡", "牛卡"))
            {
                return "纸张";
            }

            if (ContainsAny(materialName, "PET", "PVC", "胶", "膜", "磁铁", "海绵", "EVA"))
            {
                return "辅料";
            }

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

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Replace("G", "g").Trim().ToLowerInvariant();
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
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private sealed class BatchScanStats
        {
            public BatchScanStats()
            {
                Materials = new List<BatchMaterialCandidate>();
            }

            public int FileCount { get; set; }
            public int SuccessCount { get; set; }
            public int AddedCount { get; set; }
            public int FailedCount { get; set; }
            public bool Cancelled { get; set; }
            public List<BatchMaterialCandidate> Materials { get; private set; }
        }

        private sealed class BatchMaterialCandidate
        {
            public BatchMaterialCandidate(string name, string vendor, string spec, string remark)
            {
                Name = name;
                Vendor = vendor;
                Spec = spec;
                Remark = remark;
            }

            public string Name { get; private set; }
            public string Vendor { get; private set; }
            public string Spec { get; private set; }
            public string Remark { get; private set; }
        }
    }
}
