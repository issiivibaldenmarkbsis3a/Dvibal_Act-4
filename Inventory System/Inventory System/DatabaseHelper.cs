using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;

namespace Inventory_System
{
    public class StockSummary
    {
        public int TotalProducts { get; set; }
        public int InStock { get; set; }
        public int LowStock { get; set; }
        public int OutOfStock { get; set; }
    }

    public static class DatabaseHelper
    {
        private static string _connectionString = null;

        public static readonly string[] AllowedCategories = new string[]
        {
            "Electronics",
            "Computer Parts & Components",
            "Computer Accessories",
            "Office Supplies",
            "Networking & Electrical Supplies"
        };

        public static string NormalizeCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return AllowedCategories[0];

            string trimmed = category.Trim();
            foreach (string cat in AllowedCategories)
            {
                if (string.Equals(cat, trimmed, StringComparison.OrdinalIgnoreCase))
                    return cat;
            }

            string lower = trimmed.ToLowerInvariant();
            if (lower.Contains("part") || lower.Contains("hardware") || lower.Contains("component") || lower.Contains("ram") || lower.Contains("cpu") || lower.Contains("motherboard") || lower.Contains("monitor") || lower.Contains("psu"))
                return "Computer Parts & Components";

            if (lower.Contains("access") || lower.Contains("cable") || lower.Contains("mouse") || lower.Contains("keyboard") || lower.Contains("general"))
                return "Computer Accessories";

            if (lower.Contains("office") || lower.Contains("suppl") || lower.Contains("paper") || lower.Contains("furnit") || lower.Contains("chair") || lower.Contains("desk") || lower.Contains("stationery"))
                return "Office Supplies";

            if (lower.Contains("network") || lower.Contains("electr") || lower.Contains("wifi") || lower.Contains("router") || lower.Contains("switch") || lower.Contains("power") || lower.Contains("ethernet") || lower.Contains("tester"))
                return "Networking & Electrical Supplies";

            if (lower.Contains("gadget") || lower.Contains("camera") || lower.Contains("device") || lower.Contains("phone"))
                return "Electronics";

            return AllowedCategories[0];
        }

        #region Database Configuration
        public static string GetDatabasePath()
        {
            string projectDb = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\inventorydb.accdb"));
            if (File.Exists(projectDb))
            {
                return projectDb;
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventorydb.accdb");
        }

        public static List<string> GetCandidateDatabasePaths()
        {
            List<string> paths = new List<string>();

            string projectDb = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\inventorydb.accdb"));
            if (File.Exists(projectDb))
            {
                paths.Add(projectDb);
            }

            string localDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventorydb.accdb");
            if (File.Exists(localDb) && !paths.Contains(localDb))
            {
                paths.Add(localDb);
            }

            return paths;
        }

        public static string GetConnectionString(string specificDbPath = null)
        {
            if (string.IsNullOrEmpty(specificDbPath) && !string.IsNullOrEmpty(_connectionString))
                return _connectionString;

            string dbPath = !string.IsNullOrEmpty(specificDbPath) ? specificDbPath : GetDatabasePath();

            string[] providers = new string[]
            {
                "Microsoft.ACE.OLEDB.12.0",
                "Microsoft.ACE.OLEDB.16.0"
            };

            foreach (string provider in providers)
            {
                string testCs = string.Format("Provider={0};Data Source={1};Persist Security Info=False;", provider, dbPath);
                try
                {
                    using (OleDbConnection conn = new OleDbConnection(testCs))
                    {
                        conn.Open();
                        if (string.IsNullOrEmpty(specificDbPath))
                        {
                            _connectionString = testCs;
                        }
                        return testCs;
                    }
                }
                catch
                {
                }
            }

            string defaultCs = string.Format("Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};Persist Security Info=False;", dbPath);
            if (string.IsNullOrEmpty(specificDbPath))
            {
                _connectionString = defaultCs;
            }
            return defaultCs;
        }

        public static OleDbConnection GetConnection(string specificDbPath = null)
        {
            return new OleDbConnection(GetConnectionString(specificDbPath));
        }

        public static HashSet<string> GetTableColumns(OleDbConnection conn, string tableName)
        {
            HashSet<string> columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string sql = string.Format("SELECT * FROM [{0}] WHERE 1=0", tableName);
                using (OleDbCommand cmd = new OleDbCommand(sql, conn))
                using (OleDbDataReader reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly))
                {
                    DataTable schemaTable = reader.GetSchemaTable();
                    if (schemaTable != null)
                    {
                        foreach (DataRow row in schemaTable.Rows)
                        {
                            columns.Add(row["ColumnName"].ToString());
                        }
                    }
                }
            }
            catch
            {
                try
                {
                    DataTable dt = conn.GetSchema("Columns", new string[] { null, null, tableName });
                    foreach (DataRow row in dt.Rows)
                    {
                        columns.Add(row["COLUMN_NAME"].ToString());
                    }
                }
                catch { }
            }

            return columns;
        }
        #endregion

        #region Database Setup
        public static void InitializeDatabase()
        {
            List<string> candidatePaths = GetCandidateDatabasePaths();
            if (candidatePaths.Count == 0)
            {
                candidatePaths.Add(GetDatabasePath());
            }

            foreach (string dbPath in candidatePaths)
            {
                try
                {
                    InitializeDatabaseFile(dbPath);
                }
                catch { }
            }
        }

        private static void InitializeDatabaseFile(string dbPath)
        {
            using (OleDbConnection conn = GetConnection(dbPath))
            {
                conn.Open();

                // 1. Remove old unused StockTransactions table if present
                try
                {
                    using (OleDbCommand dropSt = new OleDbCommand("DROP TABLE StockTransactions", conn))
                    {
                        dropSt.ExecuteNonQuery();
                    }
                }
                catch { }

                // 2. Initialize Users Table
                HashSet<string> userColumns = GetTableColumns(conn, "Users");
                if (userColumns.Count == 0)
                {
                    string createUsersSql = "CREATE TABLE Users (" +
                                            "UserID AUTOINCREMENT PRIMARY KEY, " +
                                            "Username VARCHAR(50), " +
                                            "PasswordHarsh VARCHAR(100), " +
                                            "FullName VARCHAR(100), " +
                                            "UserRole VARCHAR(50), " +
                                            "IsActive YESNO" +
                                            ");";
                    using (OleDbCommand createCmd = new OleDbCommand(createUsersSql, conn))
                    {
                        createCmd.ExecuteNonQuery();
                    }
                }

                using (OleDbCommand countCmd = new OleDbCommand("SELECT COUNT(*) FROM Users", conn))
                {
                    int userCount = Convert.ToInt32(countCmd.ExecuteScalar());
                    if (userCount == 0)
                    {
                        string dummy;
                        RegisterUser("Administrator", "admin", "admin123", out dummy);
                    }
                }

                // 3. Initialize & Sync Categories Table
                HashSet<string> catCols = GetTableColumns(conn, "Categories");
                if (catCols.Count == 0)
                {
                    string createCatSql = "CREATE TABLE Categories (" +
                                          "CategoryID AUTOINCREMENT PRIMARY KEY, " +
                                          "CategoryName VARCHAR(100)" +
                                          ");";
                    using (OleDbCommand createCatCmd = new OleDbCommand(createCatSql, conn))
                    {
                        createCatCmd.ExecuteNonQuery();
                    }
                    catCols.Add("CategoryID");
                    catCols.Add("CategoryName");
                }

                string catNameCol = "CategoryName";
                if (catCols.Contains("Category"))
                {
                    catNameCol = "Category";
                }
                else if (catCols.Contains("CategoryName"))
                {
                    catNameCol = "CategoryName";
                }
                else if (catCols.Contains("Name"))
                {
                    catNameCol = "Name";
                }
                else
                {
                    try
                    {
                        using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Categories ADD COLUMN CategoryName VARCHAR(100)", conn))
                        {
                            alter.ExecuteNonQuery();
                        }
                        catNameCol = "CategoryName";
                    }
                    catch { }
                }

                // Delete any categories not in the allowed list
                try
                {
                    string allowedListClause = "'Electronics', 'Computer Parts & Components', 'Computer Accessories', 'Office Supplies', 'Networking & Electrical Supplies'";
                    using (OleDbCommand delOldCats = new OleDbCommand(string.Format("DELETE FROM Categories WHERE [{0}] NOT IN ({1})", catNameCol, allowedListClause), conn))
                    {
                        delOldCats.ExecuteNonQuery();
                    }
                }
                catch { }

                // Check and insert missing allowed categories
                List<string> existingCatNames = new List<string>();
                try
                {
                    using (OleDbCommand getCatsCmd = new OleDbCommand(string.Format("SELECT [{0}] FROM Categories", catNameCol), conn))
                    using (OleDbDataReader catReader = getCatsCmd.ExecuteReader())
                    {
                        while (catReader.Read())
                        {
                            if (catReader[0] != DBNull.Value)
                                existingCatNames.Add(catReader[0].ToString().Trim());
                        }
                    }
                }
                catch { }

                foreach (string allowedCat in AllowedCategories)
                {
                    bool exists = false;
                    foreach (string existing in existingCatNames)
                    {
                        if (string.Equals(existing, allowedCat, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        try
                        {
                            using (OleDbCommand insCatCmd = new OleDbCommand(string.Format("INSERT INTO Categories ([{0}]) VALUES (?)", catNameCol), conn))
                            {
                                insCatCmd.Parameters.Add("?", OleDbType.VarWChar).Value = allowedCat;
                                insCatCmd.ExecuteNonQuery();
                            }
                        }
                        catch { }
                    }
                }

                // 4. Initialize Products Table
                HashSet<string> prodColumns = GetTableColumns(conn, "Products");
                if (prodColumns.Count == 0)
                {
                    string createProductsSql = "CREATE TABLE Products (" +
                                               "ProductID AUTOINCREMENT PRIMARY KEY, " +
                                               "ProductName VARCHAR(100), " +
                                               "Category VARCHAR(100), " +
                                               "UnitPrice DOUBLE, " +
                                               "Quantity INT, " +
                                               "ReorderLevel INT, " +
                                               "Description VARCHAR(255), " +
                                               "DateAdded DATETIME" +
                                               ");";
                    using (OleDbCommand createProd = new OleDbCommand(createProductsSql, conn))
                    {
                        createProd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Ensure all required columns exist via ALTER TABLE
                    if (!prodColumns.Contains("ProductName"))
                    {
                        try { using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Products ADD COLUMN ProductName VARCHAR(100)", conn)) alter.ExecuteNonQuery(); } catch { }
                    }
                    if (!prodColumns.Contains("Category"))
                    {
                        try { using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Products ADD COLUMN Category VARCHAR(100)", conn)) alter.ExecuteNonQuery(); } catch { }
                    }
                    if (!prodColumns.Contains("UnitPrice") && !prodColumns.Contains("Price"))
                    {
                        try { using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Products ADD COLUMN UnitPrice DOUBLE", conn)) alter.ExecuteNonQuery(); } catch { }
                    }
                    if (!prodColumns.Contains("Quantity") && !prodColumns.Contains("Qty"))
                    {
                        try { using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Products ADD COLUMN Quantity INT", conn)) alter.ExecuteNonQuery(); } catch { }
                    }
                    if (!prodColumns.Contains("ReorderLevel"))
                    {
                        try { using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Products ADD COLUMN ReorderLevel INT", conn)) alter.ExecuteNonQuery(); } catch { }
                    }
                    if (!prodColumns.Contains("Description"))
                    {
                        try { using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Products ADD COLUMN Description VARCHAR(255)", conn)) alter.ExecuteNonQuery(); } catch { }
                    }
                    if (!prodColumns.Contains("DateAdded"))
                    {
                        try { using (OleDbCommand alter = new OleDbCommand("ALTER TABLE Products ADD COLUMN DateAdded DATETIME", conn)) alter.ExecuteNonQuery(); } catch { }
                        if (prodColumns.Contains("DateCreate"))
                        {
                            try { using (OleDbCommand syncDate = new OleDbCommand("UPDATE Products SET DateAdded = DateCreate WHERE DateAdded IS NULL", conn)) syncDate.ExecuteNonQuery(); } catch { }
                        }
                    }

                    // Update any existing products with legacy category names to match the 5 allowed categories
                    try
                    {
                        using (OleDbCommand fixCmd1 = new OleDbCommand("UPDATE Products SET Category = 'Computer Accessories' WHERE Category IN ('Accessories', 'General')", conn))
                        {
                            fixCmd1.ExecuteNonQuery();
                        }
                        using (OleDbCommand fixCmd2 = new OleDbCommand("UPDATE Products SET Category = 'Computer Parts & Components' WHERE Category IN ('Hardware')", conn))
                        {
                            fixCmd2.ExecuteNonQuery();
                        }
                        using (OleDbCommand fixCmd3 = new OleDbCommand("UPDATE Products SET Category = 'Office Supplies' WHERE Category IN ('Furniture', 'Groceries', 'Apparel')", conn))
                        {
                            fixCmd3.ExecuteNonQuery();
                        }
                        using (OleDbCommand fixCmd4 = new OleDbCommand(
                            "UPDATE Products SET Category = 'Electronics' WHERE Category IS NULL OR Category = '' OR Category NOT IN ('Electronics', 'Computer Parts & Components', 'Computer Accessories', 'Office Supplies', 'Networking & Electrical Supplies')", conn))
                        {
                            fixCmd4.ExecuteNonQuery();
                        }

                        // Specific product name category corrections
                        using (OleDbCommand updateMouse = new OleDbCommand("UPDATE Products SET Category = 'Computer Accessories' WHERE ProductName LIKE '%Mouse%' OR ProductName LIKE '%Keyboard%' OR ProductName LIKE '%Cable%'", conn))
                        {
                            updateMouse.ExecuteNonQuery();
                        }
                        using (OleDbCommand updateParts = new OleDbCommand("UPDATE Products SET Category = 'Computer Parts & Components' WHERE ProductName LIKE '%RAM%' OR ProductName LIKE '%Power Supply%' OR ProductName LIKE '%Monitor%' OR ProductName LIKE '%Motherboard%'", conn))
                        {
                            updateParts.ExecuteNonQuery();
                        }
                        using (OleDbCommand updateChair = new OleDbCommand("UPDATE Products SET Category = 'Office Supplies' WHERE ProductName LIKE '%Chair%' OR ProductName LIKE '%Desk%' OR ProductName LIKE '%Paper%'", conn))
                        {
                            updateChair.ExecuteNonQuery();
                        }
                        using (OleDbCommand updateNetwork = new OleDbCommand("UPDATE Products SET Category = 'Networking & Electrical Supplies' WHERE ProductName LIKE '%Ethernet%' OR ProductName LIKE '%Tester%' OR ProductName LIKE '%Multimeter%' OR ProductName LIKE '%Router%'", conn))
                        {
                            updateNetwork.ExecuteNonQuery();
                        }
                    }
                    catch { }
                }

                // 5. Seed sample products if table has no records
                using (OleDbCommand countProdCmd = new OleDbCommand("SELECT COUNT(*) FROM Products", conn))
                {
                    int prodCount = Convert.ToInt32(countProdCmd.ExecuteScalar());
                    if (prodCount == 0)
                    {
                        SeedSampleProducts(conn);
                    }
                }
            }
        }

        private static void SeedSampleProducts(OleDbConnection conn)
        {
            HashSet<string> prodCols = GetTableColumns(conn, "Products");
            string priceCol = prodCols.Contains("UnitPrice") ? "UnitPrice" : "Price";
            string dateCol = prodCols.Contains("DateAdded") ? "DateAdded" : (prodCols.Contains("DateCreate") ? "DateCreate" : "DateAdded");

            var samples = new[]
            {
                new { Name = "Wireless Optical Mouse", Cat = "Computer Accessories", Price = 25.00, Qty = 24, Reorder = 10, Desc = "2.4GHz wireless mouse with USB receiver" },
                new { Name = "Mechanical Gaming Keyboard", Cat = "Computer Accessories", Price = 65.50, Qty = 5, Reorder = 8, Desc = "RGB mechanical gaming keyboard" },
                new { Name = "16GB DDR4 RAM 3200MHz", Cat = "Computer Parts & Components", Price = 42.00, Qty = 18, Reorder = 5, Desc = "High performance desktop memory module" },
                new { Name = "500W ATX Power Supply", Cat = "Computer Parts & Components", Price = 54.00, Qty = 4, Reorder = 6, Desc = "80 Plus Bronze certified power supply" },
                new { Name = "Smart Wi-Fi Security Camera", Cat = "Electronics", Price = 39.99, Qty = 12, Reorder = 5, Desc = "1080p indoor smart surveillance camera" },
                new { Name = "A4 Multipurpose Copy Paper", Cat = "Office Supplies", Price = 6.50, Qty = 50, Reorder = 15, Desc = "70gsm 500 sheets white ream" },
                new { Name = "Cat6 Gigabit Ethernet Cable 15m", Cat = "Networking & Electrical Supplies", Price = 12.50, Qty = 30, Reorder = 8, Desc = "High speed RJ45 patch network cable" },
                new { Name = "Digital Multimeter Tester", Cat = "Networking & Electrical Supplies", Price = 18.00, Qty = 0, Reorder = 3, Desc = "AC/DC voltage resistance tester tool" }
            };

            foreach (var s in samples)
            {
                string sql = string.Format(
                    "INSERT INTO Products (ProductName, Category, {0}, Quantity, ReorderLevel, Description, {1}) VALUES (?, ?, ?, ?, ?, ?, NOW())",
                    priceCol, dateCol);

                using (OleDbCommand cmd = new OleDbCommand(sql, conn))
                {
                    cmd.Parameters.Add("?", OleDbType.VarWChar).Value = s.Name;
                    cmd.Parameters.Add("?", OleDbType.VarWChar).Value = NormalizeCategory(s.Cat);
                    cmd.Parameters.Add("?", OleDbType.Double).Value = s.Price;
                    cmd.Parameters.Add("?", OleDbType.Integer).Value = s.Qty;
                    cmd.Parameters.Add("?", OleDbType.Integer).Value = s.Reorder;
                    cmd.Parameters.Add("?", OleDbType.VarWChar).Value = s.Desc;
                    cmd.ExecuteNonQuery();
                }
            }
        }
        #endregion

        #region User Authentication & Registration
        public static bool AuthenticateUser(string username, string password, out string fullName)
        {
            fullName = string.Empty;

            using (OleDbConnection conn = GetConnection())
            {
                conn.Open();

                HashSet<string> columns = GetTableColumns(conn, "Users");

                string pwdCol = "PasswordHarsh";
                if (columns.Contains("PasswordHarsh")) pwdCol = "PasswordHarsh";
                else if (columns.Contains("Password")) pwdCol = "[Password]";
                else if (columns.Contains("PasswordHash")) pwdCol = "PasswordHash";
                else if (columns.Contains("Pass")) pwdCol = "Pass";

                string nameCol = "FullName";
                if (columns.Contains("FullName")) nameCol = "FullName";
                else if (columns.Contains("Name")) nameCol = "Name";

                string query = string.Format("SELECT {0}, {1} FROM Users WHERE Username = ?", nameCol, pwdCol);
                using (OleDbCommand cmd = new OleDbCommand(query, conn))
                {
                    cmd.Parameters.Add("?", OleDbType.VarWChar).Value = username.Trim();

                    using (OleDbDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string dbPassword = reader[1] != DBNull.Value ? reader[1].ToString() : string.Empty;

                            if (dbPassword == password)
                            {
                                string dbName = reader[0] != DBNull.Value ? reader[0].ToString() : string.Empty;
                                fullName = !string.IsNullOrWhiteSpace(dbName) ? dbName : username.Trim();
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool RegisterUser(string fullName, string username, string password, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                errorMessage = "Full Name is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "Username is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                errorMessage = "Password is required.";
                return false;
            }

            username = username.Trim();
            fullName = fullName.Trim();

            using (OleDbConnection conn = GetConnection())
            {
                conn.Open();

                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = ?";
                using (OleDbCommand checkCmd = new OleDbCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.Add("?", OleDbType.VarWChar).Value = username;
                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                    if (count > 0)
                    {
                        errorMessage = "Username already exists. Please choose another username.";
                        return false;
                    }
                }

                HashSet<string> columns = GetTableColumns(conn, "Users");

                string pwdCol = "PasswordHarsh";
                if (columns.Contains("PasswordHarsh")) pwdCol = "PasswordHarsh";
                else if (columns.Contains("Password")) pwdCol = "[Password]";
                else if (columns.Contains("PasswordHash")) pwdCol = "PasswordHash";
                else if (columns.Contains("Pass")) pwdCol = "Pass";

                string nameCol = columns.Contains("FullName") ? "FullName" : (columns.Contains("Name") ? "Name" : null);
                string roleCol = columns.Contains("UserRole") ? "UserRole" : (columns.Contains("Role") ? "Role" : null);
                bool hasIsActive = columns.Contains("IsActive");
                bool hasCreatedAt = columns.Contains("CreatedAt");
                bool hasDateCreated = columns.Contains("DateCreated");

                List<string> colList = new List<string> { "Username", pwdCol };
                List<string> valPlaceholders = new List<string> { "?", "?" };
                List<OleDbParameter> paramList = new List<OleDbParameter>
                {
                    new OleDbParameter("?", OleDbType.VarWChar) { Value = username },
                    new OleDbParameter("?", OleDbType.VarWChar) { Value = password }
                };

                if (nameCol != null)
                {
                    colList.Add(nameCol);
                    valPlaceholders.Add("?");
                    paramList.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = fullName });
                }

                if (roleCol != null)
                {
                    colList.Add(roleCol);
                    valPlaceholders.Add("?");
                    paramList.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = "User" });
                }

                if (hasIsActive)
                {
                    colList.Add("IsActive");
                    valPlaceholders.Add("True");
                }

                if (hasCreatedAt)
                {
                    colList.Add("CreatedAt");
                    valPlaceholders.Add("NOW()");
                }
                else if (hasDateCreated)
                {
                    colList.Add("DateCreated");
                    valPlaceholders.Add("NOW()");
                }

                string insertSql = string.Format(
                    "INSERT INTO Users ({0}) VALUES ({1})",
                    string.Join(", ", colList),
                    string.Join(", ", valPlaceholders)
                );

                using (OleDbCommand insertCmd = new OleDbCommand(insertSql, conn))
                {
                    foreach (var param in paramList)
                    {
                        insertCmd.Parameters.Add(param);
                    }
                    insertCmd.ExecuteNonQuery();
                }

                return true;
            }
        }
        #endregion

        #region Product Management Operations
        public static DataTable GetAllProducts(string searchFilter = "")
        {
            DataTable dt = new DataTable();
            using (OleDbConnection conn = GetConnection())
            {
                conn.Open();

                HashSet<string> prodCols = GetTableColumns(conn, "Products");
                string priceCol = prodCols.Contains("UnitPrice") ? "UnitPrice" : (prodCols.Contains("Price") ? "Price AS UnitPrice" : "0.0 AS UnitPrice");
                string reorderCol = prodCols.Contains("ReorderLevel") ? "ReorderLevel" : "5 AS ReorderLevel";
                string descCol = prodCols.Contains("Description") ? "Description" : "'' AS Description";
                string dateCol = prodCols.Contains("DateAdded") ? "DateAdded" : (prodCols.Contains("DateCreate") ? "DateCreate AS DateAdded" : "NOW() AS DateAdded");

                string sql = string.Format(
                    "SELECT ProductID, ProductName, Category, {0}, Quantity, {1}, {2}, {3} FROM Products",
                    priceCol, reorderCol, descCol, dateCol);

                if (!string.IsNullOrWhiteSpace(searchFilter))
                {
                    sql += " WHERE ProductName LIKE ? OR Category LIKE ? OR Description LIKE ?";
                }
                sql += " ORDER BY ProductID DESC";

                using (OleDbCommand cmd = new OleDbCommand(sql, conn))
                {
                    if (!string.IsNullOrWhiteSpace(searchFilter))
                    {
                        string pattern = "%" + searchFilter.Trim() + "%";
                        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = pattern;
                        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = pattern;
                        cmd.Parameters.Add("?", OleDbType.VarWChar).Value = pattern;
                    }

                    using (OleDbDataAdapter adapter = new OleDbDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }

            dt.Columns.Add("StockStatus", typeof(string));
            foreach (DataRow row in dt.Rows)
            {
                int qty = row["Quantity"] != DBNull.Value ? Convert.ToInt32(row["Quantity"]) : 0;
                int reorder = row["ReorderLevel"] != DBNull.Value ? Convert.ToInt32(row["ReorderLevel"]) : 0;

                if (qty <= 0)
                {
                    row["StockStatus"] = "Out of Stock";
                }
                else if (qty <= reorder)
                {
                    row["StockStatus"] = "Low Stock";
                }
                else
                {
                    row["StockStatus"] = "In Stock";
                }
            }

            return dt;
        }

        public static StockSummary GetStockSummary()
        {
            StockSummary summary = new StockSummary();
            using (OleDbConnection conn = GetConnection())
            {
                conn.Open();

                HashSet<string> prodCols = GetTableColumns(conn, "Products");
                string reorderCol = prodCols.Contains("ReorderLevel") ? "ReorderLevel" : "5";

                string sql = string.Format("SELECT Quantity, {0} FROM Products", reorderCol);
                using (OleDbCommand cmd = new OleDbCommand(sql, conn))
                using (OleDbDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        summary.TotalProducts++;
                        int qty = reader[0] != DBNull.Value ? Convert.ToInt32(reader[0]) : 0;
                        int reorder = reader[1] != DBNull.Value ? Convert.ToInt32(reader[1]) : 0;

                        if (qty <= 0)
                        {
                            summary.OutOfStock++;
                        }
                        else if (qty <= reorder)
                        {
                            summary.LowStock++;
                        }
                        else
                        {
                            summary.InStock++;
                        }
                    }
                }
            }
            return summary;
        }

        public static bool AddProduct(string name, string category, double price, int qty, int reorderLevel, string description, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Product name is required.";
                return false;
            }

            if (price < 0)
            {
                error = "Unit price cannot be negative.";
                return false;
            }

            if (qty < 0)
            {
                error = "Quantity cannot be negative.";
                return false;
            }

            if (reorderLevel < 0)
            {
                error = "Reorder level cannot be negative.";
                return false;
            }

            string normalizedCat = NormalizeCategory(category);

            try
            {
                using (OleDbConnection conn = GetConnection())
                {
                    conn.Open();

                    HashSet<string> prodCols = GetTableColumns(conn, "Products");
                    List<string> cols = new List<string> { "ProductName", "Category", "Quantity" };
                    List<string> phs = new List<string> { "?", "?", "?" };
                    List<OleDbParameter> parameters = new List<OleDbParameter>
                    {
                        new OleDbParameter("?", OleDbType.VarWChar) { Value = name.Trim() },
                        new OleDbParameter("?", OleDbType.VarWChar) { Value = normalizedCat },
                        new OleDbParameter("?", OleDbType.Integer) { Value = qty }
                    };

                    if (prodCols.Contains("UnitPrice"))
                    {
                        cols.Add("UnitPrice");
                        phs.Add("?");
                        parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = price });
                    }
                    else if (prodCols.Contains("Price"))
                    {
                        cols.Add("Price");
                        phs.Add("?");
                        parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = price });
                    }

                    if (prodCols.Contains("ReorderLevel"))
                    {
                        cols.Add("ReorderLevel");
                        phs.Add("?");
                        parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = reorderLevel });
                    }

                    if (prodCols.Contains("Description"))
                    {
                        cols.Add("Description");
                        phs.Add("?");
                        parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = string.IsNullOrWhiteSpace(description) ? "" : description.Trim() });
                    }

                    if (prodCols.Contains("DateAdded"))
                    {
                        cols.Add("DateAdded");
                        phs.Add("NOW()");
                    }
                    if (prodCols.Contains("DateCreate"))
                    {
                        cols.Add("DateCreate");
                        phs.Add("NOW()");
                    }

                    string sql = string.Format("INSERT INTO Products ({0}) VALUES ({1})", string.Join(", ", cols), string.Join(", ", phs));
                    using (OleDbCommand cmd = new OleDbCommand(sql, conn))
                    {
                        foreach (var p in parameters)
                        {
                            cmd.Parameters.Add(p);
                        }
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool UpdateProduct(int productId, string name, string category, double price, int qty, int reorderLevel, string description, out string error)
        {
            error = string.Empty;

            if (productId <= 0)
            {
                error = "Invalid product ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Product name is required.";
                return false;
            }

            if (price < 0)
            {
                error = "Unit price cannot be negative.";
                return false;
            }

            if (qty < 0)
            {
                error = "Quantity cannot be negative.";
                return false;
            }

            if (reorderLevel < 0)
            {
                error = "Reorder level cannot be negative.";
                return false;
            }

            string normalizedCat = NormalizeCategory(category);

            try
            {
                using (OleDbConnection conn = GetConnection())
                {
                    conn.Open();

                    HashSet<string> prodCols = GetTableColumns(conn, "Products");
                    List<string> setClauses = new List<string>
                    {
                        "ProductName = ?",
                        "Category = ?",
                        "Quantity = ?"
                    };
                    List<OleDbParameter> parameters = new List<OleDbParameter>
                    {
                        new OleDbParameter("?", OleDbType.VarWChar) { Value = name.Trim() },
                        new OleDbParameter("?", OleDbType.VarWChar) { Value = normalizedCat },
                        new OleDbParameter("?", OleDbType.Integer) { Value = qty }
                    };

                    if (prodCols.Contains("UnitPrice"))
                    {
                        setClauses.Add("UnitPrice = ?");
                        parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = price });
                    }
                    else if (prodCols.Contains("Price"))
                    {
                        setClauses.Add("Price = ?");
                        parameters.Add(new OleDbParameter("?", OleDbType.Double) { Value = price });
                    }

                    if (prodCols.Contains("ReorderLevel"))
                    {
                        setClauses.Add("ReorderLevel = ?");
                        parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = reorderLevel });
                    }

                    if (prodCols.Contains("Description"))
                    {
                        setClauses.Add("Description = ?");
                        parameters.Add(new OleDbParameter("?", OleDbType.VarWChar) { Value = string.IsNullOrWhiteSpace(description) ? "" : description.Trim() });
                    }

                    if (prodCols.Contains("DateUpdate"))
                    {
                        setClauses.Add("DateUpdate = NOW()");
                    }

                    string sql = string.Format("UPDATE Products SET {0} WHERE ProductID = ?", string.Join(", ", setClauses));
                    parameters.Add(new OleDbParameter("?", OleDbType.Integer) { Value = productId });

                    using (OleDbCommand cmd = new OleDbCommand(sql, conn))
                    {
                        foreach (var p in parameters)
                        {
                            cmd.Parameters.Add(p);
                        }
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool DeleteProduct(int productId, out string error)
        {
            error = string.Empty;

            if (productId <= 0)
            {
                error = "Invalid product ID.";
                return false;
            }

            try
            {
                using (OleDbConnection conn = GetConnection())
                {
                    conn.Open();
                    string sql = "DELETE FROM Products WHERE ProductID = ?";
                    using (OleDbCommand cmd = new OleDbCommand(sql, conn))
                    {
                        cmd.Parameters.Add("?", OleDbType.Integer).Value = productId;
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
        #endregion
    }
}
