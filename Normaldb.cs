
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;

namespace NormalizationApp
{
    public partial class Form1 : Form
    {
        private string connString = "Data Source=localhost;Initial Catalog=NormalizedDB;Integrated Security=True";

        public Form1()
        {
            InitializeComponent();
        }

        private void btnNormalize_Click(object sender, EventArgs e)
        {
            string rawData = txtRawData.Text.Trim();
            if (string.IsNullOrEmpty(rawData))
            {
                MessageBox.Show("Please enter CSV data.");
                return;
            }

            try
            {
                ClearDatabase();

                // 1NF: Eliminate repeating groups and ensure atomicity
                var lines = rawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var headers = lines[0].Split(',');

                var data = new List<Dictionary<string, string>>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length != headers.Length)
                        throw new Exception("Row column count doesn't match header.");

                    var row = new Dictionary<string, string>();
                    for (int j = 0; j < headers.Length; j++)
                    {
                        row[headers[j].Trim()] = values[j].Trim();
                    }
                    data.Add(row);
                }

                // 2NF: Remove partial dependencies (Products and Customers in separate tables)
                var products = new Dictionary<string, (int, string)>();
                var customers = new HashSet<string>();

                // 3NF: Eliminate transitive dependencies (CustomerName only belongs in Customers, ProductName only in Products)
                var orders = new List<(int, string, int)>();

                foreach (var row in data)
                {
                    string custName = row["CustomerName"];
                    int orderId = int.Parse(row["OrderID"]);
                    int productId = int.Parse(row["ProductID"]);
                    string productName = row["ProductName"];

                    customers.Add(custName);
                    products[productId.ToString()] = (productId, productName);
                    orders.Add((orderId, custName, productId));
                }

                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();

                    foreach (var name in customers)
                    {
                        SqlCommand cmd = new SqlCommand("INSERT INTO Customers (CustomerName) VALUES (@name)", conn);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.ExecuteNonQuery();
                    }

                    foreach (var prod in products.Values)
                    {
                        SqlCommand cmd = new SqlCommand("INSERT INTO Products (ProductID, ProductName) VALUES (@id, @name)", conn);
                        cmd.Parameters.AddWithValue("@id", prod.Item1);
                        cmd.Parameters.AddWithValue("@name", prod.Item2);
                        cmd.ExecuteNonQuery();
                    }

                    foreach (var ord in orders)
                    {
                        SqlCommand cmd = new SqlCommand("INSERT INTO Orders (OrderID, CustomerName, ProductID) VALUES (@oid, @cust, @pid)", conn);
                        cmd.Parameters.AddWithValue("@oid", ord.Item1);
                        cmd.Parameters.AddWithValue("@cust", ord.Item2);
                        cmd.Parameters.AddWithValue("@pid", ord.Item3);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Normalization from 1NF to 3NF and insertion successful!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void ClearDatabase()
        {
            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                new SqlCommand("DELETE FROM Orders", conn).ExecuteNonQuery();
                new SqlCommand("DELETE FROM Customers", conn).ExecuteNonQuery();
                new SqlCommand("DELETE FROM Products", conn).ExecuteNonQuery();
            }
        }
    }
}
