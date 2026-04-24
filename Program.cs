using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PPTservidor.Application.Services;
using PPTservidor.Application.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 1. Adiciona o SignalR
builder.Services.AddSignalR();

// 2. Registra o MatchService como Singleton (Estado em memória compartilhado)
builder.Services.AddSingleton<MatchService>();

// 3. Configura o CORS para desenvolvimento local
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGodotClient", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(origin => true) // Cuidado: Apenas para Dev/MVP local!
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AllowGodotClient");

// 4. Mapeia a rota do Hub
app.MapHub<MatchHub>("/matchhub");

app.MapGet("/", () => "Servidor PPT Online rodando!");

app.Run();

