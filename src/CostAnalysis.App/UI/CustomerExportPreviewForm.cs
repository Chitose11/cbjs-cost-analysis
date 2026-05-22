using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace CostAnalysis.App.UI
{
    internal sealed class CustomerExportPreviewForm : MetroForm
    {
        private readonly CostAnalysisHeader _header;
        private readonly DataGridView _sourceGrid;
        private readonly MetroGrid _previewGrid;
        private readonly MetroLabel _summaryLabel;
        private readonly MetroLabel _exportStatsLabel;

        public CustomerExportPreviewForm(CostAnalysisHeader header, DataGridView sourceGrid)
        {
            _header = header;
            _sourceGrid = sourceGrid;

            Text = "客户导出预览";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 620);
            MinimumSize = new Size(900, 520);
            Style = MetroColorStyle.Blue;
            Theme = MetroThemeStyle.Light;
            ShadowType = MetroFormShadowType.DropShadow;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(18),
                BackColor = Color.White
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            Controls.Add(root);

            root.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "客户成本分析表",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(29, 29, 31),
                Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold)
            }, 0, 0);

            _summaryLabel = new MetroLabel
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(29, 29, 31),
                Text = BuildHeaderSummary(),
                BackColor = Color.FromArgb(247, 249, 252),
                UseCustomBackColor = true,
                Padding = new Padding(12, 0, 12, 0)
            };
            root.Controls.Add(_summaryLabel, 0, 1);

            root.Controls.Add(new MetroLabel
            {
                Dock = DockStyle.Fill,
                Text = "预览内容会导出给客户；选择状态、校验状态、价格预警、AI 依据等内部字段不会导出。",
                ForeColor = Color.FromArgb(110, 110, 115),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 2);

            _previewGrid = BuildPreviewGrid();
            root.Controls.Add(_previewGrid, 0, 3);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.White
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _exportStatsLabel = new MetroLabel
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(110, 110, 115)
            };
            actions.Controls.Add(_exportStatsLabel, 0, 0);

            var cancel = CreateButton("返回修改", false);
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            actions.Controls.Add(cancel, 1, 0);

            var confirm = CreateButton("确认导出", true);
            confirm.Click += (_, __) => DialogResult = DialogResult.OK;
            actions.Controls.Add(confirm, 2, 0);
            root.Controls.Add(actions, 0, 4);

            LoadPreviewRows();
        }

        private string BuildHeaderSummary()
        {
            if (_header == null)
            {
                return "客户成本分析表";
            }

            return string.Format(
                "分析单号：{0}\r\n客户：{1}    项目：{2}    日期：{3}    税费：{4}    运费：{5}",
                Safe(_header.AnalysisNo),
                Safe(_header.CustomerName),
                Safe(_header.ProjectName),
                Safe(_header.AnalysisDate),
                Safe(_header.TaxNote),
                Safe(_header.FreightNote));
        }

        private MetroGrid BuildPreviewGrid()
        {
            var grid = new MetroGrid
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                Style = MetroColorStyle.Blue,
                Theme = MetroThemeStyle.Light
            };
            grid.EnableHeadersVisualStyles = false;
            grid.BorderStyle = System.Windows.Forms.BorderStyle.None;
            grid.GridColor = Color.FromArgb(225, 229, 234);
            grid.ColumnHeadersHeight = 38;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(22, 119, 255);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 244, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(29, 29, 31);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.RowTemplate.Height = 32;
            return grid;
        }

        private void LoadPreviewRows()
        {
            var columns = GetCustomerColumns(_sourceGrid);
            foreach (var sourceColumn in columns)
            {
                _previewGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = sourceColumn.Name,
                    HeaderText = sourceColumn.HeaderText,
                    Width = EstimatePreviewColumnWidth(sourceColumn),
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });

                var addedColumn = _previewGrid.Columns[_previewGrid.Columns.Count - 1];
                if (IsAmountColumn(sourceColumn) || IsQuantityColumn(sourceColumn))
                {
                    addedColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }

            if (_sourceGrid == null)
            {
                UpdateExportStats(0, null);
                return;
            }

            var rowCount = 0;
            decimal totalAmount = 0;
            var hasTotalAmount = false;
            var totalPriceIndex = FindColumnIndex(columns, "TotalPrice");
            foreach (DataGridViewRow sourceRow in _sourceGrid.Rows)
            {
                if (sourceRow.IsNewRow)
                {
                    continue;
                }

                var rowValues = new List<string>();
                var hasValue = false;
                foreach (var column in columns)
                {
                    var value = sourceRow.Cells[column.Name].Value;
                    var text = value == null ? string.Empty : Convert.ToString(value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        hasValue = true;
                    }

                    rowValues.Add(text);
                }

                if (hasValue)
                {
                    _previewGrid.Rows.Add(rowValues.ToArray());
                    rowCount++;

                    if (totalPriceIndex >= 0 && totalPriceIndex < rowValues.Count)
                    {
                        decimal totalPrice;
                        if (decimal.TryParse(rowValues[totalPriceIndex], out totalPrice))
                        {
                            totalAmount += totalPrice;
                            hasTotalAmount = true;
                        }
                    }
                }
            }

            AddTotalRow(columns, hasTotalAmount ? (decimal?)totalAmount : null);
            UpdateExportStats(rowCount, hasTotalAmount ? (decimal?)totalAmount : null);
        }

        private static List<DataGridViewColumn> GetCustomerColumns(DataGridView grid)
        {
            var result = new List<DataGridViewColumn>();
            if (grid == null)
            {
                return result;
            }

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible || column.Name == "Selected" || column.Name == "ValidationStatus")
                {
                    continue;
                }

                result.Add(column);
            }

            return result;
        }

        private void AddTotalRow(List<DataGridViewColumn> columns, decimal? totalAmount)
        {
            if (_previewGrid.Columns.Count == 0 || _previewGrid.Rows.Count == 0)
            {
                return;
            }

            var values = new string[_previewGrid.Columns.Count];
            values[0] = "合计";
            var totalPriceIndex = FindColumnIndex(columns, "TotalPrice");
            if (totalPriceIndex >= 0 && totalPriceIndex < values.Length && totalAmount.HasValue)
            {
                values[totalPriceIndex] = totalAmount.Value.ToString("0.####");
            }

            var rowIndex = _previewGrid.Rows.Add(values);
            var row = _previewGrid.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = Color.FromArgb(234, 244, 255);
            row.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        }

        private void UpdateExportStats(int rowCount, decimal? totalAmount)
        {
            _exportStatsLabel.Text = totalAmount.HasValue
                ? "将导出 " + rowCount + " 条明细，总价合计 " + totalAmount.Value.ToString("0.####")
                : "将导出 " + rowCount + " 条明细";
        }

        private static int EstimatePreviewColumnWidth(DataGridViewColumn column)
        {
            if (column.Name == "MaterialDescription")
            {
                return 280;
            }

            if (column.Name == "MaterialName")
            {
                return 180;
            }

            if (column.Name == "Supplier")
            {
                return 190;
            }

            if (column.Name == "MaterialCode")
            {
                return 140;
            }

            if (IsAmountColumn(column) || IsQuantityColumn(column))
            {
                return 96;
            }

            return Math.Max(90, Math.Min(150, column.Width));
        }

        private static int FindColumnIndex(List<DataGridViewColumn> columns, string columnName)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].Name == columnName)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsAmountColumn(DataGridViewColumn column)
        {
            return column.Name == "MaterialUnitPrice" ||
                   column.Name == "MaterialCost" ||
                   column.Name == "PrintingCost" ||
                   column.Name == "PostProcessCost" ||
                   column.Name == "OtherCost" ||
                   column.Name == "PurchaseUnitPrice" ||
                   column.Name == "TotalPrice";
        }

        private static bool IsQuantityColumn(DataGridViewColumn column)
        {
            return column.Name == "TotalQuantity" ||
                   column.Name == "GramWeight";
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

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }
    }
}
