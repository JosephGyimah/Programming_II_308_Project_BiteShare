namespace BiteShare.Api.Services;

public interface IJoinCodeGenerator
{
    /// <summary>Short, human-typeable code (e.g. "BITE-4F7Q") for joining a session.</summary>
    string Generate();
}

public class JoinCodeGenerator : IJoinCodeGenerator
{
    // Excludes ambiguous characters (0/O, 1/I/L) so it's easy to read aloud/type on a phone.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private static readonly Random Random = new();

    public string Generate()
    {
        Span<char> code = stackalloc char[6];
        for (var i = 0; i < code.Length; i++)
            code[i] = Alphabet[Random.Next(Alphabet.Length)];
        return new string(code);
    }
}
