using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;

namespace WpfApp1
{
    public partial class VentaClienteForm : Window
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

        public class ComboBoxItemCliente
        {
            public int clienteID { get; set; }
            public string NombreCompleto { get; set; }

            public override string ToString()
            {
                return NombreCompleto;
            }
        }

        private List<DetalleVentaItem> detalleVenta = new List<DetalleVentaItem>();

        public VentaClienteForm()
        {
            InitializeComponent();
            CargarClientes();
            CargarProductos();
            CargarPrecioActual();
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void CargarClientes()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT clienteID, Apellido, Nombre FROM Cliente", conn);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    cbClientes.Items.Add(new ComboBoxItemCliente
                    {
                        clienteID = Convert.ToInt32(reader["clienteID"]),
                        NombreCompleto = $"{reader["Apellido"]} {reader["Nombre"]}"
                    });
                }
            }
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
                    cbProductos.Items.Add(new
                    {
                        productoID = reader["productoID"],
                        Nombre = reader["Nombre"].ToString()
                    });
                }

                cbProductos.DisplayMemberPath = "Nombre";
                cbProductos.SelectedValuePath = "productoID";
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

        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (cbProductos.SelectedItem == null || string.IsNullOrWhiteSpace(txtCantidad.Text))
            {
                MessageBox.Show("Selecciona un producto y cantidad válida.");
                return;
            }

            int productoID = (int)((dynamic)cbProductos.SelectedItem).productoID;
            string nombreProducto = ((dynamic)cbProductos.SelectedItem).Nombre;
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


            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = detalleVenta;

            txtTotal.Text = detalleVenta.Sum(i => i.Subtotal).ToString("C");
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var seleccionado = dgDetalles.SelectedItem as DetalleVentaItem;
            if (seleccionado != null)
            {
                detalleVenta.Remove(seleccionado);
                dgDetalles.ItemsSource = null;
                dgDetalles.ItemsSource = detalleVenta;
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
            if (cbClientes.SelectedItem == null || detalleVenta.Count == 0)
            {
                MessageBox.Show("Seleccione un cliente y agregue al menos un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int clienteID = ((ComboBoxItemCliente)cbClientes.SelectedItem).clienteID;
            DateTime fecha = DateTime.Now;
            bool pagada = estaPagado.IsChecked == true;
            decimal totalVenta = detalleVenta.Sum(d => d.Subtotal);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insertar en tabla Venta
                    string insertVenta = @"
                INSERT INTO Venta (clienteID, Fecha, estaPagado, TotalVenta)
                OUTPUT INSERTED.ventaID 
                VALUES (@clienteID, @Fecha, @estaPagado, @TotalVenta)";
                    SqlCommand cmdVenta = new SqlCommand(insertVenta, conn, transaction);
                    cmdVenta.Parameters.AddWithValue("@clienteID", clienteID);
                    cmdVenta.Parameters.AddWithValue("@Fecha", fecha);
                    cmdVenta.Parameters.AddWithValue("@estaPagado", pagada);
                    cmdVenta.Parameters.AddWithValue("@TotalVenta", totalVenta);

                    int ventaID = (int)cmdVenta.ExecuteScalar();

                    // Insertar en DetalleVenta
                    foreach (var detalle in detalleVenta)
                    {
                        string insertDetalle = @"
                    INSERT INTO DetalleVenta 
                    (ventaID, productoID, Cantidad, PrecioUnitario, Subtotal)
                    VALUES (@ventaID, @productoID, @Cantidad, @PrecioUnitario, @Subtotal)";
                        SqlCommand cmdDetalle = new SqlCommand(insertDetalle, conn, transaction);
                        cmdDetalle.Parameters.AddWithValue("@ventaID", ventaID);
                        cmdDetalle.Parameters.AddWithValue("@productoID", detalle.productoID);
                        cmdDetalle.Parameters.AddWithValue("@Cantidad", detalle.Cantidad);
                        cmdDetalle.Parameters.AddWithValue("@PrecioUnitario", detalle.PrecioUnitario);
                        cmdDetalle.Parameters.AddWithValue("@Subtotal", detalle.Subtotal);
                        cmdDetalle.ExecuteNonQuery();
                    }

                    // Solo registrar movimiento en cuenta corriente si NO está pagada
                    if (!pagada)
                    {
                        // Obtener último saldo
                        decimal saldoAnterior = 0;
                        decimal saldoBolsasAnterior = 0;

                        string queryUltimoSaldo = @"
                    SELECT TOP 1 Saldo, SaldoBls 
                    FROM MovimientoCuentaCliente 
                    WHERE clienteID = @clienteID 
                    ORDER BY Fecha DESC";
                        SqlCommand cmdSaldo = new SqlCommand(queryUltimoSaldo, conn, transaction);
                        cmdSaldo.Parameters.AddWithValue("@clienteID", clienteID);
                        SqlDataReader reader = cmdSaldo.ExecuteReader();
                        if (reader.Read())
                        {
                            saldoAnterior = reader["Saldo"] != DBNull.Value ? Convert.ToDecimal(reader["Saldo"]) : 0;
                            saldoBolsasAnterior = reader["SaldoBls"] != DBNull.Value ? Convert.ToDecimal(reader["SaldoBls"]) : 0;
                        }
                        reader.Close();

                        // Precio actual de bolsa
                        string queryPrecio = "SELECT TOP 1 Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                        SqlCommand cmdPrecio = new SqlCommand(queryPrecio, conn, transaction);
                        object precioObj = cmdPrecio.ExecuteScalar();
                        decimal precioBolsa = precioObj != null ? Convert.ToDecimal(precioObj) : 1;

                        // Insertar movimiento en MovimientoCuentaCliente
                        string insertMov = @"
                    INSERT INTO MovimientoCuentaCliente
                    (clienteID, Fecha, Detalle, Debe, Haber, Saldo, DebeBls, HaberBls, SaldoBls)
                    VALUES (@clienteID, @Fecha, @Detalle, @Debe, @Haber, @Saldo, @DebeBls, @HaberBls, @SaldoBls)";
                        SqlCommand cmdMov = new SqlCommand(insertMov, conn, transaction);
                        cmdMov.Parameters.AddWithValue("@clienteID", clienteID);
                        cmdMov.Parameters.AddWithValue("@Fecha", fecha);
                        cmdMov.Parameters.AddWithValue("@Detalle", "Venta a cuenta");

                        decimal debe = totalVenta;
                        decimal haber = 0;
                        decimal debeBls = (precioBolsa != 0 ? totalVenta / precioBolsa : 0);
                        decimal haberBls = 0;
                        decimal saldoNuevo = ((-saldoAnterior) + debe - haber) * (-1);
                        decimal saldoBlsNuevo = saldoBolsasAnterior + debeBls - haberBls;

                        cmdMov.Parameters.AddWithValue("@Debe", debe);
                        cmdMov.Parameters.AddWithValue("@Haber", haber);
                        cmdMov.Parameters.AddWithValue("@Saldo", saldoNuevo);
                        cmdMov.Parameters.AddWithValue("@DebeBls", debeBls);
                        cmdMov.Parameters.AddWithValue("@HaberBls", haberBls);
                        cmdMov.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                        cmdMov.ExecuteNonQuery();

                        // Actualizar saldo en CuentaCorrienteCliente
                        string updateSaldo = @"
                    UPDATE CuentaCorrienteCliente 
                    SET SaldoPesos = @Saldo, SaldoBolsas = @SaldoBls 
                    WHERE clienteID = @clienteID";
                        SqlCommand cmdUpdate = new SqlCommand(updateSaldo, conn, transaction);
                        cmdUpdate.Parameters.AddWithValue("@Saldo", saldoNuevo);
                        cmdUpdate.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                        cmdUpdate.Parameters.AddWithValue("@clienteID", clienteID);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    MessageBox.Show("Venta registrada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarFormulario();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Error al registrar la venta: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void LimpiarFormulario()
        {
            cbProductos.SelectedIndex = -1;
            txtCantidad.Text = "";
            detalleVenta.Clear();
            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = detalleVenta;
            txtTotal.Text = "0";
            estaPagado.IsChecked = false;
        }

    }

}