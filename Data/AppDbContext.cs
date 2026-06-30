using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Models;

namespace SistemaVistorias.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        // Mapeia os modelos para as tabelas no MariaDB
        public DbSet<Ativo> Ativos { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Sessao> Sessoes { get; set; }
    }
}