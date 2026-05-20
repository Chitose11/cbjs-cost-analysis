using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class OcrSettingsForm : Form
    {
        private readonly TextBox _popplerDirectoryTextBox;
        private readonly TextBox _pdftotextPathTextBox;
        private readonly TextBox _pdftoppmPathTextBox;
        private readonly TextBox _tesseractPathTextBox;
        private readonly TextBox _languageTextBox;
        private readonly TextBox _statusTextBox;

        public OcrSettingsForm()
        {
            Text = "PDF / OCR 设置";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 500);
            MinimumSize = new Size(700, 460);
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimizeBox = false;
            MaximizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(16),
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 255));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            for (var i = 0; i < 6; i++)
            {
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 0 ? 52 : 38));
            }
            root.Controls.Add(fields, 0, 0);

            var tip = new Label
            {
                Text = "软件已内置 Poppler 和 Tesseract，通常无需手动配置。这里只用于检测状态，或在内置工具不可用时指定其他 exe / 目录。",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.MiddleLeft
            };
            fields.Controls.Add(tip, 0, 0);
            fields.SetColumnSpan(tip, 3);

            _popplerDirectoryTextBox = AddPathField(fields, 1, "Poppler 目录", OnBrowsePopplerDirectory);
            _pdftotextPathTextBox = AddPathField(fields, 2, "pdftotext.exe", (_, __) => BrowseExe(_pdftotextPathTextBox, "pdftotext.exe"));
            _pdftoppmPathTextBox = AddPathField(fields, 3, "pdftoppm.exe", (_, __) => BrowseExe(_pdftoppmPathTextBox, "pdftoppm.exe"));
            _tesseractPathTextBox = AddPathField(fields, 4, "tesseract.exe", (_, __) => BrowseExe(_tesseractPathTextBox, "tesseract.exe"));

            fields.Controls.Add(Label("OCR 语言"), 0, 5);
            _languageTextBox = new TextBox { Dock = DockStyle.Fill };
            fields.Controls.Add(_languageTextBox, 1, 5);
            var languageHint = new Label
            {
                Text = "如 chi_sim+eng",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.MiddleLeft
            };
            fields.Controls.Add(languageHint, 2, 5);

            _statusTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(248, 248, 250)
            };
            root.Controls.Add(_statusTextBox, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            root.Controls.Add(buttons, 0, 2);

            var save = new Button { Text = "保存", Width = 90, Height = 30 };
            save.Click += OnSave;
            buttons.Controls.Add(save);

            var cancel = new Button { Text = "取消", Width = 90, Height = 30 };
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);

            var refresh = new Button { Text = "检测", Width = 90, Height = 30 };
            refresh.Click += (_, __) => RefreshStatus();
            buttons.Controls.Add(refresh);

            LoadSettings();
            RefreshStatus();
        }

        private void LoadSettings()
        {
            var settings = new OcrToolSettingsRepository().Get();
            _popplerDirectoryTextBox.Text = settings.PopplerDirectory;
            _pdftotextPathTextBox.Text = settings.PdftotextPath;
            _pdftoppmPathTextBox.Text = settings.PdftoppmPath;
            _tesseractPathTextBox.Text = settings.TesseractPath;
            _languageTextBox.Text = string.IsNullOrWhiteSpace(settings.TesseractLanguage) ? "chi_sim+eng" : settings.TesseractLanguage;
        }

        private void OnSave(object sender, EventArgs e)
        {
            var settings = ReadSettingsFromForm();
            new OcrToolSettingsRepository().Save(settings);
            RefreshStatus(settings);
            MessageBox.Show(this, "PDF / OCR 设置已保存。", "PDF / OCR 设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }

        private OcrToolSettings ReadSettingsFromForm()
        {
            return new OcrToolSettings
            {
                PopplerDirectory = _popplerDirectoryTextBox.Text.Trim(),
                PdftotextPath = _pdftotextPathTextBox.Text.Trim(),
                PdftoppmPath = _pdftoppmPathTextBox.Text.Trim(),
                TesseractPath = _tesseractPathTextBox.Text.Trim(),
                TesseractLanguage = string.IsNullOrWhiteSpace(_languageTextBox.Text) ? "chi_sim+eng" : _languageTextBox.Text.Trim()
            };
        }

        private void RefreshStatus()
        {
            RefreshStatus(ReadSettingsFromForm());
        }

        private void RefreshStatus(OcrToolSettings settings)
        {
            _statusTextBox.Text = new ExternalTextExtractionService(settings).GetToolStatusText() + Environment.NewLine + Environment.NewLine +
                                  "推荐便携目录：" + Environment.NewLine +
                                  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "poppler", "bin") + Environment.NewLine +
                                  Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "tesseract");
        }

        private TextBox AddPathField(TableLayoutPanel root, int row, string label, EventHandler browseHandler)
        {
            root.Controls.Add(Label(label), 0, row);
            var textBox = new TextBox { Dock = DockStyle.Fill };
            root.Controls.Add(textBox, 1, row);

            var browse = new Button { Text = "浏览", Dock = DockStyle.Fill };
            browse.Click += browseHandler;
            root.Controls.Add(browse, 2, row);
            return textBox;
        }

        private void OnBrowsePopplerDirectory(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择包含 pdftotext.exe / pdftoppm.exe 的 Poppler 目录";
                dialog.SelectedPath = Directory.Exists(_popplerDirectoryTextBox.Text) ? _popplerDirectoryTextBox.Text : string.Empty;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _popplerDirectoryTextBox.Text = dialog.SelectedPath;
                    RefreshStatus();
                }
            }
        }

        private void BrowseExe(TextBox target, string fileName)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 " + fileName;
                dialog.Filter = fileName + "|" + fileName + "|可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                dialog.FileName = fileName;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    target.Text = dialog.FileName;
                    RefreshStatus();
                }
            }
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
