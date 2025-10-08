using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;

namespace WpfApp1
{
    /// <summary>
    /// Lógica de interacción para CompraForm.xaml
    /// </summary>
    public partial class CompraForm : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";

        public class DetalleCompraItem
        {
            public int productoID { get; set; }
            public string Nombre { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Subtotal { get; set; }
        }

        public class ComboBoxItemCañero
        {
            public int proveedorID { get; set; }
            public string NombreCompleto { get; set; }

            public override string ToString()
            {
                return NombreCompleto;
            }
        }

        private List<DetalleCompraItem> detalleCompra = new List<DetalleCompraItem>();

        public CompraForm()
        {
            InitializeComponent();
            CargarCañeros();
            CargarProductos();
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void CargarCañeros()
        {
            cbCañeros.Items.Clear();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT proveedorID, Apellido, Nombre FROM Proveedor", conn);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    cbCañeros.Items.Add(new ComboBoxItemCañero
                    {
                        proveedorID = Convert.ToInt32(reader["proveedorID"]),
                        NombreCompleto = $"{reader["Apellido"]} {reader["Nombre"]}"
                    });
                }
            }
        }

        private void CargarProductos()
        {
            cbProductos.Items.Clear();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT productoID, Nombre FROM Producto", conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    cbProductos.Items.Add(new
                    {
                        productoID = reader.GetInt32(0),
                        Nombre = reader.GetString(1)
                    });
                }

                cbProductos.DisplayMemberPath = "Nombre";
                cbProductos.SelectedValuePath = "productoID";
            }
        }

        private void btnAgregar_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones básicas
            if (cbProductos.SelectedItem == null)
            {
                MessageBox.Show("Seleccione un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtCantidad.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingrese una cantidad válida (mayor a 0).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtPrecio.Text, out decimal precioUnitario) || precioUnitario <= 0)
            {
                MessageBox.Show("Ingrese un precio válido (mayor a 0).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Tomar producto seleccionado (anónimo con productoID y Nombre)
            int productoID = (int)((dynamic)cbProductos.SelectedItem).productoID;
            string nombreProducto = ((dynamic)cbProductos.SelectedItem).Nombre;

            decimal subtotal = cantidad * precioUnitario;

            detalleCompra.Add(new DetalleCompraItem
            {
                productoID = productoID,
                Nombre = nombreProducto,
                Cantidad = cantidad,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotal
            });

            // Refrescar grid
            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = detalleCompra;
            txtTotal.Text = detalleCompra.Sum(i => i.Subtotal).ToString("C");

            // Limpiar inputs cantidad y precio (solicitud tuya)
            txtCantidad.Text = "";
            txtPrecio.Text = "";
            cbProductos.SelectedIndex = -1;
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var seleccionado = dgDetalles.SelectedItem as DetalleCompraItem;
            if (seleccionado != null)
            {
                detalleCompra.Remove(seleccionado);
                dgDetalles.ItemsSource = null;
                dgDetalles.ItemsSource = detalleCompra;
                txtTotal.Text = detalleCompra.Sum(i => i.Subtotal).ToString("C");
            }
            else
            {
                MessageBox.Show("Selecciona una fila para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRegistrarCompra_Click(object sender, RoutedEventArgs e)
        {
            if (cbCañeros.SelectedItem == null || detalleCompra.Count == 0)
            {
                MessageBox.Show("Seleccione un cañero y agregue al menos un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int proveedorID = ((ComboBoxItemCañero)cbCañeros.SelectedItem).proveedorID;
            DateTime fecha = DateTime.Now;
            decimal totalCompra = detalleCompra.Sum(d => d.Subtotal);
            int totalBolsas = detalleCompra.Sum(d => d.Cantidad);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // 1) Insertar en Compra y obtener compraID
                    string insertCompra = @"
                        INSERT INTO Compra (proveedorID, Fecha, TotalCompra)
                        OUTPUT INSERTED.compraID
                        VALUES (@proveedorID, @Fecha, @TotalCompra)";
                    SqlCommand cmdCompra = new SqlCommand(insertCompra, conn, transaction);
                    cmdCompra.Parameters.AddWithValue("@proveedorID", proveedorID);
                    cmdCompra.Parameters.AddWithValue("@Fecha", fecha);
                    cmdCompra.Parameters.AddWithValue("@TotalCompra", totalCompra);
                    int compraID = (int)cmdCompra.ExecuteScalar();

                    // 2) Insertar cada línea en DetalleCompra
                    foreach (var detalle in detalleCompra)
                    {
                        string insertDetalle = @"
                            INSERT INTO detalleCompra 
                            (compraID, productoID, Cantidad, PrecioUnitario, Subtotal)
                            VALUES (@compraID, @productoID, @Cantidad, @PrecioUnitario, @Subtotal)";
                        SqlCommand cmdDetalle = new SqlCommand(insertDetalle, conn, transaction);
                        cmdDetalle.Parameters.AddWithValue("@compraID", compraID);
                        cmdDetalle.Parameters.AddWithValue("@productoID", detalle.productoID);
                        cmdDetalle.Parameters.AddWithValue("@Cantidad", detalle.Cantidad);
                        cmdDetalle.Parameters.AddWithValue("@PrecioUnitario", detalle.PrecioUnitario);
                        cmdDetalle.Parameters.AddWithValue("@Subtotal", detalle.Subtotal);
                        cmdDetalle.ExecuteNonQuery();
                    }

                    // 3) Obtener último saldo (bolsas y pesos) del proveedor
                    decimal saldoBlsAnterior = 0m;
                    string queryUltimoSaldo = @"
                        SELECT TOP 1 SaldoBls
                        FROM MovimientoCuentaProveedor
                        WHERE proveedorID = @proveedorID
                        ORDER BY Fecha DESC";
                    using (SqlCommand cmdSaldo = new SqlCommand(queryUltimoSaldo, conn, transaction))
                    {
                        cmdSaldo.Parameters.AddWithValue("@proveedorID", proveedorID);
                        using (SqlDataReader reader = cmdSaldo.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["SaldoBls"] != DBNull.Value) saldoBlsAnterior = Convert.ToDecimal(reader["SaldoBls"]);
                            }
                        }
                    }

                    // 4) Preparar texto descriptivo del detalle (ej: "20 Bolsa 9kg $9.000 = $180.000, ...")
                    var lineasDetalle = detalleCompra.Select(d =>
                        $"{d.Cantidad} {d.Nombre} ${d.PrecioUnitario:N0} = ${d.Subtotal:N0}");
                    string detalleTexto = string.Join(", ", lineasDetalle);

                    decimal debeBls = totalBolsas;    // la compra aumenta la deuda en bolsas
                    decimal haberBls = 0m;
                    decimal saldoBlsNuevo = saldoBlsAnterior + debeBls - haberBls;

                    // 6) Insertar movimiento en MovimientoCuentaProveedor (todos los campos relevantes)
                    string insertMov = @"
                        INSERT INTO MovimientoCuentaProveedor
                        (proveedorID, Fecha, Detalle, DebeBls, HaberBls, SaldoBls)
                        VALUES (@proveedorID, @Fecha, @Detalle, @DebeBls, @HaberBls, @SaldoBls)";
                    using (SqlCommand cmdMov = new SqlCommand(insertMov, conn, transaction))
                    {
                        cmdMov.Parameters.AddWithValue("@proveedorID", proveedorID);
                        cmdMov.Parameters.AddWithValue("@Fecha", fecha);
                        cmdMov.Parameters.AddWithValue("@Detalle", detalleTexto);
                        cmdMov.Parameters.AddWithValue("@DebeBls", debeBls);
                        cmdMov.Parameters.AddWithValue("@HaberBls", haberBls);
                        cmdMov.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                        cmdMov.ExecuteNonQuery();
                    }

                    // 7) Actualizar CuentaCorrienteProveedor (SaldoBolsas y opcionalmente SaldoPesos)
                    string updateCuenta = @"
                        UPDATE CuentaCorrienteProveedor
                        SET SaldoBolsas = @SaldoBls
                        WHERE proveedorID = @proveedorID";
                    using (SqlCommand cmdUpdate = new SqlCommand(updateCuenta, conn, transaction))
                    {
                        cmdUpdate.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                        cmdUpdate.Parameters.AddWithValue("@proveedorID", proveedorID);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    MessageBox.Show("Compra registrada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarFormulario();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Error al registrar la compra: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LimpiarFormulario()
        {
            cbProductos.SelectedIndex = -1;
            txtCantidad.Text = "";
            txtPrecio.Text = "";
            detalleCompra.Clear();
            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = detalleCompra;
            txtTotal.Text = "0";
        }
    }
}
