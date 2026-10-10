using System.Collections.Generic;
using DTech.Parley.Editor.Agents.Local;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DTech.Parley.Tests.EditorMode
{
    public sealed class ContextBudgetTests
    {
        [Test]
        public void ElidesOldToolOutputFirst()
        {
            List<JObject> history = new ()
            {
                new JObject { ["role"] = "user", ["content"] = "start" },
                new JObject { ["role"] = "assistant", ["content"] = "", ["tool_calls"] = new JArray() },
                new JObject { ["role"] = "tool", ["tool_call_id"] = "a", ["content"] = new string('x', 7000) },
            };

            for (int i = 0; i < 6; i++)
            {
                history.Add(new JObject { ["role"] = i % 2 == 0 ? "user" : "assistant", ["content"] = "short " + i });
            }

            List<JObject> fitted = new ContextBudget().Fit(history, 500, out int estimate);
            Assert.AreEqual(ContextBudget.ElidedText, (string)fitted[2]["content"]);
            Assert.LessOrEqual(estimate, 500);
            Assert.AreEqual(new string('x', 7000), (string)history[2]["content"], "original history must not be modified");
        }
    }
}