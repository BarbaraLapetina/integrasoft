using System;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class CañeroForm : Window
    {
        private string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";
        private Cañeros parentControl;

        public CañeroForm(Cañeros parent)
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
            DateTime fechaAlta = DateTime.Now;

            string connectionString = "Server=localhost;Database=Cristo;Trusted_Connection=True;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insertar cliente sin especificar el proveedorID
                    string insertProveedor = @"
                INSERT INTO Proveedor (Apellido, Nombre, DNI, Direccion, Telefono, Email, FechaAlta)
                VALUES (@Apellido, @Nombre, @DNI, @Direccion, @Telefono, @Email, @FechaAlta);
                SELECT SCOPE_IDENTITY();";

                    SqlCommand cmdProveedor = new SqlCommand(insertProveedor, conn, transaction);
                    cmdProveedor.Parameters.AddWithValue("@Apellido", apellido);
                    cmdProveedor.Parameters.AddWithValue("@Nombre", nombre);
                    cmdProveedor.Parameters.AddWithValue("@DNI", dni);
                    cmdProveedor.Parameters.AddWithValue("@Direccion", direccion);
                    cmdProveedor.Parameters.AddWithValue("@Telefono", telefono);
                    cmdProveedor.Parameters.AddWithValue("@Email", email);
                    cmdProveedor.Parameters.AddWithValue("@FechaAlta", fechaAlta);

                    // Obtener ID recién generado
                    int nuevoProveedorID = Convert.ToInt32(cmdProveedor.ExecuteScalar());

                    // Insertar cuenta corriente asociada
                    string insertCuenta = @"
                INSERT INTO CuentaCorrienteProveedor (proveedorID, SaldoPesos, SaldoBolsas)
                VALUES (@proveedorID, 0, 0)";

                    SqlCommand cmdCuenta = new SqlCommand(insertCuenta, conn, transaction);
                    cmdCuenta.Parameters.AddWithValue("@proveedorID", nuevoProveedorID);
                    cmdCuenta.ExecuteNonQuery();

                    // Confirmar transacción
                    transaction.Commit();

                    MessageBox.Show("Cañero y cuenta corriente creados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    parentControl.RecargarProveedores();
                    this.Close(); // Cierra la ventana del formulario
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Error al agregar cañero: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
