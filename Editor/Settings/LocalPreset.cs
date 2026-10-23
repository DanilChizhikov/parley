using UnityEngine;

namespace DTech.Parley.Editor
{
    internal enum LocalPreset : byte
    {
        [InspectorName("LM Studio")]
        LmStudio = 0,
        [InspectorName("Ollama")]
        Ollama = 1,
        [InspectorName("llama.cpp")]
        LlamaCpp = 2,
        [InspectorName("vLLM")]
        Vllm = 3,
        [InspectorName("Custom (OpenAI-compatible)")]
        Custom = 4,
    }
}