using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

/// <summary>
/// Sends a fighter drawn in the game to ecec.dev - the same place the ecec.dev/draw form sends
/// to, the same fields, the same server (EricClancyEngineeringAndCrafts: intake/server.js) - so a
/// fighter drawn in the game arrives exactly like one sent from the website, and
/// tools/intake/pull_submissions.sh picks it up the same way.
///
/// The server's limits are the game's limits: at most 24 pictures, 12 MB each, 60 MB in all
/// (the droplet's nginx), and a few submissions an hour from one address. It saves each picture as
/// photo-1, photo-2... in the order sent, followed by the name it was sent with - so the game names
/// every picture after its page and layer (fighter-front-arm.png).
/// </summary>
public static class FighterIntake
{
	/// <summary>The site. DRAWFIGHT_INTAKE_URL points the game at another one - a local copy of the
	/// intake server, for testing without sending Eric a fighter.</summary>
	public const string Site = "https://ecec.dev";

	public const int MaxPictures = 24;
	public const int MaxPictureBytes = 12 * 1024 * 1024;

	/// <summary>Everything sent at once, kept under the droplet's 60 MB request cap with room to spare.</summary>
	public const int MaxSendBytes = 55 * 1024 * 1024;

	public enum Link { Unknown, Checking, Online, Offline }

	// Written by the worker thread, read by the main one; a plain int, so nothing to lock.
	static volatile int link;
	static ulong checkedAt;

	public static Link State => (Link)link;

	static string Base
	{
		get
		{
			string url = OS.GetEnvironment("DRAWFIGHT_INTAKE_URL");
			return string.IsNullOrEmpty(url) ? Site : url.TrimEnd('/');
		}
	}

	/// <summary>The site's name as a kid would read it: "ecec.dev".</summary>
	public static string Where => Base.Replace("https://", "").Replace("http://", "");

	static HttpClient Client(TimeSpan timeout)
	{
		var http = new HttpClient { Timeout = timeout };
		http.DefaultRequestHeaders.UserAgent.ParseAdd($"DrawFight/{UpdateScreen.CurrentVersion}");
		return http;
	}

	/// <summary>Asks the server whether it is there. Returns at once; <see cref="State"/> follows.</summary>
	public static void Check()
	{
		if (State == Link.Checking) return;
		link = (int)Link.Checking;
		checkedAt = Time.GetTicksMsec();
		string url = Base + "/api/draw/health";
		Task.Run(async () =>
		{
			try
			{
				using HttpClient http = Client(TimeSpan.FromSeconds(6));
				using HttpResponseMessage response = await http.GetAsync(url);
				link = (int)(response.IsSuccessStatusCode ? Link.Online : Link.Offline);
			}
			catch (Exception e)
			{
				GD.Print($"FighterIntake: cannot reach {url} ({e.GetType().Name})");
				link = (int)Link.Offline;
			}
		});
	}

	/// <summary>Checks again if the last check is older than <paramref name="seconds"/> - so a screen
	/// left open notices the internet coming back.</summary>
	public static void CheckEvery(float seconds)
	{
		if (State != Link.Checking && Time.GetTicksMsec() - checkedAt > seconds * 1000.0f) Check();
	}

	/// <summary>
	/// Sends the answers and pictures. Returns null when the server took it, or what went wrong,
	/// in words that can go straight on screen - the server's own message when it gave one.
	/// </summary>
	/// <param name="route">Where on the server: "/api/draw" for a fighter, "/api/draw/stage" for a
	/// stage, "/api/draw/suggest" for a suggestion (no pictures).</param>
	public static async Task<string> Send(IReadOnlyList<KeyValuePair<string, string>> fields,
		IReadOnlyList<(string file, byte[] png)> pictures, string route = "/api/draw")
	{
		try
		{
			using HttpClient http = Client(TimeSpan.FromMinutes(3));
			using var form = new MultipartFormDataContent();
			foreach (KeyValuePair<string, string> field in fields) form.Add(new StringContent(field.Value ?? ""), field.Key);
			foreach ((string file, byte[] png) in pictures)
			{
				var part = new ByteArrayContent(png);
				part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
				form.Add(part, "photos", file);
			}

			using HttpResponseMessage response = await http.PostAsync(Base + route, form);
			string body = await response.Content.ReadAsStringAsync();
			link = (int)Link.Online;
			if (response.IsSuccessStatusCode) return null;

			GD.Print($"FighterIntake: the server said {(int)response.StatusCode}: {body}");
			try
			{
				using JsonDocument doc = JsonDocument.Parse(body);
				if (doc.RootElement.TryGetProperty("error", out JsonElement error) && error.GetString() is string message)
				{
					return message;
				}
			}
			catch (JsonException)
			{
			}
			return $"Something went wrong ({(int)response.StatusCode}). Please try again.";
		}
		catch (Exception e)
		{
			GD.Print($"FighterIntake: sending failed ({e.GetType().Name}: {e.Message})");
			link = (int)Link.Offline;
			return "Could not send it. Check your internet and try again - your drawing is saved.";
		}
	}

	/// <summary>The connection, as a few words and a colour for a status light.</summary>
	public static (string words, Color light) Describe() => State switch
	{
		Link.Online => ($"Connected to {Where}", new Color(0.30f, 0.74f, 0.38f)),
		Link.Offline => ($"Can't reach {Where}", new Color(0.92f, 0.36f, 0.32f)),
		_ => ($"Checking {Where}...", new Color(0.95f, 0.72f, 0.30f)),
	};
}
