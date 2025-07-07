using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// Lógica de interacción para CompraForm.xaml
    /// </summary>
    public partial class CompraForm : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";
        private decimal precioActualBolsa = 0m;
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
            CargarPrecioActual();
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void CargarCañeros()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT proveedorID, Apellido, Nombre FROM Proveedor", conn);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    cbCañero.Items.Add(new ComboBoxItemCañero
                    {
                        proveedorID = Convert.ToInt32(reader["proveedorID"]),
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

            detalleCompra.Add(new DetalleCompraItem
            {
                productoID = productoID,
                Nombre = nombreProducto,
                Cantidad = cantidad,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotal
            });


            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = detalleCompra;

            txtTotal.Text = detalleCompra.Sum(i => i.Subtotal).ToString("C");
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

        private void BtnRegistrarCompra_Click(object sender, RoutedEventArgs e)
        {
            if (cbCañero.SelectedItem == null || detalleCompra.Count == 0)
            {
                MessageBox.Show("Seleccione un cañero y agregue al menos un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int proveedorID = ((ComboBoxItemCañero)cbCañero.SelectedItem).proveedorID;
            DateTime fecha = DateTime.Now;
            bool pagada = estaPagado.IsChecked == true;
            decimal totalCompra = detalleCompra.Sum(d => d.Subtotal);

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insertar en Compra
                    string insertCompra = @"
                INSERT INTO Compra (proveedorID, Fecha, estaPagado, TotalCompra)
                OUTPUT INSERTED.compraID
                VALUES (@proveedorID, @Fecha, @estaPagado, @TotalCompra)";
                    SqlCommand cmdCompra = new SqlCommand(insertCompra, conn, transaction);
                    cmdCompra.Parameters.AddWithValue("@proveedorID", proveedorID);
                    cmdCompra.Parameters.AddWithValue("@Fecha", fecha);
                    cmdCompra.Parameters.AddWithValue("@estaPagado", pagada);
                    cmdCompra.Parameters.AddWithValue("@TotalCompra", totalCompra);
                    int compraID = (int)cmdCompra.ExecuteScalar();

                    // Insertar en DetalleCompra
                    foreach (var detalle in detalleCompra)
                    {
                        string insertDetalle = @"
                    INSERT INTO DetalleCompra 
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

                    // Solo registrar movimiento si NO está pagada
                    if (!pagada)
                    {
                        // Obtener último saldo
                        decimal saldoAnterior = 0;
                        decimal saldoBolsasAnterior = 0;

                        string queryUltimoSaldo = @"
                    SELECT TOP 1 Saldo, SaldoBls 
                    FROM MovimientoCuentaProveedor
                    WHERE proveedorID = @proveedorID
                    ORDER BY Fecha DESC";
                        SqlCommand cmdSaldo = new SqlCommand(queryUltimoSaldo, conn, transaction);
                        cmdSaldo.Parameters.AddWithValue("@proveedorID", proveedorID);
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

                        // Insertar en MovimientoCuentaProveedor
                        string insertMov = @"
                    INSERT INTO MovimientoCuentaProveedor
                    (proveedorID, Fecha, Detalle, Debe, Haber, Saldo, DebeBls, HaberBls, SaldoBls)
                    VALUES (@proveedorID, @Fecha, @Detalle, @Debe, @Haber, @Saldo, @DebeBls, @HaberBls, @SaldoBls)";
                        SqlCommand cmdMov = new SqlCommand(insertMov, conn, transaction);
                        cmdMov.Parameters.AddWithValue("@proveedorID", proveedorID);
                        cmdMov.Parameters.AddWithValue("@Fecha", fecha);
                        cmdMov.Parameters.AddWithValue("@Detalle", "Compra a cuenta");

                        decimal debe = totalCompra;
                        decimal haber = 0;
                        decimal debeBls = (precioBolsa != 0 ? totalCompra / precioBolsa : 0);
                        decimal haberBls = 0;
                        decimal saldoNuevo = saldoAnterior + debe - haber;
                        decimal saldoBlsNuevo = saldoBolsasAnterior + debeBls - haberBls;

                        cmdMov.Parameters.AddWithValue("@Debe", debe);
                        cmdMov.Parameters.AddWithValue("@Haber", haber);
                        cmdMov.Parameters.AddWithValue("@Saldo", saldoNuevo);
                        cmdMov.Parameters.AddWithValue("@DebeBls", debeBls);
                        cmdMov.Parameters.AddWithValue("@HaberBls", haberBls);
                        cmdMov.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                        cmdMov.ExecuteNonQuery();

                        // Actualizar saldo en CuentaCorrienteProveedor
                        string updateSaldo = @"
                    UPDATE CuentaCorrienteProveedor
                    SET SaldoPesos = @Saldo, SaldoBolsas = @SaldoBls
                    WHERE proveedorID = @proveedorID";
                        SqlCommand cmdUpdate = new SqlCommand(updateSaldo, conn, transaction);
                        cmdUpdate.Parameters.AddWithValue("@Saldo", saldoNuevo);
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
            detalleCompra.Clear();
            dgDetalles.ItemsSource = null;
            dgDetalles.ItemsSource = detalleCompra;
            txtTotal.Text = "0";
            estaPagado.IsChecked = false;
        }



    }
}
