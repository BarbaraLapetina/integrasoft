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
    public partial class Ventas : UserControl
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";

        public Ventas()
        {
            InitializeComponent();
            dpDesde.SelectedDate = DateTime.Today.AddMonths(-1);
            dpHasta.SelectedDate = DateTime.Today;
            cbTipoVenta.SelectedIndex = 0;
        }

        public class DetalleProducto
        {
            public string Producto { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Subtotal { get; set; }
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            DateTime fechaDesde = dpDesde.SelectedDate ?? DateTime.MinValue;
            DateTime fechaHasta = (dpHasta.SelectedDate?.Date.AddDays(1).AddTicks(-1)) ?? DateTime.MaxValue;

            string tipoVenta = (cbTipoVenta.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrEmpty(tipoVenta))
            {
                MessageBox.Show("Por favor seleccione un tipo de venta.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (tipoVenta == "Ventas a clientes")
            {
                CargarVentasClientes(fechaDesde, fechaHasta);
            }
            else if (tipoVenta == "Ventas a particulares")
            {
                CargarVentasParticulares(fechaDesde, fechaHasta);
            }
        }

        private void CargarVentasClientes(DateTime desde, DateTime hasta)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(@"
            SELECT V.ventaID, C.Nombre + ' ' + C.Apellido AS Cliente, V.Fecha, V.TotalVenta AS Total, 
                   CASE WHEN V.estaPagado = 1 THEN 'Sí' ELSE 'No' END AS Pagado
            FROM Venta V
            INNER JOIN Cliente C ON V.clienteID = C.clienteID
            WHERE V.Fecha BETWEEN @Desde AND @Hasta
            ORDER BY V.Fecha DESC", conn);

                cmd.Parameters.AddWithValue("@Desde", desde);
                cmd.Parameters.AddWithValue("@Hasta", hasta);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);
                dgVentas.ItemsSource = dt.DefaultView;
            }
        }

        private void CargarVentasParticulares(DateTime desde, DateTime hasta)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(@"
            SELECT ventacfID AS ID, fecha AS Fecha, TotalVentacf AS Total
            FROM VentaCF
            WHERE fecha BETWEEN @Desde AND @Hasta
            ORDER BY fecha DESC", conn);

                cmd.Parameters.AddWithValue("@Desde", desde);
                cmd.Parameters.AddWithValue("@Hasta", hasta);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);
                dgVentas.ItemsSource = dt.DefaultView;
            }
        }

        private void BtnNuevaVentaCliente_Click(object sender, RoutedEventArgs e)
        {
            VentaClienteForm ventacliente = new VentaClienteForm();
            ventacliente.ShowDialog();
        }

        private void BtnNuevaVentaParticular_Click(object sender, RoutedEventArgs e)
        {
            VentaParticularForm ventaparticular = new VentaParticularForm();
            ventaparticular.ShowDialog();
        }
        private void dgVentas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgVentas.SelectedItem == null)
                return;

            DataRowView row = dgVentas.SelectedItem as DataRowView;
            if (row == null)
                return;

            var productos = new List<DetalleProducto>();

            // Evitar crash cuando cambia el tipo de venta
            if (cbTipoVenta.SelectedItem is ComboBoxItem selected && selected.Content.ToString() == "Ventas a particulares")
            {
              
                txtClienteDetalle.Text = "(Consumidor Final)";
                txtFechaDetalle.Text = Convert.ToDateTime(row["fecha"]).ToShortDateString();
                txtDetalleTotal.Text = Convert.ToDecimal(row["Total"]).ToString("C");

                int ventacfID = Convert.ToInt32(row["ID"]);

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
            SELECT p.Nombre AS Producto, dvcf.Cantidad, dvcf.PrecioUnitario
            FROM detalleVentaCF dvcf
            JOIN Producto p ON dvcf.productoID = p.productoID
            WHERE dvcf.ventacfID = @ventacfID";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@ventacfID", ventacfID);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        productos.Add(new DetalleProducto
                        {
                            Producto = reader["Producto"].ToString(),
                            Cantidad = Convert.ToInt32(reader["Cantidad"]),
                            PrecioUnitario = Convert.ToDecimal(reader["PrecioUnitario"]),
                            Subtotal = Convert.ToInt32(reader["Cantidad"]) * Convert.ToDecimal(reader["PrecioUnitario"])
                        });
                    }
                }

                lstDetalleProductos.ItemsSource = productos;
                return;
            }


            // Para ventas a clientes
            int ventaID = Convert.ToInt32(row["ventaID"]);
            string cliente = row["Cliente"].ToString();
            DateTime fecha = Convert.ToDateTime(row["Fecha"]);
            decimal total = Convert.ToDecimal(row["Total"]);

            txtClienteDetalle.Text = cliente;
            txtFechaDetalle.Text = fecha.ToShortDateString();
            txtDetalleTotal.Text = total.ToString("C");

            // Cargar detalle de productos
       
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
            SELECT p.Nombre AS Producto, dv.Cantidad, dv.PrecioUnitario
            FROM DetalleVenta dv
            JOIN Producto p ON dv.productoID = p.productoID
            WHERE dv.ventaID = @ventaID";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ventaID", ventaID);

                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    productos.Add(new DetalleProducto
                    {
                        Producto = reader["Producto"].ToString(),
                        Cantidad = Convert.ToInt32(reader["Cantidad"]),
                        PrecioUnitario = Convert.ToDecimal(reader["PrecioUnitario"]),
                        Subtotal = Convert.ToInt32(reader["Cantidad"]) * Convert.ToDecimal(reader["PrecioUnitario"])
                    });
                }
            }

            lstDetalleProductos.ItemsSource = productos;
        }

    }


}
