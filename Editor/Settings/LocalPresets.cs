namespace DTech.Parley.Editor
{
    internal static class LocalPresets
    {
        public static string DefaultBaseUrl(LocalPreset preset)
        {
            return preset switch
            {
                LocalPreset.LmStudio => "http://localhost:1234/v1",
                LocalPreset.Ollama => "http://localhost:11434/v1",
                LocalPreset.LlamaCpp => "http://localhost:8080/v1",
                LocalPreset.Vllm => "http://localhost:8000/v1",
                _ => "http://localhost:8080/v1",
            };
        }

        public static string DisplayName(LocalPreset preset)
        {
            return preset switch
            {
                LocalPreset.LmStudio => "LM Studio",
                LocalPreset.Ollama => "Ollama",
                LocalPreset.LlamaCpp => "llama.cpp",
                LocalPreset.Vllm => "vLLM",
                _ => "Custom (OpenAI-compatible)",
            };
        }
    }
}