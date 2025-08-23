using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Controls;

using LiveCharts;
using LiveCharts.Wpf;
using System.Linq;


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
            CargarGrafico();
            txtNuevoPrecio.Text = string.Empty;
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

        private void CargarGrafico()
        {
            var valores = new ChartValues<double>();
            var etiquetas = new List<string>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // Orden ascendente para ver la evolución cronológica
                string query = "SELECT Fecha, Precio FROM ParametroPrecio ORDER BY Fecha ASC";
                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime fecha = Convert.ToDateTime(reader["Fecha"]);
                        double precio = Convert.ToDouble(reader["Precio"]);
                        valores.Add(precio);
                        etiquetas.Add(fecha.ToString("dd/MM"));
                    }
                }
            }

            // Si no hay datos, dejamos el chart vacío
            if (valores.Count == 0)
            {
                chartPrecios.Series = new SeriesCollection();
                chartPrecios.AxisX = new AxesCollection();
                chartPrecios.AxisY = new AxesCollection();
                return;
            }

            // Mostrar solo los últimos 12 puntos para que no se amontonen
            int take = Math.Min(valores.Count, 12);
            var ultimosValores = new ChartValues<double>(valores.Skip(valores.Count - take).Take(take));
            var ultimasEtiquetas = etiquetas.Skip(etiquetas.Count - take).Take(take).ToArray();

            chartPrecios.Series = new SeriesCollection
    {
        new LineSeries
        {
            Title = "Precio (ARS)",
            Values = ultimosValores,
            PointGeometrySize = 8,
            LineSmoothness = 0.6,
            Stroke = System.Windows.Media.Brushes.SteelBlue,
            Fill = new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromArgb(60, 70, 130, 180)) // SteelBlue con alpha
        }
    };

            chartPrecios.AxisX = new AxesCollection
    {
        new Axis
        {
            Labels = ultimasEtiquetas,
            Separator = new LiveCharts.Wpf.Separator { Step = 1 }

            // LabelsRotation = 45, // activá si se pisan
        }
    };

            chartPrecios.AxisY = new AxesCollection
    {
        new Axis
        {
            LabelFormatter = v => $"$ {v:N0}"
        }
    };
        }


        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
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

