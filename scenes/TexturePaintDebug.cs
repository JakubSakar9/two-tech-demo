using Godot;
using System;

public partial class TexturePaintDebug : Node2D
{
	private TextureRect _textureRect;
	private TexturePainter _texturePainter;
	private Timer _stepTimer;
	private Vector2 _deformationCenter;

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
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
		_deformationCenter += Vector2.Up * 20.0f * (float)delta;
		_texturePainter.Params.SpriteCenter = new Vector2(0.5f, 0.5f) + _deformationCenter / _texturePainter.Params.TextureSize;
		if (_stepTimer.IsStopped())
		{
			_stepTimer.Start();
			_texturePainter.Params.CarveDepth = 1.0f;
			_texturePainter.FlipSprite();
		}
    }
}
