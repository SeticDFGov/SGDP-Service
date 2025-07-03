using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models
{
    public class Despacho
    {
        [Key]
        public Guid DespachoId { get; set; } = Guid.NewGuid();

        [Required]
        public Projeto Projeto { get; set; }

        [Required]
        [StringLength(200)]
        public string NomeOrgao { get; set; }

        [Required]
        public DateTime DataEntrada { get; set; }

        [Required]
        public DateTime? DataSaida { get; set; }
    }
} 