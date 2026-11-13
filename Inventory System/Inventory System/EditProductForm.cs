using System;
using System.Windows.Forms;

namespace Inventory_System
{
    public partial class EditProductForm : Form
    {
        private int _productId;

        public EditProductForm(int productId, string name, string category, double price, int qty, int reorderLevel, string description)
        {
            InitializeComponent();
            _productId = productId;

            lblHeaderTitle.Text = string.Format("Edit Product #{0}", productId);
            txtProductName.Text = name;

            cmbCategory.Items.Clear();
            cmbCategory.Items.AddRange(DatabaseHelper.AllowedCategories);

            string normCat = DatabaseHelper.NormalizeCategory(category);
            int catIdx = cmbCategory.FindStringExact(normCat);
            if (catIdx >= 0)
                cmbCategory.SelectedIndex = catIdx;
            else if (cmbCategory.Items.Count > 0)
                cmbCategory.SelectedIndex = 0;

            txtUnitPrice.Text = price.ToString("0.00");
            numQuantity.Value = Math.Max(0, qty);
            numReorderLevel.Value = Math.Max(0, reorderLevel);
            txtDescription.Text = description;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string name = txtProductName.Text.Trim();
            string category = cmbCategory.SelectedItem != null ? cmbCategory.SelectedItem.ToString() : DatabaseHelper.NormalizeCategory(cmbCategory.Text);
            string desc = txtDescription.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a product name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProductName.Focus();
                return;
            }

            double price;
            if (!double.TryParse(txtUnitPrice.Text.Trim(), out price) || price < 0)
            {
                MessageBox.Show("Please enter a valid non-negative unit price (e.g. 19.99).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                bool ok = DatabaseHelper.UpdateProduct(_productId, name, category, price, qty, reorder, desc, out error);
                if (ok)
                {
                    MessageBox.Show("Product updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Error updating product: " + error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            string name = txtProductName.Text.Trim();

            DialogResult confirm = MessageBox.Show(
                string.Format("Are you sure you want to delete '{0}'? This action cannot be undone.", name),
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                string error;
                bool ok = DatabaseHelper.DeleteProduct(_productId, out error);
                if (ok)
                {
                    MessageBox.Show("Product deleted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Error deleting product: " + error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
