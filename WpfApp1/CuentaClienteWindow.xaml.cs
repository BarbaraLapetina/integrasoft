using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp1
{
    public partial class CuentaClienteWindow : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";
        private int clienteID;

        public CuentaClienteWindow(int clienteID, string nombreCompleto)
        {
            InitializeComponent();
            this.clienteID = clienteID;
            txtTitulo.Text = $"Cuenta Cliente - {nombreCompleto}";
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
            CargarCuenta(clienteID);
        }

        private void CargarCuenta(int clienteID)
        {
            List<MovimientoCuentaCliente> movimientos = new List<MovimientoCuentaCliente>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Fecha, Detalle, Debe, Haber, Saldo, DebeBls, HaberBls, SaldoBls " +
                               "FROM MovimientoCuentaCliente WHERE clienteID = @clienteID ORDER BY Fecha ASC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@clienteID", clienteID);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    movimientos.Add(new MovimientoCuentaCliente
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
            decimal precioBolsa = 1;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Obtener el último saldo del cliente
                        string queryUltimo = @"SELECT TOP 1 Saldo 
                                       FROM MovimientoCuentaCliente 
                                       WHERE clienteID = @clienteID 
                                       ORDER BY Fecha DESC";
                        SqlCommand cmdUltimo = new SqlCommand(queryUltimo, conn, transaction);
                        cmdUltimo.Parameters.AddWithValue("@clienteID", clienteID);
                        object result = cmdUltimo.ExecuteScalar();
                        saldoActual = result != null ? Convert.ToDecimal(result) : 0;

                        // Obtener precio de bolsa
                        string queryPrecio = "SELECT TOP 1 Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                        SqlCommand cmdPrecio = new SqlCommand(queryPrecio, conn, transaction);
                        object precioObj = cmdPrecio.ExecuteScalar();
                        precioBolsa = precioObj != null ? Convert.ToDecimal(precioObj) : 1;

                        decimal haber = monto;
                        decimal haberBls = precioBolsa != 0 ? haber / precioBolsa : 0;
                        decimal saldoNuevo = saldoActual + haber;
                        decimal saldoBlsNuevo = Math.Abs(saldoNuevo) / precioBolsa;

                        // Insertar movimiento en cuenta del cliente
                        string insertQuery = @"INSERT INTO MovimientoCuentaCliente 
                                       (clienteID, Fecha, Detalle, Debe, Haber, Saldo, DebeBls, HaberBls, SaldoBls)
                                       VALUES (@clienteID, @Fecha, @Detalle, 0, @Haber, @Saldo, 0, @HaberBls, @SaldoBls)";
                        SqlCommand insertCmd = new SqlCommand(insertQuery, conn, transaction);
                        insertCmd.Parameters.AddWithValue("@clienteID", clienteID);
                        insertCmd.Parameters.AddWithValue("@Fecha", fecha);
                        insertCmd.Parameters.AddWithValue("@Detalle", detalle);
                        insertCmd.Parameters.AddWithValue("@Haber", haber);
                        insertCmd.Parameters.AddWithValue("@Saldo", saldoNuevo);
                        insertCmd.Parameters.AddWithValue("@HaberBls", haberBls);
                        insertCmd.Parameters.AddWithValue("@SaldoBls", saldoBlsNuevo);
                        insertCmd.ExecuteNonQuery();

                        // Actualizar saldo en CuentaCorrienteCliente
                        string updateCuentaCorriente = @"UPDATE CuentaCorrienteCliente 
                                                 SET SaldoPesos = @Saldo 
                                                 WHERE clienteID = @clienteID";
                        SqlCommand updateCmd = new SqlCommand(updateCuentaCorriente, conn, transaction);
                        updateCmd.Parameters.AddWithValue("@Saldo", saldoNuevo);
                        updateCmd.Parameters.AddWithValue("@clienteID", clienteID);
                        updateCmd.ExecuteNonQuery();

                        // === REGISTRO EN CAJA ===
                        string queryCaja = @"SELECT TOP 1 cajaID 
                                     FROM Caja 
                                     WHERE CAST(fecha AS DATE) = @fechaCaja";
                        SqlCommand cmdCaja = new SqlCommand(queryCaja, conn, transaction);
                        cmdCaja.Parameters.AddWithValue("@fechaCaja", fecha.Date);

                        object cajaIdObj = cmdCaja.ExecuteScalar();

                        if (cajaIdObj == null)
                        {
                            // Buscar saldo final de la última caja
                            SqlCommand cmdSaldoAnterior = new SqlCommand(
                                @"SELECT TOP 1 saldoFinal 
                          FROM Caja 
                          WHERE fecha < @fechaCaja
                          ORDER BY fecha DESC", conn, transaction);
                            cmdSaldoAnterior.Parameters.AddWithValue("@fechaCaja", fecha.Date);
                            object saldoAnteriorObj = cmdSaldoAnterior.ExecuteScalar();
                            decimal saldoAnterior = saldoAnteriorObj != DBNull.Value ? Convert.ToDecimal(saldoAnteriorObj) : 0;

                            // Crear nueva caja con saldo inicial y saldo final iguales
                            SqlCommand cmdInsertCaja = new SqlCommand(
                                @"INSERT INTO Caja (fecha, saldoInicial, saldoFinal) 
                          VALUES (@fechaCaja, @saldoInicial, @saldoInicial);
                          SELECT SCOPE_IDENTITY();", conn, transaction);
                            cmdInsertCaja.Parameters.AddWithValue("@fechaCaja", fecha.Date);
                            cmdInsertCaja.Parameters.AddWithValue("@saldoInicial", saldoAnterior);

                            cajaIdObj = cmdInsertCaja.ExecuteScalar();
                        }

                        int cajaID = Convert.ToInt32(cajaIdObj);

                        // Insertar movimiento en caja
                        string insertMovimientoCaja = @"INSERT INTO MovimientoCaja 
                                                (cajaID, fecha, tipoMovimiento, descripcion, monto, origen)
                                                VALUES (@cajaID, @fecha, @tipoMovimiento, @descripcion, @monto, @origen)";
                        SqlCommand cmdMovimiento = new SqlCommand(insertMovimientoCaja, conn, transaction);
                        cmdMovimiento.Parameters.AddWithValue("@cajaID", cajaID);
                        cmdMovimiento.Parameters.AddWithValue("@fecha", fecha);
                        cmdMovimiento.Parameters.AddWithValue("@tipoMovimiento", "Ingreso");
                        cmdMovimiento.Parameters.AddWithValue("@descripcion", detalle);
                        cmdMovimiento.Parameters.AddWithValue("@monto", monto);
                        cmdMovimiento.Parameters.AddWithValue("@origen", "Pago de cliente");
                        cmdMovimiento.ExecuteNonQuery();

                        // Actualizar saldo final de caja
                        string updateSaldoFinalCaja = @"UPDATE Caja 
                                                SET saldoFinal = ISNULL(saldoFinal, 0) + @monto
                                                WHERE cajaID = @cajaID";
                        SqlCommand cmdUpdateSaldoFinal = new SqlCommand(updateSaldoFinalCaja, conn, transaction);
                        cmdUpdateSaldoFinal.Parameters.AddWithValue("@monto", monto);
                        cmdUpdateSaldoFinal.Parameters.AddWithValue("@cajaID", cajaID);
                        cmdUpdateSaldoFinal.ExecuteNonQuery();

                        // Confirmar transacción
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Error al registrar el pago: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }

            // Limpiar campos y refrescar vista
            txtDetalle.Text = "";
            txtMonto.Text = "";
            CargarCuenta(clienteID);
        }


        private decimal ObtenerSaldoActual(int clienteID)
        {
            decimal saldo = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT TOP 1 Saldo FROM MovimientoCuentaCliente WHERE clienteID = @clienteID ORDER BY Fecha DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@clienteID", clienteID);

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

    public class MovimientoCuentaCliente
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
