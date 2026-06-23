using Godot;
using System;
using System.Numerics;
using Telepathy;

public partial class MmoPlayer : CharacterBody3D
{
    [Export] float Speed = 5.0f;
    [Export] float Gravity = -9.8f;
    [Export] float JumpImpulse = 4.5f;
    [Export] float rotation_velocity = 10f;
    [Export] double IdleTimeTemplate = 1;

    private Node3D OrbitalCamera;
    private Node3D Model;

    public bool IsLocalPlayer = false;

    private bool isStill = false;
    private double IdleTimer = 1;

    public override void _Ready()
    {
        OrbitalCamera = GetNode<Node3D>("OrbitalCamera");
        Model = GetNode<Node3D>("Model");

        if (!IsLocalPlayer)
        {
            OrbitalCamera.QueueFree();
        }
    }
    public override void _Process(double delta)
    {


        if (!IsLocalPlayer) return;
        if (!Velocity.IsZeroApprox())
        {
            ClientMain.Main.SendMovementUpdate(Position, Model.Rotation.Y);
        }
        else
        {
            IdleTimer -= delta;
            if (IdleTimer <= 0)
            {
                IdleTimer = IdleTimeTemplate;
                ClientMain.Main.SendMovementUpdate(Position, Model.Rotation.Y);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsLocalPlayer) return;
        if (!IsOnFloor())
        {
            float newY = Velocity.Y + Gravity * (float)delta;
            Velocity = new Godot.Vector3(Velocity.X, newY, Velocity.Z);
        }

        if (Input.IsActionJustPressed("Jump") && IsOnFloor())
        {
            Velocity = new Godot.Vector3(Velocity.X, JumpImpulse, Velocity.Z);
        }

        Godot.Vector2 directionalInput = Input.GetVector("Left", "Right", "Up", "Down");
        if (!directionalInput.IsZeroApprox())
        {
            Godot.Vector3 movementDirection = OrbitalCamera.GlobalTransform.Basis.Z * directionalInput.Y + OrbitalCamera.GlobalTransform.Basis.X * directionalInput.X;
            float oldY = Model.Rotation.Y;
            float newY = Mathf.LerpAngle(oldY, Mathf.Atan2(movementDirection.X, movementDirection.Z), rotation_velocity * (float)delta);
            Model.Rotation = new Godot.Vector3(Model.Rotation.X, newY, Model.Rotation.Z);


            float newX = movementDirection.X * Speed;
            float newZ = movementDirection.Z * Speed;

            Velocity = new Godot.Vector3(newX, Velocity.Y, newZ);
        }
        else
        {
            float newX = Godot.Mathf.MoveToward(Velocity.X, 0.0f, Speed);
            float newZ = Godot.Mathf.MoveToward(Velocity.Z, 0.0f, Speed);
            Velocity = new Godot.Vector3(newX, Velocity.Y, newZ);
            // Velocity = Velocity.MoveToward(Godot.Vector3.Zero, Speed);
        }


        MoveAndSlide();
    }

}
