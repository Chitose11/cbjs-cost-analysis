using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class QuoteImportPreviewForm : MetroForm
    {
        private readonly QuoteImportPreview _preview;
        private readonly MetroGrid _grid;
        private readonly MetroGrid _rawGrid;
        private readonly MetroLabel _summaryLabel;
        private readonly MetroButton _editPriceTiersButton;
        private readonly MetroButton _aiAssistButton;
        private readonly MetroButton _aiPreviewButton;
        private readonly MetroButton _undoAiButton;
        private readonly MetroButton _aiResultButton;
        private readonly MetroButton _aiDetailsButton;
        private readonly MetroButton _pasteTextButton;
        private readonly MetroButton _warningSummaryButton;
        private readonly ContextMenuStrip _aiDetailsMenu;
        private readonly PriceWarningService _priceWarningService;
        private string _lastAiRawContent;
        private PreviewSnapshot _lastPreviewSnapshot;
        private static readonly string[] AiWritableColumns =
        {
            "MaterialCode",
            "MaterialName",
            "FinishedSize",
            "MaterialProcess",
            "MaterialNameExtracted",
            "GramWeight"
        };

        public List<QuoteImportItem> SelectedItems { get; private set; }

        public QuoteImportPreviewForm(QuoteImportPreview preview)
        {
            _preview = preview;
            SelectedItems = new List<QuoteImportItem>();
            _priceWarningService = new PriceWarningService();

            Text = "报价单导入确认";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 700);
            Style = MetroColorStyle.Blue;
            Theme = MetroThemeStyle.Light;
            ShadowType = MetroFormShadowType.DropShadow;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(root);

            _summaryLabel = BuildSummaryLabel();
            root.Controls.Add(_summaryLabel, 0, 0);

            _grid = BuildGrid();
            _rawGrid = BuildRawGrid();
            _editPriceTiersButton = CreateMetroActionButton("阶梯价", false, 86);
            _editPriceTiersButton.Click += OnEditPriceTiers;
            root.Controls.Add(BuildPreviewLayout(), 0, 1);

            _aiAssistButton = CreateMetroActionButton("AI识别", false, 92);
            _aiAssistButton.Click += OnAiAssist;

            _pasteTextButton = CreateMetroActionButton("从文本识别", false, 108);
            _pasteTextButton.Click += OnPasteRawText;

            _warningSummaryButton = CreateMetroActionButton("价格预警", false, 96);
            _warningSummaryButton.Click += OnShowWarningSummary;

            _aiPreviewButton = CreateMetroActionButton("查看AI请求", false, 118);
            _aiPreviewButton.Click += OnAiRequestPreview;

            _aiResultButton = CreateMetroActionButton("AI结果", false, 92);
            _aiResultButton.Enabled = false;
            _aiResultButton.Click += OnViewAiResult;

            _undoAiButton = CreateMetroActionButton("撤销AI", false, 92);
            _undoAiButton.Enabled = false;
            _undoAiButton.Click += OnUndoAiChanges;

            _aiDetailsMenu = BuildAiDetailsMenu();
            _aiDetailsButton = CreateMetroActionButton("AI详情", false, 88);
            _aiDetailsButton.Click += (_, __) =>
            {
                UpdateAiDetailsMenuState();
                _aiDetailsMenu.Show(_aiDetailsButton, new Point(0, _aiDetailsButton.Height));
            };

            root.Controls.Add(BuildFooterActions(), 0, 2);
            LoadItems();
            UpdateImportSelectionSummary();
            LoadRawPreview();
        }

        private static void AddActionButton(TableLayoutPanel panel, MetroButton button)
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(6, 6, 0, 6);
            var index = panel.Controls.Count;
            panel.Controls.Add(button, index, 0);
        }

        private static MetroButton CreateMetroActionButton(string text, bool primary, int minWidth)
        {
            return new MetroButton
            {
                Text = text,
                Style = primary ? MetroColorStyle.Blue : MetroColorStyle.Silver,
                Theme = MetroThemeStyle.Light,
                UseSelectable = true,
                Highlight = primary,
                DisplayFocus = true,
                FontSize = MetroButtonSize.Medium,
                MinimumSize = new Size(minWidth, 34)
            };
        }

        private Control BuildFooterActions()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 6, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 268));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var secondary = new TableLayoutPanel
            {
                Dock = DockStyle.Left,
                ColumnCount = 4,
                RowCount = 1,
                Width = 458,
                Margin = new Padding(0)
            };
            for (var i = 0; i < 4; i++)
            {
                secondary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            }
            secondary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddActionButton(secondary, _aiAssistButton);
            AddActionButton(secondary, _pasteTextButton);
            AddActionButton(secondary, _warningSummaryButton);
            AddActionButton(secondary, _aiDetailsButton);

            var primary = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            primary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var cancel = CreateMetroActionButton("取消导入", false, 96);
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            AddActionButton(primary, cancel);

            var confirm = CreateMetroActionButton("加入成本表", true, 112);
            confirm.Click += (_, __) => Confirm();
            AddActionButton(primary, confirm);

            footer.Controls.Add(secondary, 0, 0);
            footer.Controls.Add(primary, 1, 0);
            return footer;
        }

        private ContextMenuStrip BuildAiDetailsMenu()
        {
            var menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            menu.Items.Add("查看AI请求", null, OnAiRequestPreview);
            menu.Items.Add("AI结果", null, OnViewAiResult);
            menu.Items.Add("撤销AI", null, OnUndoAiChanges);
            return menu;
        }

        private void UpdateAiDetailsMenuState()
        {
            if (_aiDetailsMenu.Items.Count < 3)
            {
                return;
            }

            _aiDetailsMenu.Items[1].Enabled = !string.IsNullOrWhiteSpace(_lastAiRawContent);
            _aiDetailsMenu.Items[2].Enabled = HasAiChanges();
        }

        private MetroLabel BuildSummaryLabel()
        {
            return new MetroLabel
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(29, 29, 31),
                Text = BuildSummaryText()
            };
        }

        private string BuildSummaryText()
        {
            return string.Format(
                "供应商：{0}\r\n识别结果：Sheet={1}    模板={2}    表头行={3}    数量行={4}    数据起始行={5}    物料={6} 条",
                ShortText(_preview.Supplier, 34),
                ShortText(_preview.SheetName, 18),
                ShortText(_preview.TemplateType, 42),
                _preview.HeaderRow,
                _preview.QuantityRow,
                _preview.DataStartRow,
                _preview.Items == null ? 0 : _preview.Items.Count);
        }

        private static string ShortText(string value, int maxLength)
        {
            var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return text.Length <= maxLength ? text : text.Substring(0, Math.Max(0, maxLength - 1)) + "…";
        }

        private MetroGrid BuildGrid()
        {
            var grid = new MetroGrid
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
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
            grid.Style = MetroColorStyle.Blue;
            grid.Theme = MetroThemeStyle.Light;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(29, 29, 31);
            grid.CellValidating += OnGridCellValidating;
            grid.CellDoubleClick += OnGridCellDoubleClick;
            grid.CurrentCellDirtyStateChanged += OnGridCurrentCellDirtyStateChanged;
            grid.CellValueChanged += OnGridCellValueChanged;
            grid.ColumnHeaderMouseClick += OnGridColumnHeaderMouseClick;

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "加入", Width = 48 });
            AddColumn(grid, "MaterialCode", "物料编码");
            AddColumn(grid, "MaterialName", "物料名称");
            AddColumn(grid, "FinishedSize", "成品尺寸");
            AddColumn(grid, "MaterialProcess", "材质/工艺");
            AddColumn(grid, "MaterialNameExtracted", "材料名称");
            AddColumn(grid, "GramWeight", "克重");
            AddColumn(grid, "UsageQuantity", "用量");
            AddColumn(grid, "PriceTiers", "阶梯价格", true);
            AddColumn(grid, "PriceWarning", "价格预警", true);
            AddColumn(grid, "AiStatus", "AI状态", true);
            ConfigureImportGridColumns(grid);
            return grid;
        }

        private Control BuildPreviewLayout()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(245, 245, 247)
            };
            split.SizeChanged += (_, __) => SetPreviewSplitterDistance(split);

            split.Panel1.Controls.Add(BuildSectionPanel("原始文件预览", _rawGrid));
            split.Panel2.Controls.Add(BuildSectionPanel("识别结果（可直接修正字段）", _grid, _editPriceTiersButton));
            return split;
        }

        private static void SetPreviewSplitterDistance(SplitContainer split)
        {
            const int topMinSize = 140;
            const int bottomMinSize = 180;
            var maxDistance = split.Height - bottomMinSize;
            if (maxDistance < topMinSize)
            {
                return;
            }

            var distance = Math.Min(230, maxDistance);
            if (distance >= topMinSize && split.SplitterDistance != distance)
            {
                split.SplitterDistance = distance;
            }
        }

        private static Control BuildSectionPanel(string title, Control content, Control action = null)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, action == null ? 0 : 98));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var label = new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = title,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0),
                ForeColor = Color.FromArgb(80, 80, 85),
                BackColor = Color.White
            };
            header.Controls.Add(label, 0, 0);
            if (action != null)
            {
                action.Dock = DockStyle.Fill;
                action.Margin = new Padding(6, 0, 0, 4);
                header.Controls.Add(action, 1, 0);
            }
            panel.Controls.Add(header, 0, 0);

            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content, 0, 1);
            return panel;
        }

        private MetroGrid BuildRawGrid()
        {
            var grid = new MetroGrid
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
                ReadOnly = true,
                RowHeadersVisible = true,
                RowHeadersWidth = 54,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                Style = MetroColorStyle.Blue,
                Theme = MetroThemeStyle.Light
            };
            return grid;
        }

        private void LoadItems()
        {
            _grid.Rows.Clear();
            foreach (var item in _preview.Items)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Tag = item;
                row.Cells["Selected"].Value = true;
                FillRow(row, item);
                ApplyImportRowStyle(row);
            }
        }

        private void LoadRawPreview()
        {
            _rawGrid.Rows.Clear();
            _rawGrid.Columns.Clear();

            if (_preview.RawSheet == null || _preview.RawSheet.Cells == null)
            {
                return;
            }

            var rows = GetVisibleRowCount(_preview.RawSheet);
            var columns = GetVisibleColumnCount(_preview.RawSheet);
            for (var column = 1; column <= columns; column++)
            {
                _rawGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Column" + column,
                    HeaderText = ToExcelColumnName(column),
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Width = 110
                });
            }

            for (var row = 1; row <= rows; row++)
            {
                var rowIndex = _rawGrid.Rows.Add();
                var gridRow = _rawGrid.Rows[rowIndex];
                gridRow.HeaderCell.Value = row.ToString();

                for (var column = 1; column <= columns; column++)
                {
                    gridRow.Cells[column - 1].Value = GetRawCell(_preview.RawSheet, row, column);
                }

                ApplyRawRowStyle(gridRow, row);
            }
        }

        private void ApplyRawRowStyle(DataGridViewRow row, int sourceRow)
        {
            if (sourceRow == _preview.HeaderRow)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(230, 244, 255);
                row.DefaultCellStyle.Font = new Font(_rawGrid.Font, FontStyle.Bold);
                row.HeaderCell.ToolTipText = "表头行";
            }
            else if (sourceRow == _preview.QuantityRow && _preview.QuantityRow != _preview.HeaderRow)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 247, 230);
                row.HeaderCell.ToolTipText = "数量行";
            }
            else if (sourceRow >= _preview.DataStartRow &&
                     sourceRow < _preview.DataStartRow + (_preview.Items == null ? 0 : _preview.Items.Count))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(246, 255, 237);
                row.HeaderCell.ToolTipText = "识别到的物料行";
            }
        }

        private void FillRow(DataGridViewRow row, QuoteImportItem item)
        {
            row.Cells["MaterialCode"].Value = item.MaterialCode;
            row.Cells["MaterialName"].Value = item.MaterialName;
            row.Cells["FinishedSize"].Value = item.FinishedSize;
            row.Cells["MaterialProcess"].Value = item.MaterialProcess;
            row.Cells["MaterialNameExtracted"].Value = item.MaterialNameExtracted;
            row.Cells["GramWeight"].Value = item.GramWeight;
            row.Cells["UsageQuantity"].Value = item.UsageQuantity.HasValue ? item.UsageQuantity.Value.ToString("0.####") : string.Empty;
            row.Cells["PriceTiers"].Value = FormatTiers(item);
            ApplyPriceWarning(row, item);
        }

        private void ApplyPriceWarning(DataGridViewRow row, QuoteImportItem item)
        {
            if (!_grid.Columns.Contains("PriceWarning"))
            {
                return;
            }

            var result = _priceWarningService.EvaluateQuoteItem(_preview.Supplier, item);
            var cell = row.Cells["PriceWarning"];
            cell.Value = result.Message;
            cell.ToolTipText = result.Message;
            cell.Style.ForeColor = Color.FromArgb(85, 85, 90);
            cell.Style.BackColor = Color.FromArgb(248, 248, 250);

            if (result.Severity == PriceWarningSeverity.Yellow)
            {
                cell.Style.BackColor = Color.FromArgb(255, 247, 230);
                cell.Style.ForeColor = Color.FromArgb(135, 88, 0);
            }
            else if (result.Severity == PriceWarningSeverity.Red)
            {
                cell.Style.BackColor = Color.FromArgb(255, 241, 240);
                cell.Style.ForeColor = Color.FromArgb(170, 0, 0);
            }
        }

        private void OnShowWarningSummary(object sender, EventArgs e)
        {
            var red = 0;
            var yellow = 0;
            var lines = new List<string>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var item = row.Tag as QuoteImportItem;
                if (item == null)
                {
                    continue;
                }

                var result = _priceWarningService.EvaluateQuoteItem(_preview.Supplier, item);
                if (result.Severity == PriceWarningSeverity.None)
                {
                    continue;
                }

                if (result.Severity == PriceWarningSeverity.Red)
                {
                    red++;
                }
                else if (result.Severity == PriceWarningSeverity.Yellow)
                {
                    yellow++;
                }

                lines.Add((row.Index + 1) + ". " + SafeText(item.MaterialCode, item.MaterialName, item.RawName) + "：" + result.Message);
            }

            if (lines.Count == 0)
            {
                MessageBox.Show(this, "当前导入明细没有发现价格预警。", "价格预警", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var message = "红色预警：" + red + " 条；黄色预警：" + yellow + " 条\r\n\r\n" + string.Join("\r\n", lines.ToArray());
            MessageBox.Show(this, message, "价格预警", MessageBoxButtons.OK, red > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private static string SafeText(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return "未命名物料";
        }

        private void Confirm()
        {
            _grid.EndEdit();
            if (!ApplyManualEditsToItems())
            {
                return;
            }

            SelectedItems.Clear();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var selected = row.Cells["Selected"].Value is bool && (bool)row.Cells["Selected"].Value;
                if (selected && row.Tag is QuoteImportItem item)
                {
                    SelectedItems.Add(item);
                }
            }

            if (SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "请至少选择一条物料。", "导入确认", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private bool ApplyManualEditsToItems()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var item = row.Tag as QuoteImportItem;
                if (item == null)
                {
                    continue;
                }

                decimal? usageQuantity;
                if (!TryParseOptionalDecimal(GetCellText(row, "UsageQuantity"), out usageQuantity))
                {
                    _grid.CurrentCell = row.Cells["UsageQuantity"];
                    MessageBox.Show(this, "用量必须是数字，或留空。", "导入确认", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                item.MaterialCode = GetCellText(row, "MaterialCode");
                item.MaterialName = GetCellText(row, "MaterialName");
                item.FinishedSize = GetCellText(row, "FinishedSize");
                item.MaterialProcess = GetCellText(row, "MaterialProcess");
                item.MaterialNameExtracted = GetCellText(row, "MaterialNameExtracted");
                item.GramWeight = GetCellText(row, "GramWeight");
                item.UsageQuantity = usageQuantity;
            }

            return true;
        }

        private void OnGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_grid.Columns[e.ColumnIndex].Name == "PriceTiers")
            {
                EditPriceTiers(_grid.Rows[e.RowIndex]);
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
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Selected")
            {
                return;
            }

            ApplyImportRowStyle(_grid.Rows[e.RowIndex]);
            UpdateImportSelectionSummary();
        }

        private void OnGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Selected")
            {
                return;
            }

            var shouldSelect = !AllImportRowsSelected();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.Cells["Selected"].Value = shouldSelect;
                ApplyImportRowStyle(row);
            }

            UpdateImportSelectionSummary();
        }

        private void ApplyImportRowStyle(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow)
            {
                return;
            }

            row.DefaultCellStyle.BackColor = IsImportRowSelected(row) ? Color.FromArgb(230, 244, 255) : Color.White;
        }

        private bool AllImportRowsSelected()
        {
            var hasRows = false;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                hasRows = true;
                if (!IsImportRowSelected(row))
                {
                    return false;
                }
            }

            return hasRows;
        }

        private static bool IsImportRowSelected(DataGridViewRow row)
        {
            var value = row.Cells["Selected"].Value;
            return value is bool && (bool)value;
        }

        private void UpdateImportSelectionSummary()
        {
            var selected = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow && IsImportRowSelected(row))
                {
                    selected++;
                }
            }

            var total = _grid.Rows.Count;
            _summaryLabel.Text = BuildSummaryText() + "\r\n已选择：" + selected + "/" + total + " 条；检查字段后点击“加入成本表”。";
        }

        private void OnEditPriceTiers(object sender, EventArgs e)
        {
            var row = GetCurrentDataRow();
            if (row == null)
            {
                MessageBox.Show(this, "请先在识别结果中选择一条物料。", "编辑阶梯价格", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            EditPriceTiers(row);
        }

        private DataGridViewRow GetCurrentDataRow()
        {
            if (_grid.CurrentRow != null && _grid.CurrentRow.Tag is QuoteImportItem)
            {
                return _grid.CurrentRow;
            }

            if (_grid.CurrentCell != null)
            {
                var row = _grid.Rows[_grid.CurrentCell.RowIndex];
                return row.Tag is QuoteImportItem ? row : null;
            }

            return null;
        }

        private void EditPriceTiers(DataGridViewRow row)
        {
            var item = row.Tag as QuoteImportItem;
            if (item == null)
            {
                return;
            }

            using (var form = new PriceTiersForm(item.PriceTiers))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                item.PriceTiers = form.PriceTiers;
                row.Cells["PriceTiers"].Value = FormatTiers(item);
                row.Cells["PriceTiers"].Style.BackColor = Color.FromArgb(232, 244, 255);
                row.Cells["PriceTiers"].ToolTipText = "阶梯价格已人工修正";
                ApplyPriceWarning(row, item);
            }
        }

        private void OnGridCellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name != "UsageQuantity")
            {
                return;
            }

            decimal? ignored;
            if (!TryParseOptionalDecimal(Convert.ToString(e.FormattedValue), out ignored))
            {
                e.Cancel = true;
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "用量必须是数字，或留空。";
            }
            else
            {
                _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = string.Empty;
            }
        }

        private async void OnAiAssist(object sender, EventArgs e)
        {
            var settings = new AiSettingsRepository().Get();
            if (!settings.IsEnabled)
            {
                MessageBox.Show(this, "AI 当前未启用，请先到系统设置中启用 AI 辅助。", "AI 辅助识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                MessageBox.Show(this, "请先到系统设置中填写 DeepSeek API Key。", "AI 辅助识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (settings.ConfirmBeforeCall)
            {
                var confirm = MessageBox.Show(
                    this,
                    "将把当前报价单预览内容发送给 DeepSeek 进行结构化复核。继续吗？",
                    "AI 辅助识别",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            SetAiBusy(true);
            try
            {
                var result = await Task.Run(() => new DeepSeekClient().RecognizeQuote(settings, _preview));
                var changedCount = ApplyAiResult(result);
                MessageBox.Show(this, BuildAiResultMessage(result, changedCount), "AI 辅助识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "AI 辅助识别失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetAiBusy(false);
            }
        }

        private void SetAiBusy(bool busy)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            _aiAssistButton.Enabled = !busy;
            _aiPreviewButton.Enabled = !busy;
            _pasteTextButton.Enabled = !busy;
            _warningSummaryButton.Enabled = !busy;
            _editPriceTiersButton.Enabled = !busy;
            _undoAiButton.Enabled = !busy && HasAiChanges();
            _aiResultButton.Enabled = !busy && !string.IsNullOrWhiteSpace(_lastAiRawContent);
            _aiDetailsButton.Enabled = !busy;
            _grid.Enabled = !busy;
            _rawGrid.Enabled = !busy;
            _aiAssistButton.Text = busy ? "识别中..." : "AI识别";
            UpdateAiDetailsMenuState();
        }

        private void OnPasteRawText(object sender, EventArgs e)
        {
            using (var form = new PasteRawTextForm())
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var lines = new List<string>();
                foreach (var line in form.RawText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line.Trim());
                    }
                }

                _preview.RawSheet = QuoteRawSheetPreview.FromLines(lines);
                if (string.IsNullOrWhiteSpace(_preview.TemplateType) ||
                    _preview.TemplateType.Contains("PDF") ||
                    _preview.TemplateType.Contains("图片") ||
                    _preview.TemplateType.Contains("待识别"))
                {
                    _preview.TemplateType = "手工文本预览";
                }

                _summaryLabel.Text = BuildSummaryText();
                LoadRawPreview();
                MessageBox.Show(this, "已更新顶部原始预览。可以继续点击“AI识别”。", "从文本识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private int ApplyAiResult(AiQuoteRecognitionResult result)
        {
            if (result == null)
            {
                return 0;
            }

            ClearAiMarkers();
            _lastPreviewSnapshot = PreviewSnapshot.Capture(_preview);
            _lastAiRawContent = result.RawContent;
            var totalChangedCount = 0;

            if (!string.IsNullOrWhiteSpace(result.TemplateType))
            {
                _preview.TemplateType = result.TemplateType;
            }

            if (!string.IsNullOrWhiteSpace(result.Supplier))
            {
                _preview.Supplier = result.Supplier;
            }

            if (!string.IsNullOrWhiteSpace(result.QuoteDate))
            {
                _preview.QuoteDate = result.QuoteDate;
            }

            if (!string.IsNullOrWhiteSpace(result.QuoteNo))
            {
                _preview.QuoteNo = result.QuoteNo;
            }

            if (result.HeaderRow.HasValue)
            {
                _preview.HeaderRow = result.HeaderRow.Value;
            }

            if (result.QuantityRow.HasValue)
            {
                _preview.QuantityRow = result.QuantityRow.Value;
            }

            if (result.DataStartRow.HasValue)
            {
                _preview.DataStartRow = result.DataStartRow.Value;
            }

            _summaryLabel.Text = BuildSummaryText();
            LoadRawPreview();

            for (var i = 0; i < result.Items.Count; i++)
            {
                var aiItem = result.Items[i];
                var rowIndex = aiItem.Index.HasValue ? aiItem.Index.Value - 1 : i;
                if (rowIndex < 0)
                {
                    continue;
                }

                DataGridViewRow row;
                QuoteImportItem item;
                if (rowIndex >= _grid.Rows.Count)
                {
                    item = CreateItemFromAi(aiItem);
                    if (_preview.Items == null)
                    {
                        _preview.Items = new List<QuoteImportItem>();
                    }

                    _preview.Items.Add(item);
                    var newRowIndex = _grid.Rows.Add();
                    row = _grid.Rows[newRowIndex];
                    row.Tag = item;
                    row.Cells["Selected"].Value = true;
                    FillRow(row, item);
                    row.Cells["AiStatus"].Value = "AI新增，需人工复核";
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 219);
                    totalChangedCount += CountFilledAiFields(aiItem);
                    continue;
                }

                row = _grid.Rows[rowIndex];
                item = row.Tag as QuoteImportItem;
                if (item == null)
                {
                    item = CreateItemFromAi(aiItem);
                    row.Tag = item;
                    if (_preview.Items == null)
                    {
                        _preview.Items = new List<QuoteImportItem>();
                    }

                    _preview.Items.Add(item);
                    row.Cells["Selected"].Value = true;
                    FillRow(row, item);
                    row.Cells["AiStatus"].Value = "AI新增，需人工复核";
                    totalChangedCount += CountFilledAiFields(aiItem);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(aiItem.RawName))
                {
                    item.RawName = aiItem.RawName.Trim();
                }

                var changedFields = new List<string>();
                ApplyAiField(row, "MaterialCode", "物料编码", item.MaterialCode, aiItem.MaterialCode, value => item.MaterialCode = value, changedFields);
                ApplyAiField(row, "MaterialName", "物料名称", item.MaterialName, aiItem.MaterialName, value => item.MaterialName = value, changedFields);
                ApplyAiField(row, "FinishedSize", "成品尺寸", item.FinishedSize, aiItem.FinishedSize, value => item.FinishedSize = value, changedFields);
                ApplyAiField(row, "MaterialProcess", "材质/工艺", item.MaterialProcess, aiItem.MaterialProcess, value => item.MaterialProcess = value, changedFields);
                ApplyAiField(row, "MaterialNameExtracted", "材料名称", item.MaterialNameExtracted, aiItem.MaterialNameExtracted, value => item.MaterialNameExtracted = value, changedFields);
                ApplyAiField(row, "GramWeight", "克重", item.GramWeight, aiItem.GramWeight, value => item.GramWeight = value, changedFields);
                FillRow(row, item);

                var status = BuildAiStatus(aiItem);
                if (changedFields.Count > 0)
                {
                    totalChangedCount += changedFields.Count;
                    status = string.IsNullOrWhiteSpace(status)
                        ? "AI已复核，改动 " + changedFields.Count + " 项：" + string.Join("、", changedFields.ToArray())
                        : status + "；改动 " + changedFields.Count + " 项：" + string.Join("、", changedFields.ToArray());
                }

                row.Cells["AiStatus"].Value = string.IsNullOrWhiteSpace(status) ? "AI已复核，无字段改动" : status;
                if (aiItem.RequiresReview || (aiItem.Warnings != null && aiItem.Warnings.Count > 0))
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 219);
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(238, 247, 255);
                }
            }

            _summaryLabel.Text = BuildSummaryText();
            _undoAiButton.Enabled = HasAiChanges();
            _aiResultButton.Enabled = !string.IsNullOrWhiteSpace(_lastAiRawContent);
            UpdateAiDetailsMenuState();
            return totalChangedCount;
        }

        private static QuoteImportItem CreateItemFromAi(AiQuoteRecognitionItem aiItem)
        {
            return new QuoteImportItem
            {
                RawName = aiItem.RawName,
                MaterialCode = aiItem.MaterialCode,
                MaterialName = aiItem.MaterialName,
                FinishedSize = aiItem.FinishedSize,
                MaterialProcess = aiItem.MaterialProcess,
                MaterialNameExtracted = aiItem.MaterialNameExtracted,
                GramWeight = aiItem.GramWeight,
                PriceTiers = new List<PriceTier>()
            };
        }

        private static int CountFilledAiFields(AiQuoteRecognitionItem aiItem)
        {
            var count = 0;
            if (!string.IsNullOrWhiteSpace(aiItem.MaterialCode)) count++;
            if (!string.IsNullOrWhiteSpace(aiItem.MaterialName)) count++;
            if (!string.IsNullOrWhiteSpace(aiItem.FinishedSize)) count++;
            if (!string.IsNullOrWhiteSpace(aiItem.MaterialProcess)) count++;
            if (!string.IsNullOrWhiteSpace(aiItem.MaterialNameExtracted)) count++;
            if (!string.IsNullOrWhiteSpace(aiItem.GramWeight)) count++;
            return count;
        }

        private void ClearAiMarkers()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                row.DefaultCellStyle.BackColor = Color.White;
                row.Cells["AiStatus"].Value = string.Empty;

                foreach (var columnName in AiWritableColumns)
                {
                    row.Cells[columnName].Style.BackColor = Color.Empty;
                    row.Cells[columnName].ToolTipText = string.Empty;
                    row.Cells[columnName].Tag = null;
                }
            }
        }

        private static void ApplyAiField(
            DataGridViewRow row,
            string columnName,
            string displayName,
            string currentValue,
            string aiValue,
            Action<string> assign,
            List<string> changedFields)
        {
            if (string.IsNullOrWhiteSpace(aiValue))
            {
                return;
            }

            var oldValue = (currentValue ?? string.Empty).Trim();
            var newValue = aiValue.Trim();
            if (string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            assign(newValue);
            changedFields.Add(displayName);
            row.Cells[columnName].Tag = new AiChangeInfo
            {
                ColumnName = columnName,
                DisplayName = displayName,
                OldValue = oldValue,
                NewValue = newValue
            };
            row.Cells[columnName].Style.BackColor = Color.FromArgb(232, 244, 255);
            row.Cells[columnName].ToolTipText = string.Format("AI 修改：{0} -> {1}", oldValue, newValue);
        }

        private void OnUndoAiChanges(object sender, EventArgs e)
        {
            if (!HasAiChanges())
            {
                MessageBox.Show(this, "当前没有可撤销的 AI 改动。", "撤销AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "确定撤销上一次 AI 回填的字段改动吗？",
                "撤销AI",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var count = UndoAiChanges();
            MessageBox.Show(this, "已撤销 AI 改动 " + count + " 项。", "撤销AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private int UndoAiChanges()
        {
            var count = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var item = row.Tag as QuoteImportItem;
                if (item == null)
                {
                    continue;
                }

                var rowChanged = false;
                foreach (var columnName in AiWritableColumns)
                {
                    var change = row.Cells[columnName].Tag as AiChangeInfo;
                    if (change == null)
                    {
                        continue;
                    }

                    SetItemValue(item, columnName, change.OldValue);
                    row.Cells[columnName].Value = change.OldValue;
                    row.Cells[columnName].Tag = null;
                    row.Cells[columnName].Style.BackColor = Color.Empty;
                    row.Cells[columnName].ToolTipText = string.Empty;
                    count++;
                    rowChanged = true;
                }

                row.DefaultCellStyle.BackColor = Color.White;
                row.Cells["AiStatus"].Value = rowChanged ? "已撤销AI改动" : string.Empty;
            }

            if (_lastPreviewSnapshot != null)
            {
                _lastPreviewSnapshot.Restore(_preview);
                _summaryLabel.Text = BuildSummaryText();
                LoadRawPreview();
                LoadItems();
            }

            _undoAiButton.Enabled = false;
            UpdateAiDetailsMenuState();
            return count;
        }

        private bool HasAiChanges()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                foreach (var columnName in AiWritableColumns)
                {
                    if (row.Cells[columnName].Tag is AiChangeInfo)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnViewAiResult(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastAiRawContent))
            {
                MessageBox.Show(this, "还没有 AI 返回结果。", "AI结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new AiRequestPreviewForm("AI 返回结果", _lastAiRawContent))
            {
                form.ShowDialog(this);
            }
        }

        private static void SetItemValue(QuoteImportItem item, string columnName, string value)
        {
            switch (columnName)
            {
                case "MaterialCode":
                    item.MaterialCode = value;
                    break;
                case "MaterialName":
                    item.MaterialName = value;
                    break;
                case "FinishedSize":
                    item.FinishedSize = value;
                    break;
                case "MaterialProcess":
                    item.MaterialProcess = value;
                    break;
                case "MaterialNameExtracted":
                    item.MaterialNameExtracted = value;
                    break;
                case "GramWeight":
                    item.GramWeight = value;
                    break;
            }
        }

        private static string BuildAiStatus(AiQuoteRecognitionItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var messages = new List<string>();
            if (item.RequiresReview)
            {
                messages.Add("需人工复核");
            }

            if (item.Warnings != null)
            {
                messages.AddRange(item.Warnings);
            }

            return string.Join("；", messages.ToArray());
        }

        private static string BuildAiResultMessage(AiQuoteRecognitionResult result, int changedCount)
        {
            var itemCount = result.Items == null ? 0 : result.Items.Count;
            var warningCount = result.Warnings == null ? 0 : result.Warnings.Count;
            return string.Format(
                "AI 识别完成。\r\n模板：{0}\r\n置信度：{1:0.##}\r\n返回物料：{2} 条\r\n字段改动：{3} 项\r\n整体提醒：{4} 条\r\n请在预览表中复核后再加入成本表。",
                result.TemplateType,
                result.Confidence,
                itemCount,
                changedCount,
                warningCount);
        }

        private void OnAiRequestPreview(object sender, EventArgs e)
        {
            var settings = new AiSettingsRepository().Get();
            if (!settings.IsEnabled)
            {
                var result = MessageBox.Show(
                    this,
                    "AI 当前未启用。仍然生成 DeepSeek 请求预览吗？",
                    "AI 请求预览",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            var requestJson = new DeepSeekClient().BuildQuoteRecognitionRequestJson(settings, _preview);
            using (var form = new AiRequestPreviewForm(requestJson))
            {
                form.ShowDialog(this);
            }
        }

        private static string FormatTiers(QuoteImportItem item)
        {
            if (item.PriceTiers == null || item.PriceTiers.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var tier in item.PriceTiers)
            {
                if (sb.Length > 0)
                {
                    sb.Append("；");
                }

                sb.Append(tier.Label);
                sb.Append("=");
                sb.Append(tier.UnitPrice.HasValue ? tier.UnitPrice.Value.ToString("0.####") : "");
            }

            return sb.ToString();
        }

        private static void AddColumn(DataGridView grid, string name, string header, bool readOnly = false)
        {
            var column = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (readOnly)
            {
                column.DefaultCellStyle.BackColor = Color.FromArgb(248, 248, 250);
                column.DefaultCellStyle.ForeColor = Color.FromArgb(85, 85, 90);
            }

            grid.Columns.Add(column);
        }

        private static void ConfigureImportGridColumns(DataGridView grid)
        {
            SetColumn(grid, "Selected", 54, true, false);
            SetColumn(grid, "MaterialCode", 118, false, false);
            SetColumn(grid, "MaterialName", 160, false, true);
            SetColumn(grid, "FinishedSize", 120, false, false);
            SetColumn(grid, "MaterialProcess", 360, false, true);
            SetColumn(grid, "MaterialNameExtracted", 130, false, true);
            SetColumn(grid, "GramWeight", 76, false, false);
            SetColumn(grid, "UsageQuantity", 76, false, false);
            SetColumn(grid, "PriceTiers", 150, false, true);
            SetColumn(grid, "PriceWarning", 220, false, true);
            SetColumn(grid, "AiStatus", 150, false, true);
        }

        private static void SetColumn(DataGridView grid, string name, int width, bool frozen, bool wrap)
        {
            if (!grid.Columns.Contains(name))
            {
                return;
            }

            var column = grid.Columns[name];
            column.Width = width;
            column.MinimumWidth = Math.Min(width, 54);
            column.Frozen = frozen;
            column.DefaultCellStyle.WrapMode = wrap ? DataGridViewTriState.True : DataGridViewTriState.False;
        }

        private static string GetCellText(DataGridViewRow row, string columnName)
        {
            return Convert.ToString(row.Cells[columnName].Value).Trim();
        }

        private static bool TryParseOptionalDecimal(string value, out decimal? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            decimal parsed;
            if (decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out parsed) ||
                decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                result = parsed;
                return true;
            }

            return false;
        }

        private static int GetVisibleRowCount(QuoteRawSheetPreview rawSheet)
        {
            var lastRow = Math.Min(rawSheet.Rows, rawSheet.Cells.GetLength(0) - 1);
            for (var row = lastRow; row >= 1; row--)
            {
                for (var column = 1; column <= Math.Min(rawSheet.Columns, rawSheet.Cells.GetLength(1) - 1); column++)
                {
                    if (!string.IsNullOrWhiteSpace(rawSheet.Cells[row, column]))
                    {
                        return row;
                    }
                }
            }

            return Math.Min(rawSheet.Rows, 1);
        }

        private static int GetVisibleColumnCount(QuoteRawSheetPreview rawSheet)
        {
            var lastColumn = Math.Min(rawSheet.Columns, rawSheet.Cells.GetLength(1) - 1);
            for (var column = lastColumn; column >= 1; column--)
            {
                for (var row = 1; row <= Math.Min(rawSheet.Rows, rawSheet.Cells.GetLength(0) - 1); row++)
                {
                    if (!string.IsNullOrWhiteSpace(rawSheet.Cells[row, column]))
                    {
                        return column;
                    }
                }
            }

            return Math.Min(rawSheet.Columns, 1);
        }

        private static string GetRawCell(QuoteRawSheetPreview rawSheet, int row, int column)
        {
            if (row <= 0 ||
                column <= 0 ||
                row >= rawSheet.Cells.GetLength(0) ||
                column >= rawSheet.Cells.GetLength(1))
            {
                return string.Empty;
            }

            return rawSheet.Cells[row, column] ?? string.Empty;
        }

        private static string ToExcelColumnName(int column)
        {
            var name = string.Empty;
            while (column > 0)
            {
                column--;
                name = (char)('A' + column % 26) + name;
                column /= 26;
            }

            return name;
        }

        private sealed class AiChangeInfo
        {
            public string ColumnName { get; set; }
            public string DisplayName { get; set; }
            public string OldValue { get; set; }
            public string NewValue { get; set; }
        }

        private sealed class PreviewSnapshot
        {
            public string Supplier { get; set; }
            public string QuoteDate { get; set; }
            public string QuoteNo { get; set; }
            public string TemplateType { get; set; }
            public int HeaderRow { get; set; }
            public int QuantityRow { get; set; }
            public int DataStartRow { get; set; }
            public List<QuoteImportItem> Items { get; set; }

            public static PreviewSnapshot Capture(QuoteImportPreview preview)
            {
                return new PreviewSnapshot
                {
                    Supplier = preview.Supplier,
                    QuoteDate = preview.QuoteDate,
                    QuoteNo = preview.QuoteNo,
                    TemplateType = preview.TemplateType,
                    HeaderRow = preview.HeaderRow,
                    QuantityRow = preview.QuantityRow,
                    DataStartRow = preview.DataStartRow,
                    Items = CloneItems(preview.Items)
                };
            }

            public void Restore(QuoteImportPreview preview)
            {
                preview.Supplier = Supplier;
                preview.QuoteDate = QuoteDate;
                preview.QuoteNo = QuoteNo;
                preview.TemplateType = TemplateType;
                preview.HeaderRow = HeaderRow;
                preview.QuantityRow = QuantityRow;
                preview.DataStartRow = DataStartRow;
                preview.Items = CloneItems(Items);
            }

            private static List<QuoteImportItem> CloneItems(List<QuoteImportItem> source)
            {
                var result = new List<QuoteImportItem>();
                if (source == null)
                {
                    return result;
                }

                foreach (var item in source)
                {
                    result.Add(new QuoteImportItem
                    {
                        RawName = item.RawName,
                        MaterialCode = item.MaterialCode,
                        MaterialName = item.MaterialName,
                        FinishedSize = item.FinishedSize,
                        MaterialProcess = item.MaterialProcess,
                        MaterialNameExtracted = item.MaterialNameExtracted,
                        GramWeight = item.GramWeight,
                        UsageQuantity = item.UsageQuantity,
                        PriceTiers = ClonePriceTiers(item.PriceTiers)
                    });
                }

                return result;
            }

            private static List<PriceTier> ClonePriceTiers(List<PriceTier> source)
            {
                var result = new List<PriceTier>();
                if (source == null)
                {
                    return result;
                }

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
        }
    }
}
