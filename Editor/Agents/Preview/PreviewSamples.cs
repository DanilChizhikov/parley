#if PARLEY_DEBUGG
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Agents.Preview
{
	internal static class PreviewSamples
	{
		public const string Account = "preview@example.com · Max";
		public const string ScriptPath = "Assets/Scripts/Player/PlayerHealth.cs";
		public const string ConfigPath = "Assets/Scripts/Player/PlayerConfig.cs";

		public const string Markdown = "# Heading 1\n## Heading 2\n### Heading 3\n\n"
			+ @"Regular text with **bold**, *italic*, `inline code`, a [link](https://docs.unity3d.com) and a file reference [PlayerHealth.cs:12](Assets/Scripts/Player/PlayerHealth.cs:12).

- Bullet item
- Another item with a longer line that wraps when the window is narrow enough to show how list items wrap under their bullet
  - Nested item
1. Numbered item
2. Second numbered item

> A quote: tips and caveats from the agent look like this.

| Setting | Value | Note |
|---|---|---|
| Max HP | 100 | Base value |
| Regen | 2.5 | Per second |

```csharp
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100.0f;

    public void TakeDamage(float amount)
    {
        _current = Mathf.Max(0.0f, _current - amount);
    }
}
```

---

Short closing paragraph after a horizontal rule.";

		public const string Reply = "Preview mode: no model was called. Type `/` to pick a scenario, or use **Preview ▾** in the toolbar.\n\nYou wrote:\n\n> ";

		public const string Thinking = "The user wants the damage clamped so health never goes negative. PlayerHealth.TakeDamage subtracts without a lower bound. "
			+ "I should read the file first, then propose a one-line change with Mathf.Max, and check the tests in Assets/Tests for TakeDamage.";

		public const string ReadResult = @"using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100.0f;

    private float _current;

    public void TakeDamage(float amount)
    {
        _current -= amount;
    }
}";

		public const string GrepResult = "Assets/Scripts/Player/PlayerHealth.cs:9:    public void TakeDamage(float amount)\n"
			+ "Assets/Scripts/Enemies/EnemyAttack.cs:31:        _target.TakeDamage(_damage);\n"
			+ "Assets/Tests/PlayerHealthTests.cs:18:        health.TakeDamage(150.0f);";

		public const string GitStatus = " M Assets/Scripts/Player/PlayerHealth.cs\n?? Assets/Scripts/Player/PlayerConfig.cs";

		public const string TestFailure = "error CS0103: The name '_curent' does not exist in the current context\n"
			+ "Assets/Scripts/Player/PlayerHealth.cs(11,9)\n\nCompilation failed: 1 error(s), 0 warning(s)";

		public const string OldMethod = "    public void TakeDamage(float amount)\n    {\n        _current -= amount;\n    }";

		public const string NewMethod = "    public void TakeDamage(float amount)\n    {\n        _current = Mathf.Max(0.0f, _current - amount);\n        OnDamaged?.Invoke(amount);\n    }";

		public const string ConfigContent = @"using UnityEngine;

[CreateAssetMenu(menuName = ""Game/Player Config"")]
public sealed class PlayerConfig : ScriptableObject
{
    [SerializeField] private float _maxHealth = 100.0f;
    [SerializeField] private float _regenPerSecond = 2.5f;

    public float MaxHealth => _maxHealth;
    public float RegenPerSecond => _regenPerSecond;
}";

		public const string Patch = "@@ -9,4 +9,4 @@\n     public void TakeDamage(float amount)\n     {\n-        _current -= amount;\n+        _current = Mathf.Max(0.0f, _current - amount);\n     }";

		public const string Plan = @"## Clamp player damage

1. **PlayerHealth.TakeDamage**: clamp with `Mathf.Max(0, …)` and raise `OnDamaged`.
2. **PlayerConfig**: new ScriptableObject with max health and regeneration.
3. **Tests**: add `TakeDamage_NeverBelowZero` to `PlayerHealthTests`.

Files: `PlayerHealth.cs`, `PlayerConfig.cs` (new), `PlayerHealthTests.cs`.";

		public static JObject Questions()
		{
			return new JObject
			{
				["questions"] = new JArray
				{
					new JObject
					{
						["header"] = "Storage",
						["question"] = "Where should the health values live?",
						["multiSelect"] = false,
						["options"] = new JArray
						{
							new JObject
							{
								["label"] = "ScriptableObject (Recommended)",
								["description"] = "One PlayerConfig asset shared by every player prefab.",
								["preview"] = "```csharp\n[CreateAssetMenu]\npublic sealed class PlayerConfig : ScriptableObject\n{\n    public float MaxHealth = 100;\n}\n```",
							},
							new JObject
							{
								["label"] = "Serialized fields",
								["description"] = "Keep the values on the PlayerHealth component.",
								["preview"] = "```csharp\n[SerializeField] private float _maxHealth = 100;\n```",
							},
						},
					},
					new JObject
					{
						["header"] = "Events",
						["question"] = "Which events should PlayerHealth raise?",
						["multiSelect"] = true,
						["options"] = new JArray
						{
							new JObject { ["label"] = "OnDamaged", ["description"] = "Every time damage is taken." },
							new JObject { ["label"] = "OnHealed" },
							new JObject { ["label"] = "OnDied", ["description"] = "Once, when health reaches zero." },
						},
					},
				},
			};
		}

		public static JArray Todos(int completed, int inProgress)
		{
			string[] items =
			{
				"Read PlayerHealth and its tests",
				"Clamp damage in TakeDamage",
				"Create PlayerConfig asset",
				"Run the edit mode tests",
			};

			JArray todos = new JArray();
			for (int i = 0; i < items.Length; i++)
			{
				string status = i < completed ? "completed" : i < completed + inProgress ? "in_progress" : "pending";
				todos.Add(new JObject { ["content"] = items[i], ["status"] = status, ["activeForm"] = items[i] + "…" });
			}

			return todos;
		}

		public static JArray BashSuggestions()
		{
			return new JArray
			{
				new JObject
				{
					["type"] = "addRules",
					["behavior"] = "allow",
					["destination"] = "localSettings",
					["rules"] = new JArray { new JObject { ["toolName"] = "Bash", ["ruleContent"] = "git push:*" } },
				},
			};
		}
	}
}
#endif
