using Godot;
using System;
using System.Net;
using System.Threading.Tasks;

namespace PPTservidor.Infraestrutura.OAuth;
public partial class GoogleOAuth : Node
{
    
    private const string ClientId = "394654207020-86nncgeqa9jp4r5l8i3gh5prg49b8r0g.apps.googleusercontent.com";
    private const string RedirectUri = "http://127.0.0.1:5200/";
    
    private HttpListener _httpListener;

    public void StartGoogleLogin()
    {
        // Monta o link de login do Google
        string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={ClientId}&redirect_uri={RedirectUri}&response_type=code&scope=email%20profile";
        
        // Abre o navegador padrão do utilizador
        OS.ShellOpen(authUrl);
        
        // Fica à escuta da resposta em segundo plano
        ListenForCodeAsync();
    }

    private async void ListenForCodeAsync()
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add(RedirectUri);
        
        try
        {
            _httpListener.Start();
            GD.Print("A escutar a porta 5200. Aguardando login no navegador...");

            // O jogo pausa aqui de forma assíncrona até o Google redirecionar de volta
            HttpListenerContext context = await _httpListener.GetContextAsync();
            HttpListenerRequest request = context.Request;
            
            // Extrai o "Código de Autorização" da resposta
            string code = request.QueryString["code"];

            // Desenha uma página HTML simples para o jogador fechar o navegador
            HttpListenerResponse response = context.Response;
            string responseString = "<html><body style='font-family: Arial; text-align: center; padding-top: 50px;'><h1>Login efetuado com sucesso!</h1><p>Pode fechar este separador e voltar ao jogo.</p></body></html>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();

            _httpListener.Stop();

            if (!string.IsNullOrEmpty(code))
            {
                GD.Print("Código de autorização capturado com sucesso!");
                GD.Print(code);
                // O PRÓXIMO PASSO SERÁ ENVIAR ESTE CÓDIGO PARA O SERVIDOR C#!
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr("Erro no servidor local de login: " + ex.Message);
        }
    }
}