using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows.Controls;
using System.Windows;
using System.Linq;
using System.Collections.ObjectModel;
using System.Net;

namespace WpfApp1
{
    public partial class Cañeros : UserControl
    {
        private string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";
        private ObservableCollection<Proveedor> listaProveedores = new ObservableCollection<Proveedor>();

        public Cañeros()
        {
            InitializeComponent();
            CargarProveedores();
            dgProveedores.SelectionChanged += DgProveedores_SelectionChanged;
            txtBuscar.TextChanged += txtBuscar_TextChanged;
        }

        private void CargarProveedores()
        {
            listaProveedores.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT proveedorID, Apellido, Nombre, DNI, Direccion, Email, Telefono, TelefonoContador FROM Proveedor";
                SqlCommand cmd = new SqlCommand(query, conn);
                conn.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    listaProveedores.Add(new Proveedor
                    {
                        proveedorID = Convert.ToInt32(reader["proveedorID"]),
                        Apellido = reader["Apellido"].ToString(),
                        Nombre = reader["Nombre"].ToString(),
                        DNI = reader["DNI"].ToString(),
                        Direccion = reader["Direccion"].ToString(),
                        Email = reader["Email"].ToString(),
                        Telefono = reader["Telefono"].ToString(),
                        TelefonoContador = reader["TelefonoContador"].ToString()
                    });
                }
            }

            dgProveedores.ItemsSource = listaProveedores;
        }

        private void DgProveedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgProveedores.SelectedItem is Proveedor proveedorSeleccionado)
            {

                // Rellenar los campos del formulario lateral
                txtApellido.Text = proveedorSeleccionado.Apellido;
                txtNombre.Text = proveedorSeleccionado.Nombre;
                txtDNI.Text = proveedorSeleccionado.DNI;
                txtDireccion.Text = proveedorSeleccionado.Direccion;
                txtTelefono.Text = proveedorSeleccionado.Telefono;
                txtEmail.Text = proveedorSeleccionado.Email;
                txtTelefonoContador.Text = proveedorSeleccionado.TelefonoContador;

                // Obtener saldo y precio
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Obtener saldo en pesos del cliente
                    string querySaldo = "SELECT SaldoBolsas FROM CuentaCorrienteProveedor WHERE proveedorID = @proveedorID";
                    SqlCommand cmdSaldo = new SqlCommand(querySaldo, conn);
                    cmdSaldo.Parameters.AddWithValue("@proveedorID", proveedorSeleccionado.proveedorID);
                    object saldoObj = cmdSaldo.ExecuteScalar();
                    decimal saldoBolsas = saldoObj != null ? Convert.ToDecimal(saldoObj) : 0;
                    txtSaldoBolsas.Text = saldoBolsas.ToString();

                }
            }
        }


        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscar.Text.ToLower();
            var proveedoresFiltrados = listaProveedores.Where(p =>
                p.Nombre.ToLower().Contains(filtro) ||
                p.Apellido.ToLower().Contains(filtro) ||
                p.proveedorID.ToString().Contains(filtro)).ToList();

            dgProveedores.ItemsSource = proveedoresFiltrados;
        }

        public void RecargarProveedores()
        {
            CargarProveedores();
        }

        private void BtnAbrirFormulario_Click(object sender, RoutedEventArgs e)
        {
            CañeroForm form = new CañeroForm(this);
            form.ShowDialog();
        }

        private void BtnGuardarCambios_Click(object sender, RoutedEventArgs e)
        {
            if (dgProveedores.SelectedItem is Proveedor proveedor)
            {
                // Tomar los valores del formulario
                string nombre = txtNombre.Text.Trim();
                string apellido = txtApellido.Text.Trim();
                string dni = txtDNI.Text.Trim();
                string direccion = txtDireccion.Text.Trim();
                string telefono = txtTelefono.Text.Trim();
                string email = txtEmail.Text.Trim();
                string telefonoContador = txtTelefonoContador.Text.Trim();

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

                // VALIDAR QUE EL DNI NO EXISTA EN OTRO PROVEEDOR
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string queryDni = "SELECT COUNT(*) FROM Proveedor WHERE DNI = @DNI AND proveedorID <> @proveedorID";
                    SqlCommand cmdDni = new SqlCommand(queryDni, conn);
                    cmdDni.Parameters.AddWithValue("@DNI", dni);
                    cmdDni.Parameters.AddWithValue("@proveedorID", proveedor.proveedorID);
                    int count = (int)cmdDni.ExecuteScalar();

                    if (count > 0)
                    {
                        MessageBox.Show("Ya existe otro proveedor con ese DNI. No se pueden guardar los cambios.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Si pasaron todas las validaciones, actualizar el objeto
               proveedor.Nombre = nombre;
                proveedor.Apellido = apellido;
                proveedor.DNI = dni;
               proveedor.Direccion = direccion;
              proveedor.Telefono = telefono;
                proveedor.Email = email;
                proveedor.TelefonoContador = telefonoContador;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"UPDATE Proveedor
                             SET Nombre = @Nombre,
                                 Apellido = @Apellido,
                                 DNI = @DNI,
                                 Direccion = @Direccion,
                                 Email = @Email,
                                 Telefono = @Telefono,
                                 TelefonoContador = @TelefonoContador
                             WHERE proveedorID = @proveedorID";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Nombre", proveedor.Nombre);
                    cmd.Parameters.AddWithValue("@Apellido", proveedor.Apellido);
                    cmd.Parameters.AddWithValue("@DNI", proveedor.DNI);
                    cmd.Parameters.AddWithValue("@Direccion", proveedor.Direccion);
                    cmd.Parameters.AddWithValue("@Email", proveedor.Email);
                    cmd.Parameters.AddWithValue("@Telefono", proveedor.Telefono);
                    cmd.Parameters.AddWithValue("@TelefonoContador", proveedor.TelefonoContador);
                    cmd.Parameters.AddWithValue("@proveedorID", proveedor.proveedorID);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Los cambios se guardaron correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                CargarProveedores(); // Refresca la lista completa
            }
            else
            {
                MessageBox.Show("Por favor, seleccione un cañero para modificar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnVerCuenta_Click(object sender, RoutedEventArgs e)
        {
            if (dgProveedores.SelectedItem is Proveedor proveedorSeleccionado)
            {
                string nombreCompleto = $"{proveedorSeleccionado.Nombre} {proveedorSeleccionado.Apellido}";
                CuentaCañeroWindow ventanaCuenta = new CuentaCañeroWindow(proveedorSeleccionado.proveedorID, nombreCompleto);
                ventanaCuenta.ShowDialog();
                ActualizarSaldoProveedorSeleccionado();
            }
            else
            {
                MessageBox.Show("Por favor, seleccione un cañero para ver su cuenta.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ActualizarSaldoProveedorSeleccionado()
        {
            if (dgProveedores.SelectedItem is Proveedor proveedorSeleccionado)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string querySaldo = "SELECT SaldoBolsas FROM CuentaCorrienteProveedor WHERE proveedorID = @proveedorID";
                    SqlCommand cmdSaldo = new SqlCommand(querySaldo, conn);
                    cmdSaldo.Parameters.AddWithValue("@proveedorID", proveedorSeleccionado.proveedorID);
                    object saldoObj = cmdSaldo.ExecuteScalar();
                    decimal saldoBolsas = saldoObj != null ? Convert.ToDecimal(saldoObj) : 0;
                    txtSaldoBolsas.Text = saldoBolsas.ToString();

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
                else if (sender == txtTelefono)
                    lblErrorTelefono.Visibility = Visibility.Visible;
                else if (sender == txtTelefonoContador)
                    lblErrorTelefonoContador.Visibility = Visibility.Visible;

                e.Handled = true;
            }
            else
            {
                if (sender == txtDNI)
                    lblErrorDNI.Visibility = Visibility.Collapsed;
                else if (sender == txtTelefono)
                    lblErrorTelefono.Visibility = Visibility.Collapsed;
                else if (sender == txtTelefonoContador)
                    lblErrorTelefonoContador.Visibility = Visibility.Collapsed;
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

        private void txtTelefonoContador_TextChanged(object sender, TextChangedEventArgs e)
        {
            lblErrorTelefonoContador.Visibility = Visibility.Collapsed;
        }




    }

    public class Proveedor
    {
        public int proveedorID { get; set; }
        public string Apellido { get; set; }
        public string Nombre { get; set; }
        public string DNI { get; set; }
        public string Direccion { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; } // nombre exacto de la columna
        public string TelefonoContador { get; set; }
        public DateTime FechaAlta { get; set; }
    }
}
