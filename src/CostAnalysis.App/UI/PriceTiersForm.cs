using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using CostAnalysis.App.Services;

namespace CostAnalysis.App.UI
{
    internal sealed class PriceTiersForm : Form
    {
        private readonly DataGridView _grid;

        public List<PriceTier> PriceTiers { get; private set; }

        public PriceTiersForm(List<PriceTier> priceTiers)
        {
            PriceTiers = CloneTiers(priceTiers);

            Text = "编辑阶梯价格";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(640, 420);
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            _grid = BuildGrid();
            root.Controls.Add(_grid, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(buttons, 0, 1);

            var ok = new Button { Text = "确定", Width = 86, Height = 32 };
            ok.Click += (_, __) => Confirm();
            buttons.Controls.Add(ok);

            var cancel = new Button { Text = "取消", Width = 86, Height = 32 };
            cancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);

            var add = new Button { Text = "新增阶梯", Width = 96, Height = 32 };
            add.Click += (_, __) => _grid.Rows.Add();
            buttons.Controls.Add(add);

            LoadTiers();
        }

        private static List<PriceTier> CloneTiers(List<PriceTier> priceTiers)
        {
            var result = new List<PriceTier>();
            if (priceTiers == null)
            {
                return result;
            }

            foreach (var tier in priceTiers)
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

        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };

            AddColumn(grid, "Label", "阶梯标签");
            AddColumn(grid, "MinQuantity", "最小数量");
            AddColumn(grid, "MaxQuantity", "最大数量");
            AddColumn(grid, "UnitPrice", "单价");
            return grid;
        }

        private void LoadTiers()
        {
            foreach (var tier in PriceTiers)
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Label"].Value = tier.Label;
                row.Cells["MinQuantity"].Value = tier.MinQuantity.HasValue ? tier.MinQuantity.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
                row.Cells["MaxQuantity"].Value = tier.MaxQuantity.HasValue ? tier.MaxQuantity.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
                row.Cells["UnitPrice"].Value = tier.UnitPrice.HasValue ? tier.UnitPrice.Value.ToString("0.####") : string.Empty;
            }
        }

        private void Confirm()
        {
            _grid.EndEdit();
            var tiers = new List<PriceTier>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var label = ReadCell(row, "Label");
                var unitPriceText = ReadCell(row, "UnitPrice");
                if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(unitPriceText))
                {
                    continue;
                }

                int? minQuantity;
                int? maxQuantity;
                decimal? unitPrice;
                if (!TryParseOptionalInt(ReadCell(row, "MinQuantity"), out minQuantity))
                {
                    ShowCellError(row, "MinQuantity", "最小数量必须是整数，或留空。");
                    return;
                }

                if (!TryParseOptionalInt(ReadCell(row, "MaxQuantity"), out maxQuantity))
                {
                    ShowCellError(row, "MaxQuantity", "最大数量必须是整数，或留空。");
                    return;
                }

                if ((!minQuantity.HasValue || !maxQuantity.HasValue) && !string.IsNullOrWhiteSpace(label))
                {
                    FillQuantityRangeFromLabel(label, ref minQuantity, ref maxQuantity);
                }

                if (!TryParseOptionalDecimal(unitPriceText, out unitPrice) || !unitPrice.HasValue)
                {
                    ShowCellError(row, "UnitPrice", "单价必须是数字。");
                    return;
                }

                if (minQuantity.HasValue && maxQuantity.HasValue && minQuantity.Value > maxQuantity.Value)
                {
                    ShowCellError(row, "MaxQuantity", "最大数量不能小于最小数量。");
                    return;
                }

                tiers.Add(new PriceTier
                {
                    Label = string.IsNullOrWhiteSpace(label) ? BuildLabel(minQuantity, maxQuantity) : label,
                    MinQuantity = minQuantity,
                    MaxQuantity = maxQuantity,
                    UnitPrice = unitPrice
                });
            }

            if (tiers.Count == 0)
            {
                MessageBox.Show(this, "请至少保留一条有效阶梯价格。", "编辑阶梯价格", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            PriceTiers = tiers;
            DialogResult = DialogResult.OK;
        }

        private void ShowCellError(DataGridViewRow row, string columnName, string message)
        {
            _grid.CurrentCell = row.Cells[columnName];
            row.Cells[columnName].ErrorText = message;
            MessageBox.Show(this, message, "编辑阶梯价格", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string BuildLabel(int? minQuantity, int? maxQuantity)
        {
            if (minQuantity.HasValue && maxQuantity.HasValue)
            {
                return minQuantity.Value + "-" + maxQuantity.Value;
            }

            return minQuantity.HasValue ? minQuantity.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private static void FillQuantityRangeFromLabel(string label, ref int? minQuantity, ref int? maxQuantity)
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

            return digits.Length > 0 && int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseOptionalInt(string value, out int? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            int parsed;
            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ||
                int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
            {
                result = parsed;
                return true;
            }

            return false;
        }

        private static bool TryParseOptionalDecimal(string value, out decimal? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            decimal parsed;
            if (decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) ||
                decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            {
                result = parsed;
                return true;
            }

            return false;
        }

        private static string ReadCell(DataGridViewRow row, string columnName)
        {
            return Convert.ToString(row.Cells[columnName].Value).Trim();
        }

        private static void AddColumn(DataGridView grid, string name, string header)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
    }
}
