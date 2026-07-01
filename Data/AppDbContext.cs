using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Models;

namespace SistemaVistorias.Data
{
    /// <summary>
    /// Contexto do Entity Framework responsável pelo mapeamento objeto-relacional (ORM)
    /// com o banco de dados MariaDB da aplicação.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Ativo> Ativos { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Sessao> Sessoes { get; set; }
    }
}