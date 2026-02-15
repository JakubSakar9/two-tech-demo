using Godot;
using System;

public partial class TexturePaintDebug : Node2D
{
	private TextureRect _textureRect;
	private TexturePainter _texturePainter;

	public override void _Ready()
	{
		_texturePainter = GetNode<TexturePainter>("%TexturePainter");
		_textureRect = GetNode<TextureRect>("%TextureRect");
		_textureRect.Texture = _texturePainter.DisplacementTexture;
	}

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				_texturePainter.Params.CarveDepth = mouseEvent.Pressed ? 1.0f : 0.0f;
			}
		}
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }
}
