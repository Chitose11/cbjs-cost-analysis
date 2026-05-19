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

        public ProcessCostRulesForm()
        {
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

        private void AddColumn(string name, string header)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, SortMode = DataGridViewColumnSortMode.NotSortable });
        }
    }
}
