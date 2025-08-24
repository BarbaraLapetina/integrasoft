using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class ControlGeneral : UserControl
    {
        string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";

        public ControlGeneral()
        {
            InitializeComponent();
            CargarKPIs();
            CargarGraficoAzucar();
            this.Loaded += ControlGeneral_Loaded;  // <- cargar cuando el control ya está en pantalla
        }

        private async void ControlGeneral_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= ControlGeneral_Loaded;   // que corra una sola vez

            // Cede un “tick” al dispatcher para que termine de pintar antes de cargar datos
            await Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);


            CargarKPIs();
            CargarGraficoAzucar();
        }

        private void CargarKPIs()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // === Dinero en caja: SALDO ACTUAL del último día de caja ===
                string qCajaActual = @"
;WITH ult AS (
  SELECT TOP (1) c.cajaID, c.saldoInicial
  FROM Caja c
  WHERE c.fecha <= CAST(GETDATE() AS date)
  ORDER BY c.fecha DESC
)
SELECT CAST(
         u.saldoInicial
       + ISNULL((SELECT SUM(monto) FROM MovimientoCaja WHERE cajaID=u.cajaID AND RTRIM(tipoMovimiento)='Ingreso'),0)
       - ISNULL((SELECT SUM(monto) FROM MovimientoCaja WHERE cajaID=u.cajaID AND RTRIM(tipoMovimiento)='Egreso'),0)
       AS decimal(18,2))
FROM ult u;";
                object cajaObj = new SqlCommand(qCajaActual, conn).ExecuteScalar();
                decimal saldoActual = (cajaObj == null || cajaObj == DBNull.Value) ? 0m : Convert.ToDecimal(cajaObj);
                txtCajaActual.Text = saldoActual.ToString("C2");

                // === Deuda clientes (si tu esquema guarda deuda negativa, se muestra en positivo) ===
                string qClientes = "SELECT CAST(ISNULL(SUM(SaldoPesos),0) AS decimal(18,2)) FROM CuentaCorrienteCliente;";
                object clientesObj = new SqlCommand(qClientes, conn).ExecuteScalar();
                decimal deudaClientes = (clientesObj == null || clientesObj == DBNull.Value) ? 0m : Convert.ToDecimal(clientesObj);
                if (deudaClientes < 0) deudaClientes = Math.Abs(deudaClientes);
                txtDeudaClientes.Text = deudaClientes.ToString("C2");

                // === Deuda proveedores (mismo criterio de signo) ===
                string qProv = "SELECT CAST(ISNULL(SUM(SaldoPesos),0) AS decimal(18,2)) FROM CuentaCorrienteProveedor;";
                object provObj = new SqlCommand(qProv, conn).ExecuteScalar();
                decimal deudaProv = (provObj == null || provObj == DBNull.Value) ? 0m : Convert.ToDecimal(provObj);
                if (deudaProv < 0) deudaProv = Math.Abs(deudaProv);
                txtDeudaProveedores.Text = deudaProv.ToString("C2");

                // === Precio azúcar (último precio cargado) ===
                string qAzucar = "SELECT TOP 1 CAST(Precio AS decimal(18,2)) FROM ParametroPrecio ORDER BY Fecha DESC;";
                object precioObj = new SqlCommand(qAzucar, conn).ExecuteScalar();
                decimal precio = (precioObj == null || precioObj == DBNull.Value) ? 0m : Convert.ToDecimal(precioObj);
                txtPrecioAzucar.Text = precio.ToString("C2");
            }
        }



        private void CargarGraficoAzucar()
        {
            var valores = new ChartValues<double>();
            var etiquetas = new List<string>();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT TOP 12 Fecha, Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                SqlCommand cmd = new SqlCommand(query, conn);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    var datos = new List<Tuple<DateTime, double>>();
                    while (reader.Read())
                    {
                        datos.Add(Tuple.Create(
                            Convert.ToDateTime(reader["Fecha"]),
                            Convert.ToDouble(reader["Precio"])
                        ));
                    }

                    // Ordenar cronológicamente
                    foreach (var d in datos.OrderBy(x => x.Item1))
                    {
                        valores.Add(d.Item2);
                        etiquetas.Add(d.Item1.ToString("dd/MM"));
                    }
                }
            }

            chartAzucar.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Precio",
                    Values = valores,
                    PointGeometrySize = 8
                }
            };

            chartAzucar.AxisX.Clear();
            chartAzucar.AxisX.Add(new Axis { Labels = etiquetas });
            chartAzucar.AxisY.Clear();
            chartAzucar.AxisY.Add(new Axis { LabelFormatter = v => $"$ {v:N0}" });
        }
    }
}
