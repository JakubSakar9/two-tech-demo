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

    public override void _Process(double delta)
    {
        base._Process(delta);
    }
}
