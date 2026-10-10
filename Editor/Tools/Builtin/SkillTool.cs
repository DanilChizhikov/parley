using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DTech.Parley.Editor.Skills;
using Newtonsoft.Json.Linq;

namespace DTech.Parley.Editor.Tools.Builtin
{
    internal sealed class SkillTool : IParleyTool
    {
        public const string ToolName = "Skill";

        private readonly Func<IReadOnlyList<SkillInfo>> _skills;

        public string Name => ToolName;

        public string Description => "Load the instructions of a skill listed under # Skills in the system prompt. Call it before a task that matches the skill, then follow the returned instructions.";

        public JObject InputSchema { get; } = new SchemaBuilder()
            .String("skill", "Skill name, exactly as listed.", true)
            .String("args", "Optional arguments for the skill.")
            .Build();

        public ToolKind Kind => ToolKind.ReadOnly;

        public SkillTool(Func<IReadOnlyList<SkillInfo>> skills)
        {
            _skills = skills;
        }

        public static string Load(SkillInfo skill, string arguments)
        {
            string body = SkillScanner.ReadBody(skill);
            return SkillRenderer.Render(body, Path.GetDirectoryName(skill.Path), arguments);
        }

        public string Summarize(JObject input)
        {
            string arguments = ToolInput.String(input, "args", string.Empty);
            return ToolInput.String(input, "skill", string.Empty) + (string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments);
        }

        public string TargetPath(JObject input) => null;

        public Task<ToolResult> ExecuteAsync(JObject input, ToolContext context, CancellationToken cancellationToken)
        {
            string name = ToolInput.String(input, "skill", string.Empty).Trim().TrimStart('/');
            foreach (SkillInfo skill in _skills())
            {
                if (skill.Name != name)
                {
                    continue;
                }

                try
                {
                    string text = Load(skill, ToolInput.String(input, "args", string.Empty));
                    return Task.FromResult(ToolResult.Ok("Base directory for this skill: " + Path.GetDirectoryName(skill.Path) + "\n\n" + text));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    return Task.FromResult(ToolResult.Error("Could not read skill '" + name + "': " + exception.Message));
                }
            }

            return Task.FromResult(ToolResult.Error("Unknown skill '" + name + "'."));
        }
    }
}
