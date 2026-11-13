using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Inventory_System
{
    public partial class DashboardForm : Form
    {
        private string _fullName;
        private Form1 _loginForm;
        private bool _isLoggingOut = false;
        private int _selectedProductId = -1;

        public DashboardForm(string fullName, Form1 loginForm)
        {
            InitializeComponent();
            _fullName = fullName;
            _loginForm = loginForm;

            lblUserGreeting.Text = "Welcome, " + (string.IsNullOrWhiteSpace(fullName) ? "User" : fullName);
        }

        #region Form Lifecycle
        private void DashboardForm_Load(object sender, EventArgs e)
        {
            try
            {
                DatabaseHelper.InitializeDatabase();

                cmbCategory.Items.Clear();
                cmbCategory.Items.AddRange(DatabaseHelper.AllowedCategories);
                if (cmbCategory.Items.Count > 0)
                {
                    cmbCategory.SelectedIndex = 0;
                }

                RefreshDashboardData();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing dashboard: " + ex.Message, "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DashboardForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isLoggingOut && e.CloseReason == CloseReason.UserClosing)
            {
                Application.Exit();
            }
        }
        #endregion

        #region Data Refresh & Display
        private void RefreshDashboardData()
        {
            try
            {
                StockSummary summary = DatabaseHelper.GetStockSummary();
                lblTotalCount.Text = summary.TotalProducts.ToString();
                lblInStockCount.Text = summary.InStock.ToString();
                lblLowStockCount.Text = summary.LowStock.ToString();
                lblOutOfStockCount.Text = summary.OutOfStock.ToString();

                string filter = txtSearch != null ? txtSearch.Text.Trim() : string.Empty;
                DataTable dt = DatabaseHelper.GetAllProducts(filter);
                dgvProducts.DataSource = dt;
                FormatDataGridView();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading inventory data: " + ex.Message, "Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FormatDataGridView()
        {
            if (dgvProducts.Columns.Count == 0) return;

            if (dgvProducts.Columns.Contains("ProductID"))
            {
                dgvProducts.Columns["ProductID"].HeaderText = "ID";
                dgvProducts.Columns["ProductID"].Width = 55;
                dgvProducts.Columns["ProductID"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            }

            if (dgvProducts.Columns.Contains("ProductName"))
            {
                dgvProducts.Columns["ProductName"].HeaderText = "Product Name";
                dgvProducts.Columns["ProductName"].FillWeight = 140;
            }

            if (dgvProducts.Columns.Contains("Category"))
            {
                dgvProducts.Columns["Category"].HeaderText = "Category";
                dgvProducts.Columns["Category"].FillWeight = 120;
            }

            if (dgvProducts.Columns.Contains("UnitPrice"))
            {
                dgvProducts.Columns["UnitPrice"].HeaderText = "Unit Price";
                dgvProducts.Columns["UnitPrice"].DefaultCellStyle.Format = "N2";
                dgvProducts.Columns["UnitPrice"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvProducts.Columns["UnitPrice"].FillWeight = 85;
            }

            if (dgvProducts.Columns.Contains("Quantity"))
            {
                dgvProducts.Columns["Quantity"].HeaderText = "Qty";
                dgvProducts.Columns["Quantity"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvProducts.Columns["Quantity"].FillWeight = 60;
            }

            if (dgvProducts.Columns.Contains("ReorderLevel"))
            {
                dgvProducts.Columns["ReorderLevel"].HeaderText = "Reorder";
                dgvProducts.Columns["ReorderLevel"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                dgvProducts.Columns["ReorderLevel"].FillWeight = 65;
            }

            if (dgvProducts.Columns.Contains("StockStatus"))
            {
                dgvProducts.Columns["StockStatus"].HeaderText = "Stock Status";
                dgvProducts.Columns["StockStatus"].FillWeight = 100;
            }

            if (dgvProducts.Columns.Contains("Description"))
            {
                dgvProducts.Columns["Description"].HeaderText = "Description";
                dgvProducts.Columns["Description"].FillWeight = 130;
            }

            if (dgvProducts.Columns.Contains("DateAdded"))
            {
                dgvProducts.Columns["DateAdded"].HeaderText = "Date Added";
                dgvProducts.Columns["DateAdded"].DefaultCellStyle.Format = "yyyy-MM-dd";
                dgvProducts.Columns["DateAdded"].FillWeight = 90;
            }
        }

        private void dgvProducts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvProducts.Columns[e.ColumnIndex].Name == "StockStatus")
            {
                object val = dgvProducts.Rows[e.RowIndex].Cells["StockStatus"].Value;
                if (val != null)
                {
                    string status = val.ToString();
                    if (status == "In Stock")
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                        e.CellStyle.Font = new Font(dgvProducts.Font, FontStyle.Bold);
                    }
                    else if (status == "Low Stock")
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(245, 158, 11);
                        e.CellStyle.Font = new Font(dgvProducts.Font, FontStyle.Bold);
                    }
                    else if (status == "Out of Stock")
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(239, 68, 68);
                        e.CellStyle.Font = new Font(dgvProducts.Font, FontStyle.Bold);
                    }
                }
            }
        }
        #endregion

        #region Product Selection & Popup
        private void PopulateFormFromRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvProducts.Rows.Count) return;

            DataGridViewRow row = dgvProducts.Rows[rowIndex];
            if (row.Cells["ProductID"].Value == null || row.Cells["ProductID"].Value == DBNull.Value) return;

            _selectedProductId = Convert.ToInt32(row.Cells["ProductID"].Value);
            lblFormTitle.Text = "Edit Product #" + _selectedProductId;

            txtProductName.Text = row.Cells["ProductName"].Value != null ? row.Cells["ProductName"].Value.ToString() : string.Empty;

            string cat = row.Cells["Category"].Value != null ? row.Cells["Category"].Value.ToString() : string.Empty;
            string normCat = DatabaseHelper.NormalizeCategory(cat);
            int catIdx = cmbCategory.FindStringExact(normCat);
            if (catIdx >= 0)
                cmbCategory.SelectedIndex = catIdx;
            else if (cmbCategory.Items.Count > 0)
                cmbCategory.SelectedIndex = 0;

            double price = 0;
            if (row.Cells["UnitPrice"].Value != null && double.TryParse(row.Cells["UnitPrice"].Value.ToString(), out price))
            {
                txtUnitPrice.Text = price.ToString("0.00");
            }
            else
            {
                txtUnitPrice.Text = "0.00";
            }

            int qty = 0;
            if (row.Cells["Quantity"].Value != null && int.TryParse(row.Cells["Quantity"].Value.ToString(), out qty))
            {
                numQuantity.Value = Math.Max(0, qty);
            }
            else
            {
                numQuantity.Value = 0;
            }

            int reorder = 5;
            if (row.Cells["ReorderLevel"].Value != null && int.TryParse(row.Cells["ReorderLevel"].Value.ToString(), out reorder))
            {
                numReorderLevel.Value = Math.Max(0, reorder);
            }
            else
            {
                numReorderLevel.Value = 5;
            }

            txtDescription.Text = row.Cells["Description"].Value != null ? row.Cells["Description"].Value.ToString() : string.Empty;
        }

        private void dgvProducts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                PopulateFormFromRow(e.RowIndex);
            }
        }

        private void dgvProducts_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                PopulateFormFromRow(e.RowIndex);
                OpenEditPopup();
            }
        }

        private void OpenEditPopup()
        {
            if (_selectedProductId <= 0)
            {
                MessageBox.Show("Please select a product from the list first.", "No Product Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = txtProductName.Text.Trim();
            string category = cmbCategory.SelectedItem != null ? cmbCategory.SelectedItem.ToString() : DatabaseHelper.NormalizeCategory(cmbCategory.Text);
            double price;
            double.TryParse(txtUnitPrice.Text.Trim(), out price);
            int qty = (int)numQuantity.Value;
            int reorder = (int)numReorderLevel.Value;
            string desc = txtDescription.Text.Trim();

            using (EditProductForm popup = new EditProductForm(_selectedProductId, name, category, price, qty, reorder, desc))
            {
                if (popup.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshDashboardData();
                    ClearInputs();
                }
            }
        }
        #endregion

        #region Product CRUD Actions
        private void btnAdd_Click(object sender, EventArgs e)
        {
            string name = txtProductName.Text.Trim();
            string category = cmbCategory.SelectedItem != null ? cmbCategory.SelectedItem.ToString() : DatabaseHelper.NormalizeCategory(cmbCategory.Text);
            string desc = txtDescription.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a product name.", "Required Field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProductName.Focus();
                return;
            }

            double price;
            if (!double.TryParse(txtUnitPrice.Text.Trim(), out price) || price < 0)
            {
                MessageBox.Show("Please enter a valid non-negative unit price (e.g. 29.99).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUnitPrice.Focus();
                return;
            }

            int qty = (int)numQuantity.Value;
            int reorder = (int)numReorderLevel.Value;

            string error;
            bool success = DatabaseHelper.AddProduct(name, category, price, qty, reorder, desc, out error);
            if (success)
            {
                MessageBox.Show("Product '" + name + "' added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshDashboardData();
                ClearInputs();
            }
            else
            {
                MessageBox.Show("Failed to add product: " + error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            if (_selectedProductId <= 0)
            {
                MessageBox.Show("Please select a product from the list to update.", "No Product Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string name = txtProductName.Text.Trim();
            string category = cmbCategory.SelectedItem != null ? cmbCategory.SelectedItem.ToString() : DatabaseHelper.NormalizeCategory(cmbCategory.Text);
            string desc = txtDescription.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a product name.", "Required Field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProductName.Focus();
                return;
            }

            double price;
            if (!double.TryParse(txtUnitPrice.Text.Trim(), out price) || price < 0)
            {
                MessageBox.Show("Please enter a valid non-negative unit price (e.g. 29.99).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUnitPrice.Focus();
                return;
            }

            int qty = (int)numQuantity.Value;
            int reorder = (int)numReorderLevel.Value;

            DialogResult confirm = MessageBox.Show(
                string.Format("Are you sure you want to update '{0}'?", name),
                "Confirm Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (confirm == DialogResult.Yes)
            {
                string error;
                bool success = DatabaseHelper.UpdateProduct(_selectedProductId, name, category, price, qty, reorder, desc, out error);
                if (success)
                {
                    MessageBox.Show("Product updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshDashboardData();
                    ClearInputs();
                }
                else
                {
                    MessageBox.Show("Failed to update product: " + error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedProductId <= 0)
            {
                MessageBox.Show("Please select a product from the list to delete.", "No Product Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string name = txtProductName.Text.Trim();

            DialogResult confirm = MessageBox.Show(
                string.Format("Are you sure you want to delete product '{0}'? This action cannot be undone.", name),
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                string error;
                bool success = DatabaseHelper.DeleteProduct(_selectedProductId, out error);
                if (success)
                {
                    MessageBox.Show("Product deleted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshDashboardData();
                    ClearInputs();
                }
                else
                {
                    MessageBox.Show("Failed to delete product: " + error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            ClearInputs();
        }

        private void ClearInputs()
        {
            _selectedProductId = -1;
            lblFormTitle.Text = "Product Details";
            txtProductName.Clear();

            if (cmbCategory.Items.Count > 0)
            {
                cmbCategory.SelectedIndex = 0;
            }

            txtUnitPrice.Clear();
            numQuantity.Value = 0;
            numReorderLevel.Value = 5;
            txtDescription.Clear();

            if (dgvProducts.Rows.Count > 0)
            {
                dgvProducts.ClearSelection();
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            RefreshDashboardData();
        }
        #endregion

        #region Navigation
        private void btnLogout_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Are you sure you want to log out?",
                "Log out",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                _isLoggingOut = true;
                this.Close();
                if (_loginForm != null)
                {
                    _loginForm.Show();
                }
            }
        }
        #endregion

        private void dgvProducts_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }
    }
}
