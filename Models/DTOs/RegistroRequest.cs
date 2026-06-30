namespace SistemaVistorias.Models.DTOs
{
    public class RegistroRequest
    {
        public string NomeUsuario { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string NomeCompleto { get; set; } = string.Empty;
    }
}
