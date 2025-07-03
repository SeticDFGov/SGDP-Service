using System;

namespace api.Despacho
{
    public class DespachoCreateDTO
    {
        public int ProjetoId { get; set; }
        public string NomeOrgao { get; set; }
        public DateTime DataEntrada { get; set; }
        public DateTime DataSaida { get; set; }
    }
} 