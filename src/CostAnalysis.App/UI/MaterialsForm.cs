using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CostAnalysis.App.Data;

namespace CostAnalysis.App.UI
{
    internal sealed class MaterialsForm : Form
    {
        private readonly DataGridView _grid;
        private readonly DataGridView _sourceGrid;

        public MaterialsForm() : this(null)
        {
        }

        public MaterialsForm(DataGridView sourceGrid)
        {
            _sourceGrid = sourceGrid;
            Text = "材料库";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 560);
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(toolbar, 0, 0);

            var save = new Button { Text = "保存", Width = 86, Height = 30 };
            save.Click += OnSave;
            toolbar.Controls.Add(save);

            var add = new Button { Text = "新增材料", Width = 96, Height = 30 };
            add.Click += (_, __) => _grid.Rows.Add();
            toolbar.Controls.Add(add);

            var scan = new Button { Text = "扫描填入", Width = 96, Height = 30 };
            scan.Click += OnScanFromCurrentAnalysis;
            toolbar.Controls.Add(scan);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };
            root.Controls.Add(_grid, 0, 1);

            AddColumn("Name", "材料名称");
            AddColumn("Aliases", "别名");
            AddColumn("Category", "类别");
            AddColumn("Vendor", "材料厂家");
            AddColumn("Spec", "克重规格");
            AddColumn("Unit", "计价单位");
            AddColumn("TaxUnitPrice", "含税单价");
            AddColumn("IncludesFreight", "含运");
            AddColumn("Remark", "备注");

            LoadMaterials();
        }

        private void OnScanFromCurrentAnalysis(object sender, EventArgs e)
        {
            var added = ScanFromCurrentAnalysis();
            MessageBox.Show(this, added == 0 ? "没有扫描到新的材料。" : "已从当前成本分析表扫描新增材料 " + added + " 条。", "扫描填入", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private int ScanFromCurrentAnalysis()
        {
            if (_sourceGrid == null)
            {
                return 0;
            }

            var existing = BuildExistingMaterialSet();
            var added = 0;
            foreach (DataGridViewRow sourceRow in _sourceGrid.Rows)
            {
                if (sourceRow.IsNewRow)
                {
                    continue;
                }

                var materialText = ReadSourceCell(sourceRow, "BaseMaterialName");
                var specText = ReadSourceCell(sourceRow, "GramWeight");
                var vendor = ReadSourceCell(sourceRow, "MaterialVendor");
                foreach (var materialName in SplitMaterials(materialText))
                {
                    var key = Normalize(materialName);
                    if (string.IsNullOrWhiteSpace(key) || existing.Contains(key))
                    {
                        continue;
                    }

                    existing.Add(key);
                    var rowIndex = _grid.Rows.Add();
                    var row = _grid.Rows[rowIndex];
                    row.Cells["Name"].Value = materialName;
                    row.Cells["Category"].Value = GuessCategory(materialName);
                    row.Cells["Vendor"].Value = vendor;
                    row.Cells["Spec"].Value = PickSpecForMaterial(materialName, specText);
                    row.Cells["Unit"].Value = "张";
                    row.Cells["IncludesFreight"].Value = "是";
                    row.Cells["Remark"].Value = "从当前成本分析表扫描";
                    row.DefaultCellStyle.BackColor = Color.FromArgb(232, 244, 255);
                    added++;
                }
            }

            return added;
        }

        private HashSet<string> BuildExistingMaterialSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.IsNewRow)
                {
                    var name = ReadCell(row, "Name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        set.Add(Normalize(name));
                    }
                }
            }

            return set;
        }

        private void LoadMaterials()
        {
            _grid.Rows.Clear();
            foreach (var material in new MaterialRepository().GetAll())
            {
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Cells["Name"].Value = material.Name;
                row.Cells["Aliases"].Value = material.Aliases;
                row.Cells["Category"].Value = material.Category;
                row.Cells["Vendor"].Value = material.Vendor;
                row.Cells["Spec"].Value = material.Spec;
                row.Cells["Unit"].Value = material.Unit;
                row.Cells["TaxUnitPrice"].Value = material.TaxUnitPrice.HasValue ? material.TaxUnitPrice.Value.ToString("0.####") : string.Empty;
                row.Cells["IncludesFreight"].Value = material.IncludesFreight ? "是" : string.Empty;
                row.Cells["Remark"].Value = material.Remark;
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            try
            {
                new MaterialRepository().SaveAll(ReadMaterials());
                MessageBox.Show(this, "材料库已保存。", "材料库", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private List<MaterialRecord> ReadMaterials()
        {
            var list = new List<MaterialRecord>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var name = ReadCell(row, "Name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                decimal price;
                var hasPrice = decimal.TryParse(ReadCell(row, "TaxUnitPrice"), out price);
                list.Add(new MaterialRecord
                {
                    Name = name,
                    Aliases = ReadCell(row, "Aliases"),
                    Category = ReadCell(row, "Category"),
                    Vendor = ReadCell(row, "Vendor"),
                    Spec = ReadCell(row, "Spec"),
                    Unit = ReadCell(row, "Unit"),
                    TaxUnitPrice = hasPrice ? price : (decimal?)null,
                    IncludesFreight = ReadCell(row, "IncludesFreight") == "是" || ReadCell(row, "IncludesFreight") == "1",
                    Remark = ReadCell(row, "Remark")
                });
            }

            return list;
        }

        private static string ReadCell(DataGridViewRow row, string columnName)
        {
            var value = row.Cells[columnName].Value;
            return value == null ? string.Empty : Convert.ToString(value).Trim();
        }

        private static string ReadSourceCell(DataGridViewRow row, string columnName)
        {
            if (row.DataGridView == null || !row.DataGridView.Columns.Contains(columnName))
            {
                return string.Empty;
            }

            var value = row.Cells[columnName].Value;
            return value == null ? string.Empty : Convert.ToString(value).Trim();
        }

        private static IEnumerable<string> SplitMaterials(string value)
        {
            return (value ?? string.Empty).Split(new[] { ';', '；', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string PickSpecForMaterial(string materialName, string specText)
        {
            if (string.IsNullOrWhiteSpace(specText))
            {
                return string.Empty;
            }

            var specs = specText.Split(new[] { ';', '；', ',', '，', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var spec in specs)
            {
                var trimmed = spec.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && materialName.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return trimmed;
                }
            }

            return specs.Length == 1 ? specs[0].Trim() : specText;
        }

        private static string GuessCategory(string materialName)
        {
            if (ContainsAny(materialName, "灰板", "双灰", "纸板", "坑", "楞"))
            {
                return "纸板";
            }

            if (ContainsAny(materialName, "纸", "双铜", "双胶", "白卡", "黑卡", "牛卡"))
            {
                return "纸张";
            }

            if (ContainsAny(materialName, "PET", "PVC", "胶", "膜", "磁铁", "海绵", "EVA"))
            {
                return "辅料";
            }

            return string.Empty;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if ((text ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).Replace("G", "g").Trim().ToLowerInvariant();
        }

        private void AddColumn(string name, string header)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
    }
}
