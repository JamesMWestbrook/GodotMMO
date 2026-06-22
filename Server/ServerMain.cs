using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class ServerMain : Node
{
    Telepathy.Server server = new Telepathy.Server(1024);

    //private Dictionary<int, string> connectedPlayers = new Dictionary<int, string>();
    private Dictionary<int, string> connectedPlayers = new Dictionary<int, string>();

    public override void _Ready()
    {
        server.Start(1337);
        server.OnConnected = (id, ipAddress) => ConnectPlayer(id, ipAddress);

        server.OnData = (connectionId, message) =>
        {
            ProcessIncomingData(connectionId, message);
        };

    }
    public override void _Process(double delta)
    {
        base._Process(delta);
        server.Tick(100);
        byte[] message = new byte[] { 0x42, 0x13, 0x37 };
        //server.Send(1, new ArraySegment<byte>(message));

    }
    public void ConnectPlayer(int id, string ipAddress)
    {
        GD.Print($"Server: Server has connected to client with ID {id} with message {ipAddress}");
    }
    private void ProcessIncomingData(int connectionId, ArraySegment<byte> message)
    {
        if (message.Count == 0) return;

        byte messageId = message.Array[message.Offset];

        if (messageId == 0x01)
        {
            string playerName = Encoding.UTF8.GetString(message.Array, message.Offset + 1, message.Count - 1);

            //before adding to database, send current players
            foreach (var existingPlayer in connectedPlayers)
            {
                SendSpawnCommand(connectionId, existingPlayer.Key, existingPlayer.Value, isLocalForTarget: false);
            }

            //record player name
            if (!connectedPlayers.ContainsKey(connectionId))
            {
                connectedPlayers.Add(connectionId, playerName);
                GD.Print($"Server added player {playerName} to Server database");

                SendStoredNameToClient(connectionId, playerName);
            }



            //broadcast new player to everyone
            foreach (var peerId in connectedPlayers.Keys)
            {
                //if peerId matches connectionId, it's their own player
                bool isLocalForTarget = (peerId == connectionId);
                SendSpawnCommand(peerId, connectionId, playerName, isLocalForTarget);
            }

        }
    }
    public void _Stop()
    {
        server.Stop();
    }

    private void SendStoredNameToClient(int connectionId, string playerName)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(playerName);
        byte[] payload = new byte[1 + nameBytes.Length];

        payload[0] = 0x02; //Message ID
        Buffer.BlockCopy(nameBytes, 0, payload, 1, nameBytes.Length);

        server.Send(connectionId, new ArraySegment<byte>(payload));
        GD.Print($"Server: Sent confirmation back to client {connectionId} to retrieve name");
    }
    private void SendSpawnCommand(int peerId, int connectionId, string playerName, bool isLocalForTarget)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(playerName);
        // Payload size: 1 byte (ID) + 4 bytes (Player ID Int) + 1 byte (IsLocal Bool) + string bytes
        byte[] payload = new byte[1 + 4 + 1 + nameBytes.Length];

        payload[0] = 0x03; //Msg ID: spawn player

        Buffer.BlockCopy(BitConverter.GetBytes(connectionId), 0, payload, 1, 4);

        payload[5] = (byte)(isLocalForTarget ? 1 : 0);

        Buffer.BlockCopy(nameBytes, 0, payload, 6, nameBytes.Length);

        server.Send(peerId, new ArraySegment<byte>(payload));
    }
}
