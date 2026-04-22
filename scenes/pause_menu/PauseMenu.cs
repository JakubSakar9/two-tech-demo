using Godot;
using System;
using System.Linq;

public partial class PauseMenu : CanvasLayer
{
    [Export] public Player PlayerRef;
    [Export] public Terrain TerrainRef;

    private Label[] _labels;
    private Label _resumeLabel;
    private Label _snowLabel;
    private Label _terrainLabel;
    private Label _quitLabel;
    private int _selectedIdx = -1;

    public override void _Ready()
    {
        base._Ready();
        HideMenu();

        _labels = new Label[4];
        _labels[0] = GetNode<Label>("%ResumeLabel");
        _labels[1] = GetNode<Label>("%SnowLabel");
        _labels[2] = GetNode<Label>("%TerrainLabel");
        _labels[3] = GetNode<Label>("%QuitLabel");


        for (int i = 0; i < _labels.Length; i++)
        {
            CreateLabelCallback(i);
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is InputEventKey keyEvent && keyEvent.Keycode == Key.Escape && keyEvent.IsPressed())
        {
            HideMenu();
        }
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.IsReleased())
        {
            HandleClick();
        }
    }
    
    public void ShowMenu()
    {
        Show();
        SetProcessInput(true);
        PlayerRef.Pause();
        TerrainRef.Pause();
    }

    public void HideMenu()
    {
        Hide();
        SetProcessInput(false);
        PlayerRef.Unpause();
        TerrainRef.Unpause();
    }

    private void CreateLabelCallback(int idx)
    {
        _labels[idx].MouseEntered += () =>
        {
            _selectedIdx = idx;
            _labels[idx].Modulate = Colors.DimGray;
            GD.Print("Entered", idx);
        };
        _labels[idx].MouseExited += () =>
        {
            _selectedIdx = -1;
            _labels[idx].Modulate = Colors.White;
        };
    }

    private void HandleClick()
    {
        if (_selectedIdx == -1) return;

        switch (_selectedIdx)
        {
            case 0:
                HideMenu();
                break;
            case 1:
                HideMenu(); // TODO: Actually change snow cover
                break;
            case 2:
                HideMenu();
                GetTree().ReloadCurrentScene();
                break;
            case 3:
                GD.Print("Should quit");
                GetTree().Quit();
                break;
        }
    }
}
