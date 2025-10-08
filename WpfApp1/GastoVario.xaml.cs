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
    /// Lógica de interacción para GastoVario.xaml
    /// </summary>
    public partial class GastoVario : Window
    {
        private string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";
        private Caja parentControl;
        public GastoVario(Caja parent)
        {
            InitializeComponent();
            parentControl = parent;

            txtFecha.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            // 1️⃣ Validaciones
            if (string.IsNullOrWhiteSpace(txtDetalle.Text))
            {
                MessageBox.Show("El campo Detalle no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!decimal.TryParse(txtMonto.Text, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Ingrese un monto válido (solo números positivos).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                DateTime fecha = DateTime.Parse(txtFecha.Text);
                string detalle = txtDetalle.Text;

                if (!decimal.TryParse(txtMonto.Text, out monto) || monto <= 0)
                {
                    MessageBox.Show("Ingrese un monto válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1️⃣ Buscar caja del día (usando rango para evitar problemas con horas)
                SqlCommand cmdCaja = new SqlCommand(@"
            SELECT cajaID 
            FROM Caja 
            WHERE fecha >= @fechaInicio AND fecha < @fechaFin", conn);
                cmdCaja.Parameters.AddWithValue("@fechaInicio", fecha.Date);
                cmdCaja.Parameters.AddWithValue("@fechaFin", fecha.Date.AddDays(1));

                object cajaIdObj = cmdCaja.ExecuteScalar();
                int cajaId;

                if (cajaIdObj == null)
                {
                    // No existe, buscamos saldoFinal de la última caja
                    SqlCommand cmdSaldo = new SqlCommand(@"
                SELECT TOP 1 saldoFinal 
                FROM Caja 
                WHERE fecha < @fechaInicio
                ORDER BY fecha DESC", conn);
                    cmdSaldo.Parameters.AddWithValue("@fechaInicio", fecha.Date);

                    object saldoAnteriorObj = cmdSaldo.ExecuteScalar();
                    decimal saldoAnterior = saldoAnteriorObj != DBNull.Value ? Convert.ToDecimal(saldoAnteriorObj) : 0;

                    // Crear nueva caja
                    SqlCommand cmdInsertCaja = new SqlCommand(@"
                INSERT INTO Caja (fecha, saldoInicial, saldoFinal) 
                VALUES (@fecha, @saldoInicial, @saldoFinal); 
                SELECT SCOPE_IDENTITY();", conn);
                    cmdInsertCaja.Parameters.AddWithValue("@fecha", fecha.Date);
                    cmdInsertCaja.Parameters.AddWithValue("@saldoInicial", saldoAnterior);
                    cmdInsertCaja.Parameters.AddWithValue("@saldoFinal", saldoAnterior);

                    cajaId = Convert.ToInt32(cmdInsertCaja.ExecuteScalar());
                }
                else
                {
                    cajaId = Convert.ToInt32(cajaIdObj);
                }

                // 2️⃣ Insertar movimiento como Egreso / Gasto Vario
                SqlCommand cmdMov = new SqlCommand(@"
            INSERT INTO MovimientoCaja (cajaID, fecha, tipoMovimiento, descripcion, monto, origen)
            VALUES (@cajaID, @fecha, 'Egreso', @detalle, @monto, 'Gasto Vario')", conn);
                cmdMov.Parameters.AddWithValue("@cajaID", cajaId);
                cmdMov.Parameters.AddWithValue("@fecha", fecha);
                cmdMov.Parameters.AddWithValue("@detalle", detalle);
                cmdMov.Parameters.AddWithValue("@monto", monto);
                cmdMov.ExecuteNonQuery();

                // 2️⃣b Insertar en tabla Gasto para historial
                SqlCommand cmdGasto = new SqlCommand(@"
    INSERT INTO Gasto (fecha, detalle, monto)
    VALUES (@fecha, @detalle, @monto)", conn);
                cmdGasto.Parameters.AddWithValue("@fecha", fecha);
                cmdGasto.Parameters.AddWithValue("@detalle", detalle);
                cmdGasto.Parameters.AddWithValue("@monto", monto);
                cmdGasto.ExecuteNonQuery();

                // 3️⃣ Actualizar saldoFinal (si está NULL usa saldoInicial como base)
                SqlCommand cmdUpdateSaldo = new SqlCommand(@"
            UPDATE Caja
            SET saldoFinal = ISNULL(saldoFinal, saldoInicial) - @monto
            WHERE cajaID = @cajaID", conn);
                cmdUpdateSaldo.Parameters.AddWithValue("@monto", monto);
                cmdUpdateSaldo.Parameters.AddWithValue("@cajaID", cajaId);
                cmdUpdateSaldo.ExecuteNonQuery();

                MessageBox.Show("Gasto registrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 4️⃣ Actualizar la vista de Caja en la ventana principal
            parentControl.CargarDatosCaja(DateTime.Today);

            // 5️⃣ Cerrar formulario
            this.Close();
        }

        // Evita que se escriban letras o signos negativos
        private void txtMonto_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !EsMontoValido(((TextBox)sender).Text + e.Text);
        }

        // Evita pegar texto inválido
        private void txtMonto_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                TextBox tb = sender as TextBox;
                if (!EsMontoValido(tb.Text + text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        // Validación de monto positivo
        private bool EsMontoValido(string input)
        {
            return decimal.TryParse(input, out decimal result) && result > 0;
        }

    }
}
