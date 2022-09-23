using static Wheat.NativeFunctions;

namespace Wheat;

internal class MouseEvent
{
	public vec2i position;
	public MOUSE_EVENT_RECORD.bs buttons;
	public MOUSE_EVENT_RECORD.ks keys;

	public static explicit operator MouseEvent(MOUSE_EVENT_RECORD m)
		=> new MouseEvent {
			position = m.dwMousePosition,
			buttons = m.dwButtonState,
			keys = m.dwControlKeyState,
		};
}

internal class KeyEvent
{
}

internal class WindowEvent
{
}

internal class WheatConsoleInput
{
	static public IntPtr handleIn;

	static WheatConsoleInput()
	{
		handleIn = GetStdHandle(STD_INPUT_HANDLE);

		if (!GetConsoleMode(handleIn, out uint originalConsoleMode))
		{
			Console.WriteLine("failed to get output console mode");
			return;
		}

		var consoleMode = (originalConsoleMode 
			| ENABLE_MOUSE_INPUT | ENABLE_WINDOW_INPUT)
			& ~ENABLE_QUICK_EDIT_MODE;
		if (!SetConsoleMode(handleIn, consoleMode))
		{
			Console.WriteLine("failed to set output console mode, error code: {GetLastError()}");
			return;
		}
	}

	static bool Run = false;
	public static void Start(Action<MouseEvent> mouse = null, Action<KeyEvent> key = null, Action<WindowEvent> window = null)
	{
		Run = true;
		new Thread(() => {
			while (true)
			{
				ReadInput(
					mouse is not null ? 
						(MOUSE_EVENT_RECORD m) => { }
						: null
					, key is not null ?
						(KEY_EVENT_RECORD k) => { }
						: null
					, window is not null ?
						(WINDOW_BUFFER_SIZE_RECORD w) => { }
						: null
					);
			}
		}).Start();
	}

	public static void ReadInput(
		Action<MOUSE_EVENT_RECORD>? mouse = null
		, Action<KEY_EVENT_RECORD>? key = null
		, Action<WINDOW_BUFFER_SIZE_RECORD>? window = null)
	{
		uint numRead = 0;
		INPUT_RECORD[] record = new INPUT_RECORD[1];
		record[0] = new INPUT_RECORD();
		ReadConsoleInput(handleIn, record, 1, ref numRead);

		switch (record[0].EventType)
		{
			case INPUT_RECORD.MOUSE_EVENT:
				mouse?.Invoke(record[0].MouseEvent);
				break;
			case INPUT_RECORD.KEY_EVENT:
				key?.Invoke(record[0].KeyEvent);
				break;
			case INPUT_RECORD.WINDOW_BUFFER_SIZE_EVENT:
				window?.Invoke(record[0].WindowBufferSizeEvent);
				break;
		}
	}

	public static class ConsoleListener
	{
		public static event ConsoleMouseEvent MouseEvent;

		public static event ConsoleKeyEvent KeyEvent;

		public static event ConsoleWindowBufferSizeEvent WindowBufferSizeEvent;

		private static bool Run = false;


		public static void Start()
		{
			if (!Run)
			{
				Run = true;
				IntPtr handleIn = GetStdHandle(STD_INPUT_HANDLE);
				new Thread(() => {
					while (true)
					{
						uint numRead = 0;
						INPUT_RECORD[] record = new INPUT_RECORD[1];
						record[0] = new INPUT_RECORD();
						ReadConsoleInput(handleIn, record, 1, ref numRead);
						if (Run)
							//ReadInput(MouseEvent, KeyEvent, WindowBufferSizeEvent);
							switch (record[0].EventType)
							{
								case INPUT_RECORD.MOUSE_EVENT:
									MouseEvent?.Invoke(record[0].MouseEvent);
									break;
								case INPUT_RECORD.KEY_EVENT:
									KeyEvent?.Invoke(record[0].KeyEvent);
									break;
								case INPUT_RECORD.WINDOW_BUFFER_SIZE_EVENT:
									WindowBufferSizeEvent?.Invoke(record[0].WindowBufferSizeEvent);
									break;
							}
						else
						{
							uint numWritten = 0;
							WriteConsoleInput(handleIn, record, 1, ref numWritten);
							return;
						}
					}
				}).Start();
			}
		}

		public static void Stop() => Run = false;


		public delegate void ConsoleMouseEvent(MOUSE_EVENT_RECORD r);

		public delegate void ConsoleKeyEvent(KEY_EVENT_RECORD r);

		public delegate void ConsoleWindowBufferSizeEvent(WINDOW_BUFFER_SIZE_RECORD r);

	}
}
