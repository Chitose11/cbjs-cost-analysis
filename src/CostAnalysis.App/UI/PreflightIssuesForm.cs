using System.Data;
using System.Drawing;
using System.Windows.Forms;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class PreflightIssuesForm : MetroForm
    {
        private readonly MetroGrid _grid;
        private readonly MetroButton _locateButton;

        public int SelectedRowIndex { get; private set; }
        public string SelectedIssueTitle { get; private set; }

        public PreflightIssuesForm(DataTable issues)
        {
            SelectedRowIndex = -1;
            Text = "问题清单";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 520);
            MinimumSize = new Size(640, 420);
            Style = MetroColorStyle.Blue;
            Theme = MetroThemeStyle.Light;
            ShadowType = MetroFormShadowType.DropShadow;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(18),
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(root);

            root.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "保存或导出前建议先处理这些问题",
                ForeColor = Color.FromArgb(29, 29, 31),
                Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            root.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "双击一条问题，或选中后点击“定位明细”，软件会展开对应成本卡片。",
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            _grid = BuildGrid();
            _grid.DataSource = issues;
            ConfigureColumns();
            _grid.CellDoubleClick += (_, __) => LocateSelectedIssue();
            _grid.SelectionChanged += (_, __) => UpdateLocateButton();
            root.Controls.Add(_grid, 0, 2);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.White
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var close = CreateButton("关闭", false);
            close.Click += (_, __) => DialogResult = DialogResult.Cancel;
            actions.Controls.Add(close, 1, 0);

            _locateButton = CreateButton("定位明细", true);
            _locateButton.Click += (_, __) => LocateSelectedIssue();
            actions.Controls.Add(_locateButton, 2, 0);
            root.Controls.Add(actions, 0, 3);

            UpdateLocateButton();
        }

        private static MetroGrid BuildGrid()
        {
            var grid = new MetroGrid
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                GridColor = Color.FromArgb(225, 229, 234),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                Style = MetroColorStyle.Blue,
                Theme = MetroThemeStyle.Light
            };
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(22, 119, 255);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 244, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(29, 29, 31);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252);
            grid.RowTemplate.Height = 34;
            return grid;
        }

        private void ConfigureColumns()
        {
            if (_grid.Columns.Contains("RowIndex"))
            {
                _grid.Columns["RowIndex"].Visible = false;
            }

            if (_grid.Columns.Contains("位置"))
            {
                _grid.Columns["位置"].Width = 210;
            }

            if (_grid.Columns.Contains("问题"))
            {
                _grid.Columns["问题"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _grid.Columns["问题"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }
        }

        private void UpdateLocateButton()
        {
            _locateButton.Enabled = ReadSelectedRowIndex() >= 0;
        }

        private void LocateSelectedIssue()
        {
            var rowIndex = ReadSelectedRowIndex();
            if (rowIndex < 0)
            {
                return;
            }

            SelectedRowIndex = rowIndex;
            SelectedIssueTitle = ReadSelectedIssueTitle();
            DialogResult = DialogResult.OK;
        }

        private int ReadSelectedRowIndex()
        {
            if (_grid.CurrentRow == null || !_grid.Columns.Contains("RowIndex"))
            {
                return -1;
            }

            var value = _grid.CurrentRow.Cells["RowIndex"].Value;
            int rowIndex;
            return value != null && int.TryParse(value.ToString(), out rowIndex) ? rowIndex : -1;
        }

        private string ReadSelectedIssueTitle()
        {
            if (_grid.CurrentRow == null || !_grid.Columns.Contains("位置"))
            {
                return string.Empty;
            }

            var value = _grid.CurrentRow.Cells["位置"].Value;
            return value == null ? string.Empty : value.ToString();
        }

        private static MetroButton CreateButton(string text, bool primary)
        {
            return new MetroButton
            {
                Dock = DockStyle.Fill,
                Text = text,
                Style = primary ? MetroColorStyle.Blue : MetroColorStyle.Silver,
                Theme = MetroThemeStyle.Light,
                UseSelectable = true,
                Highlight = primary,
                FontSize = MetroButtonSize.Medium,
                Margin = new Padding(8, 8, 0, 8)
            };
        }
    }
}
