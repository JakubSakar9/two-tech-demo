using Godot;
using System;

public partial class Player : CharacterBody3D
{
    const float MAX_PITCH = 2.0f * Mathf.Pi / 5.0f;

    [Export] public Vector2 MouseSensitivity = new(0.01f, 0.005f);
    [Export] public float WalkSpeed = 40.0f;
    [Export] public float WalkAcceleration = 35.0f;
    [Export] public float WalkDecceleration = 50.0f;

    public float LeftFootHitDistance = 0.0f;
    public float RightFootHitDistance = 0.0f;
    public Vector2 LeftFootPosition = Vector2.Zero;
    public Vector2 RightFootPosition = Vector2.Zero;

    private AnimationTree _animationTree;
    private Camera3D _mainCamera;
    private BoneAttachment3D _leftFootAttachment;
    private BoneAttachment3D _rightFootAttachment;
    private RayCast3D _heightRaycast;
    private Vector3 _gravityVelocity = Vector3.Zero;
    private float _gravity;
    private float _initialAttachmentHeight = 0.0f;

    public override void _Ready()
    {
        base._Ready();
        NodeInit();

        _initialAttachmentHeight = _leftFootAttachment.Position.Y;
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is InputEventMouseMotion motionEvent) HandleMouseMotion(-motionEvent.ScreenRelative);
        _gravity = (float) ProjectSettings.GetSetting("physics/3d/default_gravity");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        // Handle horizontal movement
        Vector2 movementInput = new(Input.GetAxis("move_left", "move_right"), Input.GetAxis("move_forward", "move_back"));
        Vector2 horizontalVelocity;
        if (movementInput != Vector2.Zero)
        {
            horizontalVelocity = new Vector2(Velocity.X, Velocity.Z) + WalkAcceleration * movementInput.Rotated(-Rotation.Y).Normalized() * (float)delta;
            if (horizontalVelocity.Length() > WalkSpeed)
            {
                horizontalVelocity = horizontalVelocity.Normalized() * WalkSpeed;
            }
            Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Y);
        }
        else
        {
            horizontalVelocity = new Vector2(Velocity.X, Velocity.Z);
            float horizontalDeltaLen = WalkDecceleration * (float)delta;
            if (horizontalDeltaLen > horizontalVelocity.Length())
            {
                horizontalVelocity = Vector2.Zero;
            }
            else
            {
                horizontalVelocity -= horizontalVelocity.Normalized() * horizontalDeltaLen;
            }
            Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Y);
        }
        Velocity += Gravity(delta);
        MoveAndSlide();

        float motionParameter = horizontalVelocity.Length() / WalkSpeed;
        _animationTree.Set("parameters/BlendSpace1D/blend_position", motionParameter);

        _heightRaycast.ForceRaycastUpdate();
        if (_heightRaycast.IsColliding())
        {
            float hitDistance = _heightRaycast.GetCollisionPoint().DistanceTo(_heightRaycast.GlobalPosition) - _heightRaycast.Position.Y;
            LeftFootHitDistance = hitDistance + _leftFootAttachment.Position.Y - _initialAttachmentHeight;
            RightFootHitDistance = hitDistance + _rightFootAttachment.Position.Y - _initialAttachmentHeight;
        }
        else
        {
            LeftFootHitDistance = 1.0f;
            RightFootHitDistance = 1.0f;
        }

        var leftPos3D = _leftFootAttachment.GlobalPosition;
        var rightPos3D = _rightFootAttachment.GlobalPosition;
        LeftFootPosition = new Vector2(leftPos3D.X, leftPos3D.Z);
        RightFootPosition = new Vector2(rightPos3D.X, rightPos3D.Z);
    }

    private void NodeInit()
    { 
        _animationTree = GetNode<AnimationTree>("%AnimationTree");
        _mainCamera = GetNode<Camera3D>("%MainCamera");
        _leftFootAttachment = GetNode<BoneAttachment3D>("%LeftFootAttachment");
        _rightFootAttachment = GetNode<BoneAttachment3D>("%RightFootAttachment");
        _heightRaycast = GetNode<RayCast3D>("%HeightRaycast");
    }

    private void HandleMouseMotion(Vector2 delta)
    {
        Vector2 scaledMotion = MouseSensitivity * delta;
        
        // Camera yaw
        RotateY(scaledMotion.X);

        // Camera pitch
        _mainCamera.RotateX(scaledMotion.Y);
        float currentPitch = Mathf.Clamp(_mainCamera.Rotation.X, -MAX_PITCH, MAX_PITCH);
        _mainCamera.Rotation = new Vector3(currentPitch, 0.0f, 0.0f);
    }

    private Vector3 Gravity(double delta)
    {
        if (IsOnFloor())
        {
            return Vector3.Zero;
        }
        return _gravityVelocity.MoveToward(new Vector3(0, Velocity.Y - _gravity, 0), _gravity * (float) delta);
    }
}
