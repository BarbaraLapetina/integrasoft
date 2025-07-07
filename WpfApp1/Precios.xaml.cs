using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class Precios : UserControl
    {
        string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";

        public Precios()
        {
            InitializeComponent();
            CargarPrecioActual();
            CargarHistorial();
        }

        private void CargarPrecioActual()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT TOP 1 Fecha, Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                SqlCommand cmd = new SqlCommand(query, conn);
                
                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    txtPrecioActual.Text = $"$ {Convert.ToDecimal(reader["Precio"]):N2}";
                    txtFechaVigencia.Text = $"Vigente desde: {Convert.ToDateTime(reader["Fecha"]).ToShortDateString()}";
                }
            }
        }

        private void CargarHistorial()
        {
            List<HistorialPrecio> lista = new List<HistorialPrecio>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT Fecha, Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    lista.Add(new HistorialPrecio
                    {
                        Fecha = Convert.ToDateTime(reader["Fecha"]).ToShortDateString(),
                        Precio = $"$ {Convert.ToDecimal(reader["Precio"]):N2}",
                    });
                }
            }

            dgHistorial.ItemsSource = lista;

          
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e) {
            if (decimal.TryParse(txtNuevoPrecio.Text, out decimal nuevoPrecio))
            {

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Consulta para insertar el nuevo precio
                    string insertQuery = "INSERT INTO ParametroPrecio (Fecha, Precio) VALUES (@Fecha, @Precio)";
                    SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                    insertCmd.Parameters.AddWithValue("@Precio", nuevoPrecio);

                    // Consulta para actualizar todas las tablas que usan bolsas
                    string updateQuery = @"
                DECLARE @PrecioActual DECIMAL(18,2);

                SELECT TOP 1 @PrecioActual = Precio
                FROM ParametroPrecio
                ORDER BY Fecha DESC;

                -- Actualizar MovimientoCuentaCliente
                UPDATE MovimientoCuentaCliente
                SET 
                    DebeBls = CASE WHEN Debe > 0 THEN Debe / @PrecioActual ELSE 0 END,
                    HaberBls = CASE WHEN Haber > 0 THEN Haber / @PrecioActual ELSE 0 END,
                    SaldoBls = CASE WHEN Saldo <> 0 THEN Saldo / @PrecioActual ELSE 0 END;

                -- Actualizar MovimientoCuentaProveedor
                UPDATE MovimientoCuentaProveedor
                SET 
                    DebeBls = CASE WHEN Debe > 0 THEN Debe / @PrecioActual ELSE 0 END,
                    HaberBls = CASE WHEN Haber > 0 THEN Haber / @PrecioActual ELSE 0 END,
                    SaldoBls = CASE WHEN Saldo <> 0 THEN Saldo / @PrecioActual ELSE 0 END;

                -- Actualizar CuentaCorrienteCliente
                UPDATE CuentaCorrienteCliente
                SET 
                    SaldoBolsas = CASE WHEN SaldoPesos <> 0 THEN SaldoPesos / @PrecioActual ELSE 0 END;

                -- Actualizar CuentaCorrienteProveedor
                UPDATE CuentaCorrienteProveedor
                SET 
                    SaldoBolsas = CASE WHEN SaldoPesos <> 0 THEN SaldoPesos / @PrecioActual ELSE 0 END;
            ";

                    SqlCommand updateCmd = new SqlCommand(updateQuery, conn);

                    try
                    {
                        conn.Open();
                        insertCmd.ExecuteNonQuery();
                        updateCmd.ExecuteNonQuery();

                        MessageBox.Show("Precio actualizado correctamente.");

                        // Recargar vistas
                        CargarPrecioActual();
                        CargarHistorial();
                        txtNuevoPrecio.Text = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error al actualizar el precio: " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Por favor, ingrese un precio válido.");
            }
        }
    }
            

    }

    public class HistorialPrecio
    {
        public string Fecha { get; set; }
        public string Precio { get; set; }
    }

