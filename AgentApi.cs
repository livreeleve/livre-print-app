using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LivrePrintAgent;

public sealed record PrintRawRequest(
    [property: JsonPropertyName("printer")] string? Printer,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("dataBase64")] string? DataBase64);

public static class AgentApi
{
    public const string AppName = "Livre Print Agent";

    public static void Map(WebApplication app, AgentConfig config)
    {
        var allowedOrigins = new HashSet<string>(config.AllowedOrigins, StringComparer.OrdinalIgnoreCase);

        app.Use(async (context, next) =>
        {
            // Protege contra DNS rebinding: só atende pedidos feitos para o endereço local.
            var host = context.Request.Host.Host;
            if (!host.Equals("127.0.0.1", StringComparison.Ordinal) && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            // Só os sistemas listados em agent.config.json podem usar o agente: qualquer outro site (que o
            // navegador identifica pelo cabeçalho Origin) é recusado ANTES de imprimir.
            var origin = context.Request.Headers.Origin.ToString().TrimEnd('/');
            if (origin.Length > 0)
            {
                if (!allowedOrigins.Contains(origin))
                {
                    Log.Error($"Origem recusada: {origin}");
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { message = $"Origem não autorizada: {origin}. Adicione-a em agent.config.json." });
                    return;
                }

                var headers = context.Response.Headers;
                headers["Access-Control-Allow-Origin"] = origin;
                headers["Vary"] = "Origin";
                headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                headers["Access-Control-Allow-Headers"] = "Content-Type";
                // O Chrome exige isso para um site público falar com o endereço local (Private Network Access).
                headers["Access-Control-Allow-Private-Network"] = "true";
                headers["Access-Control-Max-Age"] = "600";
            }

            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await next();
        });

        app.MapGet("/status", () => Results.Ok(new
        {
            app = AppName,
            version = typeof(AgentApi).Assembly.GetName().Version?.ToString(3),
            machine = Environment.MachineName,
        }));

        app.MapGet("/printers", () => Results.Ok(new
        {
            printers = RawPrinter.ListPrinters(),
            defaultPrinter = RawPrinter.DefaultPrinter(),
        }));

        app.MapPost("/print/raw", (HttpContext context, PrintRawRequest request) =>
        {
            // Exigir JSON obriga o navegador a fazer a checagem prévia (preflight) entre sites diferentes.
            if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
            {
                return Results.BadRequest(new { message = "Content-Type deve ser application/json." });
            }

            if (string.IsNullOrWhiteSpace(request.Printer))
            {
                return Results.BadRequest(new { message = "Informe a impressora." });
            }

            byte[] data;
            try
            {
                data = !string.IsNullOrEmpty(request.DataBase64)
                    ? Convert.FromBase64String(request.DataBase64)
                    : Encoding.UTF8.GetBytes(request.Text ?? string.Empty);
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { message = "dataBase64 inválido." });
            }

            if (data.Length == 0)
            {
                return Results.BadRequest(new { message = "Nada para imprimir." });
            }

            try
            {
                var timer = Stopwatch.StartNew();
                RawPrinter.Send(request.Printer, data);
                timer.Stop();

                // O tempo fica no log e na resposta, para investigar se a impressão estiver demorando.
                Log.Info($"Impresso em \"{request.Printer}\": {data.Length} bytes em {timer.ElapsedMilliseconds} ms");
                return Results.Ok(new { ok = true, bytes = data.Length, elapsedMs = timer.ElapsedMilliseconds });
            }
            catch (PrinterNotFoundException ex)
            {
                Log.Error(ex.Message);
                return Results.NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error($"Falha ao imprimir em \"{request.Printer}\": {ex.Message}");
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
