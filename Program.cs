using System.Net;
using LivrePrintAgent;

// Uma única instância por usuário do Windows. Ao reiniciar, a nova cópia espera a anterior fechar.
var restarting = args.Contains(AgentArgs.Restart);
using var mutex = new Mutex(initiallyOwned: false, name: "LivrePrintAgent.SingleInstance");

bool isFirstInstance;
try
{
    isFirstInstance = mutex.WaitOne(restarting ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
}
catch (AbandonedMutexException)
{
    // A cópia anterior terminou sem soltar o mutex (ex.: foi encerrada): a posse passa para esta.
    isFirstInstance = true;
}

if (!isFirstInstance)
{
    return;
}

ApplicationConfiguration.Initialize();

var config = AgentConfig.Load();
var isFirstRun = !File.Exists(Path.Combine(Log.Directory, "agent.log"));

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders();
// Escuta SOMENTE no endereço local: outros computadores da rede não conseguem falar com o agente.
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, config.Port));

var app = builder.Build();
AgentApi.Map(app, config);

try
{
    await app.StartAsync();
}
catch (Exception ex)
{
    Log.Error($"Não foi possível iniciar na porta {config.Port}: {ex.Message}");
    MessageBox.Show(
        $"Não foi possível iniciar o agente de impressão na porta {config.Port}.\n\n{ex.Message}\n\nA porta pode estar em uso por outro programa. Troque-a em agent.config.json.",
        AgentApi.AppName,
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
    return;
}

// Aquece a lista de impressoras (a primeira consulta ao Windows costuma ser a mais lenta).
_ = Task.Run(() =>
{
    try
    {
        RawPrinter.ListPrinters();
        RawPrinter.DefaultPrinter();
    }
    catch (Exception ex)
    {
        Log.Error($"Falha ao listar as impressoras na inicialização: {ex.Message}");
    }
});

Log.Info($"Agente iniciado na porta {config.Port}. Origens permitidas: {string.Join(", ", config.AllowedOrigins)}");

if (isFirstRun)
{
    TrayApp.SetAutoStart(true);
}

var tray = new TrayApp(config.Port, () => app.StopAsync().GetAwaiter().GetResult());
if (isFirstRun)
{
    tray.ShowFirstRunNotice(config.Port);
}

Application.Run(tray);
