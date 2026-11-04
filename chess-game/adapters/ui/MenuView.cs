
using Cheesse.Game.Adapters.Appearance;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class MenuView : Control
{
    public event Action? HumanRequested;
    public event Action? RandomRequested;
    public event Action? ContinueRequested;

    private const string OptionsPath =
        "Center/Panel/Margin/Options/";

    private Button _continue = null!;
    private Label _message = null!;

    private OptionButton _appearance = null!;
    private OptionButton _boardSkin = null!;
    private OptionButton _whiteSkin = null!;
    private OptionButton _blackSkin = null!;

    private VBoxContainer _themeDetails = null!;

    public override void _Ready()
    {
        GetNode<Button>(OptionsPath + "Human").Pressed +=
            () => HumanRequested?.Invoke();

        GetNode<Button>(OptionsPath + "Random").Pressed +=
            () => RandomRequested?.Invoke();

        _continue = GetNode<Button>(
            OptionsPath + "Continue");

        _continue.Pressed +=
            () => ContinueRequested?.Invoke();

        _message = GetNode<Label>(
            OptionsPath + "Message");

        // Controles de apariencia.
        _appearance = GetNode<OptionButton>(
            OptionsPath + "Appearance");

        _themeDetails = GetNode<VBoxContainer>(
            OptionsPath + "ThemeDetails");

        _boardSkin = GetNode<OptionButton>(
            OptionsPath + "ThemeDetails/Board");

        _whiteSkin = GetNode<OptionButton>(
            OptionsPath + "ThemeDetails/White");

        _blackSkin = GetNode<OptionButton>(
            OptionsPath + "ThemeDetails/Black");

        // Apariencia principal.
        _appearance.AddItem("Clásico");
        _appearance.AddItem("Picada personalizada");

        // Tableros disponibles.
        foreach (var skin in VisualThemeCatalog.Boards)
        {
            _boardSkin.AddItem(skin.Label);
        }

        // Quesos para blancas.
        _whiteSkin.AddItem("Aleatorio");

        foreach (var skin in VisualThemeCatalog.Cheeses)
        {
            _whiteSkin.AddItem(skin.Label);
        }

        // Fiambres para negras.
        _blackSkin.AddItem("Aleatorio");

        foreach (var skin in VisualThemeCatalog.Charcuterie)
        {
            _blackSkin.AddItem(skin.Label);
        }

        // Valores predeterminados.
        _appearance.Select(0);
        _boardSkin.Select(0);
        _whiteSkin.Select(0);
        _blackSkin.Select(0);

        _appearance.ItemSelected +=
            _ => UpdateThemeFields();

        PixelUiSkin.ApplyMenu(this);
        UpdateThemeFields();
    }

    public VisualTheme CreateVisualTheme()
    {
        if (_appearance.Selected == 0)
        {
            return VisualTheme.Classic;
        }

        return VisualThemeCatalog.Create(
            _boardSkin.Selected,
            _whiteSkin.Selected,
            _blackSkin.Selected
        );
    }

    private void UpdateThemeFields()
    {
        _themeDetails.Visible = _appearance.Selected == 1;
        PixelUiSkin.SetModePreviewVisible(this, !_themeDetails.Visible);
    }

    public void SetContinueAvailable(bool available)
    {
        _continue.Disabled = !available;

        _continue.TooltipText = available
            ? "Retomar la partida guardada"
            : "Todavía no hay una partida guardada";
    }

    public void SetMessage(string message)
    {
        _message.Text = message;
    }

    public void FocusFirstOption()
    {
        GetNode<Button>(
            OptionsPath + "Human").GrabFocus();
    }
}
