using Godot;
using System;
using Telepathy;

public partial class Main : Node
{
    public override void _Ready()
    {

        if (OS.HasFeature("server"))
        {
            PackedScene serverScene = GD.Load<PackedScene>("res://Server/server_main.tscn");
            AddChild(serverScene.Instantiate());
        }
        else if (OS.HasFeature("client"))
        {
            PackedScene clientScene = GD.Load<PackedScene>("res://Client/client_main.tscn");
            AddChild(clientScene.Instantiate());
        }
    }


}
