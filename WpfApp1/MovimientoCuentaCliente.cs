using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class MovimientoCuentaCliente
    {
        public int movimientoID { get; set; }
        public DateTime Fecha { get; set; }
        public string Detalle { get; set; }
        public decimal? Debe { get; set; }
        public decimal? Haber { get; set; }
        public decimal? Saldo { get; set; }
    }
}
