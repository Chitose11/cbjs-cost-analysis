using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.UI
{
    internal sealed class OpenAnalysisForm : Form
    {
        private readonly ListBox _listBox;
        private readonly List<CostAnalysisSummary> _items;

        public int? SelectedAnalysisId { get; private set; }

        public OpenAnalysisForm(List<CostAnalysisSummary> items)
        {
            _items = items;
            Text = "打开成本分析单";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(520, 360);
            MinimizeBox = false;
            MaximizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "选择要打开的成本分析单",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = "DisplayText"
            };
            foreach (var item in _items)
            {
                _listBox.Items.Add(item);
            }
            if (_listBox.Items.Count > 0)
            {
                _listBox.SelectedIndex = 0;
            }
            _listBox.DoubleClick += (_, __) => ConfirmSelection();
            root.Controls.Add(_listBox, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(buttons, 0, 2);

            var ok = new Button { Text = "打开", Width = 82, Height = 30 };
            ok.Click += (_, __) => ConfirmSelection();
            buttons.Controls.Add(ok);

            var cancel = new Button { Text = "取消", Width = 82, Height = 30 };
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);
        }

        private void ConfirmSelection()
        {
            var selected = _listBox.SelectedItem as CostAnalysisSummary;
            if (selected == null)
            {
                return;
            }

            SelectedAnalysisId = selected.Id;
            DialogResult = DialogResult.OK;
        }
    }
}
