using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class Compras : UserControl
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";

        public Compras()
        {
            InitializeComponent();
            dpDesde.SelectedDate = DateTime.Today.AddMonths(-1);
            dpHasta.SelectedDate = DateTime.Today;
            CargarCompras();
        }
        private void BtnNuevaCompraCliente_Click(object sender, RoutedEventArgs e)
        {
            CompraForm compraform = new CompraForm();
            compraform.ShowDialog();
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            CargarCompras();
        }

        public class DetalleProducto
        {
            public string Producto { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Subtotal { get; set; }
        }

        private void CargarCompras()
        {
            if (dpDesde.SelectedDate == null || dpHasta.SelectedDate == null)
            {
                MessageBox.Show("Seleccione un rango de fechas válido.");
                return;
            }

            DateTime desde = dpDesde.SelectedDate.Value;
            DateTime hasta = dpHasta.SelectedDate.Value.AddDays(1).AddTicks(-1);


            DataTable dt = new DataTable();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
            SELECT c.compraID, 
                   p.Apellido + ' ' + p.Nombre AS Proveedor, 
                   c.Fecha, 
                   c.TotalCompra, 
                   c.estaPagado
            FROM Compra c
            JOIN Proveedor p ON c.proveedorID = p.proveedorID
            WHERE c.Fecha BETWEEN @desde AND @hasta
            ORDER BY c.Fecha DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@desde", desde);
                cmd.Parameters.AddWithValue("@hasta", hasta);

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(dt);
            }

            dgCompras.ItemsSource = dt.DefaultView;
        }

        private void dgCompras_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgCompras.SelectedItem == null)
                return;

            DataRowView row = dgCompras.SelectedItem as DataRowView;
            if (row == null)
                return;

            int compraID = Convert.ToInt32(row["compraID"]);
            string proveedor = row["Proveedor"].ToString();
            DateTime fecha = Convert.ToDateTime(row["Fecha"]);
            decimal total = Convert.ToDecimal(row["TotalCompra"]);

            // Mostrar datos básicos
            txtCañeroDetalle.Text = proveedor;
            txtFechaDetalle.Text = fecha.ToShortDateString();
            txtDetalleTotal.Text = total.ToString("C");

            // Obtener detalle de productos
            var productos = new List<DetalleProducto>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
            SELECT p.Nombre AS Producto, dc.Cantidad, dc.PrecioUnitario
            FROM detalleCompra dc
            JOIN Producto p ON dc.productoID = p.productoID
            WHERE dc.compraID = @compraID";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@compraID", compraID);

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string nombreProducto = reader["Producto"].ToString();
                    int cantidad = Convert.ToInt32(reader["Cantidad"]);
                    decimal precioUnitario = Convert.ToDecimal(reader["PrecioUnitario"]);
                    decimal subtotal = cantidad * precioUnitario;

                    productos.Add(new DetalleProducto
                    {
                        Producto = nombreProducto,
                        Cantidad = cantidad,
                        PrecioUnitario = precioUnitario,
                        Subtotal = subtotal
                    });
                }
            }

            // Mostrar en el ListBox
            lstDetalleProductos.ItemsSource = productos;
        }

    }


}
