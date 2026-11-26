using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace LivrePrintAgent;

/// <summary>Ícone na bandeja do Windows com o status do agente e algumas opções.</summary>
public sealed class TrayApp : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "LivrePrintAgent";

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _autoStart;
    private readonly Action _stopServer;

    public TrayApp(int port, Action stopServer)
    {
        _stopServer = stopServer;

        _autoStart = new ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true, Checked = IsAutoStartEnabled() };
        _autoStart.CheckedChanged += (_, _) => SetAutoStart(_autoStart.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem($"{AgentApi.AppName} — porta {port}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStart);
        menu.Items.Add("Abrir pasta de logs", null, (_, _) => Open(Log.Directory));
        menu.Items.Add("Abrir configuração (agent.config.json)", null, (_, _) => Open(AgentConfig.ConfigPath));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Reiniciar o agente", null, (_, _) => Restart());
        menu.Items.Add("Sair", null, (_, _) => Exit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = $"{AgentApi.AppName} (porta {port})",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    public void ShowFirstRunNotice(int port)
    {
        _icon.ShowBalloonTip(
            6000,
            AgentApi.AppName,
            $"Agente de impressão ativo na porta {port}. Ele inicia com o Windows (dá para desativar no ícone da bandeja).",
            ToolTipIcon.Info);
    }

    private static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error($"Não foi possível abrir {path}: {ex.Message}");
        }
    }

    // Ícone embutido no executável (o mesmo do arquivo .exe).
    private static Icon LoadIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico");
            if (stream is not null) return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch (Exception ex)
        {
            Log.Error($"Não foi possível carregar o ícone: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    /// <summary>Abre uma nova cópia do agente (que espera esta fechar) e encerra esta. Recarrega também o agent.config.json.</summary>
    private void Restart()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, AgentArgs.Restart) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Error($"Não foi possível reiniciar o agente: {ex.Message}");
            MessageBox.Show($"Não foi possível reiniciar o agente.\n\n{ex.Message}", AgentApi.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Log.Info("Reiniciando o agente.");
        Shutdown();
    }

    private void Exit()
    {
        Log.Info("Agente encerrado pelo usuário.");
        Shutdown();
    }

    /// <summary>
    /// Fecha o servidor (esperando no máximo 3 segundos) e encerra o processo de verdade. Sem o Environment.Exit,
    /// o processo podia continuar no Gerenciador de Tarefas mesmo com o ícone já fechado.
    /// </summary>
    private void Shutdown()
    {
        try
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
        catch
        {
            // Segue para o encerramento.
        }

        try
        {
            Task.Run(_stopServer).Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Log.Error($"Erro ao parar o servidor: {ex.Message}");
        }

        Environment.Exit(0);
    }

    public static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    public static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Não foi possível alterar a inicialização com o Windows: {ex.Message}");
        }
    }
}
