using System;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Text;

namespace WpfApp1
{
    public partial class Compras : UserControl
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";

        public Compras()
        {
            InitializeComponent();
            CargarCañeros();
        }

        #region Clases auxiliares
        private class CañeroItem
        {
            public int Id { get; set; }
            public string Nombre { get; set; }
        }

        private class CompraHistorialDTO
        {
            public int IdCompra { get; set; }
            public DateTime Fecha { get; set; }
            public string Cañero { get; set; }
            public string Producto { get; set; } // Cadena larga de productos
            public int Cantidad { get; set; }    // Total unidades
            public decimal Total { get; set; }
        }
        #endregion

        #region 
        private List<CañeroItem> listaCañerosOriginal; // Guardamos la lista completa

        private void CargarCañeros()
        {
            try
            {
                listaCañerosOriginal = new List<CañeroItem>();

                // Opciones especiales
                listaCañerosOriginal.Add(new CañeroItem { Id = 0, Nombre = "Todos" });

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "SELECT proveedorID, Apellido, Nombre FROM Proveedor";
                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        listaCañerosOriginal.Add(new CañeroItem
                        {
                            Id = reader.GetInt32(0),
                            Nombre = reader.GetString(1) + " " + reader.GetString(2)
                        });
                    }
                }

                // Asignar la lista completa al ComboBox
                cbCañero.ItemsSource = listaCañerosOriginal;
                cbCañero.DisplayMemberPath = "Nombre";
                cbCañero.SelectedValuePath = "Id";
                cbCañero.SelectedIndex = 0;

                // Configuración para búsqueda dinámica
                cbCañero.IsEditable = true;
                cbCañero.IsTextSearchEnabled = false; // desactivamos la búsqueda interna
                cbCañero.StaysOpenOnEdit = true;

                // Obtenemos el TextBox interno del ComboBox
                cbCañero.Loaded += (s, e) =>
                {
                    if (cbCañero.Template.FindName("PART_EditableTextBox", cbCañero) is TextBox textBox)
                    {
                        textBox.TextChanged += CbCañero_TextChanged;
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar cañeros: " + ex.Message);
            }
        }

        // Evento que filtra la lista mientras tipeas
        private void CbCañero_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            string texto = tb.Text.ToLower();

            // Filtrar la lista original según el texto
            var listaFiltrada = listaCañerosOriginal
                .Where(c => c.Nombre.ToLower().Contains(texto))
                .ToList();

            cbCañero.ItemsSource = listaFiltrada;

            // Mantener el texto que ya escribiste
            tb.Text = texto;
            tb.SelectionStart = tb.Text.Length;
        }
        #endregion
        #region 
        private void BtnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            // 🔄 Reiniciar filtros
            cbCañero.SelectedIndex = 0; // Selecciona "Todos"
            dpDesde.SelectedDate = null;
            dpHasta.SelectedDate = null;
            FiltrarCompras();
        }
        #endregion
        private void FiltrarCompras()
        {
            try
            {
                DateTime? desde = dpDesde.SelectedDate;
                DateTime? hasta = dpHasta.SelectedDate;
                int cañeroId = 0;

                if (cbCañero.SelectedItem is CañeroItem item)
                   cañeroId = item.Id; 

                bool incluirCañeros = (cañeroId == 0 || cañeroId > 0);
                bool incluirCF = (cañeroId == 0 || cañeroId == -1);

                List<CompraHistorialDTO> historial = new List<CompraHistorialDTO>();

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    #region Ventas a clientes reales
                    if (incluirCañeros)
                    {
                        StringBuilder queryCompra = new StringBuilder(@"
                    SELECT co.compraID, co.Fecha, pr.Apellido, pr.Nombre,
                           p.Nombre AS Producto, d.Cantidad, d.Subtotal, co.TotalCompra
                    FROM Compra co
                    INNER JOIN Proveedor pr ON co.proveedorID = pr.proveedorID
                    INNER JOIN detalleCompra d ON co.compraID = d.compraID
                    INNER JOIN Producto p ON d.productoID = p.productoID
                    WHERE 1=1
                ");

                        SqlCommand cmd = new SqlCommand();
                        cmd.Connection = con;

                        if (desde.HasValue)
                        {
                            queryCompra.Append(" AND v.Fecha >= @Desde");
                            cmd.Parameters.AddWithValue("@Desde", desde.Value);
                        }
                        if (hasta.HasValue)
                        {
                            queryCompra.Append(" AND v.Fecha < @Hasta"); // Hasta el final del día
                            cmd.Parameters.AddWithValue("@Hasta", hasta.Value.AddDays(1));
                        }
                        if (cañeroId > 0)
                        {
                            queryCompra.Append(" AND co.proveedorID = @proveedorID");
                            cmd.Parameters.AddWithValue("@proveedorID", cañeroId);
                        }

                        cmd.CommandText = queryCompra.ToString();
                        SqlDataReader reader = cmd.ExecuteReader();

                        var tempCompras = new Dictionary<int, CompraHistorialDTO>();

                        while (reader.Read())
                        {
                            int compraId = reader.GetInt32(0);
                            DateTime fecha = reader.GetDateTime(1);
                            string cañero = reader.GetString(2) + " " + reader.GetString(3);
                            string producto = reader.GetString(4);
                            int cantidad = reader.GetInt32(5);
                            decimal subtotal = reader.GetDecimal(6);
                            decimal total = reader.GetDecimal(7);

                            if (!tempCompras.ContainsKey(compraId))
                            {
                                tempCompras[compraId] = new CompraHistorialDTO
                                {
                                    IdCompra = compraId,
                                    Fecha = fecha,
                                    Cañero = cañero,
                                    Producto = $"{producto} x{cantidad} (${subtotal})",
                                    Cantidad = cantidad,
                                    Total = total
                                };
                            }
                            else
                            {
                                tempCompras[compraId].Producto += $", {producto} x{cantidad} (${subtotal})";
                                tempCompras[compraId].Cantidad += cantidad;
                            }
                        }
                        reader.Close();
                        historial.AddRange(tempCompras.Values);
                    }
                    #endregion

                }

                historial = historial.OrderByDescending(x => x.Fecha).ToList();
                dgCompras.ItemsSource = historial;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al filtrar compras: " + ex.Message);
            }
        }


        private void BtnNuevaCompra_Click(object sender, RoutedEventArgs e)
        {
            CompraForm compracañero = new CompraForm();
            compracañero.ShowDialog();
        }

    }
}