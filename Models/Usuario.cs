using System.ComponentModel.DataAnnotations;

namespace SistemaVistorias.Models
{
    /// <summary>
    /// Entidade que representa um usuário do sistema de vistorias.
    /// </summary>
    public class Usuario
    {
        [Key]
        public int Id { get; set; }
        public string NomeUsuario { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public string NomeCompleto { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public bool Ativo { get; set; } = true;
    }
}
