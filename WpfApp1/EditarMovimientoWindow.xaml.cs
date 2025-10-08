using System;
using System.Data.SqlClient;
using System.Windows;

namespace WpfApp1
{
    public partial class EditarMovimientoWindow : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";
        private MovimientoCuentaCliente movimiento;
        private int clienteID; // si lo necesitas para actualizar CuentaCorriente

        public EditarMovimientoWindow(MovimientoCuentaCliente mov, int clienteID)
        {
            InitializeComponent();
            this.movimiento = mov;
            this.clienteID = clienteID;

            // Llenar los TextBox con los datos actuales
            txtDetalle.Text = movimiento.Detalle;
            txtDebe.Text = movimiento.Debe?.ToString() ?? "0";
            txtHaber.Text = movimiento.Haber?.ToString() ?? "0";
            txtSaldo.Text = movimiento.Saldo?.ToString() ?? "0";
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(txtDetalle.Text))
            {
                MessageBox.Show("Ingrese un detalle.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtDebe.Text, out decimal debe))
            {
                MessageBox.Show("Debe ingresar un valor numérico válido para Debe.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!decimal.TryParse(txtHaber.Text, out decimal haber))
            {
                MessageBox.Show("Debe ingresar un valor numérico válido para Haber.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!decimal.TryParse(txtSaldo.Text, out decimal saldo))
            {
                MessageBox.Show("Debe ingresar un valor numérico válido para Saldo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1️⃣ Actualizar el movimiento editado (sin tocar saldo todavía)
                        string updateMovimiento = @"UPDATE MovimientoCuentaCliente
                                            SET Detalle=@Detalle, Debe=@Debe, Haber=@Haber
                                            WHERE movimientoID=@ID";
                        SqlCommand cmdUpdate = new SqlCommand(updateMovimiento, conn, transaction);
                        cmdUpdate.Parameters.AddWithValue("@Detalle", txtDetalle.Text.Trim());
                        cmdUpdate.Parameters.AddWithValue("@Debe", debe);
                        cmdUpdate.Parameters.AddWithValue("@Haber", haber);
                        cmdUpdate.Parameters.AddWithValue("@ID", movimiento.movimientoID);
                        cmdUpdate.ExecuteNonQuery();

                        // 2️⃣ Obtener todos los movimientos desde el editado en adelante
                        string selectMovimientos = @"SELECT movimientoID, Debe, Haber
                                             FROM MovimientoCuentaCliente
                                             WHERE clienteID=@clienteID AND Fecha >= @fechaEditada
                                             ORDER BY Fecha ASC";
                        SqlCommand cmdSelect = new SqlCommand(selectMovimientos, conn, transaction);
                        cmdSelect.Parameters.AddWithValue("@clienteID", clienteID);
                        cmdSelect.Parameters.AddWithValue("@fechaEditada", movimiento.Fecha);

                        List<(int movimientoID, decimal Debe, decimal Haber)> movimientosPosteriores = new();
                        using (SqlDataReader reader = cmdSelect.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                movimientosPosteriores.Add((
                                    reader.GetInt32(0),
                                    reader.GetDecimal(1),
                                    reader.GetDecimal(2)
                                ));
                            }
                        }

                        // 3️⃣ Obtener saldo anterior al movimiento editado
                        string getSaldoAnterior = @"SELECT TOP 1 Saldo
                                            FROM MovimientoCuentaCliente
                                            WHERE clienteID=@clienteID AND Fecha < @fechaEditada
                                            ORDER BY Fecha DESC";
                        SqlCommand cmdSaldoAnterior = new SqlCommand(getSaldoAnterior, conn, transaction);
                        cmdSaldoAnterior.Parameters.AddWithValue("@clienteID", clienteID);
                        cmdSaldoAnterior.Parameters.AddWithValue("@fechaEditada", movimiento.Fecha);
                        object saldoPrev = cmdSaldoAnterior.ExecuteScalar();
                        decimal saldoAcumulado = saldoPrev != null ? Convert.ToDecimal(saldoPrev) : 0;

                        // 4️⃣ Actualizar saldos de todos los movimientos posteriores considerando saldos negativos
                        foreach (var mov in movimientosPosteriores)
                        {
                            // En tu sistema: Debe aumenta la deuda (más negativo), Haber disminuye la deuda
                            saldoAcumulado = saldoAcumulado - mov.Debe + mov.Haber;

                            string updateSaldo = @"UPDATE MovimientoCuentaCliente 
                                           SET Saldo = @Saldo 
                                           WHERE movimientoID = @movimientoID";
                            SqlCommand cmdUpdateSaldo = new SqlCommand(updateSaldo, conn, transaction);
                            cmdUpdateSaldo.Parameters.AddWithValue("@Saldo", saldoAcumulado);
                            cmdUpdateSaldo.Parameters.AddWithValue("@movimientoID", mov.movimientoID);
                            cmdUpdateSaldo.ExecuteNonQuery();
                        }

                        // 5️⃣ Actualizar saldo final en CuentaCorrienteCliente
                        string updateCuenta = @"UPDATE CuentaCorrienteCliente
                                        SET SaldoPesos = @SaldoFinal
                                        WHERE clienteID=@clienteID";
                        SqlCommand cmdCuenta = new SqlCommand(updateCuenta, conn, transaction);
                        cmdCuenta.Parameters.AddWithValue("@SaldoFinal", saldoAcumulado);
                        cmdCuenta.Parameters.AddWithValue("@clienteID", clienteID);
                        cmdCuenta.ExecuteNonQuery();

                        transaction.Commit();

                        MessageBox.Show("Movimiento actualizado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                        this.DialogResult = true; // Devuelve true para que la ventana padre recargue el DataGrid
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Error al actualizar el movimiento: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }


        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

}
