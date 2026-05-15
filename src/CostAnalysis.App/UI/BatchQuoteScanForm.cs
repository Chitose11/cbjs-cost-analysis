using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class BatchQuoteScanForm : Form
    {
        private readonly TextBox _folderTextBox;
        private readonly Button _browseButton;
        private readonly Button _scanButton;
        private readonly Button _cancelScanButton;
        private readonly Button _importButton;
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;

        private volatile bool _cancelRequested;
        private Thread _scanThread;
        private int _successCount;
        private int _failureCount;

        public QuoteImportPreview SelectedPreview { get; private set; }

        public BatchQuoteScanForm(string initialFolder)
        {
            Text = "报价单批量预扫描";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1120, 680);
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

            _scanButton = new Button { Text = "开始扫描", Dock = DockStyle.Fill, Margin = new Padding(0, 4, 8, 4) };
            _scanButton.Click += OnStartScan;
            folderPanel.Controls.Add(_scanButton, 2, 0);

            _cancelScanButton = new Button { Text = "停止", Dock = DockStyle.Fill, Enabled = false, Margin = new Padding(0, 4, 0, 4) };
            _cancelScanButton.Click += (_, __) => _cancelRequested = true;
            folderPanel.Controls.Add(_cancelScanButton, 3, 0);

            _grid = BuildGrid();
            root.Controls.Add(_grid, 0, 1);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "请选择报价单文件夹后开始扫描。",
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_statusLabel, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(buttons, 0, 3);

            _importButton = new Button { Text = "导入选中", Width = 96, Height = 32 };
            _importButton.Click += OnImportSelected;
            buttons.Controls.Add(_importButton);

            var closeButton = new Button { Text = "关闭", Width = 82, Height = 32 };
            closeButton.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(closeButton);

            FormClosing += (_, __) => _cancelRequested = true;
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            AddColumn(grid, "Status", "状态");
            AddColumn(grid, "FileName", "文件名");
            AddColumn(grid, "TemplateType", "模板");
            AddColumn(grid, "Supplier", "供应商");
            AddColumn(grid, "SheetName", "Sheet");
            AddColumn(grid, "ItemCount", "物料数");
            AddColumn(grid, "FilePath", "完整路径");
            AddColumn(grid, "Error", "错误信息");
            grid.Columns["FilePath"].Visible = false;
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
                MessageBox.Show(this, "请选择有效的文件夹。", "批量预扫描", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var files = FindExcelFiles(folder);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "该文件夹下没有找到 .xls 或 .xlsx 报价单。", "批量预扫描", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _grid.Rows.Clear();
            _successCount = 0;
            _failureCount = 0;
            _cancelRequested = false;

            for (var i = 0; i < files.Count; i++)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Status"].Value = "等待";
                row.Cells["FileName"].Value = Path.GetFileName(files[i]);
                row.Cells["FilePath"].Value = files[i];
            }

            SetScanningState(true);
            _statusLabel.Text = "开始扫描，共 " + files.Count + " 个文件。";

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
                        throw new InvalidOperationException("未识别到可导入物料。");
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
            row.Cells["Status"].Value = "可导入";
            row.Cells["TemplateType"].Value = preview.TemplateType;
            row.Cells["Supplier"].Value = preview.Supplier;
            row.Cells["SheetName"].Value = preview.SheetName;
            row.Cells["ItemCount"].Value = preview.Items == null ? 0 : preview.Items.Count;
            row.Cells["Error"].Value = string.Empty;
            row.DefaultCellStyle.BackColor = Color.FromArgb(238, 247, 255);
        }

        private void ApplyScanFailure(int rowIndex, string message)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            var row = _grid.Rows[rowIndex];
            row.Cells["Status"].Value = "未识别";
            row.Cells["Error"].Value = message;
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 219);
        }

        private void OnImportSelected(object sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show(this, "请先选择一条可导入的扫描结果。", "导入选中", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = _grid.SelectedRows[0].Tag as BatchQuoteScanResult;
            if (result == null || result.Preview == null)
            {
                MessageBox.Show(this, "选中的文件还没有可导入结果。", "导入选中", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedPreview = result.Preview;
            DialogResult = DialogResult.OK;
        }

        private void SetScanningState(bool scanning)
        {
            _browseButton.Enabled = !scanning;
            _scanButton.Enabled = !scanning;
            _cancelScanButton.Enabled = scanning;
            _importButton.Enabled = !scanning;
            Cursor = scanning ? Cursors.WaitCursor : Cursors.Default;
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

        private static void AddColumn(DataGridView grid, string name, string header)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private sealed class BatchQuoteScanResult
        {
            public QuoteImportPreview Preview { get; set; }
        }
    }
}
