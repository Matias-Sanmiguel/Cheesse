using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class MenuView : Control
{
    public event Action? HumanRequested;
    public event Action? RandomRequested;
    public event Action? ContinueRequested;

    private Button _continue = null!;
    private Label _message = null!;

    public override void _Ready()
    {
        GetNode<Button>("Center/Panel/Margin/Options/Human").Pressed += () => HumanRequested?.Invoke();
        GetNode<Button>("Center/Panel/Margin/Options/Random").Pressed += () => RandomRequested?.Invoke();
        _continue = GetNode<Button>("Center/Panel/Margin/Options/Continue");
        _continue.Pressed += () => ContinueRequested?.Invoke();
        _message = GetNode<Label>("Center/Panel/Margin/Options/Message");
    }

    public void SetContinueAvailable(bool available)
    {
        _continue.Disabled = !available;
        _continue.TooltipText = available ? "Retomar la partida guardada" : "Todavía no hay una partida guardada";
    }

    public void SetMessage(string message) => _message.Text = message;

    public void FocusFirstOption() =>
        GetNode<Button>("Center/Panel/Margin/Options/Human").GrabFocus();
}
