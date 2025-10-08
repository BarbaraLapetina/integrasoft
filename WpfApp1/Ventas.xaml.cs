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
    public partial class Ventas : UserControl
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";

        public Ventas()
        {
            InitializeComponent();
            CargarClientes();
        }

        #region Clases auxiliares
        private class ClienteItem
        {
            public int Id { get; set; }
            public string Nombre { get; set; }
        }

        private class VentaHistorialDTO
        {
            public int IdVenta { get; set; }
            public DateTime Fecha { get; set; }
            public string Cliente { get; set; }
            public string Producto { get; set; } // Cadena larga de productos
            public int Cantidad { get; set; }    // Total unidades
            public decimal Total { get; set; }
        }
        #endregion

        #region Cargar ComboBox Clientes con búsqueda dinámica
        private List<ClienteItem> listaClientesOriginal; // Guardamos la lista completa

        private void CargarClientes()
        {
            try
            {
                listaClientesOriginal = new List<ClienteItem>();

                // Opciones especiales
                listaClientesOriginal.Add(new ClienteItem { Id = 0, Nombre = "Todos" });
                listaClientesOriginal.Add(new ClienteItem { Id = -1, Nombre = "Consumidor Final" });

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "SELECT ClienteID, Apellido, Nombre FROM Cliente";
                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        listaClientesOriginal.Add(new ClienteItem
                        {
                            Id = reader.GetInt32(0),
                            Nombre = reader.GetString(1) + " " + reader.GetString(2)
                        });
                    }
                }

                // Asignar la lista completa al ComboBox
                cbCliente.ItemsSource = listaClientesOriginal;
                cbCliente.DisplayMemberPath = "Nombre";
                cbCliente.SelectedValuePath = "Id";
                cbCliente.SelectedIndex = 0;

                // Configuración para búsqueda dinámica
                cbCliente.IsEditable = true;
                cbCliente.IsTextSearchEnabled = false; // desactivamos la búsqueda interna
                cbCliente.StaysOpenOnEdit = true;

                // Obtenemos el TextBox interno del ComboBox
                cbCliente.Loaded += (s, e) =>
                {
                    if (cbCliente.Template.FindName("PART_EditableTextBox", cbCliente) is TextBox textBox)
                    {
                        textBox.TextChanged += CbCliente_TextChanged;
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar clientes: " + ex.Message);
            }
        }

        // Evento que filtra la lista mientras tipeas
        private void CbCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            string texto = tb.Text.ToLower();

            // Filtrar la lista original según el texto
            var listaFiltrada = listaClientesOriginal
                .Where(c => c.Nombre.ToLower().Contains(texto))
                .ToList();

            cbCliente.ItemsSource = listaFiltrada;

            // Mantener el texto que ya escribiste
            tb.Text = texto;
            tb.SelectionStart = tb.Text.Length;
        }
        #endregion
        #region 
        private void BtnFiltrar_Click(object sender, RoutedEventArgs e) 
        {
            // 🔄 Reiniciar filtros
            cbCliente.SelectedIndex = 0; // Selecciona "Todos"
            dpDesde.SelectedDate = null;
            dpHasta.SelectedDate = null;
            FiltrarVentas();
        }
        #endregion
        private void FiltrarVentas()
        {
            try
            {
                DateTime? desde = dpDesde.SelectedDate;
                DateTime? hasta = dpHasta.SelectedDate;
                int clienteId = 0;

                if (cbCliente.SelectedItem is ClienteItem item)
                    clienteId = item.Id; // 0 = Todos, -1 = CF, >0 = cliente real

                bool incluirClientes = (clienteId == 0 || clienteId > 0);
                bool incluirCF = (clienteId == 0 || clienteId == -1);

                List<VentaHistorialDTO> historial = new List<VentaHistorialDTO>();

                using (SqlConnection con = new SqlConnection(connectionString))
                {
                    con.Open();

                    #region Ventas a clientes reales
                    if (incluirClientes)
                    {
                        StringBuilder queryVenta = new StringBuilder(@"
                    SELECT v.VentaID, v.Fecha, c.Apellido, c.Nombre,
                           p.Nombre AS Producto, d.Cantidad, d.Subtotal, v.TotalVenta
                    FROM Venta v
                    INNER JOIN Cliente c ON v.ClienteID = c.ClienteID
                    INNER JOIN DetalleVenta d ON v.VentaID = d.VentaID
                    INNER JOIN Producto p ON d.ProductoID = p.ProductoID
                    WHERE 1=1
                ");

                        SqlCommand cmd = new SqlCommand();
                        cmd.Connection = con;

                        if (desde.HasValue)
                        {
                            queryVenta.Append(" AND v.Fecha >= @Desde");
                            cmd.Parameters.AddWithValue("@Desde", desde.Value);
                        }
                        if (hasta.HasValue)
                        {
                            queryVenta.Append(" AND v.Fecha < @Hasta"); // Hasta el final del día
                            cmd.Parameters.AddWithValue("@Hasta", hasta.Value.AddDays(1));
                        }
                        if (clienteId > 0)
                        {
                            queryVenta.Append(" AND v.ClienteID = @ClienteID");
                            cmd.Parameters.AddWithValue("@ClienteID", clienteId);
                        }

                        cmd.CommandText = queryVenta.ToString();
                        SqlDataReader reader = cmd.ExecuteReader();

                        var tempVentas = new Dictionary<int, VentaHistorialDTO>();

                        while (reader.Read())
                        {
                            int ventaId = reader.GetInt32(0);
                            DateTime fecha = reader.GetDateTime(1);
                            string cliente = reader.GetString(2) + " " + reader.GetString(3);
                            string producto = reader.GetString(4);
                            int cantidad = reader.GetInt32(5);
                            decimal subtotal = reader.GetDecimal(6);
                            decimal total = reader.GetDecimal(7);

                            if (!tempVentas.ContainsKey(ventaId))
                            {
                                tempVentas[ventaId] = new VentaHistorialDTO
                                {
                                    IdVenta = ventaId,
                                    Fecha = fecha,
                                    Cliente = cliente,
                                    Producto = $"{producto} x{cantidad} (${subtotal})",
                                    Cantidad = cantidad,
                                    Total = total
                                };
                            }
                            else
                            {
                                tempVentas[ventaId].Producto += $", {producto} x{cantidad} (${subtotal})";
                                tempVentas[ventaId].Cantidad += cantidad;
                            }
                        }
                        reader.Close();
                        historial.AddRange(tempVentas.Values);
                    }
                    #endregion

                    #region Ventas a Consumidor Final
                    if (incluirCF)
                    {
                        StringBuilder queryCF = new StringBuilder(@"
                    SELECT v.VentaCFID, v.Fecha, p.Nombre AS Producto, d.Cantidad, d.Subtotal, v.TotalVentacf
                    FROM VentaCF v
                    INNER JOIN DetalleVentaCF d ON v.VentaCFID = d.VentaCFID
                    INNER JOIN Producto p ON d.ProductoID = p.ProductoID
                    WHERE 1=1
                ");

                        SqlCommand cmdCF = new SqlCommand();
                        cmdCF.Connection = con;

                        if (desde.HasValue)
                        {
                            queryCF.Append(" AND v.Fecha >= @Desde");
                            cmdCF.Parameters.AddWithValue("@Desde", desde.Value);
                        }
                        if (hasta.HasValue)
                        {
                            queryCF.Append(" AND v.Fecha < @Hasta"); // Mismo truco que arriba
                            cmdCF.Parameters.AddWithValue("@Hasta", hasta.Value.AddDays(1));
                        }

                        cmdCF.CommandText = queryCF.ToString();

                        SqlDataReader readerCF = cmdCF.ExecuteReader();
                        var tempCF = new Dictionary<int, VentaHistorialDTO>();

                        while (readerCF.Read())
                        {
                            int ventaId = readerCF.GetInt32(0);
                            DateTime fecha = readerCF.GetDateTime(1);
                            string producto = readerCF.GetString(2);
                            int cantidad = readerCF.GetInt32(3);
                            decimal subtotal = readerCF.GetDecimal(4);
                            decimal total = readerCF.GetDecimal(5);

                            if (!tempCF.ContainsKey(ventaId))
                            {
                                tempCF[ventaId] = new VentaHistorialDTO
                                {
                                    IdVenta = ventaId,
                                    Fecha = fecha,
                                    Cliente = "Consumidor Final",
                                    Producto = $"{producto} x{cantidad} (${subtotal})",
                                    Cantidad = cantidad,
                                    Total = total
                                };
                            }
                            else
                            {
                                tempCF[ventaId].Producto += $", {producto} x{cantidad} (${subtotal})";
                                tempCF[ventaId].Cantidad += cantidad;
                            }
                        }
                        readerCF.Close();
                        historial.AddRange(tempCF.Values);
                    }
                    #endregion
                }

                historial = historial.OrderByDescending(x => x.Fecha).ToList();
                dgVentas.ItemsSource = historial;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al filtrar ventas: " + ex.Message);
            }
        }

    
        private void BtnNuevaVentaCliente_Click(object sender, RoutedEventArgs e)
        {
            VentaClienteForm ventacliente = new VentaClienteForm();
            ventacliente.ShowDialog();
        }

        private void BtnNuevaVentaParticular_Click(object sender, RoutedEventArgs e)
        {
            VentaParticularForm ventaparticular = new VentaParticularForm();
            ventaparticular.ShowDialog();
        }



    }
}