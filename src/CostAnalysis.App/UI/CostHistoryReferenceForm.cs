using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.UI
{
    internal sealed class CostHistoryReferenceForm : Form
    {
        private readonly List<CostHistoryItem> _items;
        private readonly DataGridView _grid;

        public CostHistoryItem SelectedItem { get; private set; }

        public CostHistoryReferenceForm(string materialCode, string materialName, List<CostHistoryItem> items)
        {
            _items = items ?? new List<CostHistoryItem>();
            Text = "历史成本参考";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 620);
            MinimumSize = new Size(920, 520);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(245, 245, 247);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(16),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(materialCode, materialName), 0, 0);

            _grid = BuildGrid();
            root.Controls.Add(_grid, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var applyButton = CreateButton("套用选中成本", true);
            applyButton.Click += OnApply;
            var closeButton = CreateButton("关闭", false);
            closeButton.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(applyButton);
            buttons.Controls.Add(closeButton);
            root.Controls.Add(buttons, 0, 2);

            LoadRows();
        }

        private Control BuildHeader(string materialCode, string materialName)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 8, 14, 8) };
            var title = new Label
            {
                Text = "历史成本参考",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 29, 31)
            };
            var subtitle = new Label
            {
                Text = "匹配条件：物料编码 " + EmptyText(materialCode) + "；物料名称 " + EmptyText(materialName),
                Dock = DockStyle.Bottom,
                Height = 20,
                ForeColor = Color.FromArgb(110, 110, 115)
            };
            panel.Controls.Add(title);
            panel.Controls.Add(subtitle);
            return panel;
        }

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                GridColor = Color.FromArgb(210, 210, 215),
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(230, 244, 255),
                    SelectionForeColor = Color.FromArgb(29, 29, 31)
                }
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(29, 29, 31);
            grid.EnableHeadersVisualStyles = false;
            grid.CellDoubleClick += (_, __) => ApplySelected();

            AddColumn(grid, "AnalysisNo", "分析单号");
            AddColumn(grid, "AnalysisDate", "分析日期");
            AddColumn(grid, "CustomerName", "客户");
            AddColumn(grid, "ProjectName", "项目");
            AddColumn(grid, "MaterialCode", "物料编码");
            AddColumn(grid, "MaterialName", "物料名称");
            AddColumn(grid, "Supplier", "供应商");
            AddColumn(grid, "BaseMaterialName", "材料名称");
            AddColumn(grid, "MaterialVendor", "材料厂家");
            AddColumn(grid, "MaterialUnitPrice", "材料单价");
            AddColumn(grid, "GramWeight", "克重");
            AddColumn(grid, "ExpandedSize", "展开尺寸");
            AddColumn(grid, "MaterialCost", "材料费");
            AddColumn(grid, "PrintingCost", "印刷费");
            AddColumn(grid, "PostProcessCost", "后工序费");
            AddColumn(grid, "OtherCost", "其他");
            AddColumn(grid, "PurchaseUnitPrice", "采购单价");
            AddColumn(grid, "TotalQuantity", "历史用量");
            AddColumn(grid, "TotalPrice", "历史总价");
            AddColumn(grid, "TierCount", "阶梯价");
            return grid;
        }

        private void LoadRows()
        {
            foreach (var item in _items)
            {
                var index = _grid.Rows.Add();
                var row = _grid.Rows[index];
                row.Tag = item;
                row.Cells["AnalysisNo"].Value = item.AnalysisNo;
                row.Cells["AnalysisDate"].Value = string.IsNullOrWhiteSpace(item.AnalysisDate) ? item.CreatedAt : item.AnalysisDate;
                row.Cells["CustomerName"].Value = item.CustomerName;
                row.Cells["ProjectName"].Value = item.ProjectName;
                row.Cells["MaterialCode"].Value = item.MaterialCode;
                row.Cells["MaterialName"].Value = item.MaterialName;
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
                row.Cells["TierCount"].Value = item.PriceTiers == null || item.PriceTiers.Count == 0 ? string.Empty : item.PriceTiers.Count + "档";
            }

            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells["AnalysisNo"];
            }
        }

        private void OnApply(object sender, EventArgs e)
        {
            ApplySelected();
        }

        private void ApplySelected()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Tag == null)
            {
                MessageBox.Show(this, "请先选择一条历史成本记录。", "历史成本参考", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedItem = _grid.CurrentRow.Tag as CostHistoryItem;
            DialogResult = SelectedItem == null ? DialogResult.Cancel : DialogResult.OK;
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

        private static Button CreateButton(string text, bool primary)
        {
            var button = new Button
            {
                Text = text,
                Width = primary ? 128 : 92,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 6, 0, 6),
                BackColor = primary ? Color.FromArgb(0, 102, 204) : Color.White,
                ForeColor = primary ? Color.White : Color.FromArgb(0, 102, 204)
            };
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(0, 102, 204) : Color.FromArgb(210, 210, 215);
            return button;
        }

        private static string FormatDecimal(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("0.####") : string.Empty;
        }

        private static string EmptyText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "（空）" : value.Trim();
        }
    }
}
