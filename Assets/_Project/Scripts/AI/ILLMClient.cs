using System;

public interface ILLMClient
{
    void SendPrompt(string prompt, Action<string> onResponse);
}
