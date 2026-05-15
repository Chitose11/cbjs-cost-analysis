using System;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class MainForm : Form
    {
        private readonly Color _canvas = Color.FromArgb(245, 245, 247);
        private readonly Color _panel = Color.White;
        private readonly Color _ink = Color.FromArgb(29, 29, 31);
        private readonly Color _muted = Color.FromArgb(110, 110, 115);
        private readonly Color _blue = Color.FromArgb(0, 102, 204);
        private readonly Color _warningBack = Color.FromArgb(255, 249, 219);
        private readonly Color _missingBack = Color.FromArgb(255, 235, 235);
        private readonly Color _selectedBack = Color.FromArgb(230, 244, 255);

        private readonly DataGridView _grid;
        private readonly Label _statusLabel;
        private readonly TextBox _analysisNoTextBox;
        private readonly TextBox _customerTextBox;
        private readonly TextBox _projectTextBox;
        private readonly TextBox _dateTextBox;
        private readonly TextBox _taxTextBox;
        private readonly TextBox _freightTextBox;

        public MainForm()
        {
            Text = "成本分析软件";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            BackColor = _canvas;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = _canvas,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(BuildSidebar(), 0, 0);
            root.Controls.Add(BuildWorkspace(), 1, 0);

            _analysisNoTextBox = FindHeaderTextBox(root, "AnalysisNo");
            _customerTextBox = FindHeaderTextBox(root, "CustomerName");
            _projectTextBox = FindHeaderTextBox(root, "ProjectName");
            _dateTextBox = FindHeaderTextBox(root, "AnalysisDate");
            _taxTextBox = FindHeaderTextBox(root, "TaxNote");
            _freightTextBox = FindHeaderTextBox(root, "FreightNote");
            _dateTextBox.Text = DateTime.Now.ToString("yyyy-MM-dd");
            _taxTextBox.Text = "含税";
            _freightTextBox.Text = "含运";

            _grid = BuildGrid();
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "就绪。本原型已初始化本地 SQLite 数据库。",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var workspace = (Panel)root.GetControlFromPosition(1, 0);
            var layout = (TableLayoutPanel)workspace.Controls[0];
            layout.Controls.Add(_grid, 0, 3);
            layout.Controls.Add(_statusLabel, 0, 4);
        }

        private Control BuildSidebar()
        {
            var sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _panel,
                Padding = new Padding(14)
            };

            var title = new Label
            {
                Text = "成本分析",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 48
            };
            sidebar.Controls.Add(title);

            var menu = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };
            sidebar.Controls.Add(menu);
            menu.BringToFront();

            AddMenuButton(menu, "成本分析列表", null);
            AddMenuButton(menu, "报价单导入", OnImportQuote);
            AddMenuButton(menu, "批量预扫描", OnBatchScanQuotes);
            AddMenuButton(menu, "材料库", OnOpenMaterials);
            AddMenuButton(menu, "系统设置", OnOpenAiSettings);

            return sidebar;
        }

        private Panel BuildWorkspace()
        {
            var workspace = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _panel,
                Padding = new Padding(18)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = _panel
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            workspace.Controls.Add(layout);

            var heading = new Label
            {
                Text = "客户成本分析表",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(heading, 0, 0);

            layout.Controls.Add(BuildHeaderPanel(), 0, 1);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            layout.Controls.Add(toolbar, 0, 2);

            AddPrimaryButton(toolbar, "导入报价单", OnImportQuote);
            AddSecondaryButton(toolbar, "批量预扫描", OnBatchScanQuotes);
            AddSecondaryButton(toolbar, "新增明细", OnAddRow);
            AddSecondaryButton(toolbar, "删除明细", OnDeleteSelectedRows);
            AddSecondaryButton(toolbar, "保存", OnSaveAnalysis);
            AddSecondaryButton(toolbar, "打开", OnOpenAnalysis);
            AddSecondaryButton(toolbar, "导出 Excel", OnExportExcelPlaceholder);

            return workspace;
        }

        private Control BuildHeaderPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2,
                BackColor = Color.White
            };
            for (var i = 0; i < 6; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
            }
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            AddHeaderField(panel, 0, "分析单号", "AnalysisNo");
            AddHeaderField(panel, 1, "客户名称", "CustomerName");
            AddHeaderField(panel, 2, "项目名称", "ProjectName");
            AddHeaderField(panel, 3, "分析日期", "AnalysisDate");
            AddHeaderField(panel, 4, "税费说明", "TaxNote");
            AddHeaderField(panel, 5, "运费说明", "FreightNote");
            return panel;
        }

        private void AddHeaderField(TableLayoutPanel panel, int column, string labelText, string controlName)
        {
            panel.Controls.Add(new Label
            {
                Text = labelText,
                ForeColor = _muted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, column, 0);

            panel.Controls.Add(new TextBox
            {
                Name = controlName,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 10, 4)
            }, column, 1);
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(210, 210, 215),
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(230, 244, 255),
                    SelectionForeColor = Color.FromArgb(29, 29, 31)
                }
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = _ink;
            grid.EnableHeadersVisualStyles = false;

            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Selected",
                HeaderText = "选择",
                Width = 56,
                Frozen = true
            });
            AddColumn(grid, "No", "No");
            AddColumn(grid, "MaterialCode", "物料编码");
            AddColumn(grid, "MaterialName", "物料名称");
            AddColumn(grid, "MaterialDescription", "物料描述");
            AddColumn(grid, "Supplier", "供应商");
            AddColumn(grid, "BaseMaterialName", "材料名称");
            AddColumn(grid, "MaterialVendor", "材料厂家");
            AddColumn(grid, "MaterialUnitPrice", "材料单价");
            AddColumn(grid, "GramWeight", "原材料克重");
            AddColumn(grid, "ExpandedSize", "展开尺寸");
            AddColumn(grid, "MaterialCost", "材料费");
            AddColumn(grid, "PrintingCost", "印刷费");
            AddColumn(grid, "PostProcessCost", "后工序费");
            AddColumn(grid, "OtherCost", "其他");
            AddColumn(grid, "PurchaseUnitPrice", "采购单价");
            AddColumn(grid, "TotalQuantity", "总用量");
            AddColumn(grid, "TotalPrice", "总价");
            AddColumn(grid, "ValidationStatus", "状态");

            grid.CellEndEdit += (_, __) => RecalculateRows();
            grid.RowsAdded += (_, __) => ApplyCheckedRowStyles();
            grid.CurrentCellDirtyStateChanged += OnGridCurrentCellDirtyStateChanged;
            grid.CellValueChanged += OnGridCellValueChanged;
            grid.CellMouseDown += OnGridCellMouseDown;
            grid.ContextMenuStrip = BuildGridContextMenu();
            return grid;
        }

        private void OnImportQuote(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择报价单";
                dialog.Filter = "Excel 报价单 (*.xls;*.xlsx)|*.xls;*.xlsx|所有文件 (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var service = new ExcelQuoteImportService();
                QuoteImportPreview preview;
                try
                {
                    preview = service.Import(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ShowPreviewAndAppend(preview);
            }
        }

        private void OnBatchScanQuotes(object sender, EventArgs e)
        {
            var defaultFolder = DirectoryExists(@"D:\1\LB报价单") ? @"D:\1\LB报价单" : string.Empty;
            using (var form = new BatchQuoteScanForm(defaultFolder))
            {
                if (form.ShowDialog(this) != DialogResult.OK || form.SelectedPreview == null)
                {
                    _statusLabel.Text = "已关闭批量预扫描。";
                    return;
                }

                ShowPreviewAndAppend(form.SelectedPreview);
            }
        }

        private void ShowPreviewAndAppend(QuoteImportPreview preview)
        {
            using (var previewForm = new QuoteImportPreviewForm(preview))
            {
                if (previewForm.ShowDialog(this) != DialogResult.OK)
                {
                    _statusLabel.Text = "已取消导入。";
                    return;
                }

                AppendPreviewRows(preview, previewForm.SelectedItems);
                _statusLabel.Text = string.Format(
                    "已加入报价单物料：{0} 条。供应商：{1}，Sheet={2}，模板={3}。请填写总用量以匹配阶梯单价。",
                    previewForm.SelectedItems.Count,
                    preview.Supplier,
                    preview.SheetName,
                    preview.TemplateType);
            }
        }

        private void OnAddRow(object sender, EventArgs e)
        {
            var rowIndex = _grid.Rows.Add();
            _grid.Rows[rowIndex].Cells["Selected"].Value = false;
            _grid.Rows[rowIndex].Cells["No"].Value = rowIndex + 1;
            _statusLabel.Text = "已新增一行明细。";
        }

        private void OnDeleteSelectedRows(object sender, EventArgs e)
        {
            var rows = GetSelectedDataRows();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "请先选择要删除的明细行。", "删除明细", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "确定删除选中的 " + rows.Count + " 行明细吗？",
                "删除明细",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            rows.Sort((a, b) => b.Index.CompareTo(a.Index));
            foreach (var row in rows)
            {
                _grid.Rows.Remove(row);
            }

            RenumberRows();
            RecalculateRows();
            _statusLabel.Text = "已删除 " + rows.Count + " 行明细。";
        }

        private System.Collections.Generic.List<DataGridViewRow> GetSelectedDataRows()
        {
            var rows = new System.Collections.Generic.List<DataGridViewRow>();
            var seen = new System.Collections.Generic.HashSet<int>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var checkedValue = row.Cells["Selected"].Value;
                if (checkedValue is bool && (bool)checkedValue && seen.Add(row.Index))
                {
                    rows.Add(row);
                }
            }

            if (rows.Count > 0)
            {
                return rows;
            }

            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (!row.IsNewRow && seen.Add(row.Index))
                {
                    rows.Add(row);
                }
            }

            foreach (DataGridViewCell cell in _grid.SelectedCells)
            {
                if (cell.OwningColumn != null && cell.OwningColumn.Name == "Selected")
                {
                    continue;
                }

                var row = _grid.Rows[cell.RowIndex];
                if (!row.IsNewRow && seen.Add(row.Index))
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private void RenumberRows()
        {
            var no = 1;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.Cells["No"].Value = no++;
            }
        }

        private ContextMenuStrip BuildGridContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("删除明细", null, OnDeleteSelectedRows);
            return menu;
        }

        private void OnGridCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
            {
                return;
            }

            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
            if (e.ColumnIndex >= 0)
            {
                _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            }
        }

        private void OnGridCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell != null && _grid.CurrentCell.OwningColumn.Name == "Selected")
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void OnGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName == "Selected")
            {
                ApplyCheckedRowStyle(_grid.Rows[e.RowIndex]);
                return;
            }

            RecalculateRows();
        }

        private void ApplyCheckedRowStyles()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow)
                {
                    ApplyCheckedRowStyle(row);
                }
            }
        }

        private void ApplyCheckedRowStyle(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow)
            {
                return;
            }

            row.DefaultCellStyle.BackColor = IsRowChecked(row) ? _selectedBack : Color.White;
        }

        private static bool IsRowChecked(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow)
            {
                return false;
            }

            var value = row.Cells["Selected"].Value;
            if (value is bool)
            {
                return (bool)value;
            }

            return string.Equals(Convert.ToString(value), "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Convert.ToString(value), "1", StringComparison.OrdinalIgnoreCase);
        }

        private void OnOpenMaterials(object sender, EventArgs e)
        {
            using (var form = new MaterialsForm())
            {
                form.ShowDialog(this);
            }
        }

        private void OnOpenAiSettings(object sender, EventArgs e)
        {
            using (var form = new AiSettingsForm())
            {
                form.ShowDialog(this);
            }
        }

        private void OnExportExcelPlaceholder(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "导出客户成本分析表";
                dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                dialog.FileName = "成本分析.xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    ValidateGrid();
                    new ExcelExportService().ExportGrid(dialog.FileName, ReadHeader(), _grid);
                    _statusLabel.Text = "已导出 Excel：" + dialog.FileName;
                    MessageBox.Show(this, "导出完成。", "导出 Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OnSaveAnalysis(object sender, EventArgs e)
        {
            RecalculateRows();
            ValidateGrid();
            try
            {
                var id = new CostAnalysisRepository().SaveFromGrid(ReadHeader(), _grid);
                _statusLabel.Text = "已保存成本分析单，ID=" + id;
                MessageBox.Show(this, "保存完成。", "保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnOpenAnalysis(object sender, EventArgs e)
        {
            try
            {
                var repository = new CostAnalysisRepository();
                var analyses = repository.GetRecentAnalyses();
                if (analyses.Count == 0)
                {
                    MessageBox.Show(this, "还没有保存过成本分析单。", "打开", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dialog = new OpenAnalysisForm(analyses))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.SelectedAnalysisId.HasValue)
                    {
                        return;
                    }

                    var analysis = repository.GetAnalysis(dialog.SelectedAnalysisId.Value);
                    LoadHeader(analysis.Header);
                    LoadSavedItems(analysis.Items);
                    _statusLabel.Text = "已打开成本分析单，ID=" + dialog.SelectedAnalysisId.Value + "，明细 " + analysis.Items.Count + " 条。";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RecalculateRows()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var quantity = ReadDecimal(row.Cells["TotalQuantity"].Value);
                var unitPrice = ReadDecimal(row.Cells["PurchaseUnitPrice"].Value);
                var tiers = row.Tag as System.Collections.Generic.List<PriceTier>;
                if (quantity.HasValue && tiers != null)
                {
                    var matched = ExcelQuoteImportService.MatchTier(tiers, quantity.Value);
                    if (matched != null && matched.UnitPrice.HasValue)
                    {
                        unitPrice = matched.UnitPrice;
                        row.Cells["PurchaseUnitPrice"].Value = unitPrice.Value.ToString("0.####");
                    }
                }

                if (unitPrice.HasValue && quantity.HasValue)
                {
                    row.Cells["TotalPrice"].Value = (unitPrice.Value * quantity.Value).ToString("0.####");
                }
            }

            ValidateGrid();
        }

        private int ValidateGrid()
        {
            var warningCount = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                ResetRowStyle(row);
                var messages = new System.Collections.Generic.List<string>();

                RequireCell(row, "MaterialCode", "缺物料编码", messages);
                RequireCell(row, "MaterialName", "缺物料名称", messages);
                RequireCell(row, "PurchaseUnitPrice", "缺采购单价", messages);
                RequireCell(row, "TotalQuantity", "缺总用量", messages);

                var materialCost = ReadDecimal(row.Cells["MaterialCost"].Value) ?? 0;
                var printingCost = ReadDecimal(row.Cells["PrintingCost"].Value) ?? 0;
                var postProcessCost = ReadDecimal(row.Cells["PostProcessCost"].Value) ?? 0;
                var otherCost = ReadDecimal(row.Cells["OtherCost"].Value) ?? 0;
                var unitPrice = ReadDecimal(row.Cells["PurchaseUnitPrice"].Value);
                var hasAnyCost = materialCost != 0 || printingCost != 0 || postProcessCost != 0 || otherCost != 0;

                if (unitPrice.HasValue && hasAnyCost)
                {
                    var costTotal = materialCost + printingCost + postProcessCost + otherCost;
                    if (Math.Abs(costTotal - unitPrice.Value) > 0.0001M)
                    {
                        if (!IsRowChecked(row))
                        {
                            row.DefaultCellStyle.BackColor = _warningBack;
                        }
                        messages.Add("成本合计不等于采购单价");
                    }
                }

                var totalQuantity = ReadDecimal(row.Cells["TotalQuantity"].Value);
                var totalPrice = ReadDecimal(row.Cells["TotalPrice"].Value);
                if (unitPrice.HasValue && totalQuantity.HasValue && totalPrice.HasValue)
                {
                    var expectedTotal = unitPrice.Value * totalQuantity.Value;
                    if (Math.Abs(expectedTotal - totalPrice.Value) > 0.0001M)
                    {
                        if (!IsRowChecked(row))
                        {
                            row.DefaultCellStyle.BackColor = _warningBack;
                        }
                        messages.Add("总价计算不一致");
                    }
                }

                if (messages.Count > 0)
                {
                    warningCount++;
                    row.Cells["ValidationStatus"].Value = string.Join("；", messages.ToArray());
                }
                else
                {
                    row.Cells["ValidationStatus"].Value = "已完成";
                }
            }

            if (warningCount > 0)
            {
                _statusLabel.Text = "校验完成：发现 " + warningCount + " 行需要确认。";
            }

            return warningCount;
        }

        private void RequireCell(DataGridViewRow row, string columnName, string message, System.Collections.Generic.List<string> messages)
        {
            var cell = row.Cells[columnName];
            if (cell.Value == null || string.IsNullOrWhiteSpace(Convert.ToString(cell.Value)))
            {
                cell.Style.BackColor = _missingBack;
                messages.Add(message);
            }
        }

        private void ResetRowStyle(DataGridViewRow row)
        {
            row.DefaultCellStyle.BackColor = IsRowChecked(row) ? _selectedBack : Color.White;
            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.Style.BackColor = Color.Empty;
            }
        }

        private void AppendPreviewRows(QuoteImportPreview preview, System.Collections.Generic.List<QuoteImportItem> items)
        {
            if (string.IsNullOrWhiteSpace(_analysisNoTextBox.Text))
            {
                _analysisNoTextBox.Text = "CA-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            }

            if (string.IsNullOrWhiteSpace(_customerTextBox.Text))
            {
                _customerTextBox.Text = "客户";
            }

            foreach (var item in items)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Tag = item.PriceTiers;
                row.Cells["Selected"].Value = false;
                row.Cells["No"].Value = rowIndex + 1;
                row.Cells["MaterialCode"].Value = item.MaterialCode;
                row.Cells["MaterialName"].Value = item.MaterialName;
                row.Cells["MaterialDescription"].Value = BuildDescription(item);
                row.Cells["Supplier"].Value = preview.Supplier;
                row.Cells["BaseMaterialName"].Value = item.MaterialNameExtracted;
                row.Cells["GramWeight"].Value = item.GramWeight;
                row.Cells["ExpandedSize"].Value = item.FinishedSize;
                ApplyMaterialMatch(row, item.MaterialNameExtracted);

                if (item.UsageQuantity.HasValue)
                {
                    row.Cells["TotalQuantity"].Value = item.UsageQuantity.Value.ToString("0.####");
                }

                if (item.PriceTiers != null && item.PriceTiers.Count == 1 && item.PriceTiers[0].UnitPrice.HasValue)
                {
                    row.Cells["PurchaseUnitPrice"].Value = item.PriceTiers[0].UnitPrice.Value.ToString("0.####");
                }
            }

            RecalculateRows();
        }

        private CostAnalysisHeader ReadHeader()
        {
            return new CostAnalysisHeader
            {
                AnalysisNo = _analysisNoTextBox.Text,
                CustomerName = _customerTextBox.Text,
                ProjectName = _projectTextBox.Text,
                AnalysisDate = _dateTextBox.Text,
                TaxNote = _taxTextBox.Text,
                FreightNote = _freightTextBox.Text
            };
        }

        private void LoadHeader(CostAnalysisHeader header)
        {
            _analysisNoTextBox.Text = header.AnalysisNo;
            _customerTextBox.Text = header.CustomerName;
            _projectTextBox.Text = header.ProjectName;
            _dateTextBox.Text = header.AnalysisDate;
            _taxTextBox.Text = header.TaxNote;
            _freightTextBox.Text = header.FreightNote;
        }

        private void LoadSavedItems(System.Collections.Generic.List<SavedCostAnalysisItem> items)
        {
            _grid.Rows.Clear();
            foreach (var item in items)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Selected"].Value = false;
                row.Cells["No"].Value = item.No;
                row.Cells["MaterialCode"].Value = item.MaterialCode;
                row.Cells["MaterialName"].Value = item.MaterialName;
                row.Cells["MaterialDescription"].Value = item.MaterialDescription;
                row.Cells["Supplier"].Value = item.Supplier;
                row.Cells["BaseMaterialName"].Value = item.BaseMaterialName;
                row.Cells["MaterialVendor"].Value = item.MaterialVendor;
                row.Cells["MaterialUnitPrice"].Value = FormatDecimal(item.MaterialUnitPrice);
                row.Cells["GramWeight"].Value = item.GramWeight;
                row.Cells["ExpandedSize"].Value = item.ExpandedSize;
                row.Cells["MaterialCost"].Value = FormatDecimal(item.MaterialCost);
                row.Cells["PrintingCost"].Value = FormatDecimal(item.PrintingCost);
                row.Cells["PostProcessCost"].Value = FormatDecimal(item.PostProcessCost);
                row.Cells["OtherCost"].Value = FormatDecimal(item.OtherCost);
                row.Cells["PurchaseUnitPrice"].Value = FormatDecimal(item.PurchaseUnitPrice);
                row.Cells["TotalQuantity"].Value = FormatDecimal(item.TotalQuantity);
                row.Cells["TotalPrice"].Value = FormatDecimal(item.TotalPrice);
            }

            ValidateGrid();
        }

        private void ApplyMaterialMatch(DataGridViewRow row, string materialName)
        {
            var match = new MaterialRepository().FindByNameOrAlias(materialName);
            if (match == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialVendor"].Value)))
            {
                row.Cells["MaterialVendor"].Value = match.Vendor;
            }

            if (row.Cells["MaterialUnitPrice"].Value == null || string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialUnitPrice"].Value)))
            {
                row.Cells["MaterialUnitPrice"].Value = match.TaxUnitPrice.HasValue ? match.TaxUnitPrice.Value.ToString("0.####") : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["GramWeight"].Value)) && !string.IsNullOrWhiteSpace(match.Spec))
            {
                row.Cells["GramWeight"].Value = match.Spec;
            }
        }

        private static string BuildDescription(QuoteImportItem item)
        {
            var size = string.IsNullOrWhiteSpace(item.FinishedSize) ? string.Empty : "尺寸：" + item.FinishedSize;
            var process = string.IsNullOrWhiteSpace(item.MaterialProcess) ? string.Empty : "材质/工艺：" + item.MaterialProcess;
            if (string.IsNullOrWhiteSpace(size))
            {
                return process;
            }

            if (string.IsNullOrWhiteSpace(process))
            {
                return size;
            }

            return size + "，" + process;
        }

        private static decimal? ReadDecimal(object value)
        {
            if (value == null)
            {
                return null;
            }

            decimal number;
            return decimal.TryParse(value.ToString(), out number) ? number : (decimal?)null;
        }

        private static string FormatDecimal(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("0.####") : string.Empty;
        }

        private void AddMenuButton(FlowLayoutPanel panel, string text, EventHandler handler)
        {
            var button = new Button
            {
                Text = text,
                Width = 150,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                ForeColor = _ink,
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 8)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(226, 226, 230);
            if (handler != null)
            {
                button.Click += handler;
            }
            panel.Controls.Add(button);
        }

        private void AddPrimaryButton(FlowLayoutPanel panel, string text, EventHandler handler)
        {
            var button = CreateToolbarButton(text, handler);
            button.BackColor = _blue;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = _blue;
            panel.Controls.Add(button);
        }

        private void AddSecondaryButton(FlowLayoutPanel panel, string text, EventHandler handler)
        {
            var button = CreateToolbarButton(text, handler);
            button.BackColor = Color.White;
            button.ForeColor = _blue;
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 215);
            panel.Controls.Add(button);
        }

        private Button CreateToolbarButton(string text, EventHandler handler)
        {
            var button = new Button
            {
                Text = text,
                Width = 110,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 4, 10, 4)
            };
            button.Click += handler;
            return button;
        }

        private static void AddColumn(DataGridView grid, string name, string headerText)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = headerText,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private static TextBox FindHeaderTextBox(Control root, string name)
        {
            var matches = root.Controls.Find(name, true);
            if (matches.Length == 0)
            {
                throw new InvalidOperationException("找不到输入框：" + name);
            }

            return (TextBox)matches[0];
        }

        private static bool DirectoryExists(string path)
        {
            return System.IO.Directory.Exists(path);
        }
    }
}
