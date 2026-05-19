using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class QuoteImportPreviewForm : Form
    {
        private readonly QuoteImportPreview _preview;
        private readonly DataGridView _grid;
        private readonly DataGridView _rawGrid;
        private readonly Label _summaryLabel;
        private readonly Button _editPriceTiersButton;
        private readonly Button _aiAssistButton;
        private readonly Button _aiPreviewButton;
        private readonly Button _undoAiButton;
        private readonly Button _aiResultButton;
        private readonly Button _pasteTextButton;
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

            Text = "报价单导入确认";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 700);
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            _summaryLabel = BuildSummaryLabel();
            root.Controls.Add(_summaryLabel, 0, 0);

            _grid = BuildGrid();
            _rawGrid = BuildRawGrid();
            root.Controls.Add(BuildPreviewLayout(), 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(buttons, 0, 2);

            var confirm = new Button { Text = "确认加入", Width = 96, Height = 32 };
            confirm.Click += (_, __) => Confirm();
            buttons.Controls.Add(confirm);

            var cancel = new Button { Text = "取消", Width = 82, Height = 32 };
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);

            _editPriceTiersButton = new Button { Text = "编辑阶梯价", Width = 108, Height = 32 };
            _editPriceTiersButton.Click += OnEditPriceTiers;
            buttons.Controls.Add(_editPriceTiersButton);

            _aiAssistButton = new Button { Text = "AI辅助识别", Width = 112, Height = 32 };
            _aiAssistButton.Click += OnAiAssist;
            buttons.Controls.Add(_aiAssistButton);

            _pasteTextButton = new Button { Text = "粘贴文本", Width = 92, Height = 32 };
            _pasteTextButton.Click += OnPasteRawText;
            buttons.Controls.Add(_pasteTextButton);

            _aiPreviewButton = new Button { Text = "AI请求预览", Width = 108, Height = 32 };
            _aiPreviewButton.Click += OnAiRequestPreview;
            buttons.Controls.Add(_aiPreviewButton);

            _aiResultButton = new Button { Text = "查看AI结果", Width = 108, Height = 32, Enabled = false };
            _aiResultButton.Click += OnViewAiResult;
            buttons.Controls.Add(_aiResultButton);

            _undoAiButton = new Button { Text = "撤销AI改动", Width = 108, Height = 32, Enabled = false };
            _undoAiButton.Click += OnUndoAiChanges;
            buttons.Controls.Add(_undoAiButton);

            LoadItems();
            LoadRawPreview();
        }

        private Label BuildSummaryLabel()
        {
            return new Label
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
                "供应商：{0}\r\nSheet：{1}    模板：{2}    表头行：{3}    数量行：{4}    数据起始行：{5}    物料：{6} 条",
                _preview.Supplier,
                _preview.SheetName,
                _preview.TemplateType,
                _preview.HeaderRow,
                _preview.QuantityRow,
                _preview.DataStartRow,
                _preview.Items == null ? 0 : _preview.Items.Count);
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
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            grid.CellValidating += OnGridCellValidating;
            grid.CellDoubleClick += OnGridCellDoubleClick;

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "加入", Width = 48 });
            AddColumn(grid, "MaterialCode", "物料编码");
            AddColumn(grid, "MaterialName", "物料名称");
            AddColumn(grid, "FinishedSize", "成品尺寸");
            AddColumn(grid, "MaterialProcess", "材质/工艺");
            AddColumn(grid, "MaterialNameExtracted", "材料名称");
            AddColumn(grid, "GramWeight", "克重");
            AddColumn(grid, "UsageQuantity", "用量");
            AddColumn(grid, "PriceTiers", "阶梯价格", true);
            AddColumn(grid, "AiStatus", "AI状态", true);
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
            split.Panel2.Controls.Add(BuildSectionPanel("识别结果（可直接修正字段）", _grid));
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

        private static Control BuildSectionPanel(string title, Control content)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var label = new Label
            {
                Dock = DockStyle.Fill,
                Text = title,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0),
                ForeColor = Color.FromArgb(80, 80, 85),
                BackColor = Color.White
            };
            panel.Controls.Add(label, 0, 0);

            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content, 0, 1);
            return panel;
        }

        private DataGridView BuildRawGrid()
        {
            return new DataGridView
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
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
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

        private static void FillRow(DataGridViewRow row, QuoteImportItem item)
        {
            row.Cells["MaterialCode"].Value = item.MaterialCode;
            row.Cells["MaterialName"].Value = item.MaterialName;
            row.Cells["FinishedSize"].Value = item.FinishedSize;
            row.Cells["MaterialProcess"].Value = item.MaterialProcess;
            row.Cells["MaterialNameExtracted"].Value = item.MaterialNameExtracted;
            row.Cells["GramWeight"].Value = item.GramWeight;
            row.Cells["UsageQuantity"].Value = item.UsageQuantity.HasValue ? item.UsageQuantity.Value.ToString("0.####") : string.Empty;
            row.Cells["PriceTiers"].Value = FormatTiers(item);
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
            _editPriceTiersButton.Enabled = !busy;
            _undoAiButton.Enabled = !busy && HasAiChanges();
            _aiResultButton.Enabled = !busy && !string.IsNullOrWhiteSpace(_lastAiRawContent);
            _grid.Enabled = !busy;
            _rawGrid.Enabled = !busy;
            _aiAssistButton.Text = busy ? "识别中..." : "AI辅助识别";
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
                MessageBox.Show(this, "已更新顶部原始预览。可以继续点击“AI辅助识别”。", "粘贴原始文本", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, "当前没有可撤销的 AI 改动。", "撤销AI改动", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "确定撤销上一次 AI 回填的字段改动吗？",
                "撤销AI改动",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var count = UndoAiChanges();
            MessageBox.Show(this, "已撤销 AI 改动 " + count + " 项。", "撤销AI改动", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(this, "还没有 AI 返回结果。", "查看AI结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                "AI 识别完成。\r\n模板：{0}\r\n置信度：{1:0.##}\r\n返回物料：{2} 条\r\n字段改动：{3} 项\r\n整体提醒：{4} 条\r\n请在预览表中复核后再确认加入。",
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
