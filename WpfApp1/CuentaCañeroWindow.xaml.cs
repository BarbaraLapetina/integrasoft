using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            CargarCuenta(proveedorID);
        }

        private void CargarCuenta(int proveedorID)
        {
            List<MovimientoCuentaProveedor> movimientos = new List<MovimientoCuentaProveedor>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Fecha, Detalle, Debe, Haber, Saldo, DebeBls, HaberBls, SaldoBls " +
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
                        Debe = reader["Debe"] != DBNull.Value ? Convert.ToDecimal(reader["Debe"]) : (decimal?)null,
                        Haber = reader["Haber"] != DBNull.Value ? Convert.ToDecimal(reader["Haber"]) : (decimal?)null,
                        Saldo = reader["Saldo"] != DBNull.Value ? Convert.ToDecimal(reader["Saldo"]) : (decimal?)null,
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
            if (!decimal.TryParse(txtMonto.Text, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Por favor, ingrese un monto válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string detalle = txtDetalle.Text.Trim();
            DateTime fecha = DateTime.Now;

            decimal saldoActual = 0;
            decimal saldoBolsasActual = 0;
            decimal precioBolsa = 1;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Obtener último saldo del proveedor
                string queryUltimo = @"SELECT TOP 1 Saldo, SaldoBls 
                               FROM MovimientoCuentaProveedor 
                               WHERE proveedorID = @proveedorID 
                               ORDER BY Fecha DESC";
                SqlCommand cmdUltimo = new SqlCommand(queryUltimo, conn);
                cmdUltimo.Parameters.AddWithValue("@proveedorID", proveedorID);
                SqlDataReader reader = cmdUltimo.ExecuteReader();
                if (reader.Read())
                {
                    saldoActual = reader["Saldo"] != DBNull.Value ? Convert.ToDecimal(reader["Saldo"]) : 0;
                    saldoBolsasActual = reader["SaldoBls"] != DBNull.Value ? Convert.ToDecimal(reader["SaldoBls"]) : 0;
                }
                reader.Close();

                // Obtener precio actual de la bolsa
                string queryPrecio = "SELECT TOP 1 Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                SqlCommand cmdPrecio = new SqlCommand(queryPrecio, conn);
                object precioObj = cmdPrecio.ExecuteScalar();
                precioBolsa = precioObj != null ? Convert.ToDecimal(precioObj) : 1;

                decimal haber = monto;
                decimal haberBls = precioBolsa != 0 ? haber / precioBolsa : 0;
                decimal saldoNuevo = saldoActual - haber;
                decimal saldoBlsNuevo = saldoBolsasActual - haberBls;

                // Insertar movimiento en cuenta proveedor
                string insertQuery = @"INSERT INTO MovimientoCuentaProveedor 
                               (proveedorID, Fecha, Detalle, Debe, Haber, Saldo, DebeBls, HaberBls, SaldoBls)
                               VALUES (@proveedorID, @Fecha, @Detalle, 0, @Haber, @Saldo, 0, @HaberBls, @SaldoBls)";
                SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("@proveedorID", proveedorID);
                insertCmd.Parameters.AddWithValue("@Fecha", fecha);
                insertCmd.Parameters.AddWithValue("@Detalle", detalle);
                insertCmd.Parameters.AddWithValue("@Haber", haber);
                insertCmd.Parameters.AddWithValue("@Saldo", saldoNuevo);
                insertCmd.Parameters.AddWithValue("@HaberBls", haberBls);
                insertCmd.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                insertCmd.ExecuteNonQuery();

                // Actualizar saldo en cuenta corriente proveedor
                string updateCuentaCorriente = @"UPDATE CuentaCorrienteProveedor 
                                         SET SaldoPesos = @Saldo 
                                         WHERE proveedorID = @proveedorID";
                SqlCommand updateCmd = new SqlCommand(updateCuentaCorriente, conn);
                updateCmd.Parameters.AddWithValue("@Saldo", saldoNuevo);
                updateCmd.Parameters.AddWithValue("@proveedorID", proveedorID);
                updateCmd.ExecuteNonQuery();

                // === REGISTRO EN CAJA COMO EGRESO ===

                // Buscar caja de la fecha
                string queryCaja = @"SELECT TOP 1 cajaID, saldoInicial, saldoFinal
                             FROM Caja 
                             WHERE CAST(fecha AS DATE) = @fechaCaja";
                SqlCommand cmdCaja = new SqlCommand(queryCaja, conn);
                cmdCaja.Parameters.AddWithValue("@fechaCaja", fecha.Date);

                int cajaID;
                decimal saldoFinalCaja;

                using (SqlDataReader cajaReader = cmdCaja.ExecuteReader())
                {
                    if (cajaReader.Read())
                    {
                        cajaID = Convert.ToInt32(cajaReader["cajaID"]);
                        saldoFinalCaja = cajaReader["saldoFinal"] != DBNull.Value ? Convert.ToDecimal(cajaReader["saldoFinal"]) : 0;

                        if (saldoFinalCaja == 0)
                        {
                            // Usar saldoInicial como base si todavía no hubo movimientos
                            saldoFinalCaja = cajaReader["saldoInicial"] != DBNull.Value ? Convert.ToDecimal(cajaReader["saldoInicial"]) : 0;
                        }

                    }
                    else
                    {
                        cajaID = -1;
                        saldoFinalCaja = 0;
                    }
                }

                // Si no existe, crearla con saldo inicial = último saldo final anterior
                if (cajaID == -1)
                {
                    SqlCommand cmdSaldoAnterior = new SqlCommand(
                        @"SELECT TOP 1 saldoFinal 
                  FROM Caja 
                  WHERE fecha < @fechaCaja
                  ORDER BY fecha DESC", conn);
                    cmdSaldoAnterior.Parameters.AddWithValue("@fechaCaja", fecha.Date);
                    object saldoAnteriorObj = cmdSaldoAnterior.ExecuteScalar();
                    decimal saldoAnterior = saldoAnteriorObj != DBNull.Value ? Convert.ToDecimal(saldoAnteriorObj) : 0;

                    SqlCommand cmdInsertCaja = new SqlCommand(
                        @"INSERT INTO Caja (fecha, saldoInicial, saldoFinal) 
                  VALUES (@fechaCaja, @saldoInicial, @saldoInicial);
                  SELECT SCOPE_IDENTITY();", conn);
                    cmdInsertCaja.Parameters.AddWithValue("@fechaCaja", fecha.Date);
                    cmdInsertCaja.Parameters.AddWithValue("@saldoInicial", saldoAnterior);

                    cajaID = Convert.ToInt32(cmdInsertCaja.ExecuteScalar());
                    saldoFinalCaja = saldoAnterior;
                }

                // Insertar movimiento como egreso
                string insertMovimientoCaja = @"INSERT INTO MovimientoCaja 
                                        (cajaID, fecha, tipoMovimiento, descripcion, monto, origen)
                                        VALUES (@cajaID, @fecha, @tipoMovimiento, @descripcion, @monto, @origen)";
                SqlCommand cmdMovimiento = new SqlCommand(insertMovimientoCaja, conn);
                cmdMovimiento.Parameters.AddWithValue("@cajaID", cajaID);
                cmdMovimiento.Parameters.AddWithValue("@fecha", fecha);
                cmdMovimiento.Parameters.AddWithValue("@tipoMovimiento", "Egreso");
                cmdMovimiento.Parameters.AddWithValue("@descripcion", detalle);
                cmdMovimiento.Parameters.AddWithValue("@monto", monto);
                cmdMovimiento.Parameters.AddWithValue("@origen", "Pago a cañero");
                cmdMovimiento.ExecuteNonQuery();

                // Actualizar saldoFinal de la caja (restar monto del egreso)
                decimal nuevoSaldoFinal = saldoFinalCaja - monto;
                SqlCommand cmdUpdateSaldoFinal = new SqlCommand(
                    @"UPDATE Caja SET saldoFinal = @nuevoSaldoFinal WHERE cajaID = @cajaID", conn);
                cmdUpdateSaldoFinal.Parameters.AddWithValue("@nuevoSaldoFinal", nuevoSaldoFinal);
                cmdUpdateSaldoFinal.Parameters.AddWithValue("@cajaID", cajaID);
                cmdUpdateSaldoFinal.ExecuteNonQuery();
            }

            // Limpiar campos
            txtDetalle.Text = "";
            txtMonto.Text = "";

            // Refrescar DataGrid
            CargarCuenta(proveedorID);
        }


        private decimal ObtenerSaldoActual(int proveedorID)
        {
            decimal saldo = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT TOP 1 Saldo FROM MovimientoCuentaProveedor WHERE proveedorID = @proveedorID ORDER BY Fecha DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@proveedorID", proveedorID);

                object result = cmd.ExecuteScalar();
                if (result != DBNull.Value && result != null)
                {
                    saldo = Convert.ToDecimal(result);
                }
            }

            return saldo;
        }

        private void txtMonto_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string textoActual = textBox.Text;

            // Insertar el nuevo carácter en la posición actual del cursor
            int caretIndex = textBox.CaretIndex;
            string textoConNuevoCaracter = textoActual.Insert(caretIndex, e.Text);

            // Validar usando expresión regular
            e.Handled = !EsMontoValido(textoConNuevoCaracter);
        }

        private bool EsMontoValido(string input)
        {
            // Acepta números enteros o con parte decimal separada por coma
            return Regex.IsMatch(input, @"^\d{0,12}([,]\d{0,2})?$");
        }

        // (Opcional) podés ocultar errores si se corrige después
        private void txtMonto_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Ejemplo: cambiar borde si querés marcar error visual
            if (!EsMontoValido(txtMonto.Text))
            {
                txtMonto.BorderBrush = System.Windows.Media.Brushes.Red;
            }
            else
            {
                txtMonto.ClearValue(Border.BorderBrushProperty);
            }
        }
    }

    public class MovimientoCuentaProveedor
    {
        public DateTime Fecha { get; set; }
        public string Detalle { get; set; }
        public decimal? Debe { get; set; }
        public decimal? Haber { get; set; }
        public decimal? Saldo { get; set; }
        public decimal? DebeBls { get; set; }
        public decimal? HaberBls { get; set; }
        public decimal? SaldoBls { get; set; }
    }
}
