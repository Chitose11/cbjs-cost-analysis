using System;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.UI
{
    internal sealed class AiSettingsForm : Form
    {
        private readonly CheckBox _enabledCheckBox;
        private readonly TextBox _apiUrlTextBox;
        private readonly TextBox _apiKeyTextBox;
        private readonly ComboBox _modelComboBox;
        private readonly NumericUpDown _timeoutInput;
        private readonly CheckBox _confirmCheckBox;
        private readonly CheckBox _customerNameCheckBox;
        private readonly CheckBox _supplierNameCheckBox;
        private readonly CheckBox _priceCheckBox;

        public AiSettingsForm()
        {
            Text = "AI 设置 - DeepSeek";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(640, 470);
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimizeBox = false;
            MaximizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(16)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            _enabledCheckBox = new CheckBox { Text = "启用 AI 辅助", Dock = DockStyle.Fill };
            root.Controls.Add(_enabledCheckBox, 1, 0);

            _apiUrlTextBox = AddTextField(root, 1, "API 地址");
            _apiKeyTextBox = AddTextField(root, 2, "API Key");
            _apiKeyTextBox.UseSystemPasswordChar = true;

            root.Controls.Add(Label("模型名称"), 0, 3);
            _modelComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            _modelComboBox.Items.Add("deepseek-v4-flash");
            _modelComboBox.Items.Add("deepseek-v4-pro");
            root.Controls.Add(_modelComboBox, 1, 3);

            root.Controls.Add(Label("超时时间"), 0, 4);
            _timeoutInput = new NumericUpDown { Dock = DockStyle.Left, Width = 120, Minimum = 10, Maximum = 300, Increment = 5 };
            root.Controls.Add(_timeoutInput, 1, 4);

            _confirmCheckBox = new CheckBox { Text = "每次调用前确认", Dock = DockStyle.Fill };
            root.Controls.Add(_confirmCheckBox, 1, 5);

            _customerNameCheckBox = new CheckBox { Text = "允许发送客户名称", Dock = DockStyle.Fill };
            root.Controls.Add(_customerNameCheckBox, 1, 6);

            _supplierNameCheckBox = new CheckBox { Text = "允许发送供应商名称", Dock = DockStyle.Fill };
            root.Controls.Add(_supplierNameCheckBox, 1, 7);

            _priceCheckBox = new CheckBox { Text = "允许发送价格", Dock = DockStyle.Fill };
            root.Controls.Add(_priceCheckBox, 1, 8);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            root.Controls.Add(buttons, 1, 9);

            var save = new Button { Text = "保存", Width = 86, Height = 30 };
            save.Click += OnSave;
            buttons.Controls.Add(save);

            var cancel = new Button { Text = "取消", Width = 86, Height = 30 };
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);

            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = new AiSettingsRepository().Get();
            _enabledCheckBox.Checked = settings.IsEnabled;
            _apiUrlTextBox.Text = string.IsNullOrWhiteSpace(settings.ApiUrl) ? "https://api.deepseek.com" : settings.ApiUrl;
            _apiKeyTextBox.Text = settings.ApiKey;
            _modelComboBox.Text = string.IsNullOrWhiteSpace(settings.ModelName) ? "deepseek-v4-flash" : settings.ModelName;
            _timeoutInput.Value = Math.Max(_timeoutInput.Minimum, Math.Min(_timeoutInput.Maximum, settings.TimeoutSeconds));
            _confirmCheckBox.Checked = settings.ConfirmBeforeCall;
            _customerNameCheckBox.Checked = settings.AllowCustomerName;
            _supplierNameCheckBox.Checked = settings.AllowSupplierName;
            _priceCheckBox.Checked = settings.AllowPrice;
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_apiUrlTextBox.Text))
            {
                MessageBox.Show(this, "请填写 API 地址。", "AI 设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            new AiSettingsRepository().Save(new AiSettings
            {
                IsEnabled = _enabledCheckBox.Checked,
                ApiUrl = _apiUrlTextBox.Text.Trim(),
                ApiKey = _apiKeyTextBox.Text,
                ModelName = _modelComboBox.Text.Trim(),
                TimeoutSeconds = (int)_timeoutInput.Value,
                ConfirmBeforeCall = _confirmCheckBox.Checked,
                AllowCustomerName = _customerNameCheckBox.Checked,
                AllowSupplierName = _supplierNameCheckBox.Checked,
                AllowPrice = _priceCheckBox.Checked
            });

            MessageBox.Show(this, "AI 设置已保存。", "AI 设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }

        private static TextBox AddTextField(TableLayoutPanel root, int row, string label)
        {
            root.Controls.Add(Label(label), 0, row);
            var textBox = new TextBox { Dock = DockStyle.Fill };
            root.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private static Label Label(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(110, 110, 115)
            };
        }
    }
}
