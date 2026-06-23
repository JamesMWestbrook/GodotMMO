using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class ServerMain : Node
{
    Telepathy.Server server = new Telepathy.Server(1024);

    //private Dictionary<int, string> connectedPlayers = new Dictionary<int, string>();
    // connection ID and player class
    private Dictionary<int, PlayerNetworkInfo> connectedPlayers = new Dictionary<int, PlayerNetworkInfo>();



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
        switch (messageId)
        {
            case 0x01:
                {
                    string playerName = Encoding.UTF8.GetString(message.Array, message.Offset + 1, message.Count - 1);

                    if (!connectedPlayers.ContainsKey(connectionId))
                    {
                        PlayerNetworkInfo newPlayerInfo = new PlayerNetworkInfo(playerName);

                        // 1. Catch up the newcomer: Spawn all PRE-EXISTING players on this new client
                        foreach (var existingPlayer in connectedPlayers)
                        {
                            // Destination: connectionId (newcomer) | Subject: existingPlayer data | Owned: false
                            SendSpawnCommand(connectionId, existingPlayer.Key, existingPlayer.Value, isLocalForTarget: false);
                        }

                        // 2. Register the newcomer into the server tracking roster
                        connectedPlayers.Add(connectionId, newPlayerInfo);
                        GD.Print($"Server added player {playerName} to Server database");

                        SendStoredNameToClient(connectionId, playerName);

                        // 3. Announce the newcomer to EVERYONE (including themselves)
                        foreach (int peerId in connectedPlayers.Keys)
                        {
                            // This will ONLY be true when sending to the newcomer's own screen
                            bool isLocalForTarget = (peerId == connectionId);

                            // Destination: peerId | Subject: newPlayerInfo | Owned: true/false based on line above
                            SendSpawnCommand(peerId, connectionId, newPlayerInfo, isLocalForTarget);
                        }
                    }
                    break;
                }
            case 0x11:
                {
                    if (connectedPlayers.TryGetValue(connectionId, out PlayerNetworkInfo player))
                    {
                        player.xPos = BitConverter.ToSingle(message.Array, message.Offset + 1);
                        player.yPos = BitConverter.ToSingle(message.Array, message.Offset + 5);
                        player.zPos = BitConverter.ToSingle(message.Array, message.Offset + 9);
                        player.yRot = BitConverter.ToSingle(message.Array, message.Offset + 13);
                        BroadcastMovementToAll(connectionId, player);
                    }


                    break;
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
    private void SendSpawnCommand(int peerId, int connectionId, PlayerNetworkInfo player, bool isLocalForTarget)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(player.PlayerName);
        // Payload size: 1 byte (ID) + 4 bytes (Player ID Int) + 1 byte (IsLocal Bool) + string bytes
        byte[] payload = new byte[1 + 4 + 1 + nameBytes.Length];

        payload[0] = 0x03; //Msg ID: spawn player

        Buffer.BlockCopy(BitConverter.GetBytes(connectionId), 0, payload, 1, 4);

        payload[5] = (byte)(isLocalForTarget ? 1 : 0);

        Buffer.BlockCopy(nameBytes, 0, payload, 6, nameBytes.Length);

        server.Send(peerId, new ArraySegment<byte>(payload));
    }

    private void BroadcastMovementToAll(int connectionId, PlayerNetworkInfo player)
    {
        // Payload: 1 byte (MsgID 0x12) + 4 bytes (Int ID) + 12 bytes (Vector3) + 4 bytes (Rot Float) = 21 bytes
        byte[] payload = new byte[1 + 4 + 12 + 4];
        payload[0] = 0x12;

        //pack connectionID
        Buffer.BlockCopy(BitConverter.GetBytes(connectionId), 0, payload, 1, 4);

        Buffer.BlockCopy(BitConverter.GetBytes(player.xPos), 0, payload, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(player.yPos), 0, payload, 9, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(player.zPos), 0, payload, 13, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(player.yRot), 0, payload, 17, 4);

        //send to everyoen but the source player
        foreach (int peerId in connectedPlayers.Keys)
        {
            if (peerId != connectionId)
            {
                server.Send(peerId, new ArraySegment<byte>(payload));
            }
        }

    }
}
