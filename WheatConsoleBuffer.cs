namespace Wheat;

public class WheatConsoleBuffer
{
	protected ANSICharacter[] data = Array.Empty<ANSICharacter>();
	public vec2i size = vec2i.zero;


	public ANSICharacter this[vec2i p] {
		get => data[p.x + p.y * size.x];
		set { if (p < size.wh()) data[p.x + p.y * size.x] = value; }
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

	internal void Scroll(vec2i position)
	{
		var delta = position - size.wh();

		var ex = delta.x > 0 ? Range(0, delta.x) : Range(0, -delta.x);
		var ey = delta.y > 0 ? Range(0, delta.y) : Range(0, -delta.y);

		if (delta.x == 0)
		{
			if (delta.y > 0) {
				//for (int i )
			}
			else if (delta.y < 0)
			{
			}
		}
	}
}
