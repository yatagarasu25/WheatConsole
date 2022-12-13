using System.Text;

namespace Wheat;

public record class WheatImage(string[] lines, vec2i size)
{
	public static WheatImage LoadWheatImage(colorb[] colors, vec2i size, string pixels = " .,:;i1tfLCG08@")
	{
		var stride = size.x;
		var llines = new List<string>();
		StringBuilder line = new StringBuilder();
		int nl = stride;
		foreach (var c in colors)
		{
			var i = (c.intencity - 0.0001f).Clamp0();
			var p = pixels[(int)(i * pixels.Length)];

			line.Append($"{p}".Color(System.Drawing.Color.FromArgb(c.a, c.r, c.g, c.b)));
			if (--nl == 0)
			{
				llines.Add(line.ToString());
				line.Clear();
				nl = stride;
			}
		}

		return new(llines.ToArray(), size);
	}
}
