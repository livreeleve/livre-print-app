using System.Text.Json;

namespace LivrePrintAgent;

/// <summary>
/// Configuração lida de agent.config.json (na mesma pasta do executável).
/// allowedOrigins: endereços do sistema autorizados a imprimir (ex.: "https://seu-sistema.com.br").
/// </summary>
public sealed class AgentConfig
{
    /// <summary>Endereços do sistema sempre autorizados, mesmo que o agent.config.json do computador seja antigo.</summary>
    public static readonly string[] BuiltInOrigins = { "https://livrecore.livreeleve.net" };

    public int Port { get; set; } = 9777;
    public List<string> AllowedOrigins { get; set; } = new();

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "agent.config.json");

    public static AgentConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AgentConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (config is not null)
                {
                    config.AllowedOrigins = config.AllowedOrigins
                        .Concat(BuiltInOrigins)
                        .Select(origin => origin.Trim().TrimEnd('/'))
                        .Where(origin => origin.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Não foi possível ler {ConfigPath}: {ex.Message}");
        }

        return new AgentConfig { AllowedOrigins = BuiltInOrigins.ToList() };
    }
}
