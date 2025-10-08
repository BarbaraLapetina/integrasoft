using System;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

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
            public override string ToString() => NombreCompleto;
        }

        private List<DetalleVentaItem> detalleVenta = new List<DetalleVentaItem>();
        private List<ComboBoxItemCliente> todosClientes; // lista completa para filtrar

        public VentaClienteForm()
        {
            InitializeComponent();
            CargarClientes();
            InicializarComboClientes();
            CargarProductos();
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void CargarClientes()
        {
            todosClientes = new List<ComboBoxItemCliente>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT clienteID, Apellido, Nombre FROM Cliente", conn);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    todosClientes.Add(new ComboBoxItemCliente
                    {
                        clienteID = Convert.ToInt32(reader["clienteID"]),
                        NombreCompleto = $"{reader["Apellido"]} {reader["Nombre"]}"
                    });
                }
            }

            cbClientes.ItemsSource = todosClientes;
            cbClientes.DisplayMemberPath = "NombreCompleto";
        }

        // Suscribir el TextChanged del TextBox interno del ComboBox
        private void InicializarComboClientes()
        {
            cbClientes.Loaded += (s, e) =>
            {
                if (cbClientes.Template.FindName("PART_EditableTextBox", cbClientes) is TextBox textBox)
                {
                    textBox.TextChanged += CbClientes_TextChanged;
                }
            };
        }

        private void CbClientes_TextChanged(object sender, TextChangedEventArgs e)
        {
            string texto = cbClientes.Text.ToLower();
            var filtrados = todosClientes
                .Where(c => c.NombreCompleto.ToLower().Contains(texto))
                .ToList();

            cbClientes.ItemsSource = filtrados;
            cbClientes.DisplayMemberPath = "NombreCompleto";
            cbClientes.IsDropDownOpen = true; // abrir lista automáticamente
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

        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (cbProductos.SelectedItem == null)
            {
                MessageBox.Show("Por favor, seleccione un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtCantidad.Text) || !int.TryParse(txtCantidad.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingrese una cantidad válida (mayor a 0).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPrecio.Text) || !decimal.TryParse(txtPrecio.Text, out decimal precioUnitario) || precioUnitario <= 0)
            {
                MessageBox.Show("Ingrese un precio válido (mayor a 0).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int productoID = (int)((dynamic)cbProductos.SelectedItem).productoID;
            string nombreProducto = ((dynamic)cbProductos.SelectedItem).Nombre;

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

            txtCantidad.Text = string.Empty;
            txtPrecio.Text = string.Empty;
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

        private void BtnRegistrarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (cbClientes.SelectedItem == null || detalleVenta.Count == 0)
            {
                MessageBox.Show("Seleccione un cliente y agregue al menos un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int clienteID = ((ComboBoxItemCliente)cbClientes.SelectedItem).clienteID;
            DateTime fecha = DateTime.Now;
            decimal totalVenta = detalleVenta.Sum(d => d.Subtotal);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insertar en tabla Venta
                    string insertVenta = @"
                        INSERT INTO Venta (clienteID, Fecha, TotalVenta)
                        OUTPUT INSERTED.ventaID 
                        VALUES (@clienteID, @Fecha, @TotalVenta)";
                    SqlCommand cmdVenta = new SqlCommand(insertVenta, conn, transaction);
                    cmdVenta.Parameters.AddWithValue("@clienteID", clienteID);
                    cmdVenta.Parameters.AddWithValue("@Fecha", fecha);
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

                    // Obtener último saldo
                    decimal saldoAnterior = 0;
                    string queryUltimoSaldo = @"
                        SELECT TOP 1 Saldo
                        FROM MovimientoCuentaCliente 
                        WHERE clienteID = @clienteID 
                        ORDER BY Fecha DESC";
                    SqlCommand cmdSaldo = new SqlCommand(queryUltimoSaldo, conn, transaction);
                    cmdSaldo.Parameters.AddWithValue("@clienteID", clienteID);
                    SqlDataReader reader = cmdSaldo.ExecuteReader();
                    if (reader.Read())
                    {
                        saldoAnterior = reader["Saldo"] != DBNull.Value ? Convert.ToDecimal(reader["Saldo"]) : 0;
                    }
                    reader.Close();

                    // Detalle texto
                    string detalleTexto = string.Join(", ", detalleVenta.Select(d => $"{d.Cantidad} {d.Nombre} {d.PrecioUnitario}"));

                    // Insertar movimiento en MovimientoCuentaCliente
                    string insertMov = @"
                        INSERT INTO MovimientoCuentaCliente
                        (clienteID, Fecha, Detalle, Debe, Haber, Saldo)
                        VALUES (@clienteID, @Fecha, @Detalle, @Debe, @Haber, @Saldo)";
                    SqlCommand cmdMov = new SqlCommand(insertMov, conn, transaction);
                    cmdMov.Parameters.AddWithValue("@clienteID", clienteID);
                    cmdMov.Parameters.AddWithValue("@Fecha", fecha);
                    cmdMov.Parameters.AddWithValue("@Detalle", detalleTexto);

                    decimal debe = totalVenta;
                    decimal haber = 0;
                    decimal saldoNuevo = ((-saldoAnterior) + debe - haber) * (-1);

                    cmdMov.Parameters.AddWithValue("@Debe", debe);
                    cmdMov.Parameters.AddWithValue("@Haber", haber);
                    cmdMov.Parameters.AddWithValue("@Saldo", saldoNuevo);
                    cmdMov.ExecuteNonQuery();

                    // Actualizar saldo en CuentaCorrienteCliente
                    string updateSaldo = @"
                        UPDATE CuentaCorrienteCliente 
                        SET SaldoPesos = @Saldo
                        WHERE clienteID = @clienteID";
                    SqlCommand cmdUpdate = new SqlCommand(updateSaldo, conn, transaction);
                    cmdUpdate.Parameters.AddWithValue("@Saldo", saldoNuevo);
                    cmdUpdate.Parameters.AddWithValue("@clienteID", clienteID);
                    cmdUpdate.ExecuteNonQuery();

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
        }
    }
}
