using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1
{
    public partial class CuentaCañeroWindow : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";
        private int proveedorID;

        public CuentaCañeroWindow(int proveedorID, string nombreCompleto)
        {
            InitializeComponent();
            this.proveedorID = proveedorID;
            txtTitulo.Text = $"Cuenta Cañero - {nombreCompleto}";
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtCantidadBolsas.TextChanged += GenerarDetalle;
            txtPrecioUnitario.TextChanged += GenerarDetalle;
            cbTipoBolsa.SelectionChanged += GenerarDetalle;
            CargarProductos();
            CargarCuenta(proveedorID);
        }

        // ✅ Nueva clase para productos
        public class ProductoItem
        {
            public int ProductoID { get; set; }
            public string Nombre { get; set; }
        }

        private void CargarProductos()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT productoID, Nombre FROM Producto";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    SqlDataReader reader = cmd.ExecuteReader();

                    var productos = new List<ProductoItem>();
                    while (reader.Read())
                    {
                        productos.Add(new ProductoItem
                        {
                            ProductoID = reader.GetInt32(0),
                            Nombre = reader.GetString(1)
                        });
                    }

                    cbTipoBolsa.ItemsSource = productos;
                    cbTipoBolsa.DisplayMemberPath = "Nombre";   // lo que se muestra
                    cbTipoBolsa.SelectedValuePath = "ProductoID"; // el valor real que se guarda
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar productos: " + ex.Message);
            }
        }

        private void txtCantidadBolsas_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void txtPrecioUnitario_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void Campos_TextChanged(object sender, RoutedEventArgs e)
        {
            GenerarDetalle(sender, null);
        }

        private void GenerarDetalle(object sender, EventArgs e)
        {
            if (int.TryParse(txtCantidadBolsas.Text, out int cantidad) &&
                int.TryParse(txtPrecioUnitario.Text, out int precioUnitario) &&
                cbTipoBolsa.SelectedItem is ProductoItem productoSeleccionado)
            {
                string tipoBolsa = productoSeleccionado.Nombre;
                long total = cantidad * precioUnitario;

                txtDetalle.Text = $"{cantidad} {tipoBolsa} ${precioUnitario:N0} = ${total:N0}";
            }
            else
            {
                txtDetalle.Text = string.Empty;
            }
        }

        private void CargarCuenta(int proveedorID)
        {
            List<MovimientoCuentaProveedor> movimientos = new List<MovimientoCuentaProveedor>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Fecha, Detalle, DebeBls, HaberBls, SaldoBls " +
                               "FROM MovimientoCuentaProveedor WHERE proveedorID = @proveedorID ORDER BY Fecha ASC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@proveedorID", proveedorID);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    movimientos.Add(new MovimientoCuentaProveedor
                    {
                        Fecha = Convert.ToDateTime(reader["Fecha"]),
                        Detalle = reader["Detalle"] != DBNull.Value ? reader["Detalle"].ToString() : string.Empty,
                        DebeBls = reader["DebeBls"] != DBNull.Value ? Convert.ToDecimal(reader["DebeBls"]) : (decimal?)null,
                        HaberBls = reader["HaberBls"] != DBNull.Value ? Convert.ToDecimal(reader["HaberBls"]) : (decimal?)null,
                        SaldoBls = reader["SaldoBls"] != DBNull.Value ? Convert.ToDecimal(reader["SaldoBls"]) : (decimal?)null
                    });
                }
            }

            dgCuenta.ItemsSource = movimientos;
        }

        private void BtnRegistrarPago_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones
            if (!int.TryParse(txtCantidadBolsas.Text, out int cantidadBolsas) || cantidadBolsas <= 0)
            {
                MessageBox.Show("Ingrese una cantidad válida de bolsas.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!decimal.TryParse(txtPrecioUnitario.Text, out decimal precioUnitario) || precioUnitario <= 0)
            {
                MessageBox.Show("Ingrese un precio unitario válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (cbTipoBolsa.SelectedItem == null)
            {
                MessageBox.Show("Seleccione un tipo de bolsa.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Obtenemos el producto seleccionado
            var productoSeleccionado = cbTipoBolsa.SelectedItem as ProductoItem; // ProductoItem es la clase que definimos en CargarProductos()
            if (productoSeleccionado == null)
            {
                MessageBox.Show("Seleccione un tipo de bolsa válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string detalle = txtDetalle.Text.Trim();
            DateTime fecha = DateTime.Now;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1️⃣ Obtener último saldoBls
                        decimal saldoBlsActual = 0;
                        string queryUltimo = @"SELECT TOP 1 SaldoBls 
                                       FROM MovimientoCuentaProveedor 
                                       WHERE proveedorID = @proveedorID 
                                       ORDER BY Fecha DESC";
                        using (SqlCommand cmdUltimo = new SqlCommand(queryUltimo, conn, transaction))
                        {
                            cmdUltimo.Parameters.AddWithValue("@proveedorID", proveedorID);
                            object result = cmdUltimo.ExecuteScalar();
                            saldoBlsActual = result != null ? Convert.ToDecimal(result) : 0;
                        }

                        // 2️⃣ Calcular HaberBls y nuevo saldo
                        decimal haberBls = cantidadBolsas; // directamente las bolsas pagadas
                        decimal saldoBlsNuevo = saldoBlsActual - haberBls;

                        // 3️⃣ Insertar movimiento en MovimientoCuentaProveedor
                        string insertMovimiento = @"INSERT INTO MovimientoCuentaProveedor
                                           (proveedorID, Fecha, Detalle, DebeBls, HaberBls, SaldoBls)
                                           VALUES (@proveedorID, @Fecha, @Detalle, 0, @HaberBls, @SaldoBls)";
                        using (SqlCommand cmdInsert = new SqlCommand(insertMovimiento, conn, transaction))
                        {
                            cmdInsert.Parameters.AddWithValue("@proveedorID", proveedorID);
                            cmdInsert.Parameters.AddWithValue("@Fecha", fecha);
                            cmdInsert.Parameters.AddWithValue("@Detalle", detalle);
                            cmdInsert.Parameters.AddWithValue("@HaberBls", haberBls);
                            cmdInsert.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                            cmdInsert.ExecuteNonQuery();
                        }

                        // 4️⃣ Actualizar saldo en CuentaCorrienteProveedor
                        string updateCuenta = @"UPDATE CuentaCorrienteProveedor
                                        SET SaldoBolsas = @SaldoBls
                                        WHERE proveedorID = @proveedorID";
                        using (SqlCommand cmdUpdateCuenta = new SqlCommand(updateCuenta, conn, transaction))
                        {
                            cmdUpdateCuenta.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                            cmdUpdateCuenta.Parameters.AddWithValue("@proveedorID", proveedorID);
                            cmdUpdateCuenta.ExecuteNonQuery();
                        }

                        // 5️⃣ Registrar egreso en Caja
                        int cajaID;
                        decimal saldoFinalCaja;
                        string queryCaja = @"SELECT TOP 1 cajaID, saldoFinal
                                     FROM Caja
                                     WHERE CAST(fecha AS DATE) = @fechaCaja";
                        using (SqlCommand cmdCaja = new SqlCommand(queryCaja, conn, transaction))
                        {
                            cmdCaja.Parameters.AddWithValue("@fechaCaja", fecha.Date);
                            using (SqlDataReader reader = cmdCaja.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    cajaID = Convert.ToInt32(reader["cajaID"]);
                                    saldoFinalCaja = reader["saldoFinal"] != DBNull.Value ? Convert.ToDecimal(reader["saldoFinal"]) : 0;
                                }
                                else
                                {
                                    cajaID = -1;
                                    saldoFinalCaja = 0;
                                }
                            }
                        }

                        // Si no existe caja, crear una nueva
                        if (cajaID == -1)
                        {
                            string getSaldoAnterior = @"SELECT TOP 1 saldoFinal FROM Caja WHERE fecha < @fechaCaja ORDER BY fecha DESC";
                            using (SqlCommand cmdSaldoAnterior = new SqlCommand(getSaldoAnterior, conn, transaction))
                            {
                                cmdSaldoAnterior.Parameters.AddWithValue("@fechaCaja", fecha.Date);
                                object saldoAnteriorObj = cmdSaldoAnterior.ExecuteScalar();
                                decimal saldoInicial = saldoAnteriorObj != null ? Convert.ToDecimal(saldoAnteriorObj) : 0;

                                string insertCaja = @"INSERT INTO Caja (fecha, saldoInicial, saldoFinal)
                                              VALUES (@fecha, @saldoInicial, @saldoInicial);
                                              SELECT SCOPE_IDENTITY();";
                                using (SqlCommand cmdInsertCaja = new SqlCommand(insertCaja, conn, transaction))
                                {
                                    cmdInsertCaja.Parameters.AddWithValue("@fecha", fecha.Date);
                                    cmdInsertCaja.Parameters.AddWithValue("@saldoInicial", saldoInicial);
                                    cajaID = Convert.ToInt32(cmdInsertCaja.ExecuteScalar());
                                    saldoFinalCaja = saldoInicial;
                                }
                            }
                        }

                        // Insertar movimiento de egreso en Caja
                        string insertMovimientoCaja = @"INSERT INTO MovimientoCaja 
                                               (cajaID, fecha, tipoMovimiento, descripcion, monto, origen)
                                               VALUES (@cajaID, @fecha, @tipoMovimiento, @descripcion, @monto, @origen)";
                        using (SqlCommand cmdMovCaja = new SqlCommand(insertMovimientoCaja, conn, transaction))
                        {
                            cmdMovCaja.Parameters.AddWithValue("@cajaID", cajaID);
                            cmdMovCaja.Parameters.AddWithValue("@fecha", fecha);
                            cmdMovCaja.Parameters.AddWithValue("@tipoMovimiento", "Egreso");
                            cmdMovCaja.Parameters.AddWithValue("@descripcion", detalle);
                            cmdMovCaja.Parameters.AddWithValue("@monto", cantidadBolsas * precioUnitario); // total en pesos
                            cmdMovCaja.Parameters.AddWithValue("@origen", "Pago a proveedor");
                            cmdMovCaja.ExecuteNonQuery();
                        }

                        // Actualizar saldo final de la caja
                        decimal nuevoSaldoFinal = saldoFinalCaja - (cantidadBolsas * precioUnitario);
                        string updateCaja = @"UPDATE Caja SET saldoFinal = @nuevoSaldoFinal WHERE cajaID = @cajaID";
                        using (SqlCommand cmdUpdateCaja = new SqlCommand(updateCaja, conn, transaction))
                        {
                            cmdUpdateCaja.Parameters.AddWithValue("@nuevoSaldoFinal", nuevoSaldoFinal);
                            cmdUpdateCaja.Parameters.AddWithValue("@cajaID", cajaID);
                            cmdUpdateCaja.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        MessageBox.Show("El pago se registró correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Limpiar campos
                        txtCantidadBolsas.Text = "";
                        txtPrecioUnitario.Text = "";
                        txtDetalle.Text = "";
                        cbTipoBolsa.SelectedIndex = -1;

                        // Refrescar DataGrid
                        CargarCuenta(proveedorID);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Error al registrar el pago: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        public class MovimientoCuentaProveedor
        {
            public DateTime Fecha { get; set; }
            public string Detalle { get; set; }
            public decimal? DebeBls { get; set; }
            public decimal? HaberBls { get; set; }
            public decimal? SaldoBls { get; set; }
        }
    }
}
