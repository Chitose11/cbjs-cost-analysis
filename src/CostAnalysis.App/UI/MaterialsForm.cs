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

        public MaterialsForm()
        {
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
