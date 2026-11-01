using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class MenuView : Control
{
    public event Action? HumanRequested;
    public event Action? RandomRequested;
    public event Action? ContinueRequested;
    public static readonly TimeControl[] Controls = [TimeControl.Unlimited, new(180, 2), new(300, 3), new(600, 0), new(900, 10)];
    public static readonly int[] EngineElos = [1400, 1800, 2200, 0];
    public static readonly string[] EngineLabels = ["Inicial · 1400", "Intermedio · 1800", "Avanzado · 2200", "Máximo"];
    private Button _continue = null!;
    private Label _message = null!;
    private OptionButton _time = null!;
    private OptionButton _difficulty = null!;
    public TimeControl SelectedTimeControl => Controls[Math.Clamp(_time.Selected, 0, Controls.Length - 1)];
    public int SelectedEngineElo => EngineElos[Math.Clamp(_difficulty.Selected, 0, EngineElos.Length - 1)];

    public override void _Ready()
    {
        const string path = "Center/Panel/Margin/Options/";
        GetNode<Button>(path + "Human").Pressed += () => HumanRequested?.Invoke();
        GetNode<Button>(path + "Random").Pressed += () => RandomRequested?.Invoke();
        _continue = GetNode<Button>(path + "Continue");
        _continue.Pressed += () => ContinueRequested?.Invoke();
        _message = GetNode<Label>(path + "Message");
        _time = GetNode<OptionButton>(path + "TimeControl");
        foreach (var label in new[] { "Sin reloj", "3 min + 2 s", "5 min + 3 s", "10 min", "15 min + 10 s" }) _time.AddItem(label);
        _difficulty = GetNode<OptionButton>(path + "Difficulty");
        foreach (var label in EngineLabels) _difficulty.AddItem(label);
        _difficulty.Select(1);
    }

    public void SetContinueAvailable(bool available)
    {
        _continue.Disabled = !available;
        _continue.TooltipText = available ? "Retomar la partida guardada" : "Todavía no hay una partida guardada";
    }
    public void SetMessage(string message) => _message.Text = message;
    public void FocusFirstOption() => GetNode<Button>("Center/Panel/Margin/Options/Human").GrabFocus();
}
