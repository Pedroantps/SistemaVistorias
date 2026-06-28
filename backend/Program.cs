using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 1. CONFIGURAÇÃO DE CORS (Libera o acesso para o navegador)
builder.Services.AddCors(options => {
    options.AddPolicy("PermitirTudo", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

// 2. ATIVAR O CORS (Tem que ficar antes do MapControllers)
app.UseCors("PermitirTudo");

app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.Run();