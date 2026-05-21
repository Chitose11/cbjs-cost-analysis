using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class EnvironmentCheckForm : Form
    {
        private readonly DataGridView _grid;
        private readonly Label _summaryLabel;

        public EnvironmentCheckForm()
        {
            Text = "运行环境检测";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 520);
            MinimumSize = new Size(720, 460);
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(16),
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            _summaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(29, 29, 31),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_summaryLabel, 0, 0);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", FillWeight = 14 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "项目", FillWeight = 22 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "检测值", FillWeight = 38 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "说明", FillWeight = 58 });
            root.Controls.Add(_grid, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(buttons, 0, 2);

            var close = new Button { Text = "关闭", Width = 90, Height = 30 };
            close.Click += (_, __) => DialogResult = DialogResult.OK;
            buttons.Controls.Add(close);

            var copy = new Button { Text = "复制结果", Width = 100, Height = 30 };
            copy.Click += OnCopy;
            buttons.Controls.Add(copy);

            var refresh = new Button { Text = "重新检测", Width = 100, Height = 30 };
            refresh.Click += (_, __) => LoadReport();
            buttons.Controls.Add(refresh);

            LoadReport();
        }

        public static void ShowStartupWarningIfNeeded(IWin32Window owner)
        {
            var report = new EnvironmentCheckService().Check();
            if (!report.HasFailure && !report.HasWarning)
            {
                return;
            }

            var message = report.BuildSummary() + Environment.NewLine + Environment.NewLine + BuildCompactText(report);
            MessageBox.Show(owner, message, "运行环境检测", MessageBoxButtons.OK, report.HasFailure ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void LoadReport()
        {
            var report = new EnvironmentCheckService().Check();
            _summaryLabel.Text = report.BuildSummary();
            _summaryLabel.ForeColor = report.HasFailure
                ? Color.FromArgb(200, 48, 44)
                : report.HasWarning ? Color.FromArgb(180, 120, 0) : Color.FromArgb(30, 130, 76);

            _grid.Rows.Clear();
            foreach (var item in report.Items)
            {
                var rowIndex = _grid.Rows.Add(StatusText(item.Status), item.Name, item.Value, item.Message);
                var row = _grid.Rows[rowIndex];
                row.DefaultCellStyle.BackColor = StatusBackColor(item.Status);
            }
        }

        private void OnCopy(object sender, EventArgs e)
        {
            var report = new EnvironmentCheckService().Check();
            Clipboard.SetText(BuildFullText(report));
            MessageBox.Show(this, "环境检测结果已复制。", "运行环境检测", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string BuildCompactText(EnvironmentCheckReport report)
        {
            var sb = new StringBuilder();
            foreach (var item in report.Items)
            {
                if (item.Status == EnvironmentCheckStatus.Pass)
                {
                    continue;
                }

                sb.AppendLine(StatusText(item.Status) + " " + item.Name + "：" + item.Message);
            }

            return sb.ToString().TrimEnd();
        }

        private static string BuildFullText(EnvironmentCheckReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine(report.BuildSummary());
            sb.AppendLine();
            foreach (var item in report.Items)
            {
                sb.AppendLine(StatusText(item.Status) + " " + item.Name);
                sb.AppendLine("检测值：" + item.Value);
                sb.AppendLine("说明：" + item.Message);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private static string StatusText(EnvironmentCheckStatus status)
        {
            if (status == EnvironmentCheckStatus.Fail) return "失败";
            if (status == EnvironmentCheckStatus.Warning) return "提醒";
            return "通过";
        }

        private static Color StatusBackColor(EnvironmentCheckStatus status)
        {
            if (status == EnvironmentCheckStatus.Fail) return Color.FromArgb(255, 235, 235);
            if (status == EnvironmentCheckStatus.Warning) return Color.FromArgb(255, 249, 219);
            return Color.White;
        }
    }
}
