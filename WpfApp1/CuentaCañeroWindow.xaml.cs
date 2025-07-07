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

                // Obtener el último saldo en pesos y en bolsas del proveedor
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

                // Insertar el nuevo movimiento
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

                // Actualizar saldo en CuentaCorrienteProveedor
                string updateCuentaCorriente = @"UPDATE CuentaCorrienteProveedor 
                                         SET SaldoPesos = @Saldo 
                                         WHERE proveedorID = @proveedorID";
                SqlCommand updateCmd = new SqlCommand(updateCuentaCorriente, conn);
                updateCmd.Parameters.AddWithValue("@Saldo", saldoNuevo);
                updateCmd.Parameters.AddWithValue("@proveedorID", proveedorID);
                updateCmd.ExecuteNonQuery();
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
