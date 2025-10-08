using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using static WpfApp1.VentaClienteForm;

namespace WpfApp1
{
    public partial class VentaParticularForm : Window
    {
        private string connectionString = "Data Source=localhost;Initial Catalog=Cristo;Integrated Security=True";
        private decimal precioActualBolsa = 0m;

        public class DetalleVentaItem
        {
            public int productoID { get; set; }
            public string Nombre { get; set; }
            public int Cantidad { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal Subtotal { get; set; }
        }

        private List<DetalleVentaItem> detalleVenta = new List<DetalleVentaItem>();

        public VentaParticularForm()
        {
            InitializeComponent();
            CargarProductos();            
            txtFecha.Text = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void CargarProductos()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT productoID, Nombre FROM Producto", conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    cbProducto.Items.Add(new
                    {
                        productoID = reader["productoID"],
                        Nombre = reader["Nombre"].ToString()
                    });
                }

                cbProducto.DisplayMemberPath = "Nombre";
                cbProducto.SelectedValuePath = "productoID";
            }
        }


        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validar producto seleccionado
            if (cbProducto.SelectedItem == null)
            {
                MessageBox.Show("Por favor, seleccione un producto.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Validar cantidad
            if (string.IsNullOrWhiteSpace(txtCantidad.Text) || !int.TryParse(txtCantidad.Text, out int cantidad) || cantidad <= 0)
            {
                MessageBox.Show("Ingrese una cantidad válida (mayor a 0).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Validar precio
            if (string.IsNullOrWhiteSpace(txtPrecio.Text) || !decimal.TryParse(txtPrecio.Text, out decimal precioUnitario) || precioUnitario <= 0)
            {
                MessageBox.Show("Ingrese un precio válido (mayor a 0).", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int productoID = (int)((dynamic)cbProducto.SelectedItem).productoID;
            string nombreProducto = ((dynamic)cbProducto.SelectedItem).Nombre;          
            decimal subtotal = cantidad * precioUnitario;

            detalleVenta.Add(new DetalleVentaItem
            {
                productoID = productoID,
                Nombre = nombreProducto,
                Cantidad = cantidad,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotal
            });


            dgDetalle.ItemsSource = null;
            dgDetalle.ItemsSource = detalleVenta;

            txtTotal.Text = detalleVenta.Sum(i => i.Subtotal).ToString("C");

            txtCantidad.Text = string.Empty;
            txtPrecio.Text = string.Empty;

        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            var seleccionado = dgDetalle.SelectedItem as DetalleVentaItem;
            if (seleccionado != null)
            {
                detalleVenta.Remove(seleccionado);
                dgDetalle.ItemsSource = null;
                dgDetalle.ItemsSource = detalleVenta;
                txtTotal.Text = detalleVenta.Sum(i => i.Subtotal).ToString("C");
            }
            else
            {
                MessageBox.Show("Selecciona una fila para eliminar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        

        private void BtnRegistrarVenta_Click(object sender, RoutedEventArgs e)
        {
            if (detalleVenta.Count == 0)
            {
                MessageBox.Show("Agregue al menos un producto.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insertar venta
                    string insertVenta = @"INSERT INTO VentaCF (fecha, TotalVentacf)
                                   VALUES (@fecha, @total);
                                   SELECT SCOPE_IDENTITY();";
                    SqlCommand cmdVenta = new SqlCommand(insertVenta, conn, transaction);
                    cmdVenta.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmdVenta.Parameters.AddWithValue("@total", detalleVenta.Sum(i => i.Subtotal));
                    int ventacfID = Convert.ToInt32(cmdVenta.ExecuteScalar());

                    // Insertar detalle
                    foreach (var item in detalleVenta)
                    {
                        string insertDetalle = @"INSERT INTO detalleVentaCF (ventacfID, productoID, Cantidad, PrecioUnitario, Subtotal)
                                         VALUES (@ventacfID, @productoID, @cantidad, @precio, @subtotal)";
                        SqlCommand cmdDetalle = new SqlCommand(insertDetalle, conn, transaction);
                        cmdDetalle.Parameters.AddWithValue("@ventacfID", ventacfID);
                        cmdDetalle.Parameters.AddWithValue("@productoID", item.productoID);
                        cmdDetalle.Parameters.AddWithValue("@cantidad", item.Cantidad);
                        cmdDetalle.Parameters.AddWithValue("@precio", item.PrecioUnitario);
                        cmdDetalle.Parameters.AddWithValue("@subtotal", item.Subtotal);
                        cmdDetalle.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    MessageBox.Show("Venta registrada correctamente.");
                    this.Close();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Error al registrar venta: " + ex.Message);
                    return;
                }

                // === REGISTRO EN CAJA ===

                // Buscar si ya existe caja para la fecha del pago
                string queryCaja = @"SELECT TOP 1 cajaID 
                             FROM Caja 
                             WHERE CAST(fecha AS DATE) = @fechaCaja";
                SqlCommand cmdCaja = new SqlCommand(queryCaja, conn);
                cmdCaja.Parameters.AddWithValue("@fechaCaja", DateTime.Parse(txtFecha.Text).Date); // CAMBIO: usar DateTime

                object cajaIdObj = cmdCaja.ExecuteScalar();

                // Si no existe caja, crear una nueva con saldo inicial
                if (cajaIdObj == null)
                {
                    SqlCommand cmdSaldoAnterior = new SqlCommand(
                        @"SELECT TOP 1 saldoFinal 
                  FROM Caja 
                  WHERE fecha < @fechaCaja
                  ORDER BY fecha DESC", conn);
                    cmdSaldoAnterior.Parameters.AddWithValue("@fechaCaja", DateTime.Parse(txtFecha.Text).Date);
                    object saldoAnteriorObj = cmdSaldoAnterior.ExecuteScalar();
                    decimal saldoAnterior = saldoAnteriorObj != DBNull.Value ? Convert.ToDecimal(saldoAnteriorObj) : 0;

                    SqlCommand cmdInsertCaja = new SqlCommand(
                        @"INSERT INTO Caja (fecha, saldoInicial) 
                  VALUES (@fechaCaja, @saldoInicial);
                  SELECT SCOPE_IDENTITY();", conn);
                    cmdInsertCaja.Parameters.AddWithValue("@fechaCaja", DateTime.Parse(txtFecha.Text).Date);
                    cmdInsertCaja.Parameters.AddWithValue("@saldoInicial", saldoAnterior);

                    cajaIdObj = cmdInsertCaja.ExecuteScalar();
                }

                int cajaID = Convert.ToInt32(cajaIdObj);

                // CAMBIO: Generar descripción como "Producto1 x Cantidad1, Producto2 x Cantidad2"
                string descripcionMovimiento = string.Join(", ",
                    detalleVenta.Select(i => $"{i.Nombre} x {i.Cantidad}"));

                // Insertar movimiento en caja
                string insertMovimientoCaja = @"INSERT INTO MovimientoCaja 
                                (cajaID, fecha, tipoMovimiento, descripcion, monto, origen)
                                VALUES (@cajaID, @fecha, @tipoMovimiento, @descripcion, @monto, @origen)";
                SqlCommand cmdMovimiento = new SqlCommand(insertMovimientoCaja, conn);
                cmdMovimiento.Parameters.AddWithValue("@cajaID", cajaID);
                cmdMovimiento.Parameters.AddWithValue("@fecha", DateTime.Now);
                cmdMovimiento.Parameters.AddWithValue("@tipoMovimiento", "Ingreso");
                cmdMovimiento.Parameters.AddWithValue("@descripcion", descripcionMovimiento); // CAMBIO
                cmdMovimiento.Parameters.AddWithValue("@monto", detalleVenta.Sum(i => i.Subtotal));
                cmdMovimiento.Parameters.AddWithValue("@origen", "Venta particular");
                cmdMovimiento.ExecuteNonQuery();

                // Actualizar saldoFinal de la caja (sumar el ingreso)
                string updateSaldoFinalCaja = @"UPDATE Caja 
        SET saldoFinal = ISNULL(saldoFinal, 0) + @monto
        WHERE cajaID = @cajaID";
                SqlCommand cmdUpdateSaldoFinal = new SqlCommand(updateSaldoFinalCaja, conn);
                cmdUpdateSaldoFinal.Parameters.AddWithValue("@monto", detalleVenta.Sum(i => i.Subtotal));
                cmdUpdateSaldoFinal.Parameters.AddWithValue("@cajaID", cajaID);
                cmdUpdateSaldoFinal.ExecuteNonQuery();
            }
        }

        private void LimpiarFormulario()
        {
            cbProducto.SelectedIndex = -1;
            txtCantidad.Text = "";
            detalleVenta.Clear();
            dgDetalle.ItemsSource = null;
            dgDetalle.ItemsSource = detalleVenta;
            txtTotal.Text = "0";
        }
    }

    }


    public class Producto
    {
        public int ProductoID { get; set; }
        public string Nombre { get; set; }
        public decimal PrecioVenta { get; set; }

        public override string ToString() => Nombre;
    }

   


