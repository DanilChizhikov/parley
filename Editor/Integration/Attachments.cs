using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor
{
	internal static class Attachments
	{
		private const int MaxTextFileBytes = 256 * 1024;
		private const int MaxImageBytes = 8 * 1024 * 1024;
		private const int ScreenshotMaxSide = 1600;

		private static readonly HashSet<string> ImageExtensions = new (StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

		public static ChatAttachment FromSelection()
		{
			UnityEngine.Object[] selected = Selection.objects;
			if (selected.Length == 0)
			{
				return null;
			}

			StringBuilder builder = new StringBuilder();
			foreach (UnityEngine.Object target in selected)
			{
				builder.Append("- ").AppendLine(UnityObjectPaths.Describe(target));
			}

			return new ChatAttachment { Kind = AttachmentKind.Text, Label = "Selection (" + selected.Length + ")", Text = builder.ToString().TrimEnd() };
		}

		public static ChatAttachment FromConsoleErrors(int limit = 20)
		{
			List<ConsoleEntry> entries = ConsoleReader.Read(limit, ConsoleSeverity.Error);
			if (entries.Count == 0)
			{
				return null;
			}

			StringBuilder builder = new StringBuilder();
			foreach (ConsoleEntry entry in entries)
			{
				builder.Append("[Error] ").AppendLine(entry.Message);
				if (!string.IsNullOrEmpty(entry.StackTrace))
				{
					string[] lines = entry.StackTrace.Split('\n');
					for (int i = 0; i < lines.Length && i < 8; i++)
					{
						builder.Append("    ").AppendLine(lines[i].Trim());
					}
				}
			}

			return new ChatAttachment { Kind = AttachmentKind.Text, Label = "Console errors (" + entries.Count + ")", Text = builder.ToString().TrimEnd() };
		}

		public static ChatAttachment FromSceneView()
		{
			SceneView view = SceneView.lastActiveSceneView;
			if (view == null || view.camera == null)
			{
				return null;
			}

			Rect rect = view.position;
			return Capture(view.camera, new Vector2Int((int)rect.width, (int)rect.height), "Scene view");
		}

		public static ChatAttachment FromGameCamera()
		{
			Camera camera = Camera.main;
			if (camera == null)
			{
				Camera[] cameras = Camera.allCameras;
				camera = cameras.Length > 0 ? cameras[0] : null;
			}

			return camera == null ? null : Capture(camera, new Vector2Int(1280, 720), "Game camera (" + camera.name + ")");
		}

		public static ChatAttachment FromPath(string path)
		{
			string full = ProjectPaths.Resolve(path);
			if (!File.Exists(full))
			{
				return null;
			}

			string extension = Path.GetExtension(full);
			if (ImageExtensions.Contains(extension))
			{
				byte[] bytes = File.ReadAllBytes(full);
				if (bytes.Length > MaxImageBytes)
				{
					return null;
				}

				return new ChatAttachment
				{
					Kind = AttachmentKind.Image,
					Label = Path.GetFileName(full),
					MediaType = MediaType(extension),
					Base64 = Convert.ToBase64String(bytes),
				};
			}

			string relative = ProjectPaths.ToProjectRelative(full);
			if (new FileInfo(full).Length > MaxTextFileBytes)
			{
				return new ChatAttachment { Kind = AttachmentKind.Text, Label = relative, Text = "(file is large; read it with tools: " + full + ")" };
			}

			return new ChatAttachment { Kind = AttachmentKind.Text, Label = relative, Text = File.ReadAllText(full) };
		}

		public static string MediaType(string extension)
		{
			switch (extension.ToLowerInvariant())
			{
				case ".jpg":
				case ".jpeg":
					return "image/jpeg";
				case ".gif":
					return "image/gif";
				case ".webp":
					return "image/webp";
				default:
					return "image/png";
			}
		}

		private static ChatAttachment Capture(Camera camera, Vector2Int size, string label)
		{
			if (size.x <= 0 || size.y <= 0)
			{
				return null;
			}

			float scale = Mathf.Min(1.0f, ScreenshotMaxSide / (float)Mathf.Max(size.x, size.y));
			int width = Mathf.Max(16, (int)(size.x * scale));
			int height = Mathf.Max(16, (int)(size.y * scale));
			RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
			RenderTexture previousTarget = camera.targetTexture;
			RenderTexture previousActive = RenderTexture.active;
			Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
			try
			{
				camera.targetTexture = target;
				camera.Render();
				RenderTexture.active = target;
				texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
				texture.Apply();
				return new ChatAttachment
				{
					Kind = AttachmentKind.Image,
					Label = label,
					MediaType = "image/png",
					Base64 = Convert.ToBase64String(texture.EncodeToPNG()),
				};
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Parley] Screenshot failed: " + exception.Message);
				return null;
			}
			finally
			{
				camera.targetTexture = previousTarget;
				RenderTexture.active = previousActive;
				RenderTexture.ReleaseTemporary(target);
				UnityEngine.Object.DestroyImmediate(texture);
			}
		}
	}
}