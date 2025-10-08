using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Excel = Microsoft.Office.Interop.Excel;


namespace WpfApp1
{
    public partial class CuentaClienteWindow : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";
        private int clienteID;
        private string nombreCompleto;

        public CuentaClienteWindow(int clienteID, string nombreCompleto)
        {
            InitializeComponent();
            this.clienteID = clienteID;
            this.nombreCompleto = nombreCompleto;
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
                string query = @"SELECT movimientoID, Fecha, Detalle, Debe, Haber, Saldo 
                         FROM MovimientoCuentaCliente 
                         WHERE clienteID = @clienteID 
                         ORDER BY Fecha ASC";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@clienteID", clienteID);

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    movimientos.Add(new MovimientoCuentaCliente
                    {
                        movimientoID = Convert.ToInt32(reader["movimientoID"]), // 👈 ahora sí lo cargamos
                        Fecha = Convert.ToDateTime(reader["Fecha"]),
                        Detalle = reader["Detalle"] != DBNull.Value ? reader["Detalle"].ToString() : string.Empty,
                        Debe = reader["Debe"] != DBNull.Value ? Convert.ToDecimal(reader["Debe"]) : (decimal?)null,
                        Haber = reader["Haber"] != DBNull.Value ? Convert.ToDecimal(reader["Haber"]) : (decimal?)null,
                        Saldo = reader["Saldo"] != DBNull.Value ? Convert.ToDecimal(reader["Saldo"]) : (decimal?)null,
                    });
                }
            }

            dgCuenta.ItemsSource = movimientos;
        }


        private void BtnExportarExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var app = new Excel.Application();
                app.Workbooks.Add();
                Excel._Worksheet worksheet = app.ActiveSheet;

                // Encabezados
                for (int i = 0; i < dgCuenta.Columns.Count; i++)
                {
                    worksheet.Cells[1, i + 1] = dgCuenta.Columns[i].Header;
                }

                // Datos
                var lista = dgCuenta.ItemsSource as List<MovimientoCuentaCliente>;
                if (lista != null)
                {
                    for (int i = 0; i < lista.Count; i++)
                    {
                        var row = lista[i];
                        worksheet.Cells[i + 2, 1] = row.Fecha.ToString("yyyy-MM-dd");
                        worksheet.Cells[i + 2, 2] = row.Detalle;
                        worksheet.Cells[i + 2, 3] = row.Debe ?? 0;
                        worksheet.Cells[i + 2, 4] = row.Haber ?? 0;
                        worksheet.Cells[i + 2, 5] = row.Saldo ?? 0;
                    }
                }

                worksheet.Columns.AutoFit();
                app.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al exportar a Excel: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BtnRegistrarPago_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtMonto.Text, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Por favor, ingrese un monto válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string detalle = txtDetalle.Text.Trim();

            if (string.IsNullOrWhiteSpace(detalle))
            {
                MessageBox.Show("Por favor, ingrese un detalle para el pago.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime fecha = DateTime.Now;

            decimal saldoActual = ObtenerSaldoActual(clienteID);
            decimal saldoNuevo = saldoActual + monto;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string insertQuery = @"INSERT INTO MovimientoCuentaCliente 
                                               (clienteID, Fecha, Detalle, Debe, Haber, Saldo)
                                               VALUES (@clienteID, @Fecha, @Detalle, 0, @Haber, @Saldo);
                                               SELECT SCOPE_IDENTITY();";
                        SqlCommand insertCmd = new SqlCommand(insertQuery, conn, transaction);
                        insertCmd.Parameters.AddWithValue("@clienteID", clienteID);
                        insertCmd.Parameters.AddWithValue("@Fecha", fecha);
                        insertCmd.Parameters.AddWithValue("@Detalle", detalle);
                        insertCmd.Parameters.AddWithValue("@Haber", monto);
                        insertCmd.Parameters.AddWithValue("@Saldo", saldoNuevo);
                        insertCmd.ExecuteNonQuery();
                        // Actualizar saldo en CuentaCorrienteCliente
                        string updateCuentaCorriente = @"UPDATE CuentaCorrienteCliente 
                     SET SaldoPesos = @Saldo WHERE clienteID = @clienteID"; 
                        SqlCommand updateCmd = new SqlCommand(updateCuentaCorriente, conn, transaction); 
                        updateCmd.Parameters.AddWithValue("@Saldo", saldoNuevo); 
                        updateCmd.Parameters.AddWithValue("@clienteID", clienteID); 
                        updateCmd.ExecuteNonQuery(); 
                        // === REGISTRO EN CAJA ===
                        string queryCaja = @"SELECT TOP 1 cajaID FROM Caja WHERE 
                        CAST(fecha AS DATE) = @fechaCaja"; 
                        SqlCommand cmdCaja = new SqlCommand(queryCaja, conn, transaction); 
                        cmdCaja.Parameters.AddWithValue("@fechaCaja", fecha.Date); 
                        object cajaIdObj = cmdCaja.ExecuteScalar(); if (cajaIdObj == null) 
                        { // Buscar saldo final de la última caja
                          SqlCommand cmdSaldoAnterior = new SqlCommand( @"SELECT TOP 1 saldoFinal 
                          FROM Caja WHERE fecha < @fechaCaja ORDER BY fecha DESC", conn, transaction); 
                            cmdSaldoAnterior.Parameters.AddWithValue("@fechaCaja", fecha.Date); 
                            object saldoAnteriorObj = cmdSaldoAnterior.ExecuteScalar(); 
                            decimal saldoAnterior = saldoAnteriorObj != DBNull.Value ? Convert.ToDecimal(saldoAnteriorObj) : 0; 
                            // Crear nueva caja con saldo inicial y saldo final iguales
                            SqlCommand cmdInsertCaja = new SqlCommand( @"INSERT INTO Caja (fecha, saldoInicial, saldoFinal) 
                         VALUES (@fechaCaja, @saldoInicial, @saldoInicial); SELECT SCOPE_IDENTITY();", 
                         conn, transaction); 
                            cmdInsertCaja.Parameters.AddWithValue("@fechaCaja", fecha.Date); 
                            cmdInsertCaja.Parameters.AddWithValue("@saldoInicial", saldoAnterior); 
                            cajaIdObj = cmdInsertCaja.ExecuteScalar(); 
                        } 
                        int cajaID = Convert.ToInt32(cajaIdObj); 
                        // Insertar movimiento en caja
                        string insertMovimientoCaja = @"INSERT INTO MovimientoCaja 
                       (cajaID, fecha, tipoMovimiento, descripcion, monto, origen) VALUES 
                       (@cajaID, @fecha, @tipoMovimiento, @descripcion, @monto, @origen)"; 
                        SqlCommand cmdMovimiento = new SqlCommand(insertMovimientoCaja, conn, transaction);
                        cmdMovimiento.Parameters.AddWithValue("@cajaID", cajaID); 
                        cmdMovimiento.Parameters.AddWithValue("@fecha", fecha); 
                        cmdMovimiento.Parameters.AddWithValue("@tipoMovimiento", "Ingreso"); 
                        cmdMovimiento.Parameters.AddWithValue("@descripcion", detalle); 
                        cmdMovimiento.Parameters.AddWithValue("@monto", monto);
                        cmdMovimiento.Parameters.AddWithValue("@origen", "Pago de cliente - " + nombreCompleto);
                        cmdMovimiento.ExecuteNonQuery(); 
                            // Actualizar saldo final de caja
                      string updateSaldoFinalCaja = @"UPDATE Caja SET 
                      saldoFinal = ISNULL(saldoFinal, 0) + @monto WHERE cajaID = @cajaID"; 
                        SqlCommand cmdUpdateSaldoFinal = new SqlCommand(updateSaldoFinalCaja, conn, transaction); 
                        cmdUpdateSaldoFinal.Parameters.AddWithValue("@monto", monto); 
                        cmdUpdateSaldoFinal.Parameters.AddWithValue("@cajaID", cajaID); 
                        cmdUpdateSaldoFinal.ExecuteNonQuery();
                        transaction.Commit();
                        MessageBox.Show("El pago se registró correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Error al registrar el pago: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }
     

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
                    saldo = Convert.ToDecimal(result);
            }
            return saldo;
        }

       
        private void txtMonto_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string textoActual = textBox.Text;
            int caretIndex = textBox.CaretIndex;
            string textoConNuevoCaracter = textoActual.Insert(caretIndex, e.Text);
            e.Handled = !EsMontoValido(textoConNuevoCaracter);
        }

        private bool EsMontoValido(string input)
        {
            return Regex.IsMatch(input, @"^\d{0,12}([,]\d{0,2})?$");
        }

        private void txtMonto_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!EsMontoValido(txtMonto.Text))
                txtMonto.BorderBrush = Brushes.Red;
            else
                txtMonto.ClearValue(Border.BorderBrushProperty);
        }

        private void BtnModificar_Click(object sender, RoutedEventArgs e)
        {
            // Verificar que haya una fila seleccionada
            if (dgCuenta.SelectedItem is MovimientoCuentaCliente movimientoSeleccionado)
            {
                // Abrir la ventana de edición y pasarle el movimiento
                EditarMovimientoWindow ventanaEditar = new EditarMovimientoWindow(movimientoSeleccionado, clienteID);
                bool? resultado = ventanaEditar.ShowDialog();

                // Si la ventana devolvió true (o sea se guardó), recargar el DataGrid
                if (resultado == true)
                {
                    CargarCuenta(clienteID); // recarga los datos desde la base
                }
            }
            else
            {
                MessageBox.Show("Seleccione un movimiento para modificar.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


    }

    public class NegativoARojoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal dec && dec < 0)
                return Brushes.Red;
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

   
}
