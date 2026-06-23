using Godot;
using System;

public partial class PlayerNetworkInfo
{
    public string PlayerName;
    public float xPos;
    public float yPos;
    public float zPos;
    public float yRot;


    public PlayerNetworkInfo(string playerName)
    {
        PlayerName = playerName;
    }

}
