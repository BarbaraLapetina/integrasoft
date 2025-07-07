using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using static WpfApp1.VentaClienteForm;

namespace WpfApp1
{
    public partial class VentaParticularForm : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";
        private decimal precioActualBolsa = 0m;

        public class DetalleVentaItem
        {
            public int productoID { get; set; }
            public string Nombre { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Subtotal { get; set; }
        }

        private List<DetalleVentaItem> detalleVenta = new List<DetalleVentaItem>();

        public VentaParticularForm()
        {
            InitializeComponent();
            CargarProductos();
            CargarPrecioActual();
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void CargarProductos()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT productoID, Nombre FROM Producto", conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    cbProducto.Items.Add(new
                    {
                        productoID = reader["productoID"],
                        Nombre = reader["Nombre"].ToString()
                    });
                }

                cbProducto.DisplayMemberPath = "Nombre";
                cbProducto.SelectedValuePath = "productoID";
            }
        }
        private void CargarPrecioActual()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT TOP 1 Precio FROM ParametroPrecio ORDER BY Fecha DESC", conn);
                precioActualBolsa = Convert.ToDecimal(cmd.ExecuteScalar());
            }
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (cbProducto.SelectedItem == null || string.IsNullOrWhiteSpace(txtCantidad.Text))
            {
                MessageBox.Show("Selecciona un producto y cantidad válida.");
                return;
            }

            int productoID = (int)((dynamic)cbProducto.SelectedItem).productoID;
            string nombreProducto = ((dynamic)cbProducto.SelectedItem).Nombre;
            int cantidad = int.Parse(txtCantidad.Text);

            int cantidadBolsas = ObtenerCantidadBolsas(productoID);
            decimal precioUnitario = cantidadBolsas * precioActualBolsa;
            decimal subtotal = cantidad * precioUnitario;

            detalleVenta.Add(new DetalleVentaItem
            {
                productoID = productoID,
                Nombre = nombreProducto,
                Cantidad = cantidad,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotal
            });


            dgDetalle.ItemsSource = null;
            dgDetalle.ItemsSource = detalleVenta;

            txtTotal.Text = detalleVenta.Sum(i => i.Subtotal).ToString("C");
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var seleccionado = dgDetalle.SelectedItem as DetalleVentaItem;
            if (seleccionado != null)
            {
                detalleVenta.Remove(seleccionado);
                dgDetalle.ItemsSource = null;
                dgDetalle.ItemsSource = detalleVenta;
                txtTotal.Text = detalleVenta.Sum(i => i.Subtotal).ToString("C");
            }
            else
            {
                MessageBox.Show("Selecciona una fila para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private int ObtenerCantidadBolsas(int productoID)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT CantidadBolsas FROM Producto WHERE ProductoID = @id", conn);
                cmd.Parameters.AddWithValue("@id", productoID);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void BtnRegistrarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (detalleVenta.Count == 0)
            {
                MessageBox.Show("Agregue al menos un producto.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insertar venta
                    string insertVenta = @"INSERT INTO VentaCF (fecha, TotalVentacf)
                                           VALUES (@fecha, @total);
                                           SELECT SCOPE_IDENTITY();";
                    SqlCommand cmdVenta = new SqlCommand(insertVenta, conn, transaction);
                    cmdVenta.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmdVenta.Parameters.AddWithValue("@total", detalleVenta.Sum(i => i.Subtotal));
                    int ventacfID = Convert.ToInt32(cmdVenta.ExecuteScalar());

                    // Insertar detalle
                    foreach (var item in detalleVenta)
                    {
                        string insertDetalle = @"INSERT INTO detalleVentaCF (ventacfID, productoID, Cantidad, PrecioUnitario, Subtotal)
                                                 VALUES (@ventacfID, @productoID, @cantidad, @precio, @subtotal)";
                        SqlCommand cmdDetalle = new SqlCommand(insertDetalle, conn, transaction);
                        cmdDetalle.Parameters.AddWithValue("@ventacfID", ventacfID);
                        cmdDetalle.Parameters.AddWithValue("@productoID", item.productoID);
                        cmdDetalle.Parameters.AddWithValue("@cantidad", item.Cantidad);
                        cmdDetalle.Parameters.AddWithValue("@precio", item.PrecioUnitario);
                        cmdDetalle.Parameters.AddWithValue("@subtotal", item.Subtotal);
                        cmdDetalle.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    MessageBox.Show("Venta registrada correctamente.");
                    this.Close();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Error al registrar venta: " + ex.Message);
                }
            }
        }

        private void LimpiarFormulario()
        {
            cbProducto.SelectedIndex = -1;
            txtCantidad.Text = "";
            detalleVenta.Clear();
            dgDetalle.ItemsSource = null;
            dgDetalle.ItemsSource = detalleVenta;
            txtTotal.Text = "0";
        }
    }



    // Clases auxiliares
    public class Producto
    {
        public int ProductoID { get; set; }
        public string Nombre { get; set; }
        public decimal PrecioVenta { get; set; }

        public override string ToString() => Nombre;
    }

   
}

