namespace Wheat;

using ANSIConsole;
using MathEx;
using System.Globalization;
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

	public static aabb2i intital_window;
	public aabb2i window = intital_window;
	public Cursor cursor;


	static WheatConsole()
	{
		intital_window = aabb2i.xywh(
			vec2i.zero, vec2i.xy(Console.WindowWidth, Console.WindowHeight));
	}

	[Dispose] DisposableValue ansiMode = new DisposableValue();

	public WheatConsole()
	{
		ansiMode._ = new ANSIInitializer();
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

		internal IDisposable Lock(Action<Cursor> a)
			=> DisposableLock.Lock(this, c => a(c));

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
	internal void WriteRaw(string s)
	{
		switch (Direction)
		{
			case WriteDirection.Original:
				Console.Write(s);
				cursor = (Cursor)Console.GetCursorPosition();
				break;
			default:
				var d = Direction switch {
					WriteDirection.Right => (1, 0),
					WriteDirection.Bottom => (0, 1),
					WriteDirection.Left => (-1, 0),
					WriteDirection.Top => (0, -1),
				};
				foreach (var c in s)
				{
					Console.Write(c);
					cursor = cursor.Move(d);
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

	public IDisposable Border(aabb2i padding)
		=> Window(new(window.a + padding.a, window.b - padding.b));

	public IDisposable Panel(aabb2i position)
		=> Window(position).Also(_ => {
			using (HideCursor())
			{
				cursor = cursor.Set(position.a);
				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Right), _ => Direction = _))
				{
					WriteRaw("\x250C"); // tl
					WriteRaw(new string('\x2500', position.width - 2));
				}

				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Bottom), _ => Direction = _))
				{
					WriteRaw("\x2510"); // tr
					WriteRaw(new string('\x2502', position.height - 2));
				}
				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Left), _ => Direction = _))
				{
					WriteRaw("\x2518"); // br
					WriteRaw(new string('\x2500', position.width - 2));
				}
				using (DisposableLock.Lock(Direction.Also(_ => Direction = WriteDirection.Top), _ => Direction = _))
				{
					WriteRaw("\x2514"); // bl
					WriteRaw(new string('\x2502', position.height - 2));
				}
			}
		});

	public IDisposable Image(WheatImage image) => Image(cursor, image);
	public IDisposable Image(vec2i position, WheatImage image)
		=> Window(position.hw(image.size)).Also(_ => {
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
