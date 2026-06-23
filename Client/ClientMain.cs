using Godot;
using System;
using System.ComponentModel;
using System.Text;
using Telepathy;

public partial class ClientMain : Node
{
    static public ClientMain Main;

    Telepathy.Client client = new Telepathy.Client(1024);
    [Export] LineEdit CharNameLineEdit;
    [Export] Button ConnectToServerButton;

    [ExportCategory("Player Stuff")]
    [Export] PackedScene PlayerScene;

    public string RegisteredName { get; private set; }
    public override void _Ready()
    {
        Main = this;
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
        //retrieve name + spawn player
        if (messageId == 0x03)
        {
            int networkId = BitConverter.ToInt32(message.Array, message.Offset + 1);
            bool isLocal = message.Array[message.Offset + 5] == 1;
            string playerName = Encoding.UTF8.GetString(message.Array, message.Offset + 6, message.Count - 6);
            SpawnNetworkPlayer(networkId, playerName, isLocal);
        }
        //position/rotation updates for other players
        if (messageId == 0x12)
        {
            int networkId = BitConverter.ToInt32(message.Array, message.Offset + 1);
            float x = BitConverter.ToSingle(message.Array, message.Offset + 5);
            float y = BitConverter.ToSingle(message.Array, message.Offset + 9);
            float z = BitConverter.ToSingle(message.Array, message.Offset + 13);
            float rotY = BitConverter.ToSingle(message.Array, message.Offset + 17);

            // Look up the node in the scene tree using the structural naming convention from SpawnNetworkPlayer
            // e.g., newPlayer.Name = $"Player_{networkId}";
            MmoPlayer puppet = GetNodeOrNull<MmoPlayer>($"Player_{networkId}");

            GD.Print($"Client Received: Rotation {rotY}");
            if (IsInstanceValid(puppet) && !puppet.IsLocalPlayer)
            {
                GD.Print("puppet is valid");
                // Update position
                puppet.GlobalPosition = new Godot.Vector3(x, y, z);

                // Update model rotation (matching your setup in MmoPlayer.cs)
                Node3D model = puppet.GetNodeOrNull<Node3D>("Model");
                if (IsInstanceValid(model))
                {
                    model.Rotation = new Godot.Vector3(model.Rotation.X, rotY, model.Rotation.Z);
                }
            }
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
    public void SendMovementUpdate(Godot.Vector3 position, float rotationY)
    {//1 byte for messageid, 4 bytes per 3 floats floats for x y z, 4 bytes for 1 float y rotation  
        byte[] payload = new byte[1 + 12 + 4];

        payload[0] = 0x11;

        Buffer.BlockCopy(BitConverter.GetBytes(position.X), 0, payload, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(position.Y), 0, payload, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(position.Z), 0, payload, 9, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(rotationY), 0, payload, 13, 4);

        client.Send(new ArraySegment<byte>(payload));
    }

}
