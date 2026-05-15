using System.Drawing;
using System.Windows.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class AiRequestPreviewForm : Form
    {
        public AiRequestPreviewForm(string requestJson)
            : this("AI 请求预览", requestJson)
        {
        }

        public AiRequestPreviewForm(string title, string content)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(900, 640);
            Font = new Font("Consolas", 9F);

            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                ReadOnly = true,
                Text = content
            };
            Controls.Add(textBox);
        }
    }
}
