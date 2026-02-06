using System.CommandLine;
using System.Text;

namespace AlienDictionaryCli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var wordsOption = new Option<string[]>(
            name: "--words",
            description: "Lista de palavras (na ordem do dicionário alien). Ex: --words wrt wrf er ett rftt")
        { AllowMultipleArgumentsPerToken = true };

        var fileOption = new Option<FileInfo?>(
            name: "--file",
            description: "Arquivo texto com 1 palavra por linha, na ordem do dicionário alien.");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Mostra detalhes do grafo e do processamento.");

        var root = new RootCommand("Alien Dictionary CLI - Inferir ordem do alfabeto desconhecido via grafo + ordenação topológica.")
        {
            wordsOption,
            fileOption,
            verboseOption
        };

        root.SetHandler(async (string[] words, FileInfo? file, bool verbose) =>
        {
            var inputWords = await LoadWordsAsync(words, file);

            if (inputWords.Count == 0)
            {
                Console.Error.WriteLine("Nenhuma palavra fornecida. Use --words ou --file.");
                Environment.ExitCode = 2;
                return;
            }

            var result = AlienDictionarySolver.InferAlphabetOrder(inputWords, verbose);

            if (!result.Success)
            {
                Console.Error.WriteLine($"ERRO: {result.ErrorMessage}");
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine(result.Order);
            Environment.ExitCode = 0;

        }, wordsOption, fileOption, verboseOption);

        return await root.InvokeAsync(args);
    }

    private static async Task<List<string>> LoadWordsAsync(string[] wordsArg, FileInfo? file)
    {
        // Prioriza --file se vier
        if (file is not null)
        {
            if (!file.Exists)
                throw new FileNotFoundException("Arquivo não encontrado.", file.FullName);

            var lines = await File.ReadAllLinesAsync(file.FullName);
            return lines
                .Select(NormalizeWord)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }

        return (wordsArg ?? Array.Empty<string>())
            .Select(NormalizeWord)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();
    }

    private static string NormalizeWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        // O desafio nao distingue maiusculas/minusculas.
        return word.Trim().ToLowerInvariant();
    }
}

public static class AlienDictionarySolver
{
    public sealed record Result(bool Success, string Order, string ErrorMessage)
    {
        public static Result Ok(string order) => new(true, order, "");
        public static Result Fail(string message) => new(false, "", message);
    }

    public static Result InferAlphabetOrder(IReadOnlyList<string> words, bool verbose = false)
    {
        // 1) Coleta todos os símbolos (chars) presentes
        var allChars = new HashSet<char>();
        foreach (var w in words)
            foreach (var c in w)
                allChars.Add(c);

        // 2) Monta grafo e indegree
        var graph = new Dictionary<char, HashSet<char>>();
        var indegree = new Dictionary<char, int>();

        foreach (var c in allChars)
        {
            graph[c] = new HashSet<char>();
            indegree[c] = 0;
        }

        // 3) Extrai restrições comparando palavras adjacentes
        for (int i = 0; i < words.Count - 1; i++)
        {
            var w1 = words[i];
            var w2 = words[i + 1];

            // Prefix conflict: w1 maior e começa com w2 -> inválido
            if (w1.Length > w2.Length && w1.StartsWith(w2, StringComparison.Ordinal))
                return Result.Fail($"Conflito de prefixo: \"{w1}\" vem antes de \"{w2}\", mas \"{w2}\" é prefixo de \"{w1}\".");

            var minLen = Math.Min(w1.Length, w2.Length);
            for (int j = 0; j < minLen; j++)
            {
                var c1 = w1[j];
                var c2 = w2[j];

                if (c1 == c2) continue;

                // Primeira diferença define ordem: c1 -> c2
                if (!graph[c1].Contains(c2))
                {
                    graph[c1].Add(c2);
                    indegree[c2]++;
                }
                break; // importante: só a primeira diferença conta
            }
        }

        if (verbose)
        {
            Console.Error.WriteLine("== Grafo (arestas) ==");
            foreach (var (from, tos) in graph.OrderBy(k => k.Key))
            {
                if (tos.Count == 0) continue;
                Console.Error.WriteLine($"{from} -> {string.Join(", ", tos)}");
            }

            Console.Error.WriteLine("== Indegree ==");
            foreach (var kv in indegree.OrderBy(k => k.Key))
                Console.Error.WriteLine($"{kv.Key}: {kv.Value}");
        }

        // 4) Ordenação topológica (Kahn)
        // Para tornar determinístico: usamos SortedSet pra sempre pegar o menor char disponível
        var queue = new SortedSet<char>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        var sb = new StringBuilder();
        while (queue.Count > 0)
        {
            var current = queue.Min;
            queue.Remove(current);

            sb.Append(current);

            foreach (var next in graph[current])
            {
                indegree[next]--;
                if (indegree[next] == 0)
                    queue.Add(next);
            }
        }

        // 5) Se não consumiu todos, tem ciclo
        if (sb.Length != allChars.Count)
            return Result.Fail("Existe um ciclo nas restrições. Não há ordem válida para o alfabeto.");

        return Result.Ok(sb.ToString());
    }
}
