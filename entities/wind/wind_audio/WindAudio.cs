using Godot;
using System;

public partial class WindAudio : Node3D
{
    const float DIR_DISTANCE = 4.0f;
    const string AMBIENT_WEAK =   "res://assets/audio/wind/weak_wind_ambient.ogg";
    const string AMBIENT_MID =    "res://assets/audio/wind/mid_wind_ambient.ogg";
    const string AMBIENT_STRONG = "res://assets/audio/wind/strong_wind_ambient.ogg";
    const string EAR_MID =        "res://assets/audio/wind/mid_wind_ear.ogg";
    const string EAR_STRONG =     "res://assets/audio/wind/strong_wind_ear.ogg";

    [Export] public float MinSpeed = 4.0f;
    [Export] public float MaxSpeed = 16.0f;
    [Export] public float MinHeight = 12.0f;
    [Export] public float MaxHeight = 40.0f;

    private AudioStreamPlayer3D _directional;
    private AudioStreamPlayer _ambient;

    private AudioStreamPlaybackPolyphonic _dPlayback;
    private AudioStreamPlaybackPolyphonic _aPlayback;

    private long _dMidId;
    private long _dStrongId;
    private long _aWeakId;
    private long _aMidId;
    private long _aStrongId;
    private Terrain _terrain;

    public override void _Ready()
    {
        base._Ready();
        _terrain = GetTree().GetFirstNodeInGroup("terrain") as Terrain;
        _directional = GetNode<AudioStreamPlayer3D>("%DirectionalAudio");
        _ambient = GetNode<AudioStreamPlayer>("%AmbientAudio");

        var dStream = new AudioStreamPolyphonic();
        dStream.Polyphony = 2;
        _directional.Stream = dStream;
        _directional.Play();
        _dPlayback = _directional.GetStreamPlayback() as AudioStreamPlaybackPolyphonic;
        if (_dPlayback == null)
        {
            GD.PrintErr("D. Playback is null");
        }
        _dMidId =    _dPlayback.PlayStream(ResourceLoader.Load<AudioStreamOggVorbis>(EAR_MID));
        _dStrongId = _dPlayback.PlayStream(ResourceLoader.Load<AudioStreamOggVorbis>(EAR_STRONG));

        var aStream  = new AudioStreamPolyphonic();
        aStream.Polyphony = 3;
        _ambient.Stream = aStream;
        _ambient.Play();
        _aPlayback = _ambient.GetStreamPlayback() as AudioStreamPlaybackPolyphonic;
        if (_aPlayback == null)
        {
            GD.PrintErr("A. Playback is null");
        }
        _aWeakId =   _aPlayback.PlayStream(ResourceLoader.Load<AudioStreamOggVorbis>(AMBIENT_WEAK));
        _aMidId =    _aPlayback.PlayStream(ResourceLoader.Load<AudioStreamOggVorbis>(AMBIENT_MID));
        _aStrongId = _aPlayback.PlayStream(ResourceLoader.Load<AudioStreamOggVorbis>(AMBIENT_STRONG));

        _dPlayback.SetStreamVolume(_dMidId,    -80.0f);
        _dPlayback.SetStreamVolume(_dStrongId, -80.0f);

        _aPlayback.SetStreamVolume(_aWeakId,     0.0f);
        _aPlayback.SetStreamVolume(_aMidId,    -80.0f);
        _aPlayback.SetStreamVolume(_aStrongId, -80.0f);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        Vector3 windVec = _terrain.GetWindAtPoint(GetParent<Camera3D>().GlobalPosition);
        _directional.GlobalPosition = GlobalPosition - windVec.Normalized() * DIR_DISTANCE;

        float windSpeed = windVec.Length();
        GD.Print(windSpeed);
        float td = (windSpeed - MinSpeed) / (MaxSpeed - MinSpeed);
        td = Mathf.Clamp(td, 0.0f, 1.0f);
        float td1 = Mathf.Min(2.0f * td, 1.0f);
        float td2 = Mathf.Max(2.0f * td - 1.0f, 0.0f);
        _dPlayback.SetStreamVolume(_dMidId,    LinearToDb(td1));
        _dPlayback.SetStreamVolume(_dStrongId, LinearToDb(td2));

        float ta = (GlobalPosition.Y - MinHeight) / (MaxHeight - MinHeight);
        ta = Mathf.Clamp(ta, 0.0f, 1.0f);
        float ta1 = Mathf.Min(2.0f * ta, 1.0f);
        float ta2 = Mathf.Max(2.0f * ta - 1.0f, 0.0f);
        _aPlayback.SetStreamVolume(_aMidId,    LinearToDb(ta1));
        _aPlayback.SetStreamVolume(_aStrongId, LinearToDb(ta2));
    }

    private static float LinearToDb(float linear)
    {
        return 20.0f * Mathf.Log(linear) / Mathf.Log(10);
    }
}
