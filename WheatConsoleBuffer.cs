namespace Wheat;

public class WheatConsoleBuffer
{
	protected ANSICharacter[] data = Array.Empty<ANSICharacter>();
	protected vec2i size = vec2i.zero;


	public ANSICharacter this[vec2i p] {
		get => data[p.x + p.y * size.x];
		set => data[p.x + p.y * size.x] = value;
	}


	public WheatConsoleBuffer()
	{
	}

	public WheatConsoleBuffer(vec2i size)
	{
		Resize(size);
	}

	public void Resize(vec2i newSize)
	{
		if (newSize == size)
			return;

		var oldData = data;
		var minSize = vec2i.Min(size, newSize);
		data = new ANSICharacter[newSize.x * newSize.y];
		for (int y = 0, oi = 0, di = 0; y < minSize.y; y++, oi += size.x, di += newSize.x)
		{
			Array.Copy(oldData, oi, data, di, minSize.x);
		}

		size = newSize;
	}
}
