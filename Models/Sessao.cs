using System.ComponentModel.DataAnnotations;

namespace SistemaVistorias.Models
{
    /// <summary>
    /// Entidade que armazena os tokens de sessão ativos de um usuário autenticado.
    /// Utilizado para controle de múltiplos acessos e revogação no logout.
    /// </summary>
    public class Sessao
    {
        [Key]
        public string Token { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public DateTime DataExpiracao { get; set; }
    }
}
