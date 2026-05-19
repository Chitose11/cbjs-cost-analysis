using System;
using System.Drawing;
using System.Windows.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class PasteRawTextForm : Form
    {
        private readonly TextBox _textBox;

        public string RawText { get; private set; }

        public PasteRawTextForm()
        {
            Text = "粘贴原始文本";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 520);
            MinimumSize = new Size(620, 420);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(245, 245, 247);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "把 PDF 复制文本、图片 OCR 文本或手工整理的报价内容粘贴到这里。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(110, 110, 115)
            }, 0, 0);

            _textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            root.Controls.Add(_textBox, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var ok = CreateButton("确定", true);
            ok.Click += OnOk;
            var cancel = CreateButton("取消", false);
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);
        }

        private void OnOk(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_textBox.Text))
            {
                MessageBox.Show(this, "请先粘贴文本。", "粘贴原始文本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RawText = _textBox.Text;
            DialogResult = DialogResult.OK;
        }

        private static Button CreateButton(string text, bool primary)
        {
            var button = new Button
            {
                Text = text,
                Width = 86,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 6, 0, 4),
                BackColor = primary ? Color.FromArgb(0, 102, 204) : Color.White,
                ForeColor = primary ? Color.White : Color.FromArgb(0, 102, 204)
            };
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(0, 102, 204) : Color.FromArgb(210, 210, 215);
            return button;
        }
    }
}
