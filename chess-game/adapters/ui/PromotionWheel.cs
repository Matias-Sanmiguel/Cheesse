
using Chess.Core.Model;
using Godot;

namespace Cheesse.Game.Adapters.Ui;

public partial class PromotionWheel : Control
{
	[Signal]
	public delegate void PieceSelectedEventHandler(int pieceType);

	[Signal]
	public delegate void CancelRequestedEventHandler();

	private const float Radius = 140f;
	private const float InnerRadius = 48f;
	private const int Segments = 32;

	private static readonly Color Ebony = new("#191714");
	private static readonly Color Wood = new("#35302A");
	private static readonly Color Bronze = new("#AA8552");
	private static readonly Color Ivory = new("#E9D8B9");

	private static readonly Color TransparentWood = new(0.21f, 0.19f, 0.16f, 0.72f);
	private static readonly Color TransparentEbony = new(0.10f, 0.09f, 0.08f, 0.82f);

	private readonly PieceType[] _pieces = [
		PieceType.Queen,
		PieceType.Rook,
		PieceType.Bishop,
		PieceType.Knight
	];

	private int _hovered = -1;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(320, 320);
		MouseFilter = MouseFilterEnum.Stop;

		MouseExited += () => { _hovered = -1; QueueRedraw(); };
	}


	public override void _Draw()
	{
		var center = Size / 2f;
		var font = ThemeDB.FallbackFont;

		for (var i = 0; i < 4; i++)
		{
			var start = Mathf.DegToRad(-135f + i * 90f);
			var end = start + Mathf.Pi / 2f;

			var points = new Vector2[(Segments + 1) * 2];

			for (var j = 0; j <= Segments; j++)
			{
				var angle = Mathf.Lerp(start, end, j / (float)Segments);
				points[j] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
			}

			for (var j = 0; j <= Segments; j++)
			{
				var angle = Mathf.Lerp(end, start, j / (float)Segments);
				points[Segments + 1 + j] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * InnerRadius;
			}

			var sectorColor = i == _hovered ? new Color("#AA8552B0") : new Color("#35302A55");
			DrawColoredPolygon(points, sectorColor);

			var middle = (start + end) / 2f;
			var iconPosition = center + new Vector2(Mathf.Cos(middle), Mathf.Sin(middle)) * 92f;

			var symbol = i switch
			{
				0 => "♕",
				1 => "♖",
				2 => "♗",
				_ => "♘"
			};

			var iconWidth = font.GetStringSize(symbol, HorizontalAlignment.Left, -1, 46).X;
			var iconBaseline = (font.GetAscent(46) - font.GetDescent(46)) / 2f;
			DrawString(font, iconPosition + new Vector2(-iconWidth / 2f, iconBaseline), symbol, HorizontalAlignment.Left, -1, 46, Ivory);
			}

			DrawArc(center, Radius + 5f, 0f, Mathf.Tau, 96, Wood, 6f, true);
			DrawArc(center, InnerRadius, 0f, Mathf.Tau, 96, Wood, 4f, true);

			for (var i = 0; i < 4; i++)
			{
				var angle = Mathf.DegToRad(-135f + i * 90f);
				var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				DrawLine(center + direction * InnerRadius, center + direction * Radius, Wood, 3f, true);
			}

			DrawCircle(center, InnerRadius - 2f, new Color("#19171488"));

			var text = "CANCEL";
			var fontSize = 12;
			var textWidth = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
			var textBaseline = (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2f;

			DrawString(font, center + new Vector2(-textWidth / 2f, textBaseline), text, HorizontalAlignment.Left, -1, fontSize, Ivory);
	}


	public override void _GuiInput(InputEvent input)
	{
		if (input is InputEventMouseMotion motion)
		{
			_hovered = GetSector(motion.Position);
			QueueRedraw();
		}

		if (input is InputEventMouseButton
			{
				Pressed: true,
				ButtonIndex: MouseButton.Left
			} mouse)
		{
			var sector = GetSector(mouse.Position);
			var distance = (mouse.Position - Size / 2f).Length();

			if (distance < InnerRadius)
			{
				EmitSignal(SignalName.CancelRequested);
				AcceptEvent();
				return;
			}

			if (sector >= 0)
			{
				EmitSignal(SignalName.PieceSelected, (int)_pieces[sector]);
				AcceptEvent();
			}
		}
	}

	private int GetSector(Vector2 position)
	{
		var delta = position - Size / 2f;
		var distance = delta.Length();

		if (distance < InnerRadius || distance > Radius)
			return -1;

		var angle = Mathf.RadToDeg(Mathf.Atan2(delta.Y, delta.X));
		return Mathf.PosMod(Mathf.FloorToInt((angle + 135f) / 90f), 4);
	}
}
