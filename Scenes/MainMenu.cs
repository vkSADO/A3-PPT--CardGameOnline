using Godot;
using System;

public partial class MainMenu : Control
{
	public Button PlayButton;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		PlayButton = GetNode<Button>("CenterContainer/Button");
		PlayButton.Pressed += OnButtonPressed;

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void OnButtonPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/lobby.tscn");
		GD.Print("Estou no lobby!");
	}
}
