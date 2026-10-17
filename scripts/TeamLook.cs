using Godot;

/// <summary>
/// A fighter in his team's colour, in a team fight (Eric, 2026-10-17): not every colour changed,
/// just enough that he reads as red, blue or green from across the stage. Each fighter names the
/// colour that is most him (FighterData.TeamHue) - EdgeLord's red, Circy's yellow, Lugnut's vest -
/// and that hue is turned to the team's, keeping its light and shade; a fighter who is mostly
/// white or grey (Triguy, DoomBot, Flambe's jacket) has those tinted instead. On the team that is
/// already his colour, he stays exactly as drawn. Black is never touched, so not one line of a
/// drawing changes: it is the colouring in that changes, the way a kid might colour the same
/// drawing twice.
/// </summary>
public static class TeamLook
{
	const string Code = @"
shader_type canvas_item;

uniform float key_hue = -1.0;
uniform float hue_width = 0.05;
uniform float min_sat = 0.35;
uniform float team_hue = 0.0;
uniform float grey_min = 2.0;
uniform float grey_max = 2.0;
uniform float grey_amount = 0.0;
uniform vec3 team_rgb = vec3(1.0);

vec3 rgb2hsv(vec3 c) {
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec4 c = COLOR;
	vec3 hsv = rgb2hsv(clamp(c.rgb, 0.0, 1.0));
	vec3 rgb = c.rgb;
	if (key_hue >= 0.0) {
		float d = fract(hsv.x - key_hue + 0.5) - 0.5;
		float w = (1.0 - smoothstep(hue_width * 0.7, hue_width, abs(d)))
			* smoothstep(min_sat - 0.08, min_sat + 0.04, hsv.y)
			* smoothstep(0.12, 0.22, hsv.z);
		vec3 turned = hsv2rgb(vec3(fract(team_hue + d * 0.6), hsv.y, hsv.z));
		rgb = mix(rgb, turned, w);
	}
	if (grey_amount > 0.0) {
		float g = (1.0 - smoothstep(0.12, 0.22, hsv.y))
			* smoothstep(grey_min - 0.05, grey_min + 0.03, hsv.z)
			* (1.0 - smoothstep(grey_max - 0.03, grey_max + 0.05, hsv.z));
		rgb = mix(rgb, rgb * team_rgb, g * grey_amount);
	}
	COLOR = vec4(rgb, c.a);
}
";

	static Shader shader;

	/// <summary>The material that colours <paramref name="data"/> for <paramref name="team"/>, or null when he looks the same on it.</summary>
	public static ShaderMaterial For(FighterData data, int team)
	{
		Color tint = MenuTheme.TeamTint(team);
		float teamHue = tint.H;
		float key = data.TeamHue < 0.0f ? -1.0f : Mathf.PosMod(data.TeamHue, 360.0f) / 360.0f;
		float width = data.TeamHueWidth / 360.0f;
		// Already his team's colour: his own colour stays as drawn.
		if (key >= 0.0f && Mathf.Abs(Mathf.PosMod(teamHue - key + 0.5f, 1.0f) - 0.5f) < width) key = -1.0f;
		bool greys = data.TeamGreyAmount > 0.0f;
		if (key < 0.0f && !greys) return null;

		shader ??= new Shader { Code = Code };
		var material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter("key_hue", key);
		material.SetShaderParameter("hue_width", width);
		material.SetShaderParameter("min_sat", data.TeamMinSaturation);
		material.SetShaderParameter("team_hue", teamHue);
		// Tinting toward the colour at its brightest, so a white body goes the team's pale colour.
		float top = Mathf.Max(tint.R, Mathf.Max(tint.G, tint.B));
		material.SetShaderParameter("team_rgb", new Vector3(tint.R / top, tint.G / top, tint.B / top));
		material.SetShaderParameter("grey_min", greys ? data.TeamGreyFrom : 2.0f);
		material.SetShaderParameter("grey_max", greys ? data.TeamGreyTo : 2.0f);
		material.SetShaderParameter("grey_amount", data.TeamGreyAmount);
		return material;
	}

	/// <summary>Puts <paramref name="material"/> on every drawn part under <paramref name="root"/>.</summary>
	public static void Wear(Node root, Material material)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is Sprite2D sprite) sprite.Material = material;
			Wear(child, material);
		}
	}
}
