using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.UI
{
    internal sealed class ProcessCostRulesForm : Form
    {
        private readonly DataGridView _grid;
        private readonly DataGridView _sourceGrid;

        public ProcessCostRulesForm() : this(null)
        {
        }

        public ProcessCostRulesForm(DataGridView sourceGrid)
        {
            _sourceGrid = sourceGrid;
            Text = "工艺费用规则";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 520);
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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

            var scan = new Button { Text = "扫描填入", Width = 96, Height = 30 };
            scan.Click += OnScanFromCurrentAnalysis;
            toolbar.Controls.Add(scan);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };
            root.Controls.Add(_grid, 0, 1);

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
                    var rowIndex = _grid.Rows.Add();
                    var row = _grid.Rows[rowIndex];
                    row.Cells["Keyword"].Value = candidate.Keyword;
                    row.Cells["CostType"].Value = ToDisplayCostType(candidate.CostType);
                    row.Cells["IsEnabled"].Value = "是";
                    row.Cells["Remark"].Value = "从当前成本分析表扫描";
                    row.DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 255);
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
            if (value.Contains("印刷")) return "PrintingCost";
            if (value.Contains("其他")) return "OtherCost";
            if (value.Contains("材料")) return "MaterialCost";
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
            return Convert.ToString(row.Cells[columnName].Value).Trim();
        }

        private static string ReadSourceCell(DataGridViewRow row, string columnName)
        {
            if (row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            return Convert.ToString(row.Cells[columnName].Value).Trim();
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }

        private void AddColumn(string name, string header)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, SortMode = DataGridViewColumnSortMode.NotSortable });
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
