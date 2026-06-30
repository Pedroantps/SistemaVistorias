using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaVistorias.Models
{
    public class Sessao
    {
        [Key]
        public string Token { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime DataExpiracao { get; set; }
    }
}
