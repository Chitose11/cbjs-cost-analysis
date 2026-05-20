using System;
using System.Drawing;
using System.Windows.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class BatchGridEditForm : Form
    {
        private readonly ComboBox _fieldCombo;
        private readonly TextBox _valueTextBox;
        private readonly CheckBox _onlyEmptyCheckBox;

        public BatchGridEditForm(string title, BatchEditField[] fields)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(420, 220);
            Font = new Font("Microsoft YaHei UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 2,
                RowCount = 4
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(new Label { Text = "字段", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _fieldCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _fieldCombo.DisplayMember = "DisplayName";
            _fieldCombo.ValueMember = "ColumnName";
            _fieldCombo.Items.AddRange(fields);
            if (_fieldCombo.Items.Count > 0)
            {
                _fieldCombo.SelectedIndex = 0;
            }
            root.Controls.Add(_fieldCombo, 1, 0);

            root.Controls.Add(new Label { Text = "新值", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _valueTextBox = new TextBox { Dock = DockStyle.Fill };
            root.Controls.Add(_valueTextBox, 1, 1);

            _onlyEmptyCheckBox = new CheckBox { Text = "只填空白单元格", Dock = DockStyle.Fill };
            root.Controls.Add(_onlyEmptyCheckBox, 1, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(buttons, 0, 3);
            root.SetColumnSpan(buttons, 2);

            var ok = new Button { Text = "应用", Width = 86, Height = 30, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", Width = 86, Height = 30, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public string ColumnName
        {
            get
            {
                var field = _fieldCombo.SelectedItem as BatchEditField;
                return field == null ? string.Empty : field.ColumnName;
            }
        }

        public string NewValue
        {
            get { return _valueTextBox.Text; }
        }

        public bool OnlyEmpty
        {
            get { return _onlyEmptyCheckBox.Checked; }
        }
    }

    internal sealed class BatchEditField
    {
        public BatchEditField(string columnName, string displayName)
        {
            ColumnName = columnName;
            DisplayName = displayName;
        }

        public string ColumnName { get; private set; }
        public string DisplayName { get; private set; }
    }
}
