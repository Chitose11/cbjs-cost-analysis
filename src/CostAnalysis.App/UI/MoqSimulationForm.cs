using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class MoqSimulationForm : Form
    {
        private readonly List<MoqSimulationResult> _results;
        private readonly DataGridView _grid;

        public MoqSimulationForm(List<MoqSimulationResult> results)
        {
            _results = results ?? new List<MoqSimulationResult>();
            Text = "最低起订量模拟器";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1220, 660);
            MinimumSize = new Size(980, 560);
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);

            _grid = BuildGrid();
            root.Controls.Add(_grid, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var closeButton = CreateButton("关闭", false);
            closeButton.Click += (_, __) => DialogResult = DialogResult.OK;
            buttons.Controls.Add(closeButton);
            root.Controls.Add(buttons, 0, 2);

            LoadRows();
        }

        private Control BuildHeader()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 8, 14, 8) };
            var title = new Label
            {
                Text = "如果...那么 起订量模拟",
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 29, 31)
            };
            var subtitle = new Label
            {
                Text = "按当前明细的阶梯价枚举临界数量，比较当前整单成本与模拟整单成本；正数表示省钱，负数表示加钱。",
                Dock = DockStyle.Bottom,
                Height = 22,
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

            AddColumn(grid, "TargetQuantity", "模拟数量");
            AddColumn(grid, "CurrentTotal", "当前整单成本");
            AddColumn(grid, "ProposedTotal", "模拟整单成本");
            AddColumn(grid, "SavingAmount", "节省/增加");
            AddColumn(grid, "QuantityIncrease", "增加数量");
            AddColumn(grid, "AffectedRowCount", "影响明细");
            AddColumn(grid, "CheaperRowCount", "降价明细");
            AddColumn(grid, "Recommendation", "建议");
            AddColumn(grid, "Detail", "明细变化");
            return grid;
        }

        private void LoadRows()
        {
            foreach (var result in _results)
            {
                var index = _grid.Rows.Add();
                var row = _grid.Rows[index];
                row.Cells["TargetQuantity"].Value = Format(result.TargetQuantity);
                row.Cells["CurrentTotal"].Value = Format(result.CurrentTotal);
                row.Cells["ProposedTotal"].Value = Format(result.ProposedTotal);
                row.Cells["SavingAmount"].Value = Format(result.SavingAmount);
                row.Cells["QuantityIncrease"].Value = Format(result.QuantityIncrease);
                row.Cells["AffectedRowCount"].Value = result.AffectedRowCount;
                row.Cells["CheaperRowCount"].Value = result.CheaperRowCount;
                row.Cells["Recommendation"].Value = result.Recommendation;
                row.Cells["Detail"].Value = result.Detail;

                if (result.SavingAmount > 0)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(232, 255, 238);
                }
                else if (result.CheaperRowCount > 0)
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 219);
                }
            }

            if (_grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells["TargetQuantity"];
            }
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

        private static string Format(decimal value)
        {
            return value.ToString("0.####");
        }
    }
}
