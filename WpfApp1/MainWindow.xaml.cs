using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnControlGeneral_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new WpfApp1.ControlGeneral();
        }
        private void BtnPrecios_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new WpfApp1.Precios(); // Cambiá "TuApp" si usás otro namespace
        }

        private void BtnClientes_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new WpfApp1.Clientes();
        }

        private void BtnProveedores_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new WpfApp1.Cañeros();

        }

        private void BtnVentas_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new WpfApp1.Ventas();
        }

        private void BtnCompras_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new WpfApp1.Compras();
        }

        private void BtnCaja_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new WpfApp1.Caja();
        }
    }

}