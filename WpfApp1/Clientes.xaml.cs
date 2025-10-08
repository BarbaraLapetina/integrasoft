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
                string query = "SELECT clienteID, Apellido, Nombre, DNI, Direccion, Email, Telefo, Estado, Observaciones FROM Cliente";
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
                        Observaciones = reader["Observaciones"].ToString()
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
                txtObservaciones.Text = clienteSeleccionado.Observaciones;

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
                // Tomar los valores del formulario
                string nombre = txtNombre.Text.Trim();
                string apellido = txtApellido.Text.Trim();
                string dni = txtDNI.Text.Trim();
                string direccion = txtDireccion.Text.Trim();
                string telefono = txtTelefo.Text.Trim();
                string email = txtEmail.Text.Trim();
                string estado = (cmbEstado.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Activo";
                string observaciones = txtObservaciones.Text.Trim();

                // VALIDACIONES
                if (string.IsNullOrWhiteSpace(nombre) || !EsSoloLetras(nombre))
                {
                    MessageBox.Show("El campo Nombre es obligatorio y solo puede contener letras.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(apellido) || !EsSoloLetras(apellido))
                {
                    MessageBox.Show("El campo Apellido es obligatorio y solo puede contener letras.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(dni) || !EsSoloNumeros(dni))
                {
                    MessageBox.Show("El campo DNI es obligatorio y solo puede contener números.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(telefono) || !EsSoloNumeros(telefono))
                {
                    MessageBox.Show("El campo Teléfono es obligatorio y solo puede contener números.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // VALIDAR QUE EL DNI NO EXISTA EN OTRO CLIENTE
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string queryDni = "SELECT COUNT(*) FROM Cliente WHERE DNI = @DNI AND clienteID <> @clienteID";
                    SqlCommand cmdDni = new SqlCommand(queryDni, conn);
                    cmdDni.Parameters.AddWithValue("@DNI", dni);
                    cmdDni.Parameters.AddWithValue("@clienteID", cliente.clienteID);
                    int count = (int)cmdDni.ExecuteScalar();

                    if (count > 0)
                    {
                        MessageBox.Show("Ya existe otro cliente con ese DNI. No se pueden guardar los cambios.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Si pasaron todas las validaciones, actualizar el objeto
                cliente.Nombre = nombre;
                cliente.Apellido = apellido;
                cliente.DNI = dni;
                cliente.Direccion = direccion;
                cliente.Telefo = telefono;
                cliente.Email = email;
                cliente.Estado = estado;
                cliente.Observaciones = observaciones;

                // ACTUALIZAR BASE DE DATOS
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"UPDATE Cliente 
                             SET Nombre = @Nombre,
                                 Apellido = @Apellido,
                                 DNI = @DNI,
                                 Direccion = @Direccion,
                                 Email = @Email,
                                 Telefo = @Telefo,
                                 Estado = @Estado,
                                 Observaciones = @Observaciones
                             WHERE clienteID = @clienteID";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Nombre", cliente.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", cliente.Apellido);
                    cmd.Parameters.AddWithValue("@DNI", cliente.DNI);
                    cmd.Parameters.AddWithValue("@Direccion", cliente.Direccion);
                    cmd.Parameters.AddWithValue("@Email", cliente.Email);
                    cmd.Parameters.AddWithValue("@Telefo", cliente.Telefo);
                    cmd.Parameters.AddWithValue("@Estado", cliente.Estado);
                    cmd.Parameters.AddWithValue("@Observaciones", cliente.Observaciones);
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
                if (sender == txtDNI)
                    lblErrorDNI.Visibility = Visibility.Visible;
                else if (sender == txtTelefo)
                    lblErrorTelefono.Visibility = Visibility.Visible;

                e.Handled = true;
            }
            else
            {
                if (sender == txtDNI)
                    lblErrorDNI.Visibility = Visibility.Collapsed;
                else if (sender == txtTelefo)
                    lblErrorTelefono.Visibility = Visibility.Collapsed;
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

        private void txtTelefono_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblErrorTelefono.Visibility = Visibility.Collapsed;
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
        public string Observaciones { get; set; }
    }
}
