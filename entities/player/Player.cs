using Godot;
using System;

public partial class Player : CharacterBody3D
{
    const float MAX_PITCH = 2.0f * Mathf.Pi / 5.0f;

    [Export] public Vector2 MouseSensitivity = new Vector2(0.01f, 0.005f);
    [Export] public float WalkSpeed = 40.0f;
    [Export] public float StrafeSpeed = 20.0f;

    private Camera3D _mainCamera;
    private Vector3 _gravityVelocity = Vector3.Zero;
    private float _gravity;

    public override void _Ready()
    {
        base._Ready();
        _mainCamera = GetNode<Camera3D>("%MainCamera");
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is InputEventMouseMotion motionEvent) _HandleMouseMotion(-motionEvent.ScreenRelative);
        _gravity = (float) ProjectSettings.GetSetting("physics/3d/default_gravity");
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
            Velocity = new Vector3(0, Velocity.Y, 0);
        }
        Velocity += _Gravity(delta);
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

    private Vector3 _Gravity(double delta)
    {
        if (IsOnFloor())
        {
            return Vector3.Zero;
        }
        return _gravityVelocity.MoveToward(new Vector3(0, Velocity.Y - _gravity, 0), _gravity * (float) delta);
    }
}
