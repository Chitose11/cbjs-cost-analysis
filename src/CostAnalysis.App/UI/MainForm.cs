using System;
using System.Collections.Generic;
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
        private ListBox _recentAnalysesListBox;

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
            RefreshRecentAnalysesList();
        }

        private Control BuildSidebar()
        {
            var sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _panel,
                Padding = new Padding(14)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                BackColor = _panel
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 292));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            sidebar.Controls.Add(layout);

            var title = new Label
            {
                Text = "成本分析",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(title, 0, 0);

            var menu = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0),
                AutoScroll = false
            };
            layout.Controls.Add(menu, 0, 1);

            AddMenuButton(menu, "成本分析列表", (_, __) => RefreshRecentAnalysesList());
            AddMenuButton(menu, "报价单导入", OnImportQuote);
            AddMenuButton(menu, "批量预扫描", OnBatchScanQuotes);
            AddMenuButton(menu, "材料库", OnOpenMaterials);
            AddMenuButton(menu, "工艺规则", OnOpenProcessRules);
            AddMenuButton(menu, "系统设置", OnOpenAiSettings);

            layout.Controls.Add(new Label
            {
                Text = "最近单据",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = _muted
            }, 0, 2);

            _recentAnalysesListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                DisplayMember = "DisplayText",
                IntegralHeight = false
            };
            _recentAnalysesListBox.DoubleClick += OnRecentAnalysisDoubleClick;
            layout.Controls.Add(_recentAnalysesListBox, 0, 3);

            var refresh = new Button
            {
                Text = "刷新列表",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                ForeColor = _blue,
                BackColor = Color.White,
                Margin = new Padding(0, 8, 0, 0)
            };
            refresh.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 215);
            refresh.Click += (_, __) => RefreshRecentAnalysesList();
            layout.Controls.Add(refresh, 0, 4);

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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
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
                WrapContents = true
            };
            layout.Controls.Add(toolbar, 0, 2);

            AddPrimaryButton(toolbar, "导入报价单", OnImportQuote);
            AddSecondaryButton(toolbar, "批量预扫描", OnBatchScanQuotes);
            AddSecondaryButton(toolbar, "新增明细", OnAddRow);
            AddSecondaryButton(toolbar, "删除明细", OnDeleteSelectedRows);
            AddSecondaryButton(toolbar, "编辑阶梯价", OnEditRowPriceTiers);
            AddSecondaryButton(toolbar, "应用工艺规则", OnApplyProcessRules);
            AddSecondaryButton(toolbar, "历史参考", OnOpenCostHistoryReference);
            AddSecondaryButton(toolbar, "AI补全成本", OnAiCompleteCosts);
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
                AllowUserToDeleteRows = false,
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
            grid.ColumnHeaderMouseClick += OnGridColumnHeaderMouseClick;
            grid.ContextMenuStrip = BuildGridContextMenu();
            return grid;
        }

        private void OnImportQuote(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择报价单";
                dialog.Filter = "报价单文件 (*.xls;*.xlsx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp)|*.xls;*.xlsx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp|Excel 报价单 (*.xls;*.xlsx)|*.xls;*.xlsx|PDF 文件 (*.pdf)|*.pdf|图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*";
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
            menu.Items.Add("编辑阶梯价", null, OnEditRowPriceTiers);
            menu.Items.Add("历史成本参考", null, OnOpenCostHistoryReference);
            menu.Items.Add("AI补全成本", null, OnAiCompleteCosts);
            menu.Items.Add("-");
            menu.Items.Add("删除明细", null, OnDeleteSelectedRows);
            return menu;
        }

        private void OnEditRowPriceTiers(object sender, EventArgs e)
        {
            var row = GetCurrentDataRow();
            if (row == null)
            {
                MessageBox.Show(this, "请先选择一条明细行。", "编辑阶梯价格", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var existingTiers = row.Tag as System.Collections.Generic.List<PriceTier>;
            using (var form = new PriceTiersForm(existingTiers))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                row.Tag = form.PriceTiers;
                RecalculateRows();
                row.Cells["PurchaseUnitPrice"].Style.BackColor = Color.FromArgb(232, 244, 255);
                row.Cells["PurchaseUnitPrice"].ToolTipText = "采购单价已按阶梯价格重新匹配";
                row.Cells["ValidationStatus"].Value = "阶梯价已更新";
                _statusLabel.Text = "已更新当前行阶梯价格，并按总用量重新匹配采购单价。";
            }
        }

        private void OnOpenCostHistoryReference(object sender, EventArgs e)
        {
            var row = GetCurrentDataRow();
            if (row == null)
            {
                MessageBox.Show(this, "请先选择一条明细行。", "历史成本参考", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var materialCode = Convert.ToString(row.Cells["MaterialCode"].Value);
            var materialName = Convert.ToString(row.Cells["MaterialName"].Value);
            var items = new CostAnalysisRepository().SearchCostHistory(materialCode, materialName, 80);
            if (items.Count == 0)
            {
                MessageBox.Show(this, "没有找到相同物料编码或相似物料名称的历史成本。", "历史成本参考", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new CostHistoryReferenceForm(materialCode, materialName, items))
            {
                if (form.ShowDialog(this) != DialogResult.OK || form.SelectedItem == null)
                {
                    return;
                }

                ApplyCostHistoryItem(row, form.SelectedItem);
                RecalculateRows();
                _statusLabel.Text = "已套用历史成本参考：" + form.SelectedItem.AnalysisNo + "。";
            }
        }

        private void ApplyCostHistoryItem(DataGridViewRow row, CostHistoryItem item)
        {
            SetCellIfEmpty(row, "MaterialCode", item.MaterialCode);
            SetCellIfEmpty(row, "MaterialName", item.MaterialName);
            SetCellIfEmpty(row, "Supplier", item.Supplier);
            SetCellIfEmpty(row, "BaseMaterialName", item.BaseMaterialName);
            SetCellIfEmpty(row, "MaterialVendor", item.MaterialVendor);
            SetCellIfEmpty(row, "MaterialUnitPrice", FormatDecimal(item.MaterialUnitPrice));
            SetCellIfEmpty(row, "GramWeight", item.GramWeight);
            SetCellIfEmpty(row, "ExpandedSize", item.ExpandedSize);

            SetCellValue(row, "MaterialCost", FormatDecimal(item.MaterialCost));
            SetCellValue(row, "PrintingCost", FormatDecimal(item.PrintingCost));
            SetCellValue(row, "PostProcessCost", FormatDecimal(item.PostProcessCost));
            SetCellValue(row, "OtherCost", FormatDecimal(item.OtherCost));
            SetCellValue(row, "PurchaseUnitPrice", FormatDecimal(item.PurchaseUnitPrice));

            if (item.PriceTiers != null && item.PriceTiers.Count > 0)
            {
                row.Tag = ClonePriceTiers(item.PriceTiers);
                row.Cells["PurchaseUnitPrice"].ToolTipText = "已套用历史阶梯价格";
            }

            row.Cells["ValidationStatus"].Value = "已套用历史成本";
        }

        private void SetCellIfEmpty(DataGridViewRow row, string columnName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value)))
            {
                SetCellValue(row, columnName, value);
            }
        }

        private static void SetCellValue(DataGridViewRow row, string columnName, string value)
        {
            row.Cells[columnName].Value = value ?? string.Empty;
            row.Cells[columnName].Style.BackColor = Color.FromArgb(232, 244, 255);
        }

        private static System.Collections.Generic.List<PriceTier> ClonePriceTiers(System.Collections.Generic.List<PriceTier> source)
        {
            var result = new System.Collections.Generic.List<PriceTier>();
            foreach (var tier in source)
            {
                result.Add(new PriceTier
                {
                    Label = tier.Label,
                    MinQuantity = tier.MinQuantity,
                    MaxQuantity = tier.MaxQuantity,
                    UnitPrice = tier.UnitPrice
                });
            }

            return result;
        }

        private void OnAiCompleteCosts(object sender, EventArgs e)
        {
            var rows = GetSelectedDataRows();
            if (rows.Count == 0)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (!row.IsNewRow && RowNeedsCostCompletion(row))
                    {
                        rows.Add(row);
                    }
                }
            }

            if (rows.Count == 0)
            {
                MessageBox.Show(this, "没有需要 AI 补全的明细行。可先勾选要补全的行，或保留费用空白后再试。", "AI补全成本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var settings = new AiSettingsRepository().Get();
            if (!settings.IsEnabled)
            {
                MessageBox.Show(this, "AI 功能尚未启用，请先在系统设置中启用并配置 DeepSeek。", "AI补全成本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (settings.ConfirmBeforeCall)
            {
                var confirm = MessageBox.Show(
                    this,
                    "将发送 " + rows.Count + " 行明细给 DeepSeek 生成成本建议。AI 只会填充空白费用或空白采购单价，已有人工金额不会被覆盖。是否继续？",
                    "AI补全成本",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                var inputs = BuildAiCostCompletionInputs(rows);
                var result = new DeepSeekClient().SuggestCosts(settings, inputs);
                var changed = ApplyAiCostCompletionSuggestions(rows, result);
                RecalculateRows();
                _statusLabel.Text = "AI 成本补全完成，更新费用单元格 " + changed + " 个。";
                MessageBox.Show(this, "AI 成本补全完成，更新费用单元格 " + changed + " 个。请继续人工确认。", "AI补全成本", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "AI补全成本失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private bool RowNeedsCostCompletion(DataGridViewRow row)
        {
            return string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["PrintingCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["PostProcessCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["OtherCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["PurchaseUnitPrice"].Value));
        }

        private List<AiCostCompletionInput> BuildAiCostCompletionInputs(List<DataGridViewRow> rows)
        {
            var inputs = new List<AiCostCompletionInput>();
            var processRepository = new ProcessCostRuleRepository();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var materialCode = ReadCellText(row, "MaterialCode");
                var materialName = ReadCellText(row, "MaterialName");
                inputs.Add(new AiCostCompletionInput
                {
                    Index = i + 1,
                    MaterialCode = materialCode,
                    MaterialName = materialName,
                    MaterialDescription = ReadCellText(row, "MaterialDescription"),
                    Supplier = ReadCellText(row, "Supplier"),
                    BaseMaterialName = ReadCellText(row, "BaseMaterialName"),
                    MaterialVendor = ReadCellText(row, "MaterialVendor"),
                    MaterialUnitPrice = ReadCellText(row, "MaterialUnitPrice"),
                    GramWeight = ReadCellText(row, "GramWeight"),
                    ExpandedSize = ReadCellText(row, "ExpandedSize"),
                    MaterialCost = ReadCellText(row, "MaterialCost"),
                    PrintingCost = ReadCellText(row, "PrintingCost"),
                    PostProcessCost = ReadCellText(row, "PostProcessCost"),
                    OtherCost = ReadCellText(row, "OtherCost"),
                    PurchaseUnitPrice = ReadCellText(row, "PurchaseUnitPrice"),
                    TotalQuantity = ReadCellText(row, "TotalQuantity"),
                    HistorySummary = BuildHistorySummary(materialCode, materialName),
                    ProcessRuleSummary = BuildProcessRuleSummary(processRepository, ReadCellText(row, "MaterialDescription"))
                });
            }

            return inputs;
        }

        private static string BuildHistorySummary(string materialCode, string materialName)
        {
            var history = new CostAnalysisRepository().SearchCostHistory(materialCode, materialName, 3);
            if (history.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var item in history)
            {
                parts.Add(string.Format(
                    "{0}: 材料费={1}, 印刷费={2}, 后工序费={3}, 其他={4}, 采购单价={5}",
                    item.AnalysisNo,
                    FormatDecimal(item.MaterialCost),
                    FormatDecimal(item.PrintingCost),
                    FormatDecimal(item.PostProcessCost),
                    FormatDecimal(item.OtherCost),
                    FormatDecimal(item.PurchaseUnitPrice)));
            }

            return string.Join("；", parts.ToArray());
        }

        private static string BuildProcessRuleSummary(ProcessCostRuleRepository repository, string description)
        {
            var matches = repository.FindMatches(description);
            if (matches.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var rule in matches)
            {
                parts.Add(rule.Keyword + " -> " + rule.CostType + "=" + FormatDecimal(rule.Amount));
            }

            return string.Join("；", parts.ToArray());
        }

        private int ApplyAiCostCompletionSuggestions(List<DataGridViewRow> rows, AiCostCompletionResult result)
        {
            var changed = 0;
            if (result == null || result.Items == null)
            {
                return changed;
            }

            foreach (var suggestion in result.Items)
            {
                if (!suggestion.Index.HasValue || suggestion.Index.Value <= 0 || suggestion.Index.Value > rows.Count)
                {
                    continue;
                }

                var row = rows[suggestion.Index.Value - 1];
                changed += ApplyAiSuggestedDecimal(row, "MaterialCost", suggestion.MaterialCost, suggestion);
                changed += ApplyAiSuggestedDecimal(row, "PrintingCost", suggestion.PrintingCost, suggestion);
                changed += ApplyAiSuggestedDecimal(row, "PostProcessCost", suggestion.PostProcessCost, suggestion);
                changed += ApplyAiSuggestedDecimal(row, "OtherCost", suggestion.OtherCost, suggestion);
                changed += ApplyAiSuggestedDecimal(row, "PurchaseUnitPrice", suggestion.PurchaseUnitPrice, suggestion);
                row.Cells["ValidationStatus"].Value = suggestion.RequiresReview ? "AI建议需确认" : "AI建议已填充";
            }

            return changed;
        }

        private static int ApplyAiSuggestedDecimal(DataGridViewRow row, string columnName, decimal? value, AiCostCompletionSuggestion suggestion)
        {
            if (!value.HasValue || !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value)))
            {
                return 0;
            }

            row.Cells[columnName].Value = value.Value.ToString("0.####");
            row.Cells[columnName].Style.BackColor = Color.FromArgb(232, 244, 255);
            row.Cells[columnName].ToolTipText = "AI成本建议；置信度=" + suggestion.Confidence.ToString("0.##") + "；" + (suggestion.Reason ?? string.Empty);
            return 1;
        }

        private static string ReadCellText(DataGridViewRow row, string columnName)
        {
            return row.Cells[columnName].Value == null ? string.Empty : Convert.ToString(row.Cells[columnName].Value);
        }

        private DataGridViewRow GetCurrentDataRow()
        {
            if (_grid.CurrentCell != null)
            {
                var row = _grid.Rows[_grid.CurrentCell.RowIndex];
                if (!row.IsNewRow)
                {
                    return row;
                }
            }

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var checkedValue = row.Cells["Selected"].Value;
                if (checkedValue is bool && (bool)checkedValue)
                {
                    return row;
                }
            }

            return null;
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
                UpdateSelectedRowsStatus();
                return;
            }

            RecalculateRows();
        }

        private void OnGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Selected")
            {
                return;
            }

            var shouldCheck = !AllDataRowsChecked();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.Cells["Selected"].Value = shouldCheck;
                ApplyCheckedRowStyle(row);
            }

            UpdateSelectedRowsStatus();
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

        private bool AllDataRowsChecked()
        {
            var hasRows = false;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                hasRows = true;
                if (!IsRowChecked(row))
                {
                    return false;
                }
            }

            return hasRows;
        }

        private int CountCheckedRows()
        {
            var count = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow && IsRowChecked(row))
                {
                    count++;
                }
            }

            return count;
        }

        private void UpdateSelectedRowsStatus()
        {
            var count = CountCheckedRows();
            _statusLabel.Text = count == 0 ? "未选择明细行。" : "已选择 " + count + " 行明细，可点击“删除明细”。";
        }

        private void OnOpenMaterials(object sender, EventArgs e)
        {
            using (var form = new MaterialsForm(_grid))
            {
                form.ShowDialog(this);
            }
        }

        private void OnOpenProcessRules(object sender, EventArgs e)
        {
            using (var form = new ProcessCostRulesForm(_grid))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _statusLabel.Text = "工艺费用规则已更新。";
                }
            }
        }

        private void OnApplyProcessRules(object sender, EventArgs e)
        {
            var rows = GetSelectedDataRows();
            if (rows.Count == 0)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        rows.Add(row);
                    }
                }
            }

            var changed = ApplyProcessRulesToRows(rows);
            RecalculateRows();
            _statusLabel.Text = "已应用工艺规则，更新费用单元格 " + changed + " 个。";
        }

        private int ApplyProcessRulesToRows(System.Collections.Generic.List<DataGridViewRow> rows)
        {
            var changed = 0;
            var repository = new ProcessCostRuleRepository();
            foreach (var row in rows)
            {
                var text = Convert.ToString(row.Cells["MaterialDescription"].Value);
                var matches = repository.FindMatches(text);
                foreach (var rule in matches)
                {
                    if (!rule.Amount.HasValue)
                    {
                        continue;
                    }

                    var columnName = NormalizeRuleCostColumn(rule.CostType);
                    if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value)))
                    {
                        row.Cells[columnName].Value = rule.Amount.Value.ToString("0.####");
                        row.Cells[columnName].Style.BackColor = Color.FromArgb(232, 244, 255);
                        row.Cells[columnName].ToolTipText = "由工艺规则匹配：" + rule.Keyword;
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static string NormalizeRuleCostColumn(string costType)
        {
            if (costType == "PrintingCost") return "PrintingCost";
            if (costType == "OtherCost") return "OtherCost";
            if (costType == "MaterialCost") return "MaterialCost";
            return "PostProcessCost";
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
                RefreshRecentAnalysesList();
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
                    OpenAnalysis(dialog.SelectedAnalysisId.Value, analysis);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshRecentAnalysesList()
        {
            if (_recentAnalysesListBox == null)
            {
                return;
            }

            try
            {
                _recentAnalysesListBox.BeginUpdate();
                _recentAnalysesListBox.Items.Clear();
                foreach (var item in new CostAnalysisRepository().GetRecentAnalyses())
                {
                    _recentAnalysesListBox.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "刷新成本分析列表失败：" + ex.Message;
            }
            finally
            {
                _recentAnalysesListBox.EndUpdate();
            }
        }

        private void OnRecentAnalysisDoubleClick(object sender, EventArgs e)
        {
            var selected = _recentAnalysesListBox.SelectedItem as CostAnalysisSummary;
            if (selected == null)
            {
                return;
            }

            try
            {
                var analysis = new CostAnalysisRepository().GetAnalysis(selected.Id);
                OpenAnalysis(selected.Id, analysis);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenAnalysis(int analysisId, SavedCostAnalysis analysis)
        {
            LoadHeader(analysis.Header);
            LoadSavedItems(analysis.Items);
            _statusLabel.Text = "已打开成本分析单，ID=" + analysisId + "，明细 " + analysis.Items.Count + " 条。";
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

            ApplyProcessRulesToRows(GetRowsByStartIndex(_grid.Rows.Count - items.Count));
            RecalculateRows();
        }

        private System.Collections.Generic.List<DataGridViewRow> GetRowsByStartIndex(int startIndex)
        {
            var rows = new System.Collections.Generic.List<DataGridViewRow>();
            for (var index = Math.Max(0, startIndex); index < _grid.Rows.Count; index++)
            {
                if (!_grid.Rows[index].IsNewRow)
                {
                    rows.Add(_grid.Rows[index]);
                }
            }

            return rows;
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
                if (item.PriceTiers != null && item.PriceTiers.Count > 0)
                {
                    row.Tag = item.PriceTiers;
                    row.Cells["PurchaseUnitPrice"].ToolTipText = "已恢复保存的阶梯价格";
                }
            }

            RecalculateRows();
        }

        private void ApplyMaterialMatch(DataGridViewRow row, string materialName)
        {
            var repository = new MaterialRepository();
            var matches = new List<MaterialRecord>();
            foreach (var name in SplitMaterialNames(materialName))
            {
                var match = repository.FindByNameOrAlias(name);
                if (match != null)
                {
                    matches.Add(match);
                }
            }

            if (matches.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialVendor"].Value)))
            {
                row.Cells["MaterialVendor"].Value = JoinUnique(matches, m => m.Vendor);
            }

            if (row.Cells["MaterialUnitPrice"].Value == null || string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialUnitPrice"].Value)))
            {
                var price = GetSingleMaterialPrice(matches);
                row.Cells["MaterialUnitPrice"].Value = price.HasValue ? price.Value.ToString("0.####") : string.Empty;
                if (!price.HasValue && matches.Count > 1)
                {
                    row.Cells["MaterialUnitPrice"].ToolTipText = "匹配到多个材料，需人工填写综合材料单价。";
                }
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["GramWeight"].Value)))
            {
                row.Cells["GramWeight"].Value = JoinUnique(matches, m => m.Spec);
            }
        }

        private static IEnumerable<string> SplitMaterialNames(string materialName)
        {
            return (materialName ?? string.Empty).Split(new[] { ';', '；', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string JoinUnique(List<MaterialRecord> matches, Func<MaterialRecord, string> selector)
        {
            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var match in matches)
            {
                var value = selector(match);
                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value.Trim()))
                {
                    values.Add(value.Trim());
                }
            }

            return string.Join("；", values.ToArray());
        }

        private static decimal? GetSingleMaterialPrice(List<MaterialRecord> matches)
        {
            decimal? price = null;
            foreach (var match in matches)
            {
                if (!match.TaxUnitPrice.HasValue)
                {
                    continue;
                }

                if (price.HasValue && price.Value != match.TaxUnitPrice.Value)
                {
                    return null;
                }

                price = match.TaxUnitPrice;
            }

            return price;
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
