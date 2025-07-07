using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows.Controls;
using System.Windows;
using System.Linq;
using System.Collections.ObjectModel;

namespace WpfApp1
{
    public partial class Clientes : UserControl
    {
        private string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";
        private ObservableCollection<Cliente> listaClientes = new ObservableCollection<Cliente>();

        public Clientes()
        {
            InitializeComponent();
            CargarClientes();
            dgClientes.SelectionChanged += DgClientes_SelectionChanged;
            txtBuscar.TextChanged += txtBuscar_TextChanged;
        }

        private void CargarClientes()
        {
            listaClientes.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT clienteID, Apellido, Nombre, DNI, Direccion, Email, Telefo, Estado, FechaAlta FROM Cliente";
                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    listaClientes.Add(new Cliente
                    {
                        clienteID = Convert.ToInt32(reader["clienteID"]),
                        Apellido = reader["Apellido"].ToString(),
                        Nombre = reader["Nombre"].ToString(),
                        DNI = reader["DNI"].ToString(),
                        Direccion = reader["Direccion"].ToString(),
                        Email = reader["Email"].ToString(),
                        Telefo = reader["Telefo"].ToString(),
                        Estado = reader["Estado"].ToString(),
                        FechaAlta = Convert.ToDateTime(reader["FechaAlta"])
                    });
                }
            }

            dgClientes.ItemsSource = listaClientes;
        }

        private void DgClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgClientes.SelectedItem is Cliente clienteSeleccionado)
            {
                
                // Rellenar los campos del formulario lateral
                txtApellido.Text = clienteSeleccionado.Apellido;
                txtNombre.Text = clienteSeleccionado.Nombre;
                txtDNI.Text = clienteSeleccionado.DNI;
                txtDireccion.Text = clienteSeleccionado.Direccion;
                txtTelefo.Text = clienteSeleccionado.Telefo;
                txtEmail.Text = clienteSeleccionado.Email;
                cmbEstado.SelectedItem = clienteSeleccionado.Estado;

                // Obtener saldo y precio
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Obtener saldo en pesos del cliente
                    string querySaldo = "SELECT SaldoPesos FROM CuentaCorrienteCliente WHERE clienteID = @clienteID";
                    SqlCommand cmdSaldo = new SqlCommand(querySaldo, conn);
                    cmdSaldo.Parameters.AddWithValue("@clienteID", clienteSeleccionado.clienteID);
                    object saldoObj = cmdSaldo.ExecuteScalar();
                    decimal saldoPesos = saldoObj != null ? Convert.ToDecimal(saldoObj) : 0;
                    txtSaldo.Text = $"$ {saldoPesos:N2}";


                    // Obtener el último precio de bolsa desde ParametroPrecio
                    string queryPrecio = "SELECT TOP 1 Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                    SqlCommand cmdPrecio = new SqlCommand(queryPrecio, conn);
                    object precioObj = cmdPrecio.ExecuteScalar();
                    decimal precioBolsa = precioObj != null ? Convert.ToDecimal(precioObj) : 1;

                    // Calcular saldo en bolsas
                    decimal saldoBolsas = (precioBolsa != 0 ? Math.Abs(saldoPesos) / precioBolsa : 0);
                    txtSaldoBolsas.Text = $"{saldoBolsas:N2}";
                }
            }
        }


        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscar.Text.ToLower();
            var clientesFiltrados = listaClientes.Where(c =>
                c.Nombre.ToLower().Contains(filtro) ||
                c.Apellido.ToLower().Contains(filtro) ||
                c.clienteID.ToString().Contains(filtro)).ToList();

            dgClientes.ItemsSource = clientesFiltrados;
        }

        public void RecargarClientes()
        {
            CargarClientes();
        }

        private void BtnAbrirFormulario_Click(object sender, RoutedEventArgs e)
        {
            ClienteForm form = new ClienteForm(this);
            form.ShowDialog();
        }

        private void BtnGuardarCambios_Click(object sender, RoutedEventArgs e)
        {
            if (dgClientes.SelectedItem is Cliente cliente)
            {
                cliente.Nombre = txtNombre.Text;
                cliente.Apellido = txtApellido.Text;
                cliente.DNI = txtDNI.Text;
                cliente.Direccion = txtDireccion.Text;
                cliente.Telefo = txtTelefo.Text;
                cliente.Email = txtEmail.Text;
                if (cmbEstado.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
                {
                    cliente.Estado = selectedItem.Content.ToString();
                }
                else
                {
                    cliente.Estado = "Activo"; // o cualquier estado por defecto válido
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"UPDATE Cliente 
                             SET Nombre = @Nombre,
                                 Apellido = @Apellido,
                                 DNI = @DNI,
                                 Direccion = @Direccion,
                                 Email = @Email,
                                 Telefo = @Telefo,
                                 Estado = @Estado
                             WHERE clienteID = @clienteID";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Nombre", cliente.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", cliente.Apellido);
                    cmd.Parameters.AddWithValue("@DNI", cliente.DNI);
                    cmd.Parameters.AddWithValue("@Direccion", cliente.Direccion);
                    cmd.Parameters.AddWithValue("@Email", cliente.Email);
                    cmd.Parameters.AddWithValue("@Telefo", cliente.Telefo);
                    cmd.Parameters.AddWithValue("@Estado", cliente.Estado);
                    cmd.Parameters.AddWithValue("@clienteID", cliente.clienteID);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Los cambios se guardaron correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                CargarClientes(); // Refresca la lista completa
            }
            else
            {
                MessageBox.Show("Por favor, seleccione un cliente para modificar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnVerCuenta_Click(object sender, RoutedEventArgs e)
        {
            if (dgClientes.SelectedItem is Cliente clienteSeleccionado)
            {
                string nombreCompleto = $"{clienteSeleccionado.Nombre} {clienteSeleccionado.Apellido}";
                CuentaClienteWindow ventanaCuenta = new CuentaClienteWindow(clienteSeleccionado.clienteID, nombreCompleto);
                ventanaCuenta.ShowDialog();
                ActualizarSaldoClienteSeleccionado();
            }
            else
            {
                MessageBox.Show("Por favor, seleccione un cliente para ver su cuenta.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ActualizarSaldoClienteSeleccionado()
        {
            if (dgClientes.SelectedItem is Cliente clienteSeleccionado)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string querySaldo = "SELECT SaldoPesos FROM CuentaCorrienteCliente WHERE clienteID = @clienteID";
                    SqlCommand cmdSaldo = new SqlCommand(querySaldo, conn);
                    cmdSaldo.Parameters.AddWithValue("@clienteID", clienteSeleccionado.clienteID);
                    object saldoObj = cmdSaldo.ExecuteScalar();
                    decimal saldoPesos = saldoObj != null ? Convert.ToDecimal(saldoObj) : 0;
                    txtSaldo.Text = $"$ {saldoPesos:N2}";

                    string queryPrecio = "SELECT TOP 1 Precio FROM ParametroPrecio ORDER BY Fecha DESC";
                    SqlCommand cmdPrecio = new SqlCommand(queryPrecio, conn);
                    object precioObj = cmdPrecio.ExecuteScalar();
                    decimal precioBolsa = precioObj != null ? Convert.ToDecimal(precioObj) : 1;

                    decimal saldoBolsas = (precioBolsa != 0 ? Math.Abs(saldoPesos) / precioBolsa : 0);

                    txtSaldoBolsas.Text = $"{saldoBolsas:N2}";
                }
            }
        }

        private void SoloLetras_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (!EsSoloLetras(e.Text))
            {
                if (sender == txtApellido)
                    lblErrorApellido.Visibility = Visibility.Visible;
                else if (sender == txtNombre)
                    lblErrorNombre.Visibility = Visibility.Visible;

                e.Handled = true;
            }
            else
            {
                if (sender == txtApellido)
                    lblErrorApellido.Visibility = Visibility.Collapsed;
                else if (sender == txtNombre)
                    lblErrorNombre.Visibility = Visibility.Collapsed;
            }
        }

        private void SoloNumeros_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (!EsSoloNumeros(e.Text))
            {
                lblErrorDNI.Visibility = Visibility.Visible;
                e.Handled = true;
            }
            else
            {
                lblErrorDNI.Visibility = Visibility.Collapsed;
            }
        }

        private bool EsSoloLetras(string texto)
        {
            foreach (char c in texto)
            {
                if (!char.IsLetter(c) && !char.IsWhiteSpace(c))
                    return false;
            }
            return true;
        }

        private bool EsSoloNumeros(string texto)
        {
            foreach (char c in texto)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }

        // Ocultar mensajes de error si se corrige el texto
        private void txtApellido_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblErrorApellido.Visibility = Visibility.Collapsed;
        }

        private void txtNombre_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblErrorNombre.Visibility = Visibility.Collapsed;
        }

        private void txtDNI_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblErrorDNI.Visibility = Visibility.Collapsed;
        }


    }

    public class Cliente
    {
        public int clienteID { get; set; }
        public string Apellido { get; set; }
        public string Nombre { get; set; }
        public string DNI { get; set; }
        public string Direccion { get; set; }
        public string Email { get; set; }
        public string Telefo { get; set; } // nombre exacto de la columna
        public string Estado { get; set; }
        public DateTime FechaAlta { get; set; }
    }
}
