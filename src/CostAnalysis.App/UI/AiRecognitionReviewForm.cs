using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class AiRecognitionReviewForm : MetroForm
    {
        private readonly string _filePath;
        private readonly string _fileName;
        private readonly MetroLabel _summaryLabel;
        private readonly MetroLabel _statusLabel;
        private readonly MetroGrid _rawGrid;
        private readonly MetroGrid _itemsGrid;
        private readonly MetroGrid _materialsGrid;
        private readonly MetroGrid _rulesGrid;

        public QuoteImportPreview Preview { get; private set; }

        public AiRecognitionReviewForm(QuoteImportPreview preview, string filePath, string fileName)
        {
            Preview = preview;
            _filePath = filePath;
            _fileName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(filePath) : fileName;

            Text = "识别校对";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 760);
            MinimumSize = new Size(980, 640);
            Style = MetroColorStyle.Blue;
            Theme = MetroThemeStyle.Light;
            ShadowType = MetroFormShadowType.DropShadow;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(root);

            _summaryLabel = BuildLabel(true);
            root.Controls.Add(_summaryLabel, 0, 0);

            _rawGrid = BuildGrid(true);
            root.Controls.Add(BuildSection("原始报价单预览", _rawGrid), 0, 1);

            _itemsGrid = BuildGrid(false);
            BuildItemsColumns();
            root.Controls.Add(BuildSection("识别出的物料明细（可直接修正）", _itemsGrid), 0, 2);

            _materialsGrid = BuildGrid(false);
            _rulesGrid = BuildGrid(false);
            BuildCandidateColumns();
            root.Controls.Add(BuildCandidateTabs(), 0, 3);

            _statusLabel = BuildLabel(false);
            root.Controls.Add(BuildFooter(), 0, 4);

            ReloadAll("请校对识别结果，确认无误后再学习规则或保存候选。");
        }

        private MetroLabel BuildLabel(bool summary)
        {
            return new MetroLabel
            {
                Dock = DockStyle.Fill,
                FontSize = summary ? MetroLabelSize.Medium : MetroLabelSize.Small,
                ForeColor = summary ? Color.FromArgb(29, 29, 31) : Color.FromArgb(95, 95, 100),
                TextAlign = summary ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleLeft,
                UseCustomForeColor = true,
                WrapToLine = true
            };
        }

        private MetroGrid BuildGrid(bool readOnly)
        {
            var grid = new MetroGrid
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                RowHeadersVisible = readOnly,
                ReadOnly = readOnly,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                Style = MetroColorStyle.Blue,
                Theme = MetroThemeStyle.Light,
                EnableHeadersVisualStyles = false,
                RowTemplate = { Height = 30 }
            };
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(29, 29, 31);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 244, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(29, 29, 31);
            return grid;
        }

        private Control BuildSection(string title, Control content)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(new MetroLabel
            {
                Text = title,
                Dock = DockStyle.Fill,
                FontSize = MetroLabelSize.Small,
                ForeColor = Color.FromArgb(70, 95, 130),
                UseCustomForeColor = true,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            panel.Controls.Add(content, 0, 1);
            return panel;
        }

        private Control BuildCandidateTabs()
        {
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8)
            };

            var materialPage = new TabPage("识别出的材料信息");
            materialPage.Controls.Add(_materialsGrid);
            tabs.TabPages.Add(materialPage);

            var rulePage = new TabPage("识别出的工艺信息");
            rulePage.Controls.Add(_rulesGrid);
            tabs.TabPages.Add(rulePage);

            return tabs;
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 6; i++)
            {
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            }
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _statusLabel.Margin = new Padding(0, 6, 8, 6);
            footer.Controls.Add(_statusLabel, 0, 0);

            AddFooterButton(footer, "本地重新识别", OnLocalRecognize);
            AddFooterButton(footer, "AI重新识别", OnAiRecognize);
            AddFooterButton(footer, "确认学习规则", OnConfirmLearning);
            AddFooterButton(footer, "保存材料候选", OnSaveMaterialCandidates);
            AddFooterButton(footer, "保存工艺候选", OnSaveRuleCandidates);
            AddFooterButton(footer, "关闭", (_, __) => Close());
            return footer;
        }

        private void AddFooterButton(TableLayoutPanel footer, string text, EventHandler handler)
        {
            var button = new MetroButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 8, 0, 8),
                UseSelectable = true,
                Theme = MetroThemeStyle.Light,
                Style = text == "确认学习规则" ? MetroColorStyle.Blue : MetroColorStyle.Silver,
                Highlight = text == "确认学习规则"
            };
            button.Click += handler;
            footer.Controls.Add(button, footer.Controls.Count, 0);
        }

        private void BuildItemsColumns()
        {
            AddColumn(_itemsGrid, "MaterialCode", "物料编码", 150);
            AddColumn(_itemsGrid, "MaterialName", "物料名称", 180);
            AddColumn(_itemsGrid, "FinishedSize", "成品尺寸", 150);
            AddColumn(_itemsGrid, "MaterialProcess", "材质/工艺", 320);
            AddColumn(_itemsGrid, "MaterialNameExtracted", "材料名称", 190);
            AddColumn(_itemsGrid, "GramWeight", "克重", 120);
            AddColumn(_itemsGrid, "UsageQuantity", "用量", 90);
            AddColumn(_itemsGrid, "PriceTiers", "阶梯价格", 220);
            AddColumn(_itemsGrid, "RawName", "原始文本", 240);
        }

        private void BuildCandidateColumns()
        {
            AddCheckColumn(_materialsGrid, "Selected", "保存", 58);
            AddColumn(_materialsGrid, "Name", "材料名称", 180);
            AddColumn(_materialsGrid, "Category", "类别", 90);
            AddColumn(_materialsGrid, "Vendor", "供应商/厂家", 190);
            AddColumn(_materialsGrid, "Spec", "规格/克重", 140);
            AddColumn(_materialsGrid, "Unit", "单位", 80);
            AddColumn(_materialsGrid, "IncludesFreight", "含运", 70);
            AddColumn(_materialsGrid, "Source", "来源", 220);
            AddColumn(_materialsGrid, "Existing", "状态", 90);

            AddCheckColumn(_rulesGrid, "Selected", "保存", 58);
            AddColumn(_rulesGrid, "Keyword", "工艺关键词", 150);
            AddColumn(_rulesGrid, "CostType", "费用类型", 110);
            AddColumn(_rulesGrid, "Amount", "金额/公式", 130);
            AddColumn(_rulesGrid, "IsEnabled", "启用", 70);
            AddColumn(_rulesGrid, "Remark", "备注", 300);
            AddColumn(_rulesGrid, "Source", "来源", 220);
            AddColumn(_rulesGrid, "Existing", "状态", 90);
        }

        private static void AddColumn(DataGridView grid, string name, string header, int width)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = Math.Min(width, 60),
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private static void AddCheckColumn(DataGridView grid, string name, string header, int width)
        {
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                MinimumWidth = Math.Min(width, 50),
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private void ReloadAll(string status)
        {
            LoadSummary();
            LoadRawPreview();
            LoadItems();
            LoadCandidates();
            _statusLabel.Text = status;
        }

        private void LoadSummary()
        {
            var itemCount = Preview == null || Preview.Items == null ? 0 : Preview.Items.Count;
            _summaryLabel.Text =
                "文件：" + Safe(_fileName) +
                "    供应商：" + Safe(Preview == null ? string.Empty : Preview.Supplier) +
                "    Sheet：" + Safe(Preview == null ? string.Empty : Preview.SheetName) +
                "    模板：" + Safe(Preview == null ? string.Empty : Preview.TemplateType) +
                "    表头行：" + (Preview == null ? 0 : Preview.HeaderRow) +
                "    数据起始行：" + (Preview == null ? 0 : Preview.DataStartRow) +
                "    物料：" + itemCount + " 条";
        }

        private void LoadRawPreview()
        {
            _rawGrid.Columns.Clear();
            _rawGrid.Rows.Clear();
            if (Preview == null || Preview.RawSheet == null || Preview.RawSheet.Cells == null)
            {
                return;
            }

            var columns = Math.Min(Math.Max(Preview.RawSheet.Columns, 1), 26);
            for (var col = 1; col <= columns; col++)
            {
                AddColumn(_rawGrid, "C" + col, ToExcelColumnName(col), 150);
            }

            var rows = Math.Min(Math.Max(Preview.RawSheet.Rows, 1), 200);
            for (var row = 1; row <= rows; row++)
            {
                var values = new object[columns];
                for (var col = 1; col <= columns; col++)
                {
                    values[col - 1] = Preview.RawSheet.Cells[row, col] ?? string.Empty;
                }

                var index = _rawGrid.Rows.Add(values);
                _rawGrid.Rows[index].HeaderCell.Value = row.ToString();
                if (row == Preview.HeaderRow || row == Preview.QuantityRow)
                {
                    _rawGrid.Rows[index].DefaultCellStyle.BackColor = Color.FromArgb(230, 244, 255);
                }
                else if (row >= Preview.DataStartRow && Preview.DataStartRow > 0 && row < Preview.DataStartRow + 20)
                {
                    _rawGrid.Rows[index].DefaultCellStyle.BackColor = Color.FromArgb(246, 255, 237);
                }
            }
        }

        private void LoadItems()
        {
            _itemsGrid.Rows.Clear();
            if (Preview == null || Preview.Items == null)
            {
                return;
            }

            foreach (var item in Preview.Items)
            {
                _itemsGrid.Rows.Add(
                    item.MaterialCode,
                    item.MaterialName,
                    item.FinishedSize,
                    item.MaterialProcess,
                    item.MaterialNameExtracted,
                    item.GramWeight,
                    item.UsageQuantity.HasValue ? item.UsageQuantity.Value.ToString("0.####") : string.Empty,
                    FormatPriceTiers(item.PriceTiers),
                    item.RawName);
            }
        }

        private void LoadCandidates()
        {
            _materialsGrid.Rows.Clear();
            _rulesGrid.Rows.Clear();
            if (Preview == null || Preview.Items == null)
            {
                return;
            }

            var source = string.IsNullOrWhiteSpace(_fileName) ? Safe(Preview.SheetName) : _fileName;
            var materialKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ruleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Preview.Items)
            {
                foreach (var candidate in ExtractMaterialCandidates(Preview, item, source))
                {
                    if (materialKeys.Add(Normalize(candidate.Name)))
                    {
                        AddMaterialCandidateRow(candidate);
                    }
                }

                foreach (var candidate in ExtractRuleCandidates(item, source))
                {
                    if (ruleKeys.Add(Normalize(candidate.Keyword)))
                    {
                        AddRuleCandidateRow(candidate);
                    }
                }
            }
        }

        private void AddMaterialCandidateRow(MaterialCandidate candidate)
        {
            var index = _materialsGrid.Rows.Add();
            var row = _materialsGrid.Rows[index];
            row.Cells["Selected"].Value = true;
            row.Cells["Name"].Value = candidate.Name;
            row.Cells["Category"].Value = GuessMaterialCategory(candidate.Name);
            row.Cells["Vendor"].Value = candidate.Vendor;
            row.Cells["Spec"].Value = candidate.Spec;
            row.Cells["Unit"].Value = "张";
            row.Cells["IncludesFreight"].Value = "是";
            row.Cells["Source"].Value = candidate.Source;
            row.Cells["Existing"].Value = "候选";
            row.DefaultCellStyle.BackColor = Color.FromArgb(238, 247, 255);
        }

        private void AddRuleCandidateRow(RuleCandidate candidate)
        {
            var index = _rulesGrid.Rows.Add();
            var row = _rulesGrid.Rows[index];
            row.Cells["Selected"].Value = true;
            row.Cells["Keyword"].Value = candidate.Keyword;
            row.Cells["CostType"].Value = ToDisplayCostType(candidate.CostType);
            row.Cells["Amount"].Value = string.Empty;
            row.Cells["IsEnabled"].Value = "是";
            row.Cells["Remark"].Value = BuildRuleRemark(candidate, candidate.Source);
            row.Cells["Source"].Value = candidate.Source;
            row.Cells["Existing"].Value = "候选";
            row.DefaultCellStyle.BackColor = Color.FromArgb(238, 247, 255);
        }

        private void OnLocalRecognize(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath))
            {
                MessageBox.Show(this, "找不到原始文件，无法本地重新识别。", "本地重新识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var refreshed = new ExcelQuoteImportService().Import(_filePath);
                if (refreshed == null)
                {
                    MessageBox.Show(this, "本地重新识别没有返回结果。", "本地重新识别", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Preview = refreshed;
                ReloadAll("已完成本地重新识别，请继续校对。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "本地重新识别失败：" + ex.Message, "本地重新识别", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnAiRecognize(object sender, EventArgs e)
        {
            ReadItemsFromGrid();
            try
            {
                var settings = new AiSettingsRepository().Get();
                var result = new DeepSeekClient().RecognizeQuote(settings, Preview);
                var count = ApplyAiResultToPreview(Preview, result);
                ReloadAll("AI重新识别完成，返回物料 " + count + " 条，请人工校对后再确认学习。");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "AI重新识别失败：" + ex.Message, "AI重新识别", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnConfirmLearning(object sender, EventArgs e)
        {
            ReadItemsFromGrid();
            if (Preview == null || Preview.Items == null || Preview.Items.Count == 0)
            {
                MessageBox.Show(this, "当前没有可学习的物料明细。", "确认学习规则", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            new QuoteTemplateRepository().SaveLearnedTemplate(Preview, _fileName, Preview.Items.Count);
            LoadSummary();
            _statusLabel.Text = "已确认学习规则。以后相似报价单会优先尝试本地规则识别。";
            MessageBox.Show(this, "已保存为本地学习规则。", "确认学习规则", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            _statusLabel.Text = added == 0 ? "没有新的材料候选需要保存。" : "已保存材料候选 " + added + " 条。";
            MessageBox.Show(this, _statusLabel.Text, "保存材料候选", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            _statusLabel.Text = added == 0 ? "没有新的工艺候选需要保存。" : "已保存工艺候选 " + added + " 条。";
            MessageBox.Show(this, _statusLabel.Text, "保存工艺候选", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ReadItemsFromGrid()
        {
            if (Preview == null)
            {
                return;
            }

            var items = new List<QuoteImportItem>();
            foreach (DataGridViewRow row in _itemsGrid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ReadCell(row, "MaterialCode")) &&
                    string.IsNullOrWhiteSpace(ReadCell(row, "MaterialName")) &&
                    string.IsNullOrWhiteSpace(ReadCell(row, "MaterialProcess")) &&
                    string.IsNullOrWhiteSpace(ReadCell(row, "MaterialNameExtracted")))
                {
                    continue;
                }

                decimal usage;
                items.Add(new QuoteImportItem
                {
                    MaterialCode = ReadCell(row, "MaterialCode"),
                    MaterialName = ReadCell(row, "MaterialName"),
                    FinishedSize = ReadCell(row, "FinishedSize"),
                    MaterialProcess = ReadCell(row, "MaterialProcess"),
                    MaterialNameExtracted = ReadCell(row, "MaterialNameExtracted"),
                    GramWeight = ReadCell(row, "GramWeight"),
                    UsageQuantity = decimal.TryParse(ReadCell(row, "UsageQuantity"), out usage) ? usage : (decimal?)null,
                    RawName = ReadCell(row, "RawName"),
                    PriceTiers = ParsePriceTiers(ReadCell(row, "PriceTiers"))
                });
            }

            Preview.Items = items;
            LoadSummary();
            LoadCandidates();
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

            var items = new List<QuoteImportItem>();
            foreach (var aiItem in result.Items)
            {
                if (aiItem == null)
                {
                    continue;
                }

                items.Add(new QuoteImportItem
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

            preview.Items = items;
            return items.Count;
        }

        private static IEnumerable<MaterialCandidate> ExtractMaterialCandidates(QuoteImportPreview preview, QuoteImportItem item, string source)
        {
            foreach (var name in SplitMaterials(item == null ? string.Empty : item.MaterialNameExtracted))
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

        private static string FormatPriceTiers(IList<PriceTier> tiers)
        {
            if (tiers == null || tiers.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var tier in tiers)
            {
                if (tier == null)
                {
                    continue;
                }

                parts.Add(Safe(tier.Label) + "=" + (tier.UnitPrice.HasValue ? tier.UnitPrice.Value.ToString("0.####") : string.Empty));
            }

            return string.Join("；", parts.ToArray());
        }

        private static List<PriceTier> ParsePriceTiers(string text)
        {
            var tiers = new List<PriceTier>();
            foreach (var part in (text ?? string.Empty).Split(new[] { ';', '；', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { '=', '＝' }, 2);
                var label = pair.Length > 0 ? pair[0].Trim() : string.Empty;
                decimal price;
                tiers.Add(new PriceTier
                {
                    Label = label,
                    UnitPrice = pair.Length > 1 && decimal.TryParse(pair[1].Trim(), out price) ? price : (decimal?)null
                });
            }

            return tiers;
        }

        private static bool IsRowChecked(DataGridViewRow row, string columnName)
        {
            var value = row.Cells[columnName].Value;
            return value is bool && (bool)value;
        }

        private static string ReadCell(DataGridViewRow row, string columnName)
        {
            if (row == null || row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            return Convert.ToString(row.Cells[columnName].Value) ?? string.Empty;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }

        private static string Safe(string value)
        {
            return value ?? string.Empty;
        }

        private static string ToExcelColumnName(int column)
        {
            var name = string.Empty;
            while (column > 0)
            {
                var modulo = (column - 1) % 26;
                name = Convert.ToChar('A' + modulo) + name;
                column = (column - modulo) / 26;
            }

            return name;
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
            public RuleCandidate(string keyword, string costType, string formulaHint = null)
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
    }
}
