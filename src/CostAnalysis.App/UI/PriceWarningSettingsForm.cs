using System;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class PriceWarningSettingsForm : MetroForm
    {
        private readonly MetroTextBox _sameYellowTextBox;
        private readonly MetroTextBox _sameRedTextBox;
        private readonly MetroTextBox _lowerYellowTextBox;
        private readonly MetroTextBox _lowerRedTextBox;
        private readonly MetroTextBox _monthsTextBox;

        public PriceWarningSettingsForm()
        {
            Text = "价格预警设置";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 390);
            MinimumSize = new Size(520, 360);
            Style = MetroColorStyle.Blue;
            Theme = MetroThemeStyle.Light;
            ShadowType = MetroFormShadowType.DropShadow;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(22),
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 178));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(root);

            root.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "设置价格预警触发阈值",
                ForeColor = Color.FromArgb(29, 29, 31),
                Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.White
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 5; i++)
            {
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            }
            root.Controls.Add(fields, 0, 1);

            _sameYellowTextBox = AddField(fields, 0, "同供应商黄灯涨幅 %");
            _sameRedTextBox = AddField(fields, 1, "同供应商红灯涨幅 %");
            _lowerYellowTextBox = AddField(fields, 2, "其他供应商低价黄灯差 %");
            _lowerRedTextBox = AddField(fields, 3, "其他供应商低价红灯差 %");
            _monthsTextBox = AddField(fields, 4, "历史参考月份（0=不限）");

            root.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "例如红灯涨幅 10 表示当前价比历史价高 10% 或以上时标红。保存后主表校验和价格预警报告会立即使用新规则。",
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.TopLeft
            }, 0, 2);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.White
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var defaults = CreateButton("恢复默认", false);
            defaults.Click += (_, __) => LoadToForm(PriceWarningSettings.Defaults());
            actions.Controls.Add(defaults, 1, 0);

            var cancel = CreateButton("取消", false);
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            actions.Controls.Add(cancel, 2, 0);

            var save = CreateButton("保存", true);
            save.Click += (_, __) => SaveSettings();
            actions.Controls.Add(save, 3, 0);
            root.Controls.Add(actions, 0, 3);

            LoadToForm(new PriceWarningSettingsRepository().Get());
        }

        private static MetroTextBox AddField(TableLayoutPanel panel, int row, string label)
        {
            panel.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = label,
                ForeColor = Color.FromArgb(29, 29, 31),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);

            var textBox = new MetroTextBox
            {
                Dock = DockStyle.Fill,
                UseSelectable = true,
                Margin = new Padding(0, 2, 0, 4)
            };
            panel.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private void LoadToForm(PriceWarningSettings settings)
        {
            settings = settings ?? PriceWarningSettings.Defaults();
            _sameYellowTextBox.Text = FormatPercentValue(settings.SameSupplierYellowRate);
            _sameRedTextBox.Text = FormatPercentValue(settings.SameSupplierRedRate);
            _lowerYellowTextBox.Text = FormatPercentValue(settings.LowerSupplierYellowRate);
            _lowerRedTextBox.Text = FormatPercentValue(settings.LowerSupplierRedRate);
            _monthsTextBox.Text = settings.HistoryMonths.ToString();
        }

        private void SaveSettings()
        {
            decimal sameYellow;
            decimal sameRed;
            decimal lowerYellow;
            decimal lowerRed;
            int months;
            if (!TryReadPercent(_sameYellowTextBox.Text, out sameYellow) ||
                !TryReadPercent(_sameRedTextBox.Text, out sameRed) ||
                !TryReadPercent(_lowerYellowTextBox.Text, out lowerYellow) ||
                !TryReadPercent(_lowerRedTextBox.Text, out lowerRed) ||
                !int.TryParse((_monthsTextBox.Text ?? string.Empty).Trim(), out months))
            {
                MessageBox.Show(this, "请填写有效数字。百分比输入 10 表示 10%。", "价格预警设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var settings = new PriceWarningSettings
            {
                SameSupplierYellowRate = sameYellow,
                SameSupplierRedRate = sameRed,
                LowerSupplierYellowRate = lowerYellow,
                LowerSupplierRedRate = lowerRed,
                HistoryMonths = months
            }.Normalize();
            new PriceWarningSettingsRepository().Save(settings);
            DialogResult = DialogResult.OK;
        }

        private static bool TryReadPercent(string text, out decimal rate)
        {
            rate = 0;
            decimal value;
            if (!decimal.TryParse((text ?? string.Empty).Trim().TrimEnd('%'), out value))
            {
                return false;
            }

            rate = value / 100m;
            return rate >= 0;
        }

        private static string FormatPercentValue(decimal rate)
        {
            return (rate * 100m).ToString("0.####");
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
