using System;
using System.Collections.Generic;
using System.Data;
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
        private sealed class PreflightIssue
        {
            public int RowIndex { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
        }

        private readonly Color _canvas = Color.White;
        private readonly Color _panel = Color.White;
        private readonly Color _ink = Color.FromArgb(29, 29, 31);
        private readonly Color _muted = Color.FromArgb(110, 110, 115);
        private readonly Color _blue = Color.FromArgb(24, 111, 232);
        private readonly Color _warningBack = Color.FromArgb(255, 249, 219);
        private readonly Color _missingBack = Color.FromArgb(255, 235, 235);
        private readonly Color _selectedBack = Color.FromArgb(230, 244, 255);
        private readonly ToolTip _commandTips = new ToolTip();
        private readonly PriceWarningService _priceWarningService = new PriceWarningService();

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
        private MetroLabel _detailHintLabel;
        private MetroLabel _summaryItemsLabel;
        private MetroLabel _summaryAmountLabel;
        private MetroLabel _summaryPendingLabel;
        private MetroLabel _summaryWarningLabel;
        private MetroLabel _selectionInfoLabel;
        private MetroButton _deleteSelectedButton;
        private MetroButton _applyRulesSelectedButton;
        private MetroPanel _detailCardsPanel;
        private TableLayoutPanel _workspaceLayout;
        private bool _syncingDetailCard;
        private bool _updatingGridFromDetail;
        private bool _recalculatingRows;
        private bool _refreshingDetailCards;
        private bool _allDetailCardsCollapsed;
        private MetroListView _recentAnalysesListBox;

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
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
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
            _statusLabel = new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "就绪。本原型已初始化本地 SQLite 数据库。",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _workspaceLayout.Controls.Add(_detailCardsArea, 0, 4);
            _workspaceLayout.Controls.Add(_grid, 0, 5);
            _workspaceLayout.Controls.Add(_statusLabel, 0, 6);
            _grid.Visible = true;
            RefreshRecentAnalysesList();
            RefreshDetailCards();
            UpdateDashboardSummary();
            UpdateSelectionActionBar();
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

            var title = new MetroLabel
            {
                Text = "成本分析",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(title, 0, 0);

            var menu = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 0),
                ColumnCount = 1,
                RowCount = 8,
                BackColor = _panel
            };
            menu.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 9; i++)
            {
                menu.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            }
            layout.Controls.Add(menu, 0, 1);

            AddMenuButton(menu, "成本分析列表", (_, __) => RefreshRecentAnalysesList(), true);
            AddMenuButton(menu, "报价单导入", OnImportQuote);
            AddMenuButton(menu, "AI学习识别", OnBatchScanQuotes);
            AddMenuButton(menu, "材料库", OnOpenMaterials);
            AddMenuButton(menu, "工艺规则", OnOpenProcessRules);
            AddMenuButton(menu, "系统设置", OnOpenAiSettings);
            AddMenuButton(menu, "OCR设置", OnOpenOcrSettings);
            AddMenuButton(menu, "预警设置", OnOpenPriceWarningSettings);
            AddMenuButton(menu, "环境检测", OnOpenEnvironmentCheck);

            layout.Controls.Add(new MetroLabel
            {
                Text = "最近单据",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = _muted
            }, 0, 2);

            _recentAnalysesListBox = new MetroListView
            {
                Dock = DockStyle.Fill,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                View = View.List,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.None,
                Style = MetroColorStyle.Blue,
                Theme = MetroThemeStyle.Light,
                UseSelectable = true
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
                RowCount = 7,
                BackColor = _panel
            };
            _workspaceLayout = layout;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            workspace.Controls.Add(layout);

            var headingRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = _panel
            };
            headingRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var heading = new MetroLabel
            {
                Text = "客户成本分析表",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            headingRow.Controls.Add(heading, 0, 0);
            layout.Controls.Add(headingRow, 0, 0);

            layout.Controls.Add(BuildHeaderPanel(), 0, 1);

            layout.Controls.Add(BuildCommandBar(), 0, 2);
            layout.Controls.Add(BuildSummaryPanel(), 0, 3);
            return workspace;
        }

        private Control BuildCommandBar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0, 8, 0, 6),
                Padding = new Padding(0)
            };
            for (var i = 0; i < 8; i++)
            {
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
            }

            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            AddCommandButton(bar, "导入报价", OnImportQuote, true, "导入单个报价单并进入确认页");
            AddCommandButton(bar, "AI识别", OnBatchScanQuotes, false, "扫描报价单文件夹，校对识别结果并学习模板/材料/工艺");
            AddCommandButton(bar, "新增", OnAddRow, false, "新增一条空白成本明细");
            AddCommandButton(bar, "删除", OnDeleteSelectedRows, false, "删除已勾选的明细行");
            AddCommandButton(bar, "价格预警", OnOpenPriceWarningReport, false, "查看供应商比价和涨价预警");
            AddCommandButton(bar, "保存", OnSaveAnalysis, false, "保存当前成本分析");
            AddCommandButton(bar, "打开", OnOpenAnalysis, false, "打开历史成本分析");
            AddCommandButton(bar, "导出", OnExportCustomerExcel, false, "导出给客户的 Excel 文件");

            return bar;
        }

        private void AddCommandButton(TableLayoutPanel panel, string text, EventHandler handler, bool primary, string tip)
        {
            var button = CreateMetroButton(text, primary);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, 10, 4);
            button.Click += handler;
            _commandTips.SetToolTip(button, tip);
            var index = panel.Controls.Count;
            var column = index % panel.ColumnCount;
            var row = index / panel.ColumnCount;

            panel.Controls.Add(button, column, row);
        }

        private Control BuildSummaryPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8)
            };
            for (var i = 0; i < 4; i++)
            {
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            }
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _summaryItemsLabel = CreateSummaryMetric("明细 0", _muted);
            _summaryAmountLabel = CreateSummaryMetric("总金额 0", _ink);
            _summaryPendingLabel = CreateSummaryMetric("待处理 0", _muted);
            _summaryWarningLabel = CreateSummaryMetric("价格预警 0", _muted);
            _summaryPendingLabel.Cursor = Cursors.Hand;
            _summaryWarningLabel.Cursor = Cursors.Hand;
            _summaryPendingLabel.Click += OnOpenIssueList;
            _summaryWarningLabel.Click += OnOpenPriceWarningReport;

            panel.Controls.Add(_summaryItemsLabel, 0, 0);
            panel.Controls.Add(_summaryAmountLabel, 1, 0);
            panel.Controls.Add(_summaryPendingLabel, 2, 0);
            panel.Controls.Add(_summaryWarningLabel, 3, 0);
            return panel;
        }

        private MetroLabel CreateSummaryMetric(string text, Color color)
        {
            return new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = text,
                ForeColor = color,
                BackColor = Color.FromArgb(250, 250, 252),
                UseCustomBackColor = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0)
            };
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
            panel.Controls.Add(new MetroLabel
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
                Height = 366,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Color.White,
                Padding = new Padding(20, 14, 20, 18),
                Margin = new Padding(0, 6, 0, 14)
            };
            ApplyRoundedRegion(shell, 14);
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
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
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

            _detailSelectedCheckBox = new MetroCheckBox
            {
                Dock = DockStyle.Fill,
                Text = string.Empty,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _detailSelectedCheckBox.CheckedChanged += OnDetailSelectedChanged;
            header.Controls.Add(_detailSelectedCheckBox, 0, 0);

            _detailTitleLabel = new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "成本明细",
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                ForeColor = _ink,
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(_detailTitleLabel, 1, 0);

            _detailStatusLabel = new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "请选择或新增一条明细",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleRight
            };
            header.Controls.Add(_detailStatusLabel, 2, 0);
            shell.Controls.Add(header, 0, 0);
            shell.Controls.Add(BuildDetailToolBar(), 0, 1);

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

            shell.Controls.Add(fields, 0, 2);
            return shell;
        }

        private Control BuildDetailToolBar()
        {
            var bar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0, 2, 0, 4)
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 4; i++)
            {
                bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            }
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _detailHintLabel = new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "当前明细工具",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            bar.Controls.Add(_detailHintLabel, 0, 0);

            AddDetailToolButton(bar, "阶梯价", OnEditRowPriceTiers, "编辑当前明细的阶梯价格");
            AddDetailToolButton(bar, "规则", OnApplyCurrentProcessRules, "按工艺规则自动填充当前明细空白费用");
            AddDetailToolButton(bar, "历史", OnOpenCostHistoryReference, "查看历史成本并套用参考值");
            AddDetailToolButton(bar, "MOQ", OnOpenMoqSimulation, "模拟阶梯起订量对整单成本的影响");
            return bar;
        }

        private void AddDetailToolButton(TableLayoutPanel panel, string text, EventHandler handler, string tip)
        {
            var button = CreateMetroButton(text, false);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(6, 4, 0, 4);
            button.Click += handler;
            _commandTips.SetToolTip(button, tip);
            var index = panel.Controls.Count;
            panel.Controls.Add(button, index, 0);
        }

        private Control BuildDetailCardsArea()
        {
            var shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0, 4, 0, 0)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "明细表单",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(title, 0, 0);

            var selectionBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.White,
                Margin = new Padding(0)
            };
            selectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            selectionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            selectionBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _selectionInfoLabel = new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "未选择",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                Margin = new Padding(0, 0, 8, 0)
            };
            selectionBar.Controls.Add(_selectionInfoLabel, 0, 0);

            _deleteSelectedButton = AddSelectionActionButton(selectionBar, "删除选中", OnDeleteSelectedRows, "删除已勾选的明细行");
            _applyRulesSelectedButton = AddSelectionActionButton(selectionBar, "规则选中", OnApplyProcessRules, "对已勾选明细应用工艺规则");

            header.Controls.Add(selectionBar, 1, 0);
            shell.Controls.Add(header, 0, 0);

            _detailCardsPanel = new MetroPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                UseCustomBackColor = true,
                Padding = new Padding(0, 0, 8, 8)
            };
            _detailCardsPanel.SizeChanged += (_, __) => ResizeDetailCards();
            shell.Controls.Add(_detailCardsPanel, 0, 1);
            return shell;
        }

        private MetroButton AddSelectionActionButton(TableLayoutPanel panel, string text, EventHandler handler, string tip)
        {
            var button = CreateMetroButton(text, false);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(6, 4, 0, 4);
            button.Enabled = false;
            button.Click += handler;
            _commandTips.SetToolTip(button, tip);
            var index = panel.Controls.Count;
            panel.Controls.Add(button, index, 0);
            return button;
        }

        private void AddSmallActionButton(TableLayoutPanel panel, string text, EventHandler handler, bool primary)
        {
            var button = CreateMetroButton(text, primary);
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(8, 6, 0, 6);
            button.Click += handler;
            var index = panel.Controls.Count;
            panel.Controls.Add(button, index, 0);
        }

        private void AddDetailField(TableLayoutPanel panel, int column, int row, string label, string columnName)
        {
            panel.Controls.Add(new MetroLabel
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
                form.ShowDialog(this);
                _statusLabel.Text = "已关闭 AI 学习识别。";
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
                var issues = CollectPreflightIssues();
                LoadCurrentRowToDetailCard();
                RefreshDetailCards();
                _statusLabel.Text = issues.Count > 0
                    ? string.Format(
                        "已加入报价单物料：{0} 条；发现 {1} 项需要确认，已展开第一条问题明细。",
                        previewForm.SelectedItems.Count,
                        issues.Count)
                    : string.Format(
                        "已加入报价单物料：{0} 条。供应商：{1}，Sheet={2}，模板={3}。",
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
            ValidateGrid();
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
            UpdateDashboardSummary();
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

            var existingTiers = GetRowPriceTiers(row);
            using (var form = new PriceTiersForm(existingTiers))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                SetRowPriceTiers(row, form.PriceTiers);
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

                var tiers = GetRowPriceTiers(row);
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
            if (source == null)
            {
                return result;
            }

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

        private static System.Collections.Generic.List<PriceTier> GetRowPriceTiers(DataGridViewRow row)
        {
            var tiers = row == null ? null : row.Tag as System.Collections.Generic.List<PriceTier>;
            NormalizeTierQuantities(tiers);
            return tiers;
        }

        private static void SetRowPriceTiers(DataGridViewRow row, System.Collections.Generic.List<PriceTier> tiers)
        {
            if (row == null)
            {
                return;
            }

            row.Tag = ClonePriceTiers(tiers);
            NormalizeTierQuantities(row.Tag as System.Collections.Generic.List<PriceTier>);
        }

        private static void NormalizeTierQuantities(System.Collections.Generic.List<PriceTier> tiers)
        {
            if (tiers == null)
            {
                return;
            }

            foreach (var tier in tiers)
            {
                if (tier == null || (tier.MinQuantity.HasValue && tier.MaxQuantity.HasValue))
                {
                    continue;
                }

                int? min = tier.MinQuantity;
                int? max = tier.MaxQuantity;
                FillQuantityRangeFromTierLabel(tier.Label, ref min, ref max);
                tier.MinQuantity = min;
                tier.MaxQuantity = max;
            }
        }

        private static void FillQuantityRangeFromTierLabel(string label, ref int? minQuantity, ref int? maxQuantity)
        {
            var text = (label ?? string.Empty)
                .Trim()
                .Replace("－", "-")
                .Replace("—", "-")
                .Replace("–", "-")
                .Replace("~", "-")
                .Replace("～", "-")
                .Replace("以上", "+");

            var parts = text.Split('-');
            if (parts.Length >= 2)
            {
                int min;
                int max;
                if (!minQuantity.HasValue && TryParseLeadingInt(parts[0], out min))
                {
                    minQuantity = min;
                }

                if (!maxQuantity.HasValue && TryParseLeadingInt(parts[1], out max))
                {
                    maxQuantity = max;
                }

                return;
            }

            int single;
            if (!minQuantity.HasValue && TryParseLeadingInt(text, out single))
            {
                minQuantity = single;
            }
        }

        private static bool TryParseLeadingInt(string value, out int result)
        {
            result = 0;
            var text = (value ?? string.Empty).Trim();
            var digits = string.Empty;
            foreach (var ch in text)
            {
                if (char.IsDigit(ch))
                {
                    digits += ch;
                    continue;
                }

                if (digits.Length > 0)
                {
                    break;
                }
            }

            return digits.Length > 0 && int.TryParse(digits, out result);
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
            if (_updatingGridFromDetail || _recalculatingRows)
            {
                return;
            }

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

            _updatingGridFromDetail = true;
            try
            {
                row.Cells[columnName].Value = textBox.Text;
                RecalculateRows();
                RefreshCalculatedDetailFields(row, textBox);
            }
            finally
            {
                _updatingGridFromDetail = false;
            }
        }

        private void RefreshCalculatedDetailFields(DataGridViewRow row, Control activeControl)
        {
            _syncingDetailCard = true;
            try
            {
                ApplyDetailStatusStyle(row);
                UpdateDetailFieldFromRow(row, "PurchaseUnitPrice", activeControl);
                UpdateDetailFieldFromRow(row, "TotalPrice", activeControl);
                UpdateDetailFieldFromRow(row, "ValidationStatus", activeControl);
            }
            finally
            {
                _syncingDetailCard = false;
            }
        }

        private void UpdateDetailFieldFromRow(DataGridViewRow row, string columnName, Control activeControl)
        {
            MetroTextBox textBox;
            if (!_detailFields.TryGetValue(columnName, out textBox) || ReferenceEquals(textBox, activeControl))
            {
                return;
            }

            textBox.Text = SafeCell(row, columnName);
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

            UpdateSelectionActionBar();
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

            var selector = new MetroCheckBox
            {
                Dock = DockStyle.Fill,
                Checked = IsRowChecked(row),
                Tag = row.Index
            };
            selector.CheckedChanged += OnCollapsedCardCheckedChanged;
            card.Controls.Add(selector, 0, 0);

            card.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "成本" + SafeCell(row, "No"),
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 1, 0);

            card.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = BuildCollapsedCardSummary(row),
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 2, 0);

            card.Controls.Add(new MetroLabel
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
            var card = new TableLayoutPanel
            {
                Height = 150,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.FromArgb(250, 250, 252),
                Padding = new Padding(18, 14, 18, 14),
                Margin = new Padding(0, 8, 0, 12)
            };
            ApplyRoundedRegion(card, 12);
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            card.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "暂无成本明细",
                ForeColor = _ink,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 0);

            card.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "可以导入供应商报价单，也可以先手动新增一条明细。",
                ForeColor = _muted,
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 1);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 1,
                BackColor = Color.FromArgb(250, 250, 252),
                Margin = new Padding(0, 6, 0, 0)
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var import = CreateMetroButton("导入报价", true);
            import.Dock = DockStyle.Fill;
            import.Click += OnImportQuote;
            actions.Controls.Add(import, 1, 0);

            var add = CreateMetroButton("新增明细", false);
            add.Dock = DockStyle.Fill;
            add.Click += OnAddRow;
            actions.Controls.Add(add, 3, 0);
            card.Controls.Add(actions, 0, 2);
            return card;
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
            var status = BuildStatusTagText(row);
            var text = string.IsNullOrWhiteSpace(name) ? code : name;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "未填写物料";
            }

            return string.IsNullOrWhiteSpace(status) ? text : text + "    " + status;
        }

        private static string BuildStatusTagText(DataGridViewRow row)
        {
            var status = SafeCell(row, "ValidationStatus");
            if (string.IsNullOrWhiteSpace(status))
            {
                return "待校验";
            }

            if (status.IndexOf("缺", StringComparison.Ordinal) >= 0)
            {
                return "缺字段";
            }

            if (status.IndexOf("价格预警", StringComparison.Ordinal) >= 0)
            {
                return "价格预警";
            }

            if (status.IndexOf("不一致", StringComparison.Ordinal) >= 0 ||
                status.IndexOf("不等于", StringComparison.Ordinal) >= 0)
            {
                return "成本异常";
            }

            if (status.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("确认", StringComparison.Ordinal) >= 0)
            {
                return "待确认";
            }

            if (status.IndexOf("已完成", StringComparison.Ordinal) >= 0)
            {
                return "已完成";
            }

            return "待确认";
        }

        private void ApplyDetailStatusStyle(DataGridViewRow row)
        {
            if (_detailStatusLabel == null)
            {
                return;
            }

            var tag = row == null ? "待选择" : BuildStatusTagText(row);
            _detailStatusLabel.Text = "状态：" + tag;
            _detailStatusLabel.BackColor = Color.White;
            _detailStatusLabel.UseCustomBackColor = true;

            if (tag == "已完成")
            {
                _detailStatusLabel.ForeColor = Color.FromArgb(42, 130, 70);
            }
            else if (tag == "缺字段" || tag == "成本异常")
            {
                _detailStatusLabel.ForeColor = Color.FromArgb(190, 70, 60);
            }
            else if (tag == "价格预警")
            {
                _detailStatusLabel.ForeColor = Color.FromArgb(200, 112, 0);
            }
            else if (tag == "待确认")
            {
                _detailStatusLabel.ForeColor = Color.FromArgb(150, 96, 0);
            }
            else
            {
                _detailStatusLabel.ForeColor = _muted;
            }

            if (row != null)
            {
                _commandTips.SetToolTip(_detailStatusLabel, SafeCell(row, "ValidationStatus"));
            }
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
                    ApplyDetailStatusStyle(null);
                    UpdateDetailHint(null);
                    _detailSelectedCheckBox.Checked = false;
                    foreach (var box in _detailFields.Values)
                    {
                        box.Text = string.Empty;
                    }

                    return;
                }

                _detailTitleLabel.Text = "成本明细 " + Convert.ToString(row.Cells["No"].Value);
                ApplyDetailStatusStyle(row);
                UpdateDetailHint(row);
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

        private void UpdateDetailHint(DataGridViewRow row)
        {
            if (_detailHintLabel == null)
            {
                return;
            }

            if (row == null)
            {
                _detailHintLabel.Text = "选择明细后显示历史参考";
                _detailHintLabel.ForeColor = _muted;
                _commandTips.SetToolTip(_detailHintLabel, string.Empty);
                return;
            }

            var materialCode = SafeCell(row, "MaterialCode");
            var materialName = SafeCell(row, "MaterialName");
            if (string.IsNullOrWhiteSpace(materialCode) && string.IsNullOrWhiteSpace(materialName))
            {
                _detailHintLabel.Text = "填写物料编码或名称后可匹配历史";
                _detailHintLabel.ForeColor = _muted;
                _commandTips.SetToolTip(_detailHintLabel, string.Empty);
                return;
            }

            var history = new CostAnalysisRepository().SearchCostHistory(materialCode, materialName, 20);
            if (history.Count == 0)
            {
                _detailHintLabel.Text = "暂无历史参考";
                _detailHintLabel.ForeColor = _muted;
                _commandTips.SetToolTip(_detailHintLabel, "未找到相同物料编码或相似物料名称的历史成本。");
                return;
            }

            var lowest = FindLowestHistoryPrice(history);
            if (lowest != null && lowest.PurchaseUnitPrice.HasValue)
            {
                _detailHintLabel.Text = "历史参考 " + history.Count + " 条，最低采购价 " + lowest.PurchaseUnitPrice.Value.ToString("0.####");
                _detailHintLabel.ForeColor = Color.FromArgb(42, 130, 70);
                _commandTips.SetToolTip(_detailHintLabel, BuildHistoryHintTooltip(lowest));
                return;
            }

            _detailHintLabel.Text = "历史参考 " + history.Count + " 条";
            _detailHintLabel.ForeColor = _blue;
            _commandTips.SetToolTip(_detailHintLabel, "可点击“历史”查看并套用参考成本。");
        }

        private static CostHistoryItem FindLowestHistoryPrice(List<CostHistoryItem> history)
        {
            CostHistoryItem lowest = null;
            foreach (var item in history)
            {
                if (!item.PurchaseUnitPrice.HasValue)
                {
                    continue;
                }

                if (lowest == null || item.PurchaseUnitPrice.Value < lowest.PurchaseUnitPrice.Value)
                {
                    lowest = item;
                }
            }

            return lowest;
        }

        private static string BuildHistoryHintTooltip(CostHistoryItem item)
        {
            return string.Format(
                "最低历史参考：{0}\r\n供应商：{1}\r\n客户：{2}\r\n项目：{3}\r\n采购单价：{4}",
                item.AnalysisNo,
                string.IsNullOrWhiteSpace(item.Supplier) ? "-" : item.Supplier,
                string.IsNullOrWhiteSpace(item.CustomerName) ? "-" : item.CustomerName,
                string.IsNullOrWhiteSpace(item.ProjectName) ? "-" : item.ProjectName,
                item.PurchaseUnitPrice.HasValue ? item.PurchaseUnitPrice.Value.ToString("0.####") : "-");
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
            UpdateSelectionActionBar(count);
            _statusLabel.Text = count == 0 ? "未选择明细行。" : "已选择 " + count + " 行明细，可使用明细标题栏的批量操作。";
        }

        private void UpdateSelectionActionBar()
        {
            UpdateSelectionActionBar(CountCheckedRows());
        }

        private void UpdateSelectionActionBar(int count)
        {
            if (_selectionInfoLabel == null)
            {
                return;
            }

            var hasSelection = count > 0;
            _selectionInfoLabel.Text = hasSelection ? "已选 " + count + " 条" : "未选择";
            _selectionInfoLabel.ForeColor = hasSelection ? _blue : _muted;

            if (_deleteSelectedButton != null)
            {
                _deleteSelectedButton.Enabled = hasSelection;
            }

            if (_applyRulesSelectedButton != null)
            {
                _applyRulesSelectedButton.Enabled = hasSelection;
            }
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

        private void OnApplyCurrentProcessRules(object sender, EventArgs e)
        {
            var row = GetCurrentDataRow();
            if (row == null)
            {
                MessageBox.Show(this, "请先选择一条明细。", "应用规则", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var changed = ApplyProcessRulesToRows(new List<DataGridViewRow> { row });
            RecalculateRows();
            LoadCurrentRowToDetailCard();
            _statusLabel.Text = "已对当前明细应用工艺规则，更新费用单元格 " + changed + " 个。";
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

        private void OnOpenPriceWarningSettings(object sender, EventArgs e)
        {
            using (var form = new PriceWarningSettingsForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    ValidateGrid();
                    _statusLabel.Text = "价格预警阈值已保存，并已重新校验当前明细。";
                }
            }
        }

        private void OnOpenEnvironmentCheck(object sender, EventArgs e)
        {
            using (var form = new EnvironmentCheckForm())
            {
                form.ShowDialog(this);
            }
        }

        private void OnExportCustomerExcel(object sender, EventArgs e)
        {
            RecalculateRows();
            if (!ConfirmPreflightIssues("导出"))
            {
                return;
            }

            if (!ShowCustomerExportPreview())
            {
                _statusLabel.Text = "已取消导出，返回修改。";
                return;
            }

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

        private bool ShowCustomerExportPreview()
        {
            using (var form = new CustomerExportPreviewForm(ReadHeader(), _grid))
            {
                return form.ShowDialog(this) == DialogResult.OK;
            }
        }

        private void OnSaveAnalysis(object sender, EventArgs e)
        {
            RecalculateRows();
            if (!ConfirmPreflightIssues("保存"))
            {
                return;
            }

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
                    _recentAnalysesListBox.Items.Add(new ListViewItem(item.DisplayText) { Tag = item });
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
            if (_recentAnalysesListBox.SelectedItems.Count == 0)
            {
                return;
            }

            var selected = _recentAnalysesListBox.SelectedItems[0].Tag as CostAnalysisSummary;
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
            var issues = CollectPreflightIssues();
            var issueRowIndex = FindFirstIssueRowIndex(issues);
            if (issueRowIndex >= 0)
            {
                SelectGridRow(issueRowIndex);
                _statusLabel.Text = "已打开成本分析单，ID=" + analysisId + "，明细 " + analysis.Items.Count + " 条；发现 " + issues.Count + " 项需要确认，已展开第一条。";
            }
            else
            {
                _statusLabel.Text = "已打开成本分析单，ID=" + analysisId + "，明细 " + analysis.Items.Count + " 条；全部明细已完成。";
            }
        }

        private void RecalculateRows()
        {
            if (_recalculatingRows)
            {
                return;
            }

            _recalculatingRows = true;
            try
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.IsNewRow)
                    {
                        continue;
                    }

                    var quantity = ReadDecimal(row.Cells["TotalQuantity"].Value);
                    var unitPrice = ReadDecimal(row.Cells["PurchaseUnitPrice"].Value);
                    AutoFillAreaMaterialCost(row);
                    var costTotal = CalculateCostComponentTotal(row);
                    if (costTotal.HasValue)
                    {
                        unitPrice = costTotal.Value;
                        SetCellTextIfChanged(row, "PurchaseUnitPrice", costTotal.Value.ToString("0.####"));
                        row.Cells["PurchaseUnitPrice"].ToolTipText = "采购单价已按材料费+印刷费+后工序费+其他费用合计";
                    }
                    else
                    {
                        var tiers = GetRowPriceTiers(row);
                        if (quantity.HasValue && tiers != null)
                        {
                            var matched = ExcelQuoteImportService.MatchTier(tiers, quantity.Value);
                            if (matched != null && matched.UnitPrice.HasValue)
                            {
                                unitPrice = matched.UnitPrice;
                                SetCellTextIfChanged(row, "PurchaseUnitPrice", unitPrice.Value.ToString("0.####"));
                            }
                        }
                    }

                    if (unitPrice.HasValue && quantity.HasValue)
                    {
                        SetCellTextIfChanged(row, "TotalPrice", (unitPrice.Value * quantity.Value).ToString("0.####"));
                    }
                }

                ValidateGrid();
            }
            finally
            {
                _recalculatingRows = false;
            }
        }

        private static void SetCellTextIfChanged(DataGridViewRow row, string columnName, string value)
        {
            var next = value ?? string.Empty;
            var current = Convert.ToString(row.Cells[columnName].Value);
            if (!string.Equals(current, next, StringComparison.Ordinal))
            {
                row.Cells[columnName].Value = next;
            }
        }

        private void AutoFillAreaMaterialCost(DataGridViewRow row)
        {
            if (row == null || !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells["MaterialCost"].Value)))
            {
                return;
            }

            var materialUnitPrice = ReadDecimal(row.Cells["MaterialUnitPrice"].Value);
            var text = string.Join(" ", new[]
            {
                SafeCell(row, "MaterialName"),
                SafeCell(row, "MaterialDescription"),
                SafeCell(row, "BaseMaterialName")
            });
            var sizeText = SafeCell(row, "ExpandedSize");
            var result = new AreaMaterialCostService().TryCalculateStickerCost(text, sizeText, materialUnitPrice);
            if (!result.Amount.HasValue)
            {
                return;
            }

            SetCellTextIfChanged(row, "MaterialCost", result.Amount.Value.ToString("0.####"));
            row.Cells["MaterialCost"].ToolTipText = result.Evidence;
        }

        private decimal? CalculateCostComponentTotal(DataGridViewRow row)
        {
            var materialCost = ReadDecimal(row.Cells["MaterialCost"].Value);
            var printingCost = ReadDecimal(row.Cells["PrintingCost"].Value);
            var postProcessCost = ReadDecimal(row.Cells["PostProcessCost"].Value);
            var otherCost = ReadDecimal(row.Cells["OtherCost"].Value);
            if (!materialCost.HasValue && !printingCost.HasValue && !postProcessCost.HasValue && !otherCost.HasValue)
            {
                return null;
            }

            return materialCost.GetValueOrDefault() +
                   printingCost.GetValueOrDefault() +
                   postProcessCost.GetValueOrDefault() +
                   otherCost.GetValueOrDefault();
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

                var priceWarning = EvaluateRowPriceWarning(row);
                if (priceWarning.Severity != PriceWarningSeverity.None)
                {
                    if (!IsRowChecked(row))
                    {
                        row.DefaultCellStyle.BackColor = priceWarning.Severity == PriceWarningSeverity.Red
                            ? Color.FromArgb(255, 241, 240)
                            : _warningBack;
                    }
                    messages.Add("价格预警：" + priceWarning.Message);
                }

                if (messages.Count > 0)
                {
                    warningCount++;
                    var status = string.Join("；", messages.ToArray());
                    row.Cells["ValidationStatus"].Value = status;
                    ApplyValidationStatusCellStyle(row, status);
                }
                else
                {
                    row.Cells["ValidationStatus"].Value = "已完成";
                    ApplyValidationStatusCellStyle(row, "已完成");
                }
            }

            if (warningCount > 0)
            {
                _statusLabel.Text = "校验完成：发现 " + warningCount + " 行需要确认。";
            }

            UpdateDashboardSummary();
            return warningCount;
        }

        private PriceWarningResult EvaluateRowPriceWarning(DataGridViewRow row)
        {
            var unitPrice = ReadDecimal(row.Cells["PurchaseUnitPrice"].Value);
            if (!unitPrice.HasValue || unitPrice.Value <= 0)
            {
                return PriceWarningResult.Empty;
            }

            var item = new QuoteImportItem
            {
                MaterialCode = SafeCell(row, "MaterialCode"),
                MaterialName = SafeCell(row, "MaterialName"),
                RawName = SafeCell(row, "MaterialName"),
                FinishedSize = SafeCell(row, "ExpandedSize"),
                MaterialNameExtracted = SafeCell(row, "BaseMaterialName"),
                GramWeight = SafeCell(row, "GramWeight"),
                PriceTiers = new List<PriceTier>
                {
                    new PriceTier
                    {
                        Label = "当前采购单价",
                        UnitPrice = unitPrice
                    }
                }
            };

            return _priceWarningService.EvaluateQuoteItem(SafeCell(row, "Supplier"), item);
        }

        private void ApplyValidationStatusCellStyle(DataGridViewRow row, string status)
        {
            var cell = row.Cells["ValidationStatus"];
            cell.ToolTipText = status ?? string.Empty;
            cell.Style.ForeColor = _muted;
            cell.Style.BackColor = Color.Empty;

            var tag = BuildStatusTagText(row);
            if (tag == "已完成")
            {
                cell.Style.ForeColor = Color.FromArgb(42, 130, 70);
                cell.Style.BackColor = Color.FromArgb(240, 250, 243);
            }
            else if (tag == "缺字段" || tag == "成本异常")
            {
                cell.Style.ForeColor = Color.FromArgb(190, 70, 60);
                cell.Style.BackColor = Color.FromArgb(255, 241, 240);
            }
            else if (tag == "价格预警")
            {
                cell.Style.ForeColor = Color.FromArgb(200, 112, 0);
                cell.Style.BackColor = Color.FromArgb(255, 247, 230);
            }
            else if (tag == "待确认")
            {
                cell.Style.ForeColor = Color.FromArgb(150, 96, 0);
                cell.Style.BackColor = Color.FromArgb(255, 247, 230);
            }
        }

        private void UpdateDashboardSummary()
        {
            if (_summaryItemsLabel == null)
            {
                return;
            }

            var rowCount = 0;
            var pendingCount = 0;
            var priceWarningCount = 0;
            decimal totalAmount = 0;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                rowCount++;
                var totalPrice = ReadDecimal(row.Cells["TotalPrice"].Value);
                if (totalPrice.HasValue)
                {
                    totalAmount += totalPrice.Value;
                }

                var status = SafeCell(row, "ValidationStatus");
                if (string.IsNullOrWhiteSpace(status) || status != "已完成")
                {
                    pendingCount++;
                }

                if (status.IndexOf("价格预警", StringComparison.Ordinal) >= 0)
                {
                    priceWarningCount++;
                }
            }

            _summaryItemsLabel.Text = "明细 " + rowCount;
            _summaryAmountLabel.Text = "总金额 " + totalAmount.ToString("0.####");
            _summaryPendingLabel.Text = "待处理 " + pendingCount;
            _summaryWarningLabel.Text = "价格预警 " + priceWarningCount;

            _summaryPendingLabel.ForeColor = pendingCount > 0 ? Color.FromArgb(190, 70, 60) : Color.FromArgb(42, 130, 70);
            _summaryWarningLabel.ForeColor = priceWarningCount > 0 ? Color.FromArgb(200, 112, 0) : _muted;
            _commandTips.SetToolTip(_summaryAmountLabel, "按各明细总价汇总。");
            _commandTips.SetToolTip(_summaryPendingLabel, pendingCount > 0 ? "点击查看待处理问题清单。" : "当前明细均已完成。");
            _commandTips.SetToolTip(_summaryWarningLabel, priceWarningCount > 0 ? "点击查看价格预警明细。" : "暂无价格预警。");
        }

        private void OnOpenIssueList(object sender, EventArgs e)
        {
            var issues = CollectPreflightIssues();
            if (issues.Count == 0)
            {
                MessageBox.Show(this, "当前没有需要处理的问题。", "问题清单", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _statusLabel.Text = "检查完成：所有明细均已完成。";
                return;
            }

            using (var form = new PreflightIssuesForm(BuildIssueTable(issues)))
            {
                if (form.ShowDialog(this) == DialogResult.OK && form.SelectedRowIndex >= 0)
                {
                    SelectGridRow(form.SelectedRowIndex);
                    _statusLabel.Text = "已定位到问题明细：" + (form.SelectedIssueTitle ?? string.Empty);
                }
            }
        }

        private void OnOpenPriceWarningReport(object sender, EventArgs e)
        {
            var warnings = BuildPriceWarningTable();
            if (warnings.Rows.Count == 0)
            {
                MessageBox.Show(this, "当前没有价格预警。", "价格预警", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _statusLabel.Text = "价格预警检查完成：暂无风险。";
                return;
            }

            using (var form = new PriceWarningReportForm(warnings))
            {
                var result = form.ShowDialog(this);
                if (result == DialogResult.Retry || form.SettingsChanged)
                {
                    ValidateGrid();
                    _statusLabel.Text = "价格预警阈值已更新，并已重新校验当前明细。";
                    return;
                }

                if ((result == DialogResult.Yes || form.HistoryRequested) &&
                    (!string.IsNullOrWhiteSpace(form.SelectedMaterialCode) || !string.IsNullOrWhiteSpace(form.SelectedMaterialName)))
                {
                    OpenHistoryForWarning(form.SelectedMaterialCode, form.SelectedMaterialName, form.SelectedItemTitle);
                    return;
                }

                if (result == DialogResult.OK && form.SelectedRowIndex >= 0)
                {
                    SelectGridRow(form.SelectedRowIndex);
                    _statusLabel.Text = "已定位到价格预警明细：" + (form.SelectedItemTitle ?? string.Empty);
                }
            }
        }

        private void OpenHistoryForWarning(string materialCode, string materialName, string title)
        {
            var items = new CostAnalysisRepository().SearchCostHistory(materialCode, materialName, 120);
            if (items.Count == 0)
            {
                MessageBox.Show(this, "当前预警明细没有找到可查看的历史记录。", "历史成本参考", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _statusLabel.Text = "未找到价格预警对应的历史记录：" + (title ?? string.Empty);
                return;
            }

            using (var form = new CostHistoryReferenceForm(materialCode, materialName, items, false))
            {
                form.ShowDialog(this);
            }

            _statusLabel.Text = "已查看价格预警历史记录：" + (title ?? string.Empty);
        }

        private DataTable BuildPriceWarningTable()
        {
            ValidateGrid();
            var table = new DataTable();
            table.Columns.Add("RowIndex", typeof(int));
            table.Columns.Add("MaterialCode", typeof(string));
            table.Columns.Add("MaterialName", typeof(string));
            table.Columns.Add("级别", typeof(string));
            table.Columns.Add("明细", typeof(string));
            table.Columns.Add("供应商", typeof(string));
            table.Columns.Add("采购单价", typeof(string));
            table.Columns.Add("预警原因", typeof(string));
            table.Columns.Add("历史依据", typeof(string));

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var warning = EvaluateRowPriceWarning(row);
                if (warning.Severity == PriceWarningSeverity.None)
                {
                    continue;
                }

                table.Rows.Add(
                    row.Index,
                    SafeCell(row, "MaterialCode"),
                    SafeCell(row, "MaterialName"),
                    warning.Severity == PriceWarningSeverity.Red ? "红色" : "黄色",
                    BuildIssueRowTitle(row),
                    SafeCell(row, "Supplier"),
                    SafeCell(row, "PurchaseUnitPrice"),
                    warning.Message,
                    warning.Evidence);
            }

            var view = table.DefaultView;
            view.Sort = "级别 DESC, 采购单价 ASC";
            return view.ToTable();
        }

        private static DataTable BuildIssueTable(List<PreflightIssue> issues)
        {
            var table = new DataTable();
            table.Columns.Add("RowIndex", typeof(int));
            table.Columns.Add("位置", typeof(string));
            table.Columns.Add("问题", typeof(string));

            foreach (var issue in issues)
            {
                table.Rows.Add(issue.RowIndex, issue.Title, issue.Message);
            }

            return table;
        }

        private List<PreflightIssue> CollectPreflightIssues()
        {
            ValidateGrid();
            var issues = new List<PreflightIssue>();
            var hasRows = false;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                hasRows = true;
                var status = SafeCell(row, "ValidationStatus");
                if (string.IsNullOrWhiteSpace(status) || status == "已完成")
                {
                    continue;
                }

                issues.Add(new PreflightIssue
                {
                    RowIndex = row.Index,
                    Title = BuildIssueRowTitle(row),
                    Message = status
                });
            }

            if (!hasRows)
            {
                issues.Add(new PreflightIssue
                {
                    RowIndex = -1,
                    Title = "当前分析单",
                    Message = "暂无成本明细"
                });
            }

            return issues;
        }

        private bool ConfirmPreflightIssues(string actionName)
        {
            var issues = CollectPreflightIssues();
            if (issues.Count == 0)
            {
                _statusLabel.Text = "检查完成：所有明细均已完成。";
                return true;
            }

            var firstRowIndex = FindFirstIssueRowIndex(issues);
            if (firstRowIndex >= 0)
            {
                SelectGridRow(firstRowIndex);
            }

            var message = BuildPreflightMessage(actionName, issues);
            var result = MessageBox.Show(
                this,
                message,
                actionName + "前检查",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            _statusLabel.Text = "检查完成：发现 " + issues.Count + " 项需要确认。";
            return result == DialogResult.Yes;
        }

        private static int FindFirstIssueRowIndex(List<PreflightIssue> issues)
        {
            foreach (var issue in issues)
            {
                if (issue.RowIndex >= 0)
                {
                    return issue.RowIndex;
                }
            }

            return -1;
        }

        private static string BuildPreflightMessage(string actionName, List<PreflightIssue> issues)
        {
            var lines = new List<string>();
            lines.Add(actionName + "前发现 " + issues.Count + " 项需要确认：");
            lines.Add(string.Empty);

            var take = Math.Min(8, issues.Count);
            for (var i = 0; i < take; i++)
            {
                var issue = issues[i];
                lines.Add((i + 1) + ". " + issue.Title + "：" + issue.Message);
            }

            if (issues.Count > take)
            {
                lines.Add("……还有 " + (issues.Count - take) + " 项未显示。");
            }

            lines.Add(string.Empty);
            lines.Add(FindFirstIssueRowIndex(issues) >= 0
                ? "已自动展开第一条问题明细。是否仍然继续" + actionName + "？"
                : "是否仍然继续" + actionName + "？");
            return string.Join("\r\n", lines.ToArray());
        }

        private static string BuildIssueRowTitle(DataGridViewRow row)
        {
            var no = SafeCell(row, "No");
            var name = SafeCell(row, "MaterialName");
            var code = SafeCell(row, "MaterialCode");
            var title = string.IsNullOrWhiteSpace(name) ? code : name;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = "未填写物料";
            }

            return "成本" + no + " " + title;
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
                SetRowPriceTiers(row, item.PriceTiers);
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

            var appendedRows = GetRowsByStartIndex(_grid.Rows.Count - items.Count);
            ApplyProcessRulesToRows(appendedRows);
            RecalculateRows();
            var firstNewRow = Math.Max(0, _grid.Rows.Count - items.Count);
            var firstProblemRow = FindFirstProblemRowIndexFromRows(appendedRows);
            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[firstProblemRow >= 0 ? firstProblemRow : firstNewRow].Cells["MaterialCode"];
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

        private static int FindFirstProblemRowIndexFromRows(List<DataGridViewRow> rows)
        {
            if (rows == null)
            {
                return -1;
            }

            foreach (var row in rows)
            {
                if (row == null || row.IsNewRow)
                {
                    continue;
                }

                var status = SafeCell(row, "ValidationStatus");
                if (!string.IsNullOrWhiteSpace(status) && status != "已完成")
                {
                    return row.Index;
                }
            }

            return -1;
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
                    SetRowPriceTiers(row, item.PriceTiers);
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

        private void AddMenuButton(TableLayoutPanel panel, string text, EventHandler handler, bool active = false)
        {
            var button = CreateMetroButton(text, active);
            button.Dock = DockStyle.Fill;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Margin = new Padding(0, 0, 0, 8);
            if (handler != null)
            {
                button.Click += handler;
            }
            var row = panel.Controls.Count;
            panel.Controls.Add(button, 0, row);
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
            foreach (var match in matches)
            {
                var textBox = match as MetroTextBox;
                if (textBox != null)
                {
                    return textBox;
                }
            }

            if (matches.Length == 0)
            {
                throw new InvalidOperationException("找不到输入框：" + name);
            }

            throw new InvalidOperationException("找到的控件不是 MetroTextBox：" + name + "，实际类型：" + matches[0].GetType().FullName);
        }

        private static bool DirectoryExists(string path)
        {
            return System.IO.Directory.Exists(path);
        }
    }
}
