using Godot;
using System;

public partial class Player : CharacterBody3D
{
    const float MAX_PITCH = 2.0f * Mathf.Pi / 5.0f;

    [Export] public Vector2 MouseSensitivity = new Vector2(0.01f, 0.005f);
    [Export] public float WalkSpeed = 10.0f;
    [Export] public float StrafeSpeed = 5.0f;

    private Camera3D _mainCamera;

    public override void _Ready()
    {
        base._Ready();
        _mainCamera = GetNode<Camera3D>("%MainCamera");
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is InputEventMouseMotion motionEvent) _HandleMouseMotion(-motionEvent.ScreenRelative);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        // Handle horizontal movement
        Vector2 movementInput = new Vector2(Input.GetAxis("move_left", "move_right"), Input.GetAxis("move_forward", "move_back"));
        Vector2 horizontalVelocity = new Vector2(movementInput.X * StrafeSpeed, movementInput.Y * WalkSpeed);
        horizontalVelocity = horizontalVelocity.Rotated(-Rotation.Y);
        if (movementInput != Vector2.Zero)
        {
            Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Y);
        }
        else
        {
            Velocity = Vector3.Zero;
        }
        MoveAndSlide();
    }

    private void _HandleMouseMotion(Vector2 delta)
    {
        Vector2 scaledMotion = MouseSensitivity * delta;
        
        // Camera yaw
        RotateY(scaledMotion.X);

        // Camera pitch
        _mainCamera.RotateX(scaledMotion.Y);
        float currentPitch = Mathf.Clamp(_mainCamera.Rotation.X, -MAX_PITCH, MAX_PITCH);
        _mainCamera.Rotation = new Vector3(currentPitch, 0.0f, 0.0f);
    }
}
