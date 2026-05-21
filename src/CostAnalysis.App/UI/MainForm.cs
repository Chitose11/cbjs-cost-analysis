using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class MainForm : MetroForm
    {
        private readonly Color _canvas = Color.FromArgb(244, 248, 255);
        private readonly Color _panel = Color.White;
        private readonly Color _ink = Color.FromArgb(29, 29, 31);
        private readonly Color _muted = Color.FromArgb(110, 110, 115);
        private readonly Color _blue = Color.FromArgb(24, 111, 232);
        private readonly Color _warningBack = Color.FromArgb(255, 249, 219);
        private readonly Color _missingBack = Color.FromArgb(255, 235, 235);
        private readonly Color _selectedBack = Color.FromArgb(230, 244, 255);
        private readonly ToolTip _commandTips = new ToolTip();

        private readonly MetroGrid _grid;
        private readonly Control _detailCard;
        private readonly Control _detailCardsArea;
        private readonly MetroLabel _statusLabel;
        private readonly MetroTextBox _analysisNoTextBox;
        private readonly MetroTextBox _customerTextBox;
        private readonly MetroTextBox _projectTextBox;
        private readonly MetroTextBox _dateTextBox;
        private readonly MetroTextBox _taxTextBox;
        private readonly MetroTextBox _freightTextBox;
        private readonly Dictionary<string, MetroTextBox> _detailFields = new Dictionary<string, MetroTextBox>();
        private MetroCheckBox _detailSelectedCheckBox;
        private MetroLabel _detailTitleLabel;
        private MetroLabel _detailStatusLabel;
        private MetroPanel _detailCardsPanel;
        private bool _syncingDetailCard;
        private bool _refreshingDetailCards;
        private bool _allDetailCardsCollapsed;
        private ListBox _recentAnalysesListBox;

        public MainForm()
        {
            Text = "成本分析软件";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            Style = MetroColorStyle.Blue;
            Theme = MetroThemeStyle.Light;
            ShadowType = MetroFormShadowType.DropShadow;
            BackColor = _canvas;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = _canvas,
                Padding = new Padding(16)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(BuildSidebar(), 0, 0);
            root.Controls.Add(BuildWorkspace(), 1, 0);

            _analysisNoTextBox = FindHeaderTextBox(root, "AnalysisNo");
            _customerTextBox = FindHeaderTextBox(root, "CustomerName");
            _projectTextBox = FindHeaderTextBox(root, "ProjectName");
            _dateTextBox = FindHeaderTextBox(root, "AnalysisDate");
            _taxTextBox = FindHeaderTextBox(root, "TaxNote");
            _freightTextBox = FindHeaderTextBox(root, "FreightNote");
            _dateTextBox.Text = DateTime.Now.ToString("yyyy-MM-dd");
            _taxTextBox.Text = "含税";
            _freightTextBox.Text = "含运";

            _grid = BuildGrid();
            _detailCard = BuildDetailCard();
            _detailCardsArea = BuildDetailCardsArea();
            _statusLabel = CreateMetroLabel()
            {
                Dock = DockStyle.Fill,
                Text = "就绪。本原型已初始化本地 SQLite 数据库。",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var workspace = (MetroPanel)root.GetControlFromPosition(1, 0);
            var layout = (TableLayoutPanel)workspace.Controls[0];
            layout.Controls.Add(_detailCardsArea, 0, 3);
            layout.Controls.Add(_grid, 0, 4);
            layout.Controls.Add(_statusLabel, 0, 5);
            _grid.Visible = true;
            RefreshRecentAnalysesList();
            RefreshDetailCards();
        }

        private Control BuildSidebar()
        {
            var sidebar = new MetroPanel
            {
                Dock = DockStyle.Fill,
                BackColor = _panel,
                UseCustomBackColor = true,
                Padding = new Padding(16)
            };
            ApplyRoundedRegion(sidebar, 14);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                BackColor = _panel
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 420));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            sidebar.Controls.Add(layout);

            var title = CreateMetroLabel()
            {
                Text = "成本分析",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(title, 0, 0);

            var menu = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0),
                AutoScroll = true
            };
            layout.Controls.Add(menu, 0, 1);

            AddMenuButton(menu, "成本分析列表", (_, __) => RefreshRecentAnalysesList(), true);
            AddMenuButton(menu, "报价单导入", OnImportQuote);
            AddMenuButton(menu, "批量预扫描", OnBatchScanQuotes);
            AddMenuButton(menu, "材料库", OnOpenMaterials);
            AddMenuButton(menu, "工艺规则", OnOpenProcessRules);
            AddMenuButton(menu, "系统设置", OnOpenAiSettings);
            AddMenuButton(menu, "OCR设置", OnOpenOcrSettings);
            AddMenuButton(menu, "环境检测", OnOpenEnvironmentCheck);

            layout.Controls.Add(CreateMetroLabel()
            {
                Text = "最近单据",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = _muted
            }, 0, 2);

            _recentAnalysesListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                DisplayMember = "DisplayText",
                IntegralHeight = false
            };
            _recentAnalysesListBox.DoubleClick += OnRecentAnalysisDoubleClick;
            layout.Controls.Add(_recentAnalysesListBox, 0, 3);

            var refresh = CreateMetroButton("刷新列表", false);
            refresh.Dock = DockStyle.Fill;
            refresh.Margin = new Padding(0, 8, 0, 0);
            refresh.Click += (_, __) => RefreshRecentAnalysesList();
            layout.Controls.Add(refresh, 0, 4);

            return sidebar;
        }

        private MetroPanel BuildWorkspace()
        {
            var workspace = new MetroPanel
            {
                Dock = DockStyle.Fill,
                BackColor = _panel,
                UseCustomBackColor = true,
                Padding = new Padding(24)
            };
            ApplyRoundedRegion(workspace, 16);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = _panel
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 144));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            workspace.Controls.Add(layout);

            var headingRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = _panel
            };
            headingRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headingRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));

            var heading = CreateMetroLabel()
            {
                Text = "客户成本分析表",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            headingRow.Controls.Add(heading, 0, 0);

            var quickActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = _panel
            };
            AddSmallActionButton(quickActions, "保存分析单", OnSaveAnalysis, true);
            AddSmallActionButton(quickActions, "清空", OnClearAnalysis, false);
            headingRow.Controls.Add(quickActions, 1, 0);
            layout.Controls.Add(headingRow, 0, 0);

            layout.Controls.Add(BuildHeaderPanel(), 0, 1);

            layout.Controls.Add(BuildCommandBar(), 0, 2);
            return workspace;
        }

        private Control BuildCommandBar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0, 4, 0, 8)
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));

            var importGroup = CreateCommandGroup("数据导入");
            AddCommandButton(importGroup, "导入报价", OnImportQuote, true, "导入单个报价单并进入确认页");
            AddCommandButton(importGroup, "批量预扫", OnBatchScanQuotes, false, "扫描文件夹内多个报价单");
            bar.Controls.Add(importGroup.Parent, 0, 0);

            var detailGroup = CreateCommandGroup("明细操作");
            AddCommandButton(detailGroup, "新增", OnAddRow, false, "新增一条空白成本明细");
            AddCommandButton(detailGroup, "删除", OnDeleteSelectedRows, false, "删除已勾选的明细行");
            AddCommandButton(detailGroup, "阶梯价", OnEditRowPriceTiers, false, "编辑当前明细的阶梯价格");
            bar.Controls.Add(detailGroup.Parent, 1, 0);

            var costGroup = CreateCommandGroup("成本工具");
            AddCommandButton(costGroup, "应用规则", OnApplyProcessRules, false, "按工艺规则自动填充空白费用");
            AddCommandButton(costGroup, "历史参考", OnOpenCostHistoryReference, false, "查看历史成本并套用参考值");
            AddCommandButton(costGroup, "AI补全", OnAiCompleteCosts, false, "调用 AI 补全空白成本金额");
            AddCommandButton(costGroup, "MOQ模拟", OnOpenMoqSimulation, false, "模拟阶梯起订量对整单成本的影响");
            bar.Controls.Add(costGroup.Parent, 2, 0);

            var fileGroup = CreateCommandGroup("文件");
            AddCommandButton(fileGroup, "保存", OnSaveAnalysis, false, "保存当前成本分析");
            AddCommandButton(fileGroup, "打开", OnOpenAnalysis, false, "打开历史成本分析");
            AddCommandButton(fileGroup, "导出", OnExportExcelPlaceholder, false, "导出给客户的 Excel 文件");
            bar.Controls.Add(fileGroup.Parent, 3, 0);

            return bar;
        }

        private TableLayoutPanel CreateCommandGroup(string title)
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.FromArgb(250, 250, 252),
                Padding = new Padding(12, 8, 12, 10),
                Margin = new Padding(0, 0, 10, 0)
            };
            ApplyRoundedRegion(shell, 12);
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            shell.Controls.Add(CreateMetroLabel()
            {
                Dock = DockStyle.Fill,
                Text = title,
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var buttonGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttonGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            buttonGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            shell.Controls.Add(buttonGrid, 0, 1);
            return buttonGrid;
        }

        private void AddCommandButton(TableLayoutPanel panel, string text, EventHandler handler, bool primary, string tip)
        {
            var button = CreateMetroButton(text, primary);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 4, 8, 4);
            button.Click += handler;
            _commandTips.SetToolTip(button, tip);
            var index = panel.Controls.Count;
            var row = index / 2;
            if (row >= panel.RowCount)
            {
                panel.RowCount++;
                panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            }

            panel.Controls.Add(button, index % 2, row);
        }

        private Control BuildHeaderPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 2,
                BackColor = Color.White
            };
            for (var i = 0; i < 6; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F));
            }
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            AddHeaderField(panel, 0, "分析单号", "AnalysisNo");
            AddHeaderField(panel, 1, "客户名称", "CustomerName");
            AddHeaderField(panel, 2, "项目名称", "ProjectName");
            AddHeaderField(panel, 3, "分析日期", "AnalysisDate");
            AddHeaderField(panel, 4, "税费说明", "TaxNote");
            AddHeaderField(panel, 5, "运费说明", "FreightNote");
            return panel;
        }

        private void AddHeaderField(TableLayoutPanel panel, int column, string labelText, string controlName)
        {
            panel.Controls.Add(CreateMetroLabel()
            {
                Text = labelText,
                ForeColor = _muted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            }, column, 0);

            panel.Controls.Add(CreateMetroTextBox(controlName, string.Empty), column, 1);
        }

        private MetroGrid BuildGrid()
        {
            var grid = new MetroGrid
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(210, 210, 215),
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(230, 244, 255),
                    SelectionForeColor = Color.FromArgb(29, 29, 31)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(250, 250, 252)
                },
                RowTemplate = { Height = 30 }
            };
            grid.Style = MetroColorStyle.Blue;
            grid.Theme = MetroThemeStyle.Light;

            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = _ink;
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false;

            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Selected",
                HeaderText = "选择",
                Width = 56,
                Frozen = true
            });
            AddColumn(grid, "No", "No");
            AddColumn(grid, "MaterialCode", "物料编码");
            AddColumn(grid, "MaterialName", "物料名称");
            AddColumn(grid, "MaterialDescription", "物料描述");
            AddColumn(grid, "Supplier", "供应商");
            AddColumn(grid, "BaseMaterialName", "材料名称");
            AddColumn(grid, "MaterialVendor", "材料厂家");
            AddColumn(grid, "MaterialUnitPrice", "材料单价");
            AddColumn(grid, "GramWeight", "原材料克重");
            AddColumn(grid, "ExpandedSize", "展开尺寸");
            AddColumn(grid, "MaterialCost", "材料费");
            AddColumn(grid, "PrintingCost", "印刷费");
            AddColumn(grid, "PostProcessCost", "后工序费");
            AddColumn(grid, "OtherCost", "其他");
            AddColumn(grid, "PurchaseUnitPrice", "采购单价");
            AddColumn(grid, "TotalQuantity", "总用量");
            AddColumn(grid, "TotalPrice", "总价");
            AddColumn(grid, "ValidationStatus", "状态");

            ConfigureMainGridColumns(grid);
            grid.CellEndEdit += (_, __) => RecalculateRows();
            grid.RowsAdded += (_, __) => ApplyCheckedRowStyles();
            grid.CurrentCellDirtyStateChanged += OnGridCurrentCellDirtyStateChanged;
            grid.CellValueChanged += OnGridCellValueChanged;
            grid.CellMouseDown += OnGridCellMouseDown;
            grid.ColumnHeaderMouseClick += OnGridColumnHeaderMouseClick;
            grid.SelectionChanged += OnGridSelectionChanged;
            grid.DataError += OnGridDataError;
            grid.ContextMenuStrip = BuildGridContextMenu();
            return grid;
        }

        private Control BuildDetailCard()
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 320,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.White,
                Padding = new Padding(20, 14, 20, 18),
                Margin = new Padding(0, 6, 0, 14)
            };
            ApplyRoundedRegion(shell, 14);
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.White
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 46));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

            _detailSelectedCheckBox = new MetroCheckBox
            {
                Dock = DockStyle.Fill,
                Text = string.Empty,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _detailSelectedCheckBox.CheckedChanged += OnDetailSelectedChanged;
            header.Controls.Add(_detailSelectedCheckBox, 0, 0);

            _detailTitleLabel = CreateMetroLabel()
            {
                Dock = DockStyle.Fill,
                Text = "成本明细",
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = _ink,
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(_detailTitleLabel, 1, 0);

            _detailStatusLabel = CreateMetroLabel()
            {
                Dock = DockStyle.Fill,
                Text = "请选择或新增一条明细",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleRight
            };
            header.Controls.Add(_detailStatusLabel, 2, 0);
            shell.Controls.Add(header, 0, 0);

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 8,
                BackColor = Color.White
            };
            for (var i = 0; i < 4; i++)
            {
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            }

            for (var i = 0; i < 4; i++)
            {
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            }

            AddDetailField(fields, 0, 0, "物料编码", "MaterialCode");
            AddDetailField(fields, 1, 0, "物料名称", "MaterialName");
            AddDetailField(fields, 2, 0, "物料描述", "MaterialDescription");
            AddDetailField(fields, 3, 0, "供应商", "Supplier");
            AddDetailField(fields, 0, 2, "材料厂家", "MaterialVendor");
            AddDetailField(fields, 1, 2, "材料单价", "MaterialUnitPrice");
            AddDetailField(fields, 2, 2, "原材料克重", "GramWeight");
            AddDetailField(fields, 3, 2, "展开尺寸", "ExpandedSize");
            AddDetailField(fields, 0, 4, "材料费", "MaterialCost");
            AddDetailField(fields, 1, 4, "印刷费", "PrintingCost");
            AddDetailField(fields, 2, 4, "后工序费", "PostProcessCost");
            AddDetailField(fields, 3, 4, "其他费用", "OtherCost");
            AddDetailField(fields, 0, 6, "采购单价", "PurchaseUnitPrice");
            AddDetailField(fields, 1, 6, "总用量", "TotalQuantity");
            AddDetailField(fields, 2, 6, "总价", "TotalPrice");
            AddDetailField(fields, 3, 6, "状态", "ValidationStatus");

            shell.Controls.Add(fields, 0, 1);
            return shell;
        }

        private Control BuildDetailCardsArea()
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0, 8, 0, 0)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var titleRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White
            };
            titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            titleRow.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "明细表单    卡片式录入，支持展开/收起",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.White
            };
            AddSmallActionButton(actionPanel, "新增明细", OnAddRow, true);
            AddSmallActionButton(actionPanel, "全部收起", (_, __) => CollapseAllDetailCards(), false);
            AddSmallActionButton(actionPanel, "展开首条", (_, __) => ExpandFirstDetailCard(), false);
            titleRow.Controls.Add(actionPanel, 1, 0);
            shell.Controls.Add(titleRow, 0, 0);

            _detailCardsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(0, 0, 8, 8)
            };
            _detailCardsPanel.SizeChanged += (_, __) => ResizeDetailCards();
            shell.Controls.Add(_detailCardsPanel, 0, 1);
            return shell;
        }

        private void AddSmallActionButton(FlowLayoutPanel panel, string text, EventHandler handler, bool primary)
        {
            var button = CreateMetroButton(text, primary);
            button.Width = GetAdaptiveButtonWidth(text, primary ? 108 : 94, 132);
            button.Height = 32;
            button.Margin = new Padding(8, 5, 0, 5);
            button.Click += handler;
            panel.Controls.Add(button);
        }

        private void AddDetailField(TableLayoutPanel panel, int column, int row, string label, string columnName)
        {
            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = label,
                ForeColor = _ink,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 0, 16, 0)
            }, column, row);

            var textBox = CreateMetroTextBox("Detail" + columnName, label);
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 3, 16, 8);
            if (columnName == "TotalPrice" || columnName == "ValidationStatus")
            {
                textBox.ReadOnly = true;
            }
            textBox.TextChanged += OnDetailFieldTextChanged;
            panel.Controls.Add(textBox, column, row + 1);
            _detailFields[columnName] = textBox;
        }

        private void OnImportQuote(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择报价单";
                dialog.Filter = "报价单文件 (*.xls;*.xlsx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp)|*.xls;*.xlsx;*.pdf;*.png;*.jpg;*.jpeg;*.bmp|Excel 报价单 (*.xls;*.xlsx)|*.xls;*.xlsx|PDF 文件 (*.pdf)|*.pdf|图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                var service = new ExcelQuoteImportService();
                QuoteImportPreview preview;
                try
                {
                    preview = service.Import(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ShowPreviewAndAppend(preview);
            }
        }

        private void OnBatchScanQuotes(object sender, EventArgs e)
        {
            var defaultFolder = DirectoryExists(@"D:\1\LB报价单") ? @"D:\1\LB报价单" : string.Empty;
            using (var form = new BatchQuoteScanForm(defaultFolder))
            {
                if (form.ShowDialog(this) != DialogResult.OK || form.SelectedPreview == null)
                {
                    _statusLabel.Text = "已关闭批量预扫描。";
                    return;
                }

                ShowPreviewAndAppend(form.SelectedPreview);
            }
        }

        private void ShowPreviewAndAppend(QuoteImportPreview preview)
        {
            using (var previewForm = new QuoteImportPreviewForm(preview))
            {
                if (previewForm.ShowDialog(this) != DialogResult.OK)
                {
                    _statusLabel.Text = "已取消导入。";
                    return;
                }

                AppendPreviewRows(preview, previewForm.SelectedItems);
                _statusLabel.Text = string.Format(
                    "已加入报价单物料：{0} 条。供应商：{1}，Sheet={2}，模板={3}。请填写总用量以匹配阶梯单价。",
                    previewForm.SelectedItems.Count,
                    preview.Supplier,
                    preview.SheetName,
                    preview.TemplateType);
            }
        }

        private void OnAddRow(object sender, EventArgs e)
        {
            var rowIndex = _grid.Rows.Add();
            _grid.Rows[rowIndex].Cells["Selected"].Value = false;
            _grid.Rows[rowIndex].Cells["No"].Value = rowIndex + 1;
            _grid.CurrentCell = _grid.Rows[rowIndex].Cells["MaterialCode"];
            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
            _statusLabel.Text = "已新增一行明细。";
        }

        private void OnClearAnalysis(object sender, EventArgs e)
        {
            if (_grid.Rows.Count > 0)
            {
                var confirm = MessageBox.Show(
                    this,
                    "确定清空当前表头和所有明细吗？",
                    "清空分析单",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            _analysisNoTextBox.Text = string.Empty;
            _customerTextBox.Text = string.Empty;
            _projectTextBox.Text = string.Empty;
            _dateTextBox.Text = DateTime.Now.ToString("yyyy-MM-dd");
            _taxTextBox.Text = "含税";
            _freightTextBox.Text = "含运";
            _grid.Rows.Clear();
            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
            _statusLabel.Text = "已清空当前分析单。";
        }

        private void OnDeleteSelectedRows(object sender, EventArgs e)
        {
            var rows = GetSelectedDataRows();
            if (rows.Count == 0)
            {
                MessageBox.Show(this, "请先选择要删除的明细行。", "删除明细", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "确定删除选中的 " + rows.Count + " 行明细吗？",
                "删除明细",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            rows.Sort((a, b) => b.Index.CompareTo(a.Index));
            foreach (var row in rows)
            {
                _grid.Rows.Remove(row);
            }

            RenumberRows();
            RecalculateRows();
            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
            _statusLabel.Text = "已删除 " + rows.Count + " 行明细。";
        }

        private System.Collections.Generic.List<DataGridViewRow> GetSelectedDataRows()
        {
            var rows = new System.Collections.Generic.List<DataGridViewRow>();
            var seen = new System.Collections.Generic.HashSet<int>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var checkedValue = row.Cells["Selected"].Value;
                if (checkedValue is bool && (bool)checkedValue && seen.Add(row.Index))
                {
                    rows.Add(row);
                }
            }

            if (rows.Count > 0)
            {
                return rows;
            }

            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (!row.IsNewRow && seen.Add(row.Index))
                {
                    rows.Add(row);
                }
            }

            foreach (DataGridViewCell cell in _grid.SelectedCells)
            {
                if (cell.OwningColumn != null && cell.OwningColumn.Name == "Selected")
                {
                    continue;
                }

                var row = _grid.Rows[cell.RowIndex];
                if (!row.IsNewRow && seen.Add(row.Index))
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private void RenumberRows()
        {
            var no = 1;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.Cells["No"].Value = no++;
            }
        }

        private ContextMenuStrip BuildGridContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("编辑阶梯价", null, OnEditRowPriceTiers);
            menu.Items.Add("历史成本参考", null, OnOpenCostHistoryReference);
            menu.Items.Add("AI补全成本", null, OnAiCompleteCosts);
            menu.Items.Add("-");
            menu.Items.Add("删除明细", null, OnDeleteSelectedRows);
            menu.Items.Add("MOQ模拟", null, OnOpenMoqSimulation);
            return menu;
        }

        private void OnEditRowPriceTiers(object sender, EventArgs e)
        {
            var row = GetCurrentDataRow();
            if (row == null)
            {
                MessageBox.Show(this, "请先选择一条明细行。", "编辑阶梯价格", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var existingTiers = row.Tag as System.Collections.Generic.List<PriceTier>;
            using (var form = new PriceTiersForm(existingTiers))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                row.Tag = form.PriceTiers;
                RecalculateRows();
                row.Cells["PurchaseUnitPrice"].Style.BackColor = Color.FromArgb(232, 244, 255);
                row.Cells["PurchaseUnitPrice"].ToolTipText = "采购单价已按阶梯价格重新匹配";
                row.Cells["ValidationStatus"].Value = "阶梯价已更新";
                _statusLabel.Text = "已更新当前行阶梯价格，并按总用量重新匹配采购单价。";
            }
        }

        private void OnOpenMoqSimulation(object sender, EventArgs e)
        {
            RecalculateRows();

            var lines = BuildMoqSimulationLines();
            if (lines.Count == 0)
            {
                MessageBox.Show(this, "没有可模拟的明细。请先导入报价单、填写总用量，并保留至少一条阶梯价。", "MOQ模拟", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var results = new MoqSimulationService().Simulate(lines);
            if (results.Count == 0)
            {
                MessageBox.Show(this, "当前阶梯价没有产生可比较的临界数量。可以先检查“编辑阶梯价”里是否填写了最小数量和单价。", "MOQ模拟", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new MoqSimulationForm(results))
            {
                form.ShowDialog(this);
            }

            var best = results[0];
            _statusLabel.Text = best.SavingAmount > 0
                ? "MOQ模拟完成：最佳建议数量 " + best.TargetQuantity.ToString("0.####") + "，预计节省 " + best.SavingAmount.ToString("0.####") + "。"
                : "MOQ模拟完成：当前没有发现整单总成本更低的起订量。";
        }

        private List<MoqSimulationLine> BuildMoqSimulationLines()
        {
            var lines = new List<MoqSimulationLine>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var tiers = row.Tag as List<PriceTier>;
                if (tiers == null || tiers.Count == 0)
                {
                    continue;
                }

                var quantity = ReadDecimal(row.Cells["TotalQuantity"].Value);
                if (!quantity.HasValue || quantity.Value <= 0)
                {
                    continue;
                }

                lines.Add(new MoqSimulationLine
                {
                    No = row.Index + 1,
                    MaterialCode = Convert.ToString(row.Cells["MaterialCode"].Value),
                    MaterialName = Convert.ToString(row.Cells["MaterialName"].Value),
                    CurrentQuantity = quantity,
                    CurrentUnitPrice = ReadDecimal(row.Cells["PurchaseUnitPrice"].Value),
                    PriceTiers = ClonePriceTiers(tiers)
                });
            }

            return lines;
        }

        private void OnOpenCostHistoryReference(object sender, EventArgs e)
        {
            var row = GetCurrentDataRow();
            if (row == null)
            {
                MessageBox.Show(this, "请先选择一条明细行。", "历史成本参考", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var materialCode = Convert.ToString(row.Cells["MaterialCode"].Value);
            var materialName = Convert.ToString(row.Cells["MaterialName"].Value);
            var items = new CostAnalysisRepository().SearchCostHistory(materialCode, materialName, 80);
            if (items.Count == 0)
            {
                MessageBox.Show(this, "没有找到相同物料编码或相似物料名称的历史成本。", "历史成本参考", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new CostHistoryReferenceForm(materialCode, materialName, items))
            {
                if (form.ShowDialog(this) != DialogResult.OK || form.SelectedItem == null)
                {
                    return;
                }

                ApplyCostHistoryItem(row, form.SelectedItem);
                RecalculateRows();
                _statusLabel.Text = "已套用历史成本参考：" + form.SelectedItem.AnalysisNo + "。";
            }
        }

        private void ApplyCostHistoryItem(DataGridViewRow row, CostHistoryItem item)
        {
            SetCellIfEmpty(row, "MaterialCode", item.MaterialCode);
            SetCellIfEmpty(row, "MaterialName", item.MaterialName);
            SetCellIfEmpty(row, "Supplier", item.Supplier);
            SetCellIfEmpty(row, "BaseMaterialName", item.BaseMaterialName);
            SetCellIfEmpty(row, "MaterialVendor", item.MaterialVendor);
            SetCellIfEmpty(row, "MaterialUnitPrice", FormatDecimal(item.MaterialUnitPrice));
            SetCellIfEmpty(row, "GramWeight", item.GramWeight);
            SetCellIfEmpty(row, "ExpandedSize", item.ExpandedSize);

            SetCellValue(row, "MaterialCost", FormatDecimal(item.MaterialCost));
            SetCellValue(row, "PrintingCost", FormatDecimal(item.PrintingCost));
            SetCellValue(row, "PostProcessCost", FormatDecimal(item.PostProcessCost));
            SetCellValue(row, "OtherCost", FormatDecimal(item.OtherCost));
            SetCellValue(row, "PurchaseUnitPrice", FormatDecimal(item.PurchaseUnitPrice));

            if (item.PriceTiers != null && item.PriceTiers.Count > 0)
            {
                row.Tag = ClonePriceTiers(item.PriceTiers);
                row.Cells["PurchaseUnitPrice"].ToolTipText = "已套用历史阶梯价格";
            }

            row.Cells["ValidationStatus"].Value = "已套用历史成本";
        }

        private void SetCellIfEmpty(DataGridViewRow row, string columnName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value)))
            {
                SetCellValue(row, columnName, value);
            }
        }

        private static void SetCellValue(DataGridViewRow row, string columnName, string value)
        {
            row.Cells[columnName].Value = value ?? string.Empty;
            row.Cells[columnName].Style.BackColor = Color.FromArgb(232, 244, 255);
        }

        private static System.Collections.Generic.List<PriceTier> ClonePriceTiers(System.Collections.Generic.List<PriceTier> source)
        {
            var result = new System.Collections.Generic.List<PriceTier>();
            foreach (var tier in source)
            {
                result.Add(new PriceTier
                {
                    Label = tier.Label,
                    MinQuantity = tier.MinQuantity,
                    MaxQuantity = tier.MaxQuantity,
                    UnitPrice = tier.UnitPrice
                });
            }

            return result;
        }

        private void OnAiCompleteCosts(object sender, EventArgs e)
        {
            var rows = GetSelectedDataRows();
            if (rows.Count == 0)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (!row.IsNewRow && RowNeedsCostCompletion(row))
                    {
                        rows.Add(row);
                    }
                }
            }

            if (rows.Count == 0)
            {
                MessageBox.Show(this, "没有需要 AI 补全的明细行。可先勾选要补全的行，或保留费用空白后再试。", "AI补全成本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var settings = new AiSettingsRepository().Get();
            if (!settings.IsEnabled)
            {
                MessageBox.Show(this, "AI 功能尚未启用，请先在系统设置中启用并配置 DeepSeek。", "AI补全成本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (settings.ConfirmBeforeCall)
            {
                var confirm = MessageBox.Show(
                    this,
                    "将发送 " + rows.Count + " 行明细给 DeepSeek 生成成本建议。AI 只会填充空白费用或空白采购单价，已有人工金额不会被覆盖。是否继续？",
                    "AI补全成本",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                var inputs = BuildAiCostCompletionInputs(rows);
                var result = new DeepSeekClient().SuggestCosts(settings, inputs);
                var changed = ApplyAiCostCompletionSuggestions(rows, result, inputs);
                RecalculateRows();
                _statusLabel.Text = "AI 成本补全完成，更新费用单元格 " + changed + " 个。";
                MessageBox.Show(this, "AI 成本补全完成，更新费用单元格 " + changed + " 个。请继续人工确认。", "AI补全成本", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "AI补全成本失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private bool RowNeedsCostCompletion(DataGridViewRow row)
        {
            return string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["PrintingCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["PostProcessCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["OtherCost"].Value)) ||
                   string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["PurchaseUnitPrice"].Value));
        }

        private List<AiCostCompletionInput> BuildAiCostCompletionInputs(List<DataGridViewRow> rows)
        {
            var inputs = new List<AiCostCompletionInput>();
            var processRepository = new ProcessCostRuleRepository();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var materialCode = ReadCellText(row, "MaterialCode");
                var materialName = ReadCellText(row, "MaterialName");
                inputs.Add(new AiCostCompletionInput
                {
                    Index = i + 1,
                    MaterialCode = materialCode,
                    MaterialName = materialName,
                    MaterialDescription = ReadCellText(row, "MaterialDescription"),
                    Supplier = ReadCellText(row, "Supplier"),
                    BaseMaterialName = ReadCellText(row, "BaseMaterialName"),
                    MaterialVendor = ReadCellText(row, "MaterialVendor"),
                    MaterialUnitPrice = ReadCellText(row, "MaterialUnitPrice"),
                    GramWeight = ReadCellText(row, "GramWeight"),
                    ExpandedSize = ReadCellText(row, "ExpandedSize"),
                    MaterialCost = ReadCellText(row, "MaterialCost"),
                    PrintingCost = ReadCellText(row, "PrintingCost"),
                    PostProcessCost = ReadCellText(row, "PostProcessCost"),
                    OtherCost = ReadCellText(row, "OtherCost"),
                    PurchaseUnitPrice = ReadCellText(row, "PurchaseUnitPrice"),
                    TotalQuantity = ReadCellText(row, "TotalQuantity"),
                    HistorySummary = BuildHistorySummary(materialCode, materialName),
                    ProcessRuleSummary = BuildProcessRuleSummary(processRepository, ReadCellText(row, "MaterialDescription"))
                });
            }

            return inputs;
        }

        private static string BuildHistorySummary(string materialCode, string materialName)
        {
            var history = new CostAnalysisRepository().SearchCostHistory(materialCode, materialName, 3);
            if (history.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var item in history)
            {
                parts.Add(string.Format(
                    "{0}: 材料费={1}, 印刷费={2}, 后工序费={3}, 其他={4}, 采购单价={5}",
                    item.AnalysisNo,
                    FormatDecimal(item.MaterialCost),
                    FormatDecimal(item.PrintingCost),
                    FormatDecimal(item.PostProcessCost),
                    FormatDecimal(item.OtherCost),
                    FormatDecimal(item.PurchaseUnitPrice)));
            }

            return string.Join("；", parts.ToArray());
        }

        private static string BuildProcessRuleSummary(ProcessCostRuleRepository repository, string description)
        {
            var matches = repository.FindMatches(description);
            if (matches.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var calculator = new ProcessCostCalculationService();
            foreach (var rule in matches)
            {
                var calculated = calculator.Calculate(rule, description, description);
                var amount = calculated.Amount.HasValue ? FormatDecimal(calculated.Amount) : FormatDecimal(rule.Amount);
                var evidence = string.IsNullOrWhiteSpace(calculated.Evidence) ? string.Empty : " (" + calculated.Evidence + ")";
                parts.Add(rule.Keyword + " -> " + rule.CostType + "=" + amount + evidence);
            }

            return string.Join("；", parts.ToArray());
        }

        private int ApplyAiCostCompletionSuggestions(List<DataGridViewRow> rows, AiCostCompletionResult result, List<AiCostCompletionInput> inputs)
        {
            var changed = 0;
            if (result == null || result.Items == null)
            {
                return changed;
            }

            foreach (var suggestion in result.Items)
            {
                if (!suggestion.Index.HasValue || suggestion.Index.Value <= 0 || suggestion.Index.Value > rows.Count)
                {
                    continue;
                }

                var row = rows[suggestion.Index.Value - 1];
                var evidence = BuildAiCostEvidence(suggestion.Index.Value, inputs);
                changed += ApplyAiSuggestedDecimal(row, "MaterialCost", suggestion.MaterialCost, suggestion, evidence);
                changed += ApplyAiSuggestedDecimal(row, "PrintingCost", suggestion.PrintingCost, suggestion, evidence);
                changed += ApplyAiSuggestedDecimal(row, "PostProcessCost", suggestion.PostProcessCost, suggestion, evidence);
                changed += ApplyAiSuggestedDecimal(row, "OtherCost", suggestion.OtherCost, suggestion, evidence);
                changed += ApplyAiSuggestedDecimal(row, "PurchaseUnitPrice", suggestion.PurchaseUnitPrice, suggestion, evidence);
                if (!string.IsNullOrWhiteSpace(evidence))
                {
                    row.Cells["ValidationStatus"].ToolTipText = evidence;
                }
                row.Cells["ValidationStatus"].Value = suggestion.RequiresReview ? "AI建议需确认" : "AI建议已填充";
            }

            return changed;
        }

        private static string BuildAiCostEvidence(int index, List<AiCostCompletionInput> inputs)
        {
            if (inputs == null || index <= 0 || index > inputs.Count)
            {
                return string.Empty;
            }

            var input = inputs[index - 1];
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(input.HistorySummary))
            {
                parts.Add("历史参考：" + input.HistorySummary);
            }

            if (!string.IsNullOrWhiteSpace(input.ProcessRuleSummary))
            {
                parts.Add("工艺依据：" + input.ProcessRuleSummary);
            }

            return string.Join("\r\n", parts.ToArray());
        }

        private static int ApplyAiSuggestedDecimal(DataGridViewRow row, string columnName, decimal? value, AiCostCompletionSuggestion suggestion, string evidence)
        {
            if (!value.HasValue || !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value)))
            {
                return 0;
            }

            row.Cells[columnName].Value = value.Value.ToString("0.####");
            row.Cells[columnName].Style.BackColor = Color.FromArgb(232, 244, 255);
            row.Cells[columnName].ToolTipText = "AI成本建议；置信度=" + suggestion.Confidence.ToString("0.##") + "；" + (suggestion.Reason ?? string.Empty);
            return 1;
        }

        private static string ReadCellText(DataGridViewRow row, string columnName)
        {
            return row.Cells[columnName].Value == null ? string.Empty : Convert.ToString(row.Cells[columnName].Value);
        }

        private DataGridViewRow GetCurrentDataRow()
        {
            if (_grid.CurrentCell != null)
            {
                var row = _grid.Rows[_grid.CurrentCell.RowIndex];
                if (!row.IsNewRow)
                {
                    return row;
                }
            }

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var checkedValue = row.Cells["Selected"].Value;
                if (checkedValue is bool && (bool)checkedValue)
                {
                    return row;
                }
            }

            return null;
        }

        private void OnGridCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0)
            {
                return;
            }

            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
            if (e.ColumnIndex >= 0)
            {
                _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            }
        }

        private void OnGridCurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell != null && _grid.CurrentCell.OwningColumn.Name == "Selected")
            {
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void OnGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            var columnName = _grid.Columns[e.ColumnIndex].Name;
            if (columnName == "Selected")
            {
                ApplyCheckedRowStyle(_grid.Rows[e.RowIndex]);
                UpdateSelectedRowsStatus();
                LoadCurrentRowToDetailCard();
                return;
            }

            RecalculateRows();
            LoadCurrentRowToDetailCard();
        }

        private void OnGridSelectionChanged(object sender, EventArgs e)
        {
            if (!_refreshingDetailCards)
            {
                _allDetailCardsCollapsed = false;
            }

            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
        }

        private void OnDetailSelectedChanged(object sender, EventArgs e)
        {
            if (_syncingDetailCard)
            {
                return;
            }

            var row = GetCurrentDataRow();
            if (row == null)
            {
                return;
            }

            row.Cells["Selected"].Value = _detailSelectedCheckBox.Checked;
            ApplyCheckedRowStyle(row);
            UpdateSelectedRowsStatus();
        }

        private void OnDetailFieldTextChanged(object sender, EventArgs e)
        {
            if (_syncingDetailCard)
            {
                return;
            }

            var textBox = sender as MetroTextBox;
            if (textBox == null)
            {
                return;
            }

            var columnName = GetDetailColumnName(textBox);
            if (string.IsNullOrWhiteSpace(columnName) ||
                columnName == "TotalPrice" ||
                columnName == "ValidationStatus")
            {
                return;
            }

            var row = GetCurrentDataRow();
            if (row == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return;
            }

            row.Cells[columnName].Value = textBox.Text;
            RecalculateRows();
            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
        }

        private void RefreshDetailCards()
        {
            if (_detailCardsPanel == null || _refreshingDetailCards)
            {
                return;
            }

            _refreshingDetailCards = true;
            try
            {
                var current = GetCurrentDataRow();
                _detailCardsPanel.SuspendLayout();
                _detailCardsPanel.Controls.Clear();

                var added = false;
                var top = 0;
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.IsNewRow)
                    {
                        continue;
                    }

                    if (!_allDetailCardsCollapsed && current != null && row.Index == current.Index)
                    {
                        AddDetailCardControl(_detailCard, ref top);
                        added = true;
                    }
                    else
                    {
                        AddDetailCardControl(BuildCollapsedDetailCard(row), ref top);
                    }
                }

                if (!added && _grid.Rows.Count == 0)
                {
                    AddDetailCardControl(BuildEmptyDetailCard(), ref top);
                }

                _detailCardsPanel.AutoScrollMinSize = new Size(0, top + 12);
                ResizeDetailCards();
            }
            finally
            {
                if (_detailCardsPanel != null)
                {
                    _detailCardsPanel.ResumeLayout();
                }

                _refreshingDetailCards = false;
            }
        }

        private void AddDetailCardControl(Control control, ref int top)
        {
            control.Dock = DockStyle.None;
            control.Left = 0;
            control.Top = top;
            control.Width = GetDetailCardWidth();
            _detailCardsPanel.Controls.Add(control);
            top += control.Height + 12;
        }

        private Control BuildCollapsedDetailCard(DataGridViewRow row)
        {
            var card = new TableLayoutPanel
            {
                Height = 64,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.FromArgb(253, 254, 255),
                Padding = new Padding(16, 10, 16, 10),
                Margin = new Padding(0, 0, 0, 12),
                Tag = row.Index
            };
            ApplyRoundedRegion(card, 12);
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            card.Click += OnCollapsedCardClick;

            var selector = new CheckBox
            {
                Dock = DockStyle.Fill,
                Checked = IsRowChecked(row),
                Tag = row.Index
            };
            selector.CheckedChanged += OnCollapsedCardCheckedChanged;
            card.Controls.Add(selector, 0, 0);

            card.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "成本" + SafeCell(row, "No"),
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 1, 0);

            card.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = BuildCollapsedCardSummary(row),
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 2, 0);

            card.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "⌄",
                ForeColor = _blue,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold)
            }, 3, 0);
            WireCollapsedCardClicks(card, row.Index);
            return card;
        }

        private void WireCollapsedCardClicks(Control parent, int rowIndex)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is CheckBox)
                {
                    continue;
                }

                child.Tag = rowIndex;
                child.Click += OnCollapsedCardClick;
                WireCollapsedCardClicks(child, rowIndex);
            }
        }

        private Control BuildEmptyDetailCard()
        {
            return new Label
            {
                Height = 96,
                Dock = DockStyle.Top,
                Text = "暂无明细。点击上方“新增明细”或“导入报价”。",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(250, 250, 252)
            };
        }

        private void OnCollapsedCardClick(object sender, EventArgs e)
        {
            var control = sender as Control;
            if (control == null || !(control.Tag is int))
            {
                return;
            }

            SelectGridRow((int)control.Tag);
        }

        private void OnCollapsedCardCheckedChanged(object sender, EventArgs e)
        {
            if (_refreshingDetailCards)
            {
                return;
            }

            var checkBox = sender as CheckBox;
            if (checkBox == null || !(checkBox.Tag is int))
            {
                return;
            }

            var rowIndex = (int)checkBox.Tag;
            if (rowIndex >= 0 && rowIndex < _grid.Rows.Count)
            {
                _grid.Rows[rowIndex].Cells["Selected"].Value = checkBox.Checked;
                ApplyCheckedRowStyle(_grid.Rows[rowIndex]);
                UpdateSelectedRowsStatus();
            }
        }

        private void SelectGridRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            {
                return;
            }

            _grid.CurrentCell = _grid.Rows[rowIndex].Cells["MaterialCode"];
            _allDetailCardsCollapsed = false;
            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
        }

        private void ResizeDetailCards()
        {
            if (_detailCardsPanel == null)
            {
                return;
            }

            var width = GetDetailCardWidth();
            foreach (Control control in _detailCardsPanel.Controls)
            {
                control.Width = width;
            }
        }

        private int GetDetailCardWidth()
        {
            if (_detailCardsPanel == null)
            {
                return 620;
            }

            var scrollbarReserve = _detailCardsPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth + 8 : 8;
            return Math.Max(520, _detailCardsPanel.ClientSize.Width - scrollbarReserve);
        }

        private void CollapseAllDetailCards()
        {
            _allDetailCardsCollapsed = true;
            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
        }

        private void ExpandFirstDetailCard()
        {
            if (_grid.Rows.Count > 0)
            {
                SelectGridRow(0);
            }
        }

        private static string BuildCollapsedCardSummary(DataGridViewRow row)
        {
            var name = SafeCell(row, "MaterialName");
            var code = SafeCell(row, "MaterialCode");
            var status = SafeCell(row, "ValidationStatus");
            var text = string.IsNullOrWhiteSpace(name) ? code : name;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "未填写物料";
            }

            return string.IsNullOrWhiteSpace(status) ? text : text + "    " + status;
        }

        private static string SafeCell(DataGridViewRow row, string columnName)
        {
            if (row == null || row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            return Convert.ToString(row.Cells[columnName].Value);
        }

        private void LoadCurrentRowToDetailCard()
        {
            if (_detailFields.Count == 0)
            {
                return;
            }

            var row = GetCurrentDataRow();
            _syncingDetailCard = true;
            try
            {
                if (row == null)
                {
                    _detailTitleLabel.Text = "成本明细";
                    _detailStatusLabel.Text = "请选择或新增一条明细";
                    _detailSelectedCheckBox.Checked = false;
                    foreach (var box in _detailFields.Values)
                    {
                        box.Text = string.Empty;
                    }

                    return;
                }

                _detailTitleLabel.Text = "成本明细 " + Convert.ToString(row.Cells["No"].Value);
                _detailStatusLabel.Text = Convert.ToString(row.Cells["ValidationStatus"].Value);
                _detailSelectedCheckBox.Checked = IsRowChecked(row);
                foreach (var pair in _detailFields)
                {
                    pair.Value.Text = row.DataGridView.Columns.Contains(pair.Key)
                        ? Convert.ToString(row.Cells[pair.Key].Value)
                        : string.Empty;
                }
            }
            finally
            {
                _syncingDetailCard = false;
            }
        }

        private static string GetDetailColumnName(Control control)
        {
            const string prefix = "Detail";
            return control != null && control.Name.StartsWith(prefix, StringComparison.Ordinal)
                ? control.Name.Substring(prefix.Length)
                : string.Empty;
        }

        private void OnGridDataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.ErrorText = "当前输入格式不正确，请检查后再继续。";
                cell.Style.BackColor = _missingBack;
                _statusLabel.Text = "发现格式错误：请检查高亮单元格。";
            }

            e.ThrowException = false;
        }

        private void OnGridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Selected")
            {
                return;
            }

            var shouldCheck = !AllDataRowsChecked();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                row.Cells["Selected"].Value = shouldCheck;
                ApplyCheckedRowStyle(row);
            }

            UpdateSelectedRowsStatus();
        }

        private void ApplyCheckedRowStyles()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow)
                {
                    ApplyCheckedRowStyle(row);
                }
            }
        }

        private void ApplyCheckedRowStyle(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow)
            {
                return;
            }

            row.DefaultCellStyle.BackColor = IsRowChecked(row) ? _selectedBack : Color.White;
        }

        private static bool IsRowChecked(DataGridViewRow row)
        {
            if (row == null || row.IsNewRow)
            {
                return false;
            }

            var value = row.Cells["Selected"].Value;
            if (value is bool)
            {
                return (bool)value;
            }

            return string.Equals(Convert.ToString(value), "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Convert.ToString(value), "1", StringComparison.OrdinalIgnoreCase);
        }

        private bool AllDataRowsChecked()
        {
            var hasRows = false;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                hasRows = true;
                if (!IsRowChecked(row))
                {
                    return false;
                }
            }

            return hasRows;
        }

        private int CountCheckedRows()
        {
            var count = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow && IsRowChecked(row))
                {
                    count++;
                }
            }

            return count;
        }

        private void UpdateSelectedRowsStatus()
        {
            var count = CountCheckedRows();
            _statusLabel.Text = count == 0 ? "未选择明细行。" : "已选择 " + count + " 行明细，可点击“删除明细”。";
        }

        private void OnOpenMaterials(object sender, EventArgs e)
        {
            using (var form = new MaterialsForm(_grid))
            {
                form.ShowDialog(this);
            }
        }

        private void OnOpenProcessRules(object sender, EventArgs e)
        {
            using (var form = new ProcessCostRulesForm(_grid))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _statusLabel.Text = "工艺费用规则已更新。";
                }
            }
        }

        private void OnApplyProcessRules(object sender, EventArgs e)
        {
            var rows = GetSelectedDataRows();
            if (rows.Count == 0)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        rows.Add(row);
                    }
                }
            }

            var changed = ApplyProcessRulesToRows(rows);
            RecalculateRows();
            _statusLabel.Text = "已应用工艺规则，更新费用单元格 " + changed + " 个。";
        }

        private int ApplyProcessRulesToRows(System.Collections.Generic.List<DataGridViewRow> rows)
        {
            var changed = 0;
            var repository = new ProcessCostRuleRepository();
            var calculator = new ProcessCostCalculationService();
            foreach (var row in rows)
            {
                var text = Convert.ToString(row.Cells["MaterialDescription"].Value);
                var sizeText = Convert.ToString(row.Cells["ExpandedSize"].Value);
                var matches = repository.FindMatches(text);
                foreach (var rule in matches)
                {
                    var calculated = calculator.Calculate(rule, text, sizeText);
                    if (!calculated.Amount.HasValue)
                    {
                        continue;
                    }

                    var columnName = NormalizeRuleCostColumn(rule.CostType);
                    if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[columnName].Value)))
                    {
                        row.Cells[columnName].Value = calculated.Amount.Value.ToString("0.####");
                        row.Cells[columnName].Style.BackColor = Color.FromArgb(232, 244, 255);
                        row.Cells[columnName].ToolTipText = "由工艺规则匹配：" + rule.Keyword;
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static string NormalizeRuleCostColumn(string costType)
        {
            if (costType == "PrintingCost") return "PrintingCost";
            if (costType == "OtherCost") return "OtherCost";
            if (costType == "MaterialCost") return "MaterialCost";
            return "PostProcessCost";
        }

        private void OnOpenAiSettings(object sender, EventArgs e)
        {
            using (var form = new AiSettingsForm())
            {
                form.ShowDialog(this);
            }
        }

        private void OnOpenOcrSettings(object sender, EventArgs e)
        {
            using (var form = new OcrSettingsForm())
            {
                form.ShowDialog(this);
            }
        }

        private void OnOpenEnvironmentCheck(object sender, EventArgs e)
        {
            using (var form = new EnvironmentCheckForm())
            {
                form.ShowDialog(this);
            }
        }

        private void OnExportExcelPlaceholder(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "导出客户成本分析表";
                dialog.Filter = "Excel 工作簿 (*.xlsx)|*.xlsx";
                dialog.FileName = "成本分析.xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    ValidateGrid();
                    new ExcelExportService().ExportGrid(dialog.FileName, ReadHeader(), _grid);
                    _statusLabel.Text = "已导出 Excel：" + dialog.FileName;
                    MessageBox.Show(this, "导出完成。", "导出 Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void OnSaveAnalysis(object sender, EventArgs e)
        {
            RecalculateRows();
            ValidateGrid();
            try
            {
                var id = new CostAnalysisRepository().SaveFromGrid(ReadHeader(), _grid);
                _statusLabel.Text = "已保存成本分析单，ID=" + id;
                RefreshRecentAnalysesList();
                MessageBox.Show(this, "保存完成。", "保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnOpenAnalysis(object sender, EventArgs e)
        {
            try
            {
                var repository = new CostAnalysisRepository();
                var analyses = repository.GetRecentAnalyses();
                if (analyses.Count == 0)
                {
                    MessageBox.Show(this, "还没有保存过成本分析单。", "打开", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dialog = new OpenAnalysisForm(analyses))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.SelectedAnalysisId.HasValue)
                    {
                        return;
                    }

                    var analysis = repository.GetAnalysis(dialog.SelectedAnalysisId.Value);
                    OpenAnalysis(dialog.SelectedAnalysisId.Value, analysis);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshRecentAnalysesList()
        {
            if (_recentAnalysesListBox == null)
            {
                return;
            }

            try
            {
                _recentAnalysesListBox.BeginUpdate();
                _recentAnalysesListBox.Items.Clear();
                foreach (var item in new CostAnalysisRepository().GetRecentAnalyses())
                {
                    _recentAnalysesListBox.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "刷新成本分析列表失败：" + ex.Message;
            }
            finally
            {
                _recentAnalysesListBox.EndUpdate();
            }
        }

        private void OnRecentAnalysisDoubleClick(object sender, EventArgs e)
        {
            var selected = _recentAnalysesListBox.SelectedItem as CostAnalysisSummary;
            if (selected == null)
            {
                return;
            }

            try
            {
                var analysis = new CostAnalysisRepository().GetAnalysis(selected.Id);
                OpenAnalysis(selected.Id, analysis);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenAnalysis(int analysisId, SavedCostAnalysis analysis)
        {
            LoadHeader(analysis.Header);
            LoadSavedItems(analysis.Items);
            _statusLabel.Text = "已打开成本分析单，ID=" + analysisId + "，明细 " + analysis.Items.Count + " 条。";
        }

        private void RecalculateRows()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var quantity = ReadDecimal(row.Cells["TotalQuantity"].Value);
                var unitPrice = ReadDecimal(row.Cells["PurchaseUnitPrice"].Value);
                var tiers = row.Tag as System.Collections.Generic.List<PriceTier>;
                if (quantity.HasValue && tiers != null)
                {
                    var matched = ExcelQuoteImportService.MatchTier(tiers, quantity.Value);
                    if (matched != null && matched.UnitPrice.HasValue)
                    {
                        unitPrice = matched.UnitPrice;
                        row.Cells["PurchaseUnitPrice"].Value = unitPrice.Value.ToString("0.####");
                    }
                }

                if (unitPrice.HasValue && quantity.HasValue)
                {
                    row.Cells["TotalPrice"].Value = (unitPrice.Value * quantity.Value).ToString("0.####");
                }
            }

            ValidateGrid();
        }

        private int ValidateGrid()
        {
            var warningCount = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                ResetRowStyle(row);
                var messages = new System.Collections.Generic.List<string>();

                RequireCell(row, "MaterialCode", "缺物料编码", messages);
                RequireCell(row, "MaterialName", "缺物料名称", messages);
                RequireCell(row, "PurchaseUnitPrice", "缺采购单价", messages);
                RequireCell(row, "TotalQuantity", "缺总用量", messages);

                var materialCost = ReadDecimal(row.Cells["MaterialCost"].Value) ?? 0;
                var printingCost = ReadDecimal(row.Cells["PrintingCost"].Value) ?? 0;
                var postProcessCost = ReadDecimal(row.Cells["PostProcessCost"].Value) ?? 0;
                var otherCost = ReadDecimal(row.Cells["OtherCost"].Value) ?? 0;
                var unitPrice = ReadDecimal(row.Cells["PurchaseUnitPrice"].Value);
                var hasAnyCost = materialCost != 0 || printingCost != 0 || postProcessCost != 0 || otherCost != 0;

                if (unitPrice.HasValue && hasAnyCost)
                {
                    var costTotal = materialCost + printingCost + postProcessCost + otherCost;
                    if (Math.Abs(costTotal - unitPrice.Value) > 0.0001M)
                    {
                        if (!IsRowChecked(row))
                        {
                            row.DefaultCellStyle.BackColor = _warningBack;
                        }
                        messages.Add("成本合计不等于采购单价");
                    }
                }

                var totalQuantity = ReadDecimal(row.Cells["TotalQuantity"].Value);
                var totalPrice = ReadDecimal(row.Cells["TotalPrice"].Value);
                if (unitPrice.HasValue && totalQuantity.HasValue && totalPrice.HasValue)
                {
                    var expectedTotal = unitPrice.Value * totalQuantity.Value;
                    if (Math.Abs(expectedTotal - totalPrice.Value) > 0.0001M)
                    {
                        if (!IsRowChecked(row))
                        {
                            row.DefaultCellStyle.BackColor = _warningBack;
                        }
                        messages.Add("总价计算不一致");
                    }
                }

                if (messages.Count > 0)
                {
                    warningCount++;
                    row.Cells["ValidationStatus"].Value = string.Join("；", messages.ToArray());
                }
                else
                {
                    row.Cells["ValidationStatus"].Value = "已完成";
                }
            }

            if (warningCount > 0)
            {
                _statusLabel.Text = "校验完成：发现 " + warningCount + " 行需要确认。";
            }

            return warningCount;
        }

        private void RequireCell(DataGridViewRow row, string columnName, string message, System.Collections.Generic.List<string> messages)
        {
            var cell = row.Cells[columnName];
            if (cell.Value == null || string.IsNullOrWhiteSpace(Convert.ToString(cell.Value)))
            {
                cell.Style.BackColor = _missingBack;
                messages.Add(message);
            }
        }

        private void ResetRowStyle(DataGridViewRow row)
        {
            row.DefaultCellStyle.BackColor = IsRowChecked(row) ? _selectedBack : Color.White;
            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.Style.BackColor = Color.Empty;
            }
        }

        private void AppendPreviewRows(QuoteImportPreview preview, System.Collections.Generic.List<QuoteImportItem> items)
        {
            if (string.IsNullOrWhiteSpace(_analysisNoTextBox.Text))
            {
                _analysisNoTextBox.Text = "CA-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            }

            if (string.IsNullOrWhiteSpace(_customerTextBox.Text))
            {
                _customerTextBox.Text = "客户";
            }

            foreach (var item in items)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Tag = item.PriceTiers;
                row.Cells["Selected"].Value = false;
                row.Cells["No"].Value = rowIndex + 1;
                row.Cells["MaterialCode"].Value = item.MaterialCode;
                row.Cells["MaterialName"].Value = item.MaterialName;
                row.Cells["MaterialDescription"].Value = BuildDescription(item);
                row.Cells["Supplier"].Value = preview.Supplier;
                row.Cells["BaseMaterialName"].Value = item.MaterialNameExtracted;
                row.Cells["GramWeight"].Value = item.GramWeight;
                row.Cells["ExpandedSize"].Value = item.FinishedSize;
                ApplyMaterialMatch(row, item.MaterialNameExtracted);

                if (item.UsageQuantity.HasValue)
                {
                    row.Cells["TotalQuantity"].Value = item.UsageQuantity.Value.ToString("0.####");
                }

                if (item.PriceTiers != null && item.PriceTiers.Count == 1 && item.PriceTiers[0].UnitPrice.HasValue)
                {
                    row.Cells["PurchaseUnitPrice"].Value = item.PriceTiers[0].UnitPrice.Value.ToString("0.####");
                }
            }

            ApplyProcessRulesToRows(GetRowsByStartIndex(_grid.Rows.Count - items.Count));
            RecalculateRows();
            var firstNewRow = Math.Max(0, _grid.Rows.Count - items.Count);
            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[firstNewRow].Cells["MaterialCode"];
            }

            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
        }

        private System.Collections.Generic.List<DataGridViewRow> GetRowsByStartIndex(int startIndex)
        {
            var rows = new System.Collections.Generic.List<DataGridViewRow>();
            for (var index = Math.Max(0, startIndex); index < _grid.Rows.Count; index++)
            {
                if (!_grid.Rows[index].IsNewRow)
                {
                    rows.Add(_grid.Rows[index]);
                }
            }

            return rows;
        }

        private CostAnalysisHeader ReadHeader()
        {
            return new CostAnalysisHeader
            {
                AnalysisNo = _analysisNoTextBox.Text,
                CustomerName = _customerTextBox.Text,
                ProjectName = _projectTextBox.Text,
                AnalysisDate = _dateTextBox.Text,
                TaxNote = _taxTextBox.Text,
                FreightNote = _freightTextBox.Text
            };
        }

        private void LoadHeader(CostAnalysisHeader header)
        {
            _analysisNoTextBox.Text = header.AnalysisNo;
            _customerTextBox.Text = header.CustomerName;
            _projectTextBox.Text = header.ProjectName;
            _dateTextBox.Text = header.AnalysisDate;
            _taxTextBox.Text = header.TaxNote;
            _freightTextBox.Text = header.FreightNote;
        }

        private void LoadSavedItems(System.Collections.Generic.List<SavedCostAnalysisItem> items)
        {
            _grid.Rows.Clear();
            foreach (var item in items)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Selected"].Value = false;
                row.Cells["No"].Value = item.No;
                row.Cells["MaterialCode"].Value = item.MaterialCode;
                row.Cells["MaterialName"].Value = item.MaterialName;
                row.Cells["MaterialDescription"].Value = item.MaterialDescription;
                row.Cells["Supplier"].Value = item.Supplier;
                row.Cells["BaseMaterialName"].Value = item.BaseMaterialName;
                row.Cells["MaterialVendor"].Value = item.MaterialVendor;
                row.Cells["MaterialUnitPrice"].Value = FormatDecimal(item.MaterialUnitPrice);
                row.Cells["GramWeight"].Value = item.GramWeight;
                row.Cells["ExpandedSize"].Value = item.ExpandedSize;
                row.Cells["MaterialCost"].Value = FormatDecimal(item.MaterialCost);
                row.Cells["PrintingCost"].Value = FormatDecimal(item.PrintingCost);
                row.Cells["PostProcessCost"].Value = FormatDecimal(item.PostProcessCost);
                row.Cells["OtherCost"].Value = FormatDecimal(item.OtherCost);
                row.Cells["PurchaseUnitPrice"].Value = FormatDecimal(item.PurchaseUnitPrice);
                row.Cells["TotalQuantity"].Value = FormatDecimal(item.TotalQuantity);
                row.Cells["TotalPrice"].Value = FormatDecimal(item.TotalPrice);
                if (item.PriceTiers != null && item.PriceTiers.Count > 0)
                {
                    row.Tag = item.PriceTiers;
                    row.Cells["PurchaseUnitPrice"].ToolTipText = "已恢复保存的阶梯价格";
                }
            }

            RecalculateRows();
            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[0].Cells["MaterialCode"];
            }

            LoadCurrentRowToDetailCard();
            RefreshDetailCards();
        }

        private void ApplyMaterialMatch(DataGridViewRow row, string materialName)
        {
            var repository = new MaterialRepository();
            var matches = new List<MaterialRecord>();
            foreach (var name in SplitMaterialNames(materialName))
            {
                var match = repository.FindByNameOrAlias(name);
                if (match != null)
                {
                    matches.Add(match);
                }
            }

            if (matches.Count == 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialVendor"].Value)))
            {
                row.Cells["MaterialVendor"].Value = JoinUnique(matches, m => m.Vendor);
            }

            if (row.Cells["MaterialUnitPrice"].Value == null || string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialUnitPrice"].Value)))
            {
                var price = GetSingleMaterialPrice(matches);
                row.Cells["MaterialUnitPrice"].Value = price.HasValue ? price.Value.ToString("0.####") : string.Empty;
                if (!price.HasValue && matches.Count > 1)
                {
                    row.Cells["MaterialUnitPrice"].ToolTipText = "匹配到多个材料，需人工填写综合材料单价。";
                }
            }

            if (string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["GramWeight"].Value)))
            {
                row.Cells["GramWeight"].Value = JoinUnique(matches, m => m.Spec);
            }
        }

        private static IEnumerable<string> SplitMaterialNames(string materialName)
        {
            return (materialName ?? string.Empty).Split(new[] { ';', '；', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string JoinUnique(List<MaterialRecord> matches, Func<MaterialRecord, string> selector)
        {
            var values = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var match in matches)
            {
                var value = selector(match);
                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value.Trim()))
                {
                    values.Add(value.Trim());
                }
            }

            return string.Join("；", values.ToArray());
        }

        private static decimal? GetSingleMaterialPrice(List<MaterialRecord> matches)
        {
            decimal? price = null;
            foreach (var match in matches)
            {
                if (!match.TaxUnitPrice.HasValue)
                {
                    continue;
                }

                if (price.HasValue && price.Value != match.TaxUnitPrice.Value)
                {
                    return null;
                }

                price = match.TaxUnitPrice;
            }

            return price;
        }

        private static string BuildDescription(QuoteImportItem item)
        {
            var size = string.IsNullOrWhiteSpace(item.FinishedSize) ? string.Empty : "尺寸：" + item.FinishedSize;
            var process = string.IsNullOrWhiteSpace(item.MaterialProcess) ? string.Empty : "材质/工艺：" + item.MaterialProcess;
            if (string.IsNullOrWhiteSpace(size))
            {
                return process;
            }

            if (string.IsNullOrWhiteSpace(process))
            {
                return size;
            }

            return size + "，" + process;
        }

        private static decimal? ReadDecimal(object value)
        {
            if (value == null)
            {
                return null;
            }

            decimal number;
            return decimal.TryParse(value.ToString(), out number) ? number : (decimal?)null;
        }

        private static string FormatDecimal(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("0.####") : string.Empty;
        }

        private void AddMenuButton(FlowLayoutPanel panel, string text, EventHandler handler, bool active = false)
        {
            var button = CreateMetroButton(text, active);
            button.Width = 178;
            button.Height = 42;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Margin = new Padding(0, 0, 0, 8);
            if (handler != null)
            {
                button.Click += handler;
            }
            panel.Controls.Add(button);
        }

        private void AddPrimaryButton(FlowLayoutPanel panel, string text, EventHandler handler)
        {
            var button = CreateToolbarButton(text, handler);
            button.BackColor = _blue;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = _blue;
            panel.Controls.Add(button);
        }

        private void AddSecondaryButton(FlowLayoutPanel panel, string text, EventHandler handler)
        {
            var button = CreateToolbarButton(text, handler);
            button.BackColor = Color.White;
            button.ForeColor = _blue;
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 215);
            panel.Controls.Add(button);
        }

        private Button CreateToolbarButton(string text, EventHandler handler)
        {
            var button = CreateMetroButton(text, false);
            button.Width = 110;
            button.Height = 34;
            button.Margin = new Padding(0, 4, 10, 4);
            button.Click += handler;
            return button;
        }

        private static void AddColumn(DataGridView grid, string name, string headerText)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = headerText,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        private static void ConfigureMainGridColumns(DataGridView grid)
        {
            SetColumn(grid, "Selected", 56, true, false);
            SetColumn(grid, "No", 52, true, true);
            SetColumn(grid, "MaterialCode", 116, true, true);
            SetColumn(grid, "MaterialName", 150, false, true);
            SetColumn(grid, "MaterialDescription", 340, false, true);
            SetColumn(grid, "Supplier", 150, false, true);
            SetColumn(grid, "BaseMaterialName", 120, false, true);
            SetColumn(grid, "MaterialVendor", 120, false, true);
            SetColumn(grid, "MaterialUnitPrice", 86, false, false);
            SetColumn(grid, "GramWeight", 92, false, false);
            SetColumn(grid, "ExpandedSize", 116, false, false);
            SetColumn(grid, "MaterialCost", 76, false, false);
            SetColumn(grid, "PrintingCost", 76, false, false);
            SetColumn(grid, "PostProcessCost", 88, false, false);
            SetColumn(grid, "OtherCost", 70, false, false);
            SetColumn(grid, "PurchaseUnitPrice", 86, false, false);
            SetColumn(grid, "TotalQuantity", 86, false, false);
            SetColumn(grid, "TotalPrice", 86, false, false);
            SetColumn(grid, "ValidationStatus", 180, false, true);
        }

        private static void SetColumn(DataGridView grid, string name, int width, bool frozen, bool wrap)
        {
            if (!grid.Columns.Contains(name))
            {
                return;
            }

            var column = grid.Columns[name];
            column.Width = width;
            column.MinimumWidth = Math.Min(width, 60);
            column.Frozen = frozen;
            column.DefaultCellStyle.WrapMode = wrap ? DataGridViewTriState.True : DataGridViewTriState.False;
        }

        private static int GetAdaptiveButtonWidth(string text, int minWidth, int maxWidth)
        {
            var measured = TextRenderer.MeasureText(text ?? string.Empty, new Font("Microsoft YaHei UI", 9F)).Width + 28;
            return Math.Max(minWidth, Math.Min(maxWidth, measured));
        }

        private static MetroButton CreateMetroButton(string text, bool primary)
        {
            return new MetroButton
            {
                Text = text,
                Style = primary ? MetroColorStyle.Blue : MetroColorStyle.Silver,
                Theme = MetroThemeStyle.Light,
                UseSelectable = true,
                Highlight = primary,
                DisplayFocus = true,
                FontSize = MetroButtonSize.Medium,
                FontWeight = primary ? MetroButtonWeight.Regular : MetroButtonWeight.Light
            };
        }

        private static MetroTextBox CreateMetroTextBox(string name, string promptText)
        {
            return new MetroTextBox
            {
                Name = name,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 10, 4),
                WaterMark = promptText ?? string.Empty,
                Style = MetroColorStyle.Blue,
                Theme = MetroThemeStyle.Light,
                UseSelectable = true,
                FontSize = MetroTextBoxSize.Medium
            };
        }

        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control == null)
            {
                return;
            }

            EventHandler handler = (sender, args) =>
            {
                var target = sender as Control;
                if (target == null || target.Width <= 0 || target.Height <= 0)
                {
                    return;
                }

                using (var path = CreateRoundPath(new Rectangle(0, 0, target.Width, target.Height), radius))
                {
                    target.Region = new Region(path);
                }
            };
            control.SizeChanged += handler;
        }

        private static GraphicsPath CreateRoundPath(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static MetroTextBox FindHeaderTextBox(Control root, string name)
        {
            var matches = root.Controls.Find(name, true);
            if (matches.Length == 0)
            {
                throw new InvalidOperationException("找不到输入框：" + name);
            }

            return (MetroTextBox)matches[0];
        }

        private static bool DirectoryExists(string path)
        {
            return System.IO.Directory.Exists(path);
        }
    }
}
