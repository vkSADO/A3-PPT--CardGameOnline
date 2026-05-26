using Godot;
using PPTservidor.Infraestrutura.OAuth;
using System;


public partial class LoginMenu : Control
{
    [Export] public Button LoginButton;
    private GoogleOAuth _googleOAuth;

    public override void _Ready()
    {
        // Obtém a referência do GoogleAuth que deve estar na cena
        _googleOAuth = GetNode<GoogleOAuth>("/root/GoogleOAuth");
        LoginButton.Pressed += OnLoginPressed;
    }

    private void OnLoginPressed()
    {
        if (_googleOAuth != null)
        {
            _googleOAuth.StartGoogleLogin();
        }
        else
        {
            GD.PrintErr("GoogleAuth não foi encontrado!");
        }
    }

}
