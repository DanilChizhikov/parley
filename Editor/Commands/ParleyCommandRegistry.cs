using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DTech.Parley.Editor.Commands
{
    internal static class ParleyCommandRegistry
    {
        private static List<IParleyCommand> _commands;

        public static IReadOnlyList<IParleyCommand> All => _commands ??= Discover();

        public static void Refresh()
        {
            _commands = null;
        }

        public static IParleyCommand Find(string name)
        {
            return ParleyCommandParser.Find(All, name);
        }

        public static string Expand(string text)
        {
            return Expand(text, All);
        }

        public static string Expand(string text, IReadOnlyList<IParleyCommand> commands)
        {
            IParleyCommand command = ParleyCommandParser.Match(text, commands, out string arguments);
            if (command == null)
            {
                return null;
            }

            try
            {
                string prompt = command.BuildPrompt(new ParleyCommandRequest(arguments, ProjectPaths.Root));
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    Debug.LogWarning("[Parley] Command '" + command.Name + "' produced no prompt.");
                    return null;
                }

                return prompt;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Parley] Command '" + command.Name + "' failed: " + exception.Message);
                return null;
            }
        }

        public static void Merge(List<SlashCommandInfo> target, IReadOnlyList<SlashCommandInfo> backend)
        {
            if (target == null)
            {
                return;
            }

            target.Clear();
            IReadOnlyList<IParleyCommand> commands = All;
            foreach (IParleyCommand command in commands)
            {
                target.Add(new SlashCommandInfo
                {
                    Name = command.Name,
                    Description = command.Description,
                    ArgumentHint = command.ArgumentHint,
                    IsBuiltin = true,
                });
            }

            if (backend == null)
            {
                return;
            }

            foreach (SlashCommandInfo command in backend)
            {
                if (ParleyCommandParser.Find(commands, command.Name) == null)
                {
                    target.Add(command);
                }
            }
        }

        public static List<IParleyCommand> Build(IEnumerable<IParleyCommand> builtin, IEnumerable<IParleyCommand> external)
        {
            List<IParleyCommand> commands = new ();
            Add(commands, external, false);
            Add(commands, builtin, true);
            return commands;
        }

        private static void Add(List<IParleyCommand> commands, IEnumerable<IParleyCommand> source, bool builtin)
        {
            if (source == null)
            {
                return;
            }

            foreach (IParleyCommand command in source)
            {
                if (command == null)
                {
                    continue;
                }

                if (!ParleyCommandParser.IsValidName(command.Name))
                {
                    Debug.LogWarning("[Parley] Command " + command.GetType().FullName + " has an invalid name '" + command.Name
                        + "': use lowercase letters, digits and '-'.");
                    continue;
                }

                IParleyCommand existing = ParleyCommandParser.Find(commands, command.Name);
                if (existing != null)
                {
                    Debug.LogWarning(builtin
                        ? "[Parley] Built-in command '" + command.Name + "' is overridden by " + existing.GetType().FullName + "."
                        : "[Parley] Command '" + command.Name + "' already comes from " + existing.GetType().FullName
                            + ", ignoring " + command.GetType().FullName + ".");
                    continue;
                }

                commands.Add(command);
            }
        }

        private static List<IParleyCommand> Discover()
        {
            Assembly own = typeof(ParleyCommandRegistry).Assembly;
            List<Type> types = new (TypeCache.GetTypesDerivedFrom<IParleyCommand>());
            types.Sort((left, right) => string.CompareOrdinal(left.FullName, right.FullName));
            List<IParleyCommand> builtin = new ();
            List<IParleyCommand> external = new ();
            foreach (Type type in types)
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                bool isOwn = type.Assembly == own;
                if (!isOwn && !type.IsPublic)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                try
                {
                    IParleyCommand command = (IParleyCommand)Activator.CreateInstance(type);
                    (isOwn ? builtin : external).Add(command);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Parley] Could not create command " + type.FullName + ": " + exception.Message);
                }
            }

            return Build(builtin, external);
        }
    }
}
