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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data;
using System.Data.SqlClient;


namespace WpfApp1
{
    /// <summary>
    /// Lógica de interacción para Caja.xaml
    /// </summary>
    public partial class Caja : UserControl
    {
        private string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";
       
        public Caja()
        {
            InitializeComponent();
            this.Loaded += Caja_Loaded;
        }

        private void Caja_Loaded(object sender, RoutedEventArgs e)
        {
            // Asignar fecha actual al DatePicker
            dpFechaCaja.SelectedDate = DateTime.Now;

            // Llamar a verificación y carga inicial
            VerificarOAbrirCaja(DateTime.Now);
            CargarDatosCaja(DateTime.Now);
        }
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        { 

            if (dpFechaCaja.SelectedDate.HasValue)
            {
                VerificarOAbrirCaja(dpFechaCaja.SelectedDate.Value);
                CargarDatosCaja(dpFechaCaja.SelectedDate.Value);
            }
        }

        private void VerificarOAbrirCaja(DateTime fechaSeleccionada)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                SqlCommand cmdExiste = new SqlCommand(
                    "SELECT COUNT(*) FROM Caja WHERE fecha = @fecha", conn);
                cmdExiste.Parameters.AddWithValue("@fecha", fechaSeleccionada.Date);

                int existe = (int)cmdExiste.ExecuteScalar();

                if (existe == 0)
                {
                    SqlCommand cmdSaldo = new SqlCommand(
                        @"SELECT TOP 1 saldoFinal 
                      FROM Caja 
                      WHERE fecha < @fecha
                      ORDER BY fecha DESC", conn);
                    cmdSaldo.Parameters.AddWithValue("@fecha", fechaSeleccionada.Date);

                    object saldoAnteriorObj = cmdSaldo.ExecuteScalar();
                    decimal saldoAnterior = saldoAnteriorObj != DBNull.Value ? Convert.ToDecimal(saldoAnteriorObj) : 0;

                    SqlCommand cmdInsert = new SqlCommand(
                        @"INSERT INTO Caja (fecha, saldoInicial) 
                      VALUES (@fecha, @saldoInicial)", conn);
                    cmdInsert.Parameters.AddWithValue("@fecha", fechaSeleccionada.Date);
                    cmdInsert.Parameters.AddWithValue("@saldoInicial", saldoAnterior);

                    cmdInsert.ExecuteNonQuery();
                }
            }
        }

        public void CargarDatosCaja(DateTime fechaSeleccionada)
        {
            DataTable dt = new DataTable();

            decimal saldoInicial = 0;
            decimal totalIngresos = 0;
            decimal totalEgresos = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // 1. Buscar cajaID y saldoInicial
                string getCajaQuery = @"
            SELECT cajaID, saldoInicial
            FROM Caja
            WHERE fecha >= @fechaInicioCaja
              AND fecha <  @fechaFinCaja";

                SqlCommand cmdCaja = new SqlCommand(getCajaQuery, conn);
                cmdCaja.Parameters.Add("@fechaInicioCaja", SqlDbType.DateTime).Value = fechaSeleccionada.Date;
                cmdCaja.Parameters.Add("@fechaFinCaja", SqlDbType.DateTime).Value = fechaSeleccionada.Date.AddDays(1);

                SqlDataReader readerCaja = cmdCaja.ExecuteReader();
                if (!readerCaja.Read())
                {
                    dgMovimientos.ItemsSource = null;
                    txtSaldoInicial.Text = "$0.00";
                    txtIngresos.Text = "$0.00";
                    txtEgresos.Text = "$0.00";
                    txtSaldoActual.Text = "$0.00";
                    return;
                }

                int cajaId = readerCaja.GetInt32(0);
                saldoInicial = readerCaja.GetDecimal(1);
                readerCaja.Close();

                // 2. Traer movimientos
                string queryMovimientos = @"
            SELECT m.fecha, 
                   m.tipoMovimiento, 
                   m.descripcion, 
                   m.monto, 
                   m.origen
            FROM MovimientoCaja m
            WHERE m.cajaID = @cajaID
            ORDER BY m.fecha ASC";

                SqlCommand cmdMov = new SqlCommand(queryMovimientos, conn);
                cmdMov.Parameters.Add("@cajaID", SqlDbType.Int).Value = cajaId;

                SqlDataAdapter da = new SqlDataAdapter(cmdMov);
                da.Fill(dt);

                // 3. Calcular ingresos y egresos
                foreach (DataRow row in dt.Rows)
                {
                    string tipo = row["tipoMovimiento"].ToString();
                    decimal monto = Convert.ToDecimal(row["monto"]);

                    if (tipo.Equals("Ingreso", StringComparison.OrdinalIgnoreCase))
                        totalIngresos += monto;
                    else if (tipo.Equals("Egreso", StringComparison.OrdinalIgnoreCase))
                        totalEgresos += monto;
                }
            }

            // 4. Asignar DataGrid
            dgMovimientos.ItemsSource = dt.DefaultView;

            // 5. Actualizar tarjetas
            txtSaldoInicial.Text = saldoInicial.ToString("C2"); // formato moneda
            txtIngresos.Text = totalIngresos.ToString("C2");
            txtEgresos.Text = totalEgresos.ToString("C2");
            txtSaldoActual.Text = (saldoInicial + totalIngresos - totalEgresos).ToString("C2");

            txtTotalIngresos.Text = totalIngresos.ToString("C2");
            txtTotalEgresos.Text = totalEgresos.ToString("C2");
            txtTotalSaldo.Text = (saldoInicial + totalIngresos - totalEgresos).ToString("C2");
        }

        private void BtnGasto_Click(object sender, RoutedEventArgs e)
        {
            GastoVario form = new GastoVario(this);
            form.ShowDialog();
        }
    }
}
