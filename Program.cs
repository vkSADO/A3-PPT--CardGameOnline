using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PPTservidor.Application.Services;
using PPTservidor.Application.Hubs;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// --- CONFIGURAÇÃO DO COSMOS DB (EMULADOR LOCAL) ---
string cosmosUrl = "https://localhost:8081/";
// Esta é a chave padrão mundial do Emulador. Nunca muda!
string cosmosKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

CosmosClient cosmosClient = new CosmosClient(cosmosUrl, cosmosKey);

// Cria o Banco de Dados e a Tabela (Container) automaticamente se for a primeira vez
var database = cosmosClient.CreateDatabaseIfNotExistsAsync("PPTOnlineDB").GetAwaiter().GetResult();
database.Database.CreateContainerIfNotExistsAsync("MatchHistory", "/id").GetAwaiter().GetResult();

// Adiciona o CosmosClient como Singleton para o servidor inteiro usar
builder.Services.AddSingleton(cosmosClient);


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

