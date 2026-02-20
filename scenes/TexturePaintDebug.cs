using Godot;
using System;

public partial class TexturePaintDebug : Node2D
{
	private TextureRect _textureRect;
	private TexturePainter _texturePainter;
	private Timer _stepTimer;

	public override void _Ready()
	{
		_texturePainter = GetNode<TexturePainter>("%TexturePainter");
		_textureRect = GetNode<TextureRect>("%TextureRect");
		_stepTimer = GetNode<Timer>("%StepTimer");
		_textureRect.Texture = _texturePainter.DisplacementTexture;
		_stepTimer.WaitTime = 0.5f;
	}

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
		if (@event is InputEventMouseButton mouseEvent)
		{
			// if (mouseEvent.ButtonIndex == MouseButton.Left)
			// {
			// 	_texturePainter.Params.CarveDepth = mouseEvent.Pressed ? 1.0f : 0.0f;
			// }
		}
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
		_texturePainter.Params.TextureOffset = new Vector2(0.0f, -0.1f * (float)delta);
		if (_stepTimer.IsStopped())
		{
			_texturePainter.Params.CarveDepth = 1.0f;
			_texturePainter.Params.SpriteOffset = -(_texturePainter.Params.SpriteOffset - new Vector2(0.5f, 0.5f)) + new Vector2(0.5f, 0.5f);
			_texturePainter.Params.FlipSprite = !_texturePainter.Params.FlipSprite;
			_stepTimer.Start();
		}
    }
}
