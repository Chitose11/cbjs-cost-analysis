using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class PriceWarningReportForm : MetroForm
    {
        private readonly MetroGrid _grid;
        private readonly MetroButton _locateButton;
        private readonly MetroButton _historyButton;

        public int SelectedRowIndex { get; private set; }
        public string SelectedItemTitle { get; private set; }
        public string SelectedMaterialCode { get; private set; }
        public string SelectedMaterialName { get; private set; }
        public bool SettingsChanged { get; private set; }
        public bool HistoryRequested { get; private set; }

        public PriceWarningReportForm(DataTable warnings)
        {
            SelectedRowIndex = -1;
            Text = "价格预警报告";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 560);
            MinimumSize = new Size(760, 460);
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
                Text = "供应商比价与涨价预警",
                ForeColor = Color.FromArgb(29, 29, 31),
                Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            root.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "红色优先处理；双击预警或点击“定位明细”可回到对应成本卡片。",
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            _grid = BuildGrid();
            _grid.DataSource = warnings;
            ConfigureColumns();
            _grid.CellFormatting += OnCellFormatting;
            _grid.CellDoubleClick += (_, __) => LocateSelectedWarning();
            _grid.SelectionChanged += (_, __) => UpdateLocateButton();
            root.Controls.Add(_grid, 0, 2);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.White
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var settings = CreateButton("阈值设置", false);
            settings.Click += (_, __) => OpenSettings();
            actions.Controls.Add(settings, 1, 0);

            _historyButton = CreateButton("查看历史", false);
            _historyButton.Click += (_, __) => RequestHistory();
            actions.Controls.Add(_historyButton, 2, 0);

            var close = CreateButton("关闭", false);
            close.Click += (_, __) => DialogResult = DialogResult.Cancel;
            actions.Controls.Add(close, 3, 0);

            _locateButton = CreateButton("定位明细", true);
            _locateButton.Click += (_, __) => LocateSelectedWarning();
            actions.Controls.Add(_locateButton, 4, 0);
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
            grid.RowTemplate.Height = 36;
            return grid;
        }

        private void ConfigureColumns()
        {
            if (_grid.Columns.Contains("RowIndex"))
            {
                _grid.Columns["RowIndex"].Visible = false;
            }

            if (_grid.Columns.Contains("MaterialCode"))
            {
                _grid.Columns["MaterialCode"].Visible = false;
            }

            if (_grid.Columns.Contains("MaterialName"))
            {
                _grid.Columns["MaterialName"].Visible = false;
            }

            SetColumnWidth("级别", 72);
            SetColumnWidth("明细", 230);
            SetColumnWidth("供应商", 150);
            SetColumnWidth("采购单价", 90);

            if (_grid.Columns.Contains("预警原因"))
            {
                _grid.Columns["预警原因"].Width = 260;
                _grid.Columns["预警原因"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }

            if (_grid.Columns.Contains("历史依据"))
            {
                _grid.Columns["历史依据"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _grid.Columns["历史依据"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            }
        }

        private void SetColumnWidth(string columnName, int width)
        {
            if (_grid.Columns.Contains(columnName))
            {
                _grid.Columns[columnName].Width = width;
            }
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || !_grid.Columns.Contains("级别"))
            {
                return;
            }

            var severity = Convert.ToString(_grid.Rows[e.RowIndex].Cells["级别"].Value);
            if (severity == "红色")
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 241, 240);
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(115, 28, 28);
            }
            else if (severity == "黄色")
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 219);
                _grid.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.FromArgb(110, 77, 0);
            }

            if (_grid.Columns.Contains("历史依据"))
            {
                var evidence = Convert.ToString(_grid.Rows[e.RowIndex].Cells["历史依据"].Value);
                _grid.Rows[e.RowIndex].Cells["历史依据"].ToolTipText = evidence ?? string.Empty;
            }

            if (_grid.Columns.Contains("预警原因"))
            {
                var message = Convert.ToString(_grid.Rows[e.RowIndex].Cells["预警原因"].Value);
                _grid.Rows[e.RowIndex].Cells["预警原因"].ToolTipText = message ?? string.Empty;
            }
        }

        private void UpdateLocateButton()
        {
            var hasSelection = ReadSelectedRowIndex() >= 0;
            _locateButton.Enabled = hasSelection;
            _historyButton.Enabled = hasSelection && (!string.IsNullOrWhiteSpace(ReadSelectedMaterialCode()) || !string.IsNullOrWhiteSpace(ReadSelectedMaterialName()));
        }

        private void LocateSelectedWarning()
        {
            var rowIndex = ReadSelectedRowIndex();
            if (rowIndex < 0)
            {
                return;
            }

            SelectedRowIndex = rowIndex;
            SelectedItemTitle = ReadSelectedItemTitle();
            DialogResult = DialogResult.OK;
        }

        private void RequestHistory()
        {
            var rowIndex = ReadSelectedRowIndex();
            if (rowIndex < 0)
            {
                return;
            }

            SelectedRowIndex = rowIndex;
            SelectedItemTitle = ReadSelectedItemTitle();
            SelectedMaterialCode = ReadSelectedMaterialCode();
            SelectedMaterialName = ReadSelectedMaterialName();
            HistoryRequested = true;
            DialogResult = DialogResult.Yes;
        }

        private void OpenSettings()
        {
            using (var form = new PriceWarningSettingsForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    SettingsChanged = true;
                    DialogResult = DialogResult.Retry;
                }
            }
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

        private string ReadSelectedItemTitle()
        {
            if (_grid.CurrentRow == null || !_grid.Columns.Contains("明细"))
            {
                return string.Empty;
            }

            var value = _grid.CurrentRow.Cells["明细"].Value;
            return value == null ? string.Empty : value.ToString();
        }

        private string ReadSelectedMaterialCode()
        {
            return ReadSelectedHiddenText("MaterialCode");
        }

        private string ReadSelectedMaterialName()
        {
            return ReadSelectedHiddenText("MaterialName");
        }

        private string ReadSelectedHiddenText(string columnName)
        {
            if (_grid.CurrentRow == null || !_grid.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            var value = _grid.CurrentRow.Cells[columnName].Value;
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
