using System.Runtime.InteropServices;

namespace Wheat;

public static class NativeFunctions
{
	public struct COORD
	{
		public short X;
		public short Y;

		public COORD(short x, short y)
		{
			X = x;
			Y = y;
		}

		public static implicit operator vec2i(COORD v)
			=> vec2i.xy(v.X, v.Y);
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct INPUT_RECORD
	{
		public const ushort KEY_EVENT = 0x0001,
			MOUSE_EVENT = 0x0002,
			WINDOW_BUFFER_SIZE_EVENT = 0x0004; //more

		[FieldOffset(0)]
		public ushort EventType;
		[FieldOffset(4)]
		public KEY_EVENT_RECORD KeyEvent;
		[FieldOffset(4)]
		public MOUSE_EVENT_RECORD MouseEvent;
		[FieldOffset(4)]
		public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
		/*
		and:
		 MENU_EVENT_RECORD MenuEvent;
		 FOCUS_EVENT_RECORD FocusEvent;
		 */
	}

	public struct MOUSE_EVENT_RECORD
	{
		public COORD dwMousePosition;

		public enum bs : uint
		{
			FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001,
			FROM_LEFT_2ND_BUTTON_PRESSED = 0x0004,
			FROM_LEFT_3RD_BUTTON_PRESSED = 0x0008,
			FROM_LEFT_4TH_BUTTON_PRESSED = 0x0010,
			RIGHTMOST_BUTTON_PRESSED = 0x0002
		}
		public bs dwButtonState;

		public enum ks : uint
		{
			CAPSLOCK_ON = 0x0080,
			ENHANCED_KEY = 0x0100,
			LEFT_ALT_PRESSED = 0x0002,
			LEFT_CTRL_PRESSED = 0x0008,
			NUMLOCK_ON = 0x0020,
			RIGHT_ALT_PRESSED = 0x0001,
			RIGHT_CTRL_PRESSED = 0x0004,
			SCROLLLOCK_ON = 0x0040,
			SHIFT_PRESSED = 0x0010
		}
		public ks dwControlKeyState;

		public const int DOUBLE_CLICK = 0x0002,
			MOUSE_HWHEELED = 0x0008,
			MOUSE_MOVED = 0x0001,
			MOUSE_WHEELED = 0x0004;
		public uint dwEventFlags;
	}

	[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
	public struct KEY_EVENT_RECORD
	{
		[FieldOffset(0)]
		public bool bKeyDown;
		[FieldOffset(4)]
		public ushort wRepeatCount;
		[FieldOffset(6)]
		public ushort wVirtualKeyCode;
		[FieldOffset(8)]
		public ushort wVirtualScanCode;
		[FieldOffset(10)]
		public char UnicodeChar;
		[FieldOffset(10)]
		public byte AsciiChar;

		public const int CAPSLOCK_ON = 0x0080,
			ENHANCED_KEY = 0x0100,
			LEFT_ALT_PRESSED = 0x0002,
			LEFT_CTRL_PRESSED = 0x0008,
			NUMLOCK_ON = 0x0020,
			RIGHT_ALT_PRESSED = 0x0001,
			RIGHT_CTRL_PRESSED = 0x0004,
			SCROLLLOCK_ON = 0x0040,
			SHIFT_PRESSED = 0x0010;
		[FieldOffset(12)]
		public uint dwControlKeyState;
	}

	public struct WINDOW_BUFFER_SIZE_RECORD
	{
		public COORD dwSize;
	}

	public const uint STD_INPUT_HANDLE = unchecked((uint)-10),
		STD_OUTPUT_HANDLE = unchecked((uint)-11),
		STD_ERROR_HANDLE = unchecked((uint)-12);

	[DllImport("kernel32.dll")]
	public static extern IntPtr GetStdHandle(uint nStdHandle);

	[DllImport("kernel32.dll")]
	public static extern uint GetLastError();

	public const uint ENABLE_MOUSE_INPUT = 0x0010,
		ENABLE_QUICK_EDIT_MODE = 0x0040,
		ENABLE_EXTENDED_FLAGS = 0x0080,
		ENABLE_ECHO_INPUT = 0x0004,
		ENABLE_WINDOW_INPUT = 0x0008; //more

	[DllImportAttribute("kernel32.dll")]
	public static extern bool GetConsoleMode(IntPtr hConsoleInput, out uint lpMode);

	[DllImportAttribute("kernel32.dll")]
	public static extern bool SetConsoleMode(IntPtr hConsoleInput, uint dwMode);


	[DllImportAttribute("kernel32.dll", CharSet = CharSet.Unicode)]
	public static extern bool ReadConsoleInput(IntPtr hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, uint nLength, ref uint lpNumberOfEventsRead);

	[DllImportAttribute("kernel32.dll", CharSet = CharSet.Unicode)]
	public static extern bool WriteConsoleInput(IntPtr hConsoleInput, INPUT_RECORD[] lpBuffer, uint nLength, ref uint lpNumberOfEventsWritten);

	[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
	[return: MarshalAs(UnmanagedType.Bool)] //          ̲┌──────────^
	public static extern bool ReadConsoleOutputCharacterA(
		IntPtr hStdout,   // result of 'GetStdHandle(-11)'
		out byte ch,      // A̲N̲S̲I̲ character result
		uint c_in,        // (set to '1')
		COORD coord_XY,    // screen location to read, X:loword, Y:hiword
		out uint c_out);  // (unwanted, discard)

	[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)] //          ̲┌────────────^
	public static extern bool ReadConsoleOutputCharacterW(
		IntPtr hStdout,   // result of 'GetStdHandle(-11)'
		out Char ch,      // U̲n̲i̲c̲o̲d̲e̲ character result
		uint c_in,        // (set to '1')
		COORD coord_XY,    // screen location to read, X:loword, Y:hiword
		out uint c_out);  // (unwanted, discard)
}
