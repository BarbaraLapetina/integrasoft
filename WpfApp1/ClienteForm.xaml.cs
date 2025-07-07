using System;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class ClienteForm : Window
    {
        private string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";
        private Clientes parentControl;

        public ClienteForm(Clientes parent)
        {
            InitializeComponent();
            parentControl = parent;
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            string apellido = txtApellido.Text;
            string nombre = txtNombre.Text;
            string dni = txtDNI.Text;
            string direccion = txtDireccion.Text;
            string telefono = txtTelefono.Text;
            string email = txtEmail.Text;
            string estado = (cmbEstado.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Activo";
            DateTime fechaAlta = DateTime.Now;

            string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insertar cliente sin especificar el clienteID
                    string insertCliente = @"
                INSERT INTO Cliente (Apellido, Nombre, DNI, Direccion, Telefo, Email, Estado, FechaAlta)
                VALUES (@Apellido, @Nombre, @DNI, @Direccion, @Telefo, @Email, @Estado, @FechaAlta);
                SELECT SCOPE_IDENTITY();";

                    SqlCommand cmdCliente = new SqlCommand(insertCliente, conn, transaction);
                    cmdCliente.Parameters.AddWithValue("@Apellido", apellido);
                    cmdCliente.Parameters.AddWithValue("@Nombre", nombre);
                    cmdCliente.Parameters.AddWithValue("@DNI", dni);
                    cmdCliente.Parameters.AddWithValue("@Direccion", direccion);
                    cmdCliente.Parameters.AddWithValue("@Telefo", telefono);
                    cmdCliente.Parameters.AddWithValue("@Email", email);
                    cmdCliente.Parameters.AddWithValue("@Estado", estado);
                    cmdCliente.Parameters.AddWithValue("@FechaAlta", fechaAlta);

                    // Obtener ID recién generado
                    int nuevoClienteID = Convert.ToInt32(cmdCliente.ExecuteScalar());

                    // Insertar cuenta corriente asociada
                    string insertCuenta = @"
                INSERT INTO CuentaCorrienteCliente (clienteID, SaldoPesos, SaldoBolsas)
                VALUES (@clienteID, 0, 0)";

                    SqlCommand cmdCuenta = new SqlCommand(insertCuenta, conn, transaction);
                    cmdCuenta.Parameters.AddWithValue("@clienteID", nuevoClienteID);
                    cmdCuenta.ExecuteNonQuery();

                    // Confirmar transacción
                    transaction.Commit();

                    MessageBox.Show("Cliente y cuenta corriente creados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    parentControl.RecargarClientes();
                    this.Close(); // Cierra la ventana del formulario
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Error al agregar cliente: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
