using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class Stock : UserControl
    {
        string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";

        public Stock()
        {
            InitializeComponent();
            dpFechaStock.SelectedDate = DateTime.Now;
            dpFechaStock.DisplayDate = DateTime.Now;

            // Evento cuando cambia la fecha
            dpFechaStock.SelectedDateChanged += DpFechaStock_SelectedDateChanged;

            // Para cargar los valores al iniciar con la fecha actual
            CargarDatos(DateTime.Now);
        }

        private void DpFechaStock_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpFechaStock.SelectedDate.HasValue)
            {
                DateTime fechaSeleccionada = dpFechaStock.SelectedDate.Value;
                CargarDatos(fechaSeleccionada);
            }
        }

        // Función que carga todo: StockDiario y movimientos
        private void CargarDatos(DateTime fecha)
        {
            txtFecha.Text = fecha.ToString("yyyy-MM-dd"); // poner la fecha en el TextBox

            CargarStock(fecha);
            CargarMovimientosStock(fecha);
        }

        // Carga stock inicial y final
        private void CargarStock(DateTime fecha)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT stockInicial, stockFinal FROM StockDiario WHERE CAST(fecha AS DATE) = @fecha";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@fecha", fecha.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtStockInicial.Text = reader["stockInicial"].ToString();
                                txtStockActual.Text = reader["stockFinal"].ToString();
                            }
                            else
                            {
                                txtStockInicial.Text = "0";
                                txtStockActual.Text = "0";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar stock: " + ex.Message);
            }
        }

        // Carga movimientos del día seleccionado en el DataGrid usando lista de objetos
        private void CargarMovimientosStock(DateTime fecha)
        {
            try
            {
                List<MovimientoStockItem> lista = new List<MovimientoStockItem>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            m.movimientoID AS IdProducto,
                            p.Nombre AS Producto,
                            m.cantidad AS Cantidad,
                            m.tipo AS Tipo,
                            m.origen AS Origen
                        FROM MovimientoStock m
                        INNER JOIN Producto p ON m.productoID = p.productoID
                        INNER JOIN StockDiario s ON m.stockID = s.stockID
                        WHERE CAST(s.fecha AS DATE) = @fecha
                        ORDER BY m.movimientoID DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@fecha", fecha.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new MovimientoStockItem
                                {
                                    IdProducto = Convert.ToInt32(reader["IdProducto"]),
                                    Producto = reader["Producto"].ToString(),
                                    Cantidad = Convert.ToDecimal(reader["Cantidad"]),
                                    Tipo = reader["Tipo"].ToString(),
                                    Origen = reader["Origen"].ToString()
                                });
                            }
                        }
                    }
                }

                dgStock.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar movimientos de stock: " + ex.Message);
            }
        }
    }

    // Clase para representar un movimiento de stock
    public class MovimientoStockItem
    {
        public int IdProducto { get; set; }
        public string Producto { get; set; }
        public decimal Cantidad { get; set; }
        public string Tipo { get; set; }
        public string Origen { get; set; }
    }
}


