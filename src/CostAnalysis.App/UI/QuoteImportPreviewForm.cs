using System;
using System.Collections.Generic;
using System.Drawing;
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
        private readonly Label _summaryLabel;
        private readonly Button _aiAssistButton;
        private readonly Button _aiPreviewButton;
        private readonly Button _undoAiButton;
        private readonly Button _aiResultButton;
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
            Size = new Size(1120, 640);
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
            root.Controls.Add(_grid, 0, 1);

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

            _aiAssistButton = new Button { Text = "AI辅助识别", Width = 112, Height = 32 };
            _aiAssistButton.Click += OnAiAssist;
            buttons.Controls.Add(_aiAssistButton);

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

            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "加入", Width = 48 });
            AddColumn(grid, "MaterialCode", "物料编码");
            AddColumn(grid, "MaterialName", "物料名称");
            AddColumn(grid, "FinishedSize", "成品尺寸");
            AddColumn(grid, "MaterialProcess", "材质/工艺");
            AddColumn(grid, "MaterialNameExtracted", "材料名称");
            AddColumn(grid, "GramWeight", "克重");
            AddColumn(grid, "UsageQuantity", "用量");
            AddColumn(grid, "PriceTiers", "阶梯价格");
            AddColumn(grid, "AiStatus", "AI状态");
            return grid;
        }

        private void LoadItems()
        {
            foreach (var item in _preview.Items)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Tag = item;
                row.Cells["Selected"].Value = true;
                FillRow(row, item);
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
            _undoAiButton.Enabled = !busy && HasAiChanges();
            _aiResultButton.Enabled = !busy && !string.IsNullOrWhiteSpace(_lastAiRawContent);
            _grid.Enabled = !busy;
            _aiAssistButton.Text = busy ? "识别中..." : "AI辅助识别";
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

            for (var i = 0; i < result.Items.Count; i++)
            {
                var aiItem = result.Items[i];
                var rowIndex = aiItem.Index.HasValue ? aiItem.Index.Value - 1 : i;
                if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
                {
                    continue;
                }

                var row = _grid.Rows[rowIndex];
                var item = row.Tag as QuoteImportItem;
                if (item == null)
                {
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

            _undoAiButton.Enabled = HasAiChanges();
            _aiResultButton.Enabled = !string.IsNullOrWhiteSpace(_lastAiRawContent);
            return totalChangedCount;
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

        private static void AddColumn(DataGridView grid, string name, string header)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
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
                    DataStartRow = preview.DataStartRow
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
            }
        }
    }
}
