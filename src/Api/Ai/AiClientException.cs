namespace Api.Ai;

/// <summary>Falha ao obter resposta da IA (erro HTTP, timeout, resposta vazia ou bloqueada).</summary>
public class AiClientException : Exception
{
    public AiClientException(string message) : base(message)
    {
    }

    public AiClientException(string message, Exception inner) : base(message, inner)
    {
    }
}
