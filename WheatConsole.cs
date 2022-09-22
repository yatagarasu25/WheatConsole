namespace Wheat;

using ANSIConsole;
using MathEx;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SystemEx;

public partial class WheatConsole : IDisposable
{
	enum WriteDirection
	{
		Right,
		Bottom,
		Left,
		Top,

		Original = Right,
		CW90 = Bottom,
		CW190 = Left,
		CCW90 = Top,
	};

	static WriteDirection Direction = WriteDirection.Original;
	public static int Tabs { get; private set; }
	public static int Spaces { get; private set; }
	static int PendingSpaces = 0;
	static bool NeedIndent = true;
	static int LineWidth = 65;

	public aabb2i screen => aabb2i.wh(vec2i.xy(Console.WindowWidth, Console.WindowHeight));
	public aabb2i window;
	public Cursor cursor;
	public Cursor readCursor;


	[Dispose] DisposableValue ansiMode = new DisposableValue();


	public WheatConsole()
	{ 
		ansiMode._ = new ANSIInitializer();
		window = screen;
		cursor = (Cursor)Console.GetCursorPosition();
	}

	public void Dispose()
	{
		this.DisposeFields();
	}


	public IDisposable HideCursor()
	=> DisposableLock.Lock(
		true.Also(_ => { Console.CursorVisible = false; })
		, _ => Console.CursorVisible = _);

	public IDisposable Paint(ConsoleColor bg, ConsoleColor fg)
	{
		var o = (bg: Console.BackgroundColor, fg: Console.ForegroundColor);

		Console.BackgroundColor = bg;
		Console.ForegroundColor = fg;

		return DisposableLock.Lock(() => {
			Console.BackgroundColor = o.bg;
			Console.ForegroundColor = o.fg;
		});
	}

	public record class Cursor(int x, int y)
	{
		public static explicit operator Cursor(vec2i v) => new(v.x, v.y);
		public static explicit operator Cursor((int Left, int Top) v) => new(v.Left, v.Top);
		public static implicit operator vec2i(Cursor c) => vec2i.xy(c.x, c.y);

		internal Cursor Set(int _x, int _y) => new Cursor(_x, _y).Also(_ => _.Set());
		internal Cursor Set(vec2i a) => new Cursor(a.x, a.y).Also(_ => _.Set());

		internal Cursor Set()
			=> this.Also(_ => Console.SetCursorPosition(x.Clamp0(0), y.Clamp(0, Console.BufferHeight - 1)));

		internal Cursor Move(int dx) => Set(x + dx, y);
		internal Cursor Move((int x, int y) d) => Set(x + d.x, y + d.y);
		internal Cursor Move(vec2i d) => Set(x + d.x, y + d.y);
	}
	public void SetCursor(int _x, int _y) => cursor = cursor.Set(window.a.x + _x, window.a.y + _y);


	public ConsoleKeyInfo ReadKey()
	{
		using var g = DisposableLock.Lock(Console.Out, Console.SetOut);
		Console.SetOut(TextWriter.Null);

		return Console.ReadKey();
	}

	public bool Ask()
	{
		var str = Console.ReadLine();
		if (!str.IsNullOrEmpty())
			return str[0] switch { // catch after experssion sequence
				'y' or 'Y' => true,
				'n' or 'N' => false,
				_ => false,
			};

		return false;
	}

	public bool ReadValue<T>(ref T value, string format, Func<int, T> step = null)
		where T : INumber<T>
	{
		EndLine(false);

		try
		{
			string set_str(T v) => new StringBuilder().AppendFormat($"{{0:{format}}}", v).ToString();
			bool parse_str(string s, out T v) => T.TryParse(s, CultureInfo.InvariantCulture, out v);

			var v = value;
			var str = set_str(v);
			var cancel = false;

			void f_change_value(int d)
			{
				str = string.Empty;
				step.Elvis(_ => v += _.Invoke(d));
				str = set_str(v);
				f_validate_cursor();
			};

			int lpl = str.Length;
			var start = cursor;
			var current = cursor;

			int f_cursor() => current.x - start.x;
			void f_validate_cursor() {
				if (current.x < start.x) current = current with { x = start.x };
			};
			void f_move_cursor(int dx) {
				current = current.Move(dx);
				f_validate_cursor();
			};

			ConsoleKeyInfo f_wait_key(string str) {
				using (HideCursor())
				{
					start.Set();
					WriteRaw(str);
					if (str.Length < lpl)
						WriteRaw(new string(' ', lpl - str.Length));

					lpl = str.Length;
					current.Set();
				}

				return Console.ReadKey(true);
			};

			// should be do cycle but no variable visibility.
			while (f_wait_key(str) switch {
				(Key: ConsoleKey.Enter) => false,
				(Key: ConsoleKey.Escape) => false.Also(_ => cancel = true),
				(Key: ConsoleKey.Backspace) => true.Also(_ => {
					var c = f_cursor();
					if (c > 0)
					{
						str = str.Remove(c - 1, 1);
						if (parse_str(str, out var pv)) v = pv;
						f_move_cursor(-1);
					}
				}),
				(Key: ConsoleKey.UpArrow) => true.Also(_ => f_change_value(1)),
				(Key: ConsoleKey.DownArrow) => true.Also(_ => f_change_value(-1)),
				(Key: ConsoleKey.RightArrow) => true.Also(_ => f_move_cursor(1)),
				(Key: ConsoleKey.LeftArrow) => true.Also(_ => f_move_cursor(-1)),
				var k => true.Also(__ => {
					var tstr = str.Insert(f_cursor(), $"{k.KeyChar}");
					if (parse_str(tstr, out var pv))
					{
						v = pv;
						str = tstr;
					}

					f_move_cursor(1);
				})
			});

			if (cancel) return false;

			value = parse_str(str, out var pv) ? pv : v;

			return true;
		}
		catch { }

		return false;
	}

	internal void ReadRaw(out char c)
	{
		
#if false
		using (cursor.Lock(
			c => Console.SetCursorPosition(readCursor.x, readCursor.y)
			, c => Console.SetCursorPosition(c.x, c.y)))
		{
			Console.ForegroundColor
		}
#endif
	}

	public void Indent()
	{
		if (NeedIndent)
		{
			WriteRaw(new string('\t', Tabs));
			WriteRaw(new string(' ', Spaces));

			NeedIndent = false;
		}
	}

	List<IDisposable> colorStack = new List<IDisposable>();
	public int Write(string v = "")
	{
		Indent();

		int OutputLength = v.Length;

		var commands = new[] { "[", "]", "c.." };
		var pattern = commands.Select((i) => i switch {
			"]" or "[" => $"\\{i}",
			_ => $"{i}"
		})
		.Join('|');
		foreach (var t in Regex.Split(v, $"(\f(?:{pattern})+)").Where(s => !s.IsNullOrEmpty()))
		{
			var ReadColor = (char c, ConsoleColor color) => c == '*' ? color : (ConsoleColor)Convert.ToInt32($"{c}", 16);

			if (t[0] == '\f')
			{
				var ci = t[1] switch {
					'[' => this.Also(_ => colorStack.Add(Paint(Console.BackgroundColor, ConsoleColor.White))).Let(_ => 0),
					']' => this.Also(_ => colorStack.Pop().Dispose()).Let(_ => 1),
					'c' => this.Also(_ => {
						var bgcolor = ReadColor(t[2], Console.BackgroundColor);
						var fgcolor = ReadColor(t[3], Console.ForegroundColor);
						colorStack.Add(Paint(bgcolor, fgcolor));
					}).Let(_ => 2),
					_ => this.Also(_ => Console.WriteLine(" ### format error ### ")).Let(_ => -100)
				};

				OutputLength -= commands[ci].Length + 1;
			}
			else
			{
				WriteRaw(t);
			}
		}

		Spaces += PendingSpaces;
		PendingSpaces = 0;

		return OutputLength;
	}

	internal void WriteRaw(ANSIString s) => WriteRaw(s.ToString());
	internal void WriteRaw(char c)
	{
		var d = Direction switch {
			WriteDirection.Right => (1, 0),
			WriteDirection.Bottom => (0, 1),
			WriteDirection.Left => (-1, 0),
			WriteDirection.Top => (0, -1),
		};

		Console.Write(c);
		cursor = cursor.Move(d);
		readCursor = cursor;
	}
	internal void WriteRaw(string s)
	{
		switch (Direction)
		{
			//case WriteDirection.Original:
			//	Console.Write(s);
			//	cursor = (Cursor)Console.GetCursorPosition();
			//	break;
			default:
				foreach (var c in s)
				{
					WriteRaw(c);
				}
				break;
		}
	}

	internal void WriteLineRaw(string s = "")
	{
		WriteRaw(s);

		cursor = Direction switch {
			WriteDirection.Right => cursor.Set(window.a.x, cursor.y + 1),
			WriteDirection.Bottom => cursor.Set(window.a.x - 1, window.a.y),
			WriteDirection.Left => cursor.Set(window.b.x, cursor.y - 1),
			WriteDirection.Top => cursor.Set(window.a.x + 1, window.b.y),
		};
		cursor.Set();

		if (clearmode)
		{
			if (Direction == WriteDirection.Original)
			{
				using (cursor.Lock(c => cursor = c.Set()))
				{
					WriteRaw(new string(' ', window.width)); // echoes of the past
				}
			}
		}
	}

	public void EndLine() => EndLine(true);

	private void EndLine(bool writeLine)
	{
		if (writeLine) WriteLineRaw();
		NeedIndent = true;
	}

	public void WriteLine(string v = "")
	{
		Indent();
		Write(v);

		EndLine();
	}

	public void WriteTearLine(string message, string pagemark = "<<< ", char tear = '-')
	{
		var message_center = (LineWidth - message.Length) >> 1;
		var message_halflength = (message.Length + 1) >> 1;

		using (Paint(Console.BackgroundColor, ConsoleColor.White))
			WriteRaw(pagemark);

		var message_start = message_center - message_halflength;
		var tear_length = (message_start - pagemark.Length).Clamp0(0);

		using (Paint(Console.BackgroundColor, ConsoleColor.DarkGray))
			WriteRaw(new string(tear, tear_length));
		
		WriteRaw(message);

		var message_end = pagemark.Length + tear_length + message.Length;
		tear_length = (LineWidth - message_end);
		using (Paint(Console.BackgroundColor, ConsoleColor.DarkGray))
			WriteRaw(new string(tear, tear_length));

		PendingSpaces = 0;
		EndLine();
	}

	public string bold<T>(T v) => $"\f[{v}\f]";

	public static string color<T>(int fg, T v) => $"\fc*{Convert.ToString(fg, 16)}{v}\f]";
	public static string color<T>(ConsoleColor fg, T v) => $"\fc*{Convert.ToString((int)fg, 16)}{v}\f]";
	public string darkgreen<T>(T v) => color(ConsoleColor.DarkGreen, v);
	public string green<T>(T v) => color(ConsoleColor.Green, v);
	public string gold<T>(T v) => color(ConsoleColor.Yellow, v);


	bool clearmode = false;
	public void Clear()
	{
		cursor.Set(0, 0);
		clearmode = true;
	}


	public IDisposable Window(aabb2i newWindow)
	{
		return DisposableLock.Lock(
			(window, cursor).Also(
				_ => {
					window = newWindow;
					//var cp = window.clamp(cursor);
					//((Cursor)cp).Set();
				}),
				_ => {
					window = _.window;
					cursor = _.cursor.Set();
				});
	}

	IDisposable<WriteDirection> LockDirection(WriteDirection d)
		=> Direction.Also(_ => Direction = d).Lock(_ => Direction = _);
	


	void DrawVerticalLine(vec2i a, vec2i b)
	{
		var lineCh = BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, bottom = BoxDrawing.CellLine.Single });
		vec2i.order(ref a, ref b);
		cursor = cursor.Set(a);

		using (LockDirection(WriteDirection.Bottom))
		{
			WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { bottom = BoxDrawing.CellLine.Single, left = BoxDrawing.CellLine.Single }));
			WriteRaw(new string(lineCh, (b.y - a.y - 2).Clamp0()));
		}
	}

	void DrawBorder(aabb2i rect)
	{
		cursor = cursor.Set(rect.a);

		using (LockDirection(WriteDirection.Right))
		{
			WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { bottom = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }));
			WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { left = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }), position.width - 2));
		}

		using (LockDirection(WriteDirection.Bottom))
		{
			WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { bottom = BoxDrawing.CellLine.Single, left = BoxDrawing.CellLine.Single }));
			WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, bottom = BoxDrawing.CellLine.Single }), position.width - 2));
		}
		using (LockDirection(WriteDirection.Left))
		{
			WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, left = BoxDrawing.CellLine.Single }));
			WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { left = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }), position.width - 2));
		}
		using (LockDirection(WriteDirection.Top))
		{
			WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }));
			WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, bottom = BoxDrawing.CellLine.Single }), position.width - 2));
		}
	}

	public IDisposable Border(aabb2i padding)
		=> Window(new(window.a + padding.a, window.b - padding.b)).Also(_ => { 

		});

	public IDisposable Panel(aabb2i position)
		=> Window(position).Also(_ => {
			using (HideCursor())
			{
				cursor = cursor.Set(position.a);
				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Right), _ => Direction = _))
				{
					WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { bottom = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }));
					WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { left = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }), position.width - 2));
				}

				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Bottom), _ => Direction = _))
				{
					WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { bottom = BoxDrawing.CellLine.Single, left = BoxDrawing.CellLine.Single }));
					WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, bottom = BoxDrawing.CellLine.Single }), position.width - 2));
				}
				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Left), _ => Direction = _))
				{
					WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, left = BoxDrawing.CellLine.Single }));
					WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { left = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }), position.width - 2));
				}
				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Top), _ => Direction = _))
				{
					WriteRaw(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, right = BoxDrawing.CellLine.Single }));
					WriteRaw(new string(BoxDrawing.GetChar(new BoxDrawing.Cell { top = BoxDrawing.CellLine.Single, bottom = BoxDrawing.CellLine.Single }), position.width - 2));
				}
			}
		});

	public IDisposable Image(WheatImage image) => Image(cursor, image);
	public IDisposable Image(vec2i position, WheatImage image)
		=> Window(position.wh(image.size)).Also(_ => {
			using (DisposableLock.Lock(() => clearmode.Also(_ => clearmode = false), _ => clearmode = _))
			{
				foreach (var l in image.lines)
				{
					WriteLine(l);
				}
			}
		});

	public IDisposable Section(string title, int tabs = 1)
	{
		var oldTabs = Tabs;
		WriteLineRaw();
		WriteLineRaw($"{title}:");
		Tabs += tabs;

		return DisposableLock.Lock(() => {
			Tabs = oldTabs;

			EndLine();
		});
	}

	public IDisposable WriteSection(string message)
	{
		var oldSpaces = Spaces;
		var oldPendingSpaces = PendingSpaces;
		PendingSpaces = Write(message);

		return DisposableLock.Lock(() => {
			Spaces = oldSpaces;
			PendingSpaces = oldPendingSpaces; // = 0 ? not for empty nested sections

			EndLine();
		});
	}
}

public class BoxDrawing
{
	class CellAttribute : Attribute
	{
		public CellLine top = CellLine.None;
		public CellLine right = CellLine.None;
		public CellLine bottom = CellLine.None;
		public CellLine left = CellLine.None;

		public Cell cell => new Cell { top= top, right = right, bottom = bottom, left = left };
	}


	[Flags]
	public enum CellLine
	{
		None = 0x00,
		Single = 0x01,
		Double = 0x02,
		Bold = 0x04
	}

	public struct Cell
	{
		public CellLine top = CellLine.None;
		public CellLine right = CellLine.None;
		public CellLine bottom = CellLine.None;
		public CellLine left = CellLine.None;

		public Cell()
		{
		}

		public static implicit operator int(Cell c)
			=> ((int)c.top << 6)
				| ((int)c.right << 4)
				| ((int)c.bottom << 2)
				| ((int)c.left);
	}

	public const char ErrorChar = '\x0000';

	[Cell(right = CellLine.Single, left = CellLine.Single)]
	public const char HorizontalLine = '\x2500';
	[Cell(right = CellLine.Single | CellLine.Bold, left = CellLine.Single | CellLine.Bold)]
	public const char HorizontalLineBold = '\x2501';
	[Cell(top = CellLine.Single, bottom = CellLine.Single)]
	public const char VerticalLine = '\x2502';
	[Cell(top = CellLine.Single | CellLine.Bold, bottom = CellLine.Single | CellLine.Bold)]
	public const char VerticalLineBold = '\x2503';
	public const char HorizontalDash2Line = '\x254C';
	public const char HorizontalDash2LineBold = '\x254D';
	public const char VerticalDash2Line = '\x254E';
	public const char VerticalDash2LineBold = '\x254F';
	public const char HorizontalDash3Line = '\x2504';
	public const char HorizontalDash3LineBold = '\x2505';
	public const char VerticalDash3Line = '\x2506';
	public const char VerticalDash3LineBold = '\x2507';
	public const char HorizontalDash4Line = '\x2508';
	public const char HorizontalDash4LineBold = '\x2509';
	public const char VerticalDash4Line = '\x250A';
	public const char VerticalDash4LineBold = '\x250B';
	[Cell(right = CellLine.Double, left = CellLine.Double)]
	public const char HorizontalDoubleLine = '\x2550';
	[Cell(top = CellLine.Double, bottom = CellLine.Double)]
	public const char VerticalDoubleLine = '\x2551';

	[Cell(bottom = CellLine.Single, right = CellLine.Single)]
	public const char TopLeftCornerLineNN = '\x250C';
	[Cell(bottom = CellLine.Single, right = CellLine.Single | CellLine.Bold)]
	public const char TopLeftCornerLineNB = '\x250D';
	[Cell(bottom = CellLine.Single | CellLine.Bold, right = CellLine.Single)]
	public const char TopLeftCornerLineBN = '\x250E';
	[Cell(bottom = CellLine.Single | CellLine.Bold, right = CellLine.Single | CellLine.Bold)]
	public const char TopLeftCornerLineBB = '\x250F';
	[Cell(bottom = CellLine.Single, left = CellLine.Single)]
	public const char TopRightCornerLineNN = '\x2510';
	[Cell(bottom = CellLine.Single, left = CellLine.Single | CellLine.Bold)]
	public const char TopRightCornerLineNB = '\x2511';
	[Cell(bottom = CellLine.Single | CellLine.Bold, left = CellLine.Single)]
	public const char TopRightCornerLineBN = '\x2512';
	[Cell(bottom = CellLine.Single | CellLine.Bold, left = CellLine.Single | CellLine.Bold)]
	public const char TopRightCornerLineBB = '\x2513';
	[Cell(top = CellLine.Single, right = CellLine.Single)]
	public const char BottomLeftCornerLineNN = '\x2514';
	[Cell(top = CellLine.Single, right = CellLine.Single | CellLine.Bold)]
	public const char BottomLeftCornerLineNB = '\x2515';
	[Cell(top = CellLine.Single | CellLine.Bold, right = CellLine.Single)]
	public const char BottomLeftCornerLineBN = '\x2516';
	[Cell(top = CellLine.Single | CellLine.Bold, right = CellLine.Single | CellLine.Bold)]
	public const char BottomLeftCornerLineBB = '\x2517';
	[Cell(top = CellLine.Single, left = CellLine.Single)]
	public const char BottomRightCornerLineNN = '\x2518';
	[Cell(top = CellLine.Single, left = CellLine.Single | CellLine.Bold)]
	public const char BottomRightCornerLineNB = '\x2519';
	[Cell(top = CellLine.Single | CellLine.Bold, left = CellLine.Single)]
	public const char BottomRightCornerLineBN = '\x251A';
	[Cell(top = CellLine.Single | CellLine.Bold, left = CellLine.Single | CellLine.Bold)]
	public const char BottomRightCornerLineBB = '\x251B';

	[Cell(bottom = CellLine.Single, right = CellLine.Double)]
	public const char TopLeftCornerLineSD = '\x2502';
	[Cell(bottom = CellLine.Double, right = CellLine.Single)]
	public const char TopLeftCornerLineDS = '\x2503';
	[Cell(bottom = CellLine.Double, right = CellLine.Double)]
	public const char TopLeftCornerLineDD = '\x2504';
	[Cell(bottom = CellLine.Single, left = CellLine.Double)]
	public const char TopRightCornerLineSD = '\x2505';
	[Cell(bottom = CellLine.Double, left = CellLine.Single)]
	public const char TopRightCornerLineDS = '\x2506';
	[Cell(bottom = CellLine.Double, left = CellLine.Double)]
	public const char TopRightCornerLineDD = '\x2507';
	[Cell(top = CellLine.Single, right = CellLine.Double)]
	public const char BottomLeftCornerLineSD = '\x2508';
	[Cell(top = CellLine.Double, right = CellLine.Single)]
	public const char BottomLeftCornerLineDS = '\x2509';
	[Cell(top = CellLine.Double, right = CellLine.Double)]
	public const char BottomLeftCornerLineDD = '\x250A';
	[Cell(top = CellLine.Single, left = CellLine.Double)]
	public const char BottomRightCornerLineSD = '\x250B';
	[Cell(top = CellLine.Double, left = CellLine.Single)]
	public const char BottomRightCornerLineDS = '\x250C';
	[Cell(top = CellLine.Double, left = CellLine.Double)]
	public const char BottomRightCornerLineDD = '\x250D';


	private static Dictionary<Cell, char> CellToChars;
	private static Dictionary<char, Cell> CharToCells;

	static BoxDrawing()
	{
		var fields = typeof(BoxDrawing)
			.GetFieldsAndAttributes<CellAttribute>(BindingFlags.Public | BindingFlags.Static)
			.ToList();

		CellToChars = fields
			.ToDictionary(fa => fa.Item2.cell, fa => (char)fa.Item1.GetValue(null));
		CharToCells = fields
			.ToDictionary(fa => (char)fa.Item1.GetValue(null), fa => fa.Item2.cell);
	}

	public static char GetChar(Cell cell)
	{
		if (CellToChars.TryGetValue(cell, out char c))
			return c;

		return ErrorChar;
	}
}