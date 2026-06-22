using Godot;
using System;
using System.ComponentModel;
using System.Text;
using Telepathy;

public partial class ClientMain : Node
{
    Telepathy.Client client = new Telepathy.Client(1024);
    [Export] LineEdit CharNameLineEdit;
    [Export] Button ConnectToServerButton;

    [ExportCategory("Player Stuff")]
    [Export] PackedScene PlayerScene;

    public string RegisteredName { get; private set; }
    public override void _Ready()
    {
        client.OnConnected = () =>
        {
            GD.Print("Client: Client Connected. Sending player details.");
            SendJoinRequest(CharNameLineEdit.Text);
        };
        client.OnData = (message) =>
        {
            ProcessIncomingData(message);
        };
        client.OnDisconnected = () => GD.Print("Client: Client disconnected");

        ConnectToServerButton.ButtonDown += _ConnectToServer;
    }

    public override void _Process(double delta)
    {
        client.Tick(100);

    }

    private void _ConnectToServer()
    {
        client.Connect("localhost", 1337);
    }

    private void _Disconnect()
    {
        client.Disconnect();
    }
    private void SendJoinRequest(string playerName)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(playerName);
        byte[] payload = new byte[1 + nameBytes.Length];

        payload[0] = 0x01; //message ID for join request
        Buffer.BlockCopy(nameBytes, 0, payload, 1, nameBytes.Length);

        client.Send(new ArraySegment<byte>(payload));
    }

    private void ProcessIncomingData(ArraySegment<byte> message)
    {
        if (message.Count == 0) return;

        byte messageId = message.Array[message.Offset];
        //retrieve name acknolwedgement from Server
        if (messageId == 0x02)
        {
            string serverConfirmedName = Encoding.UTF8.GetString(message.Array, message.Offset + 1, message.Count - 1);
            RegisteredName = serverConfirmedName;
            GD.Print($"Client: Successfully retrieved playname from server {RegisteredName}");
        }
        if (messageId == 0x03)
        {
            int networkId = BitConverter.ToInt32(message.Array, message.Offset + 1);
            bool isLocal = message.Array[message.Offset + 5] == 1;
            string playerName = Encoding.UTF8.GetString(message.Array, message.Offset + 6, message.Count - 6);
            SpawnNetworkPlayer(networkId, playerName, isLocal);
        }

    }
    private void SpawnNetworkPlayer(int networkId, string playerName, bool isLocal)
    {
        if (!IsInstanceValid(PlayerScene))
        {
            GD.PrintErr("Client error. Playerscene export is not valid instance.");
            return;
        }

        MmoPlayer newPlayer = PlayerScene.Instantiate<MmoPlayer>();

        newPlayer.Name = $"Player_{networkId}";
        newPlayer.IsLocalPlayer = isLocal;

        AddChild(newPlayer);
        GD.Print($"Client: Spawned player '{playerName}' | Network ID: {networkId} | IsLocal: {isLocal}");
    }
}
