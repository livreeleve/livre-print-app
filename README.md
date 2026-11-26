# Livre Print Agent

Programa pequeno para Windows que fica na bandeja e permite ao sistema (Livre Core) imprimir **direto nas impressoras**, sem diálogo do navegador e sem pedir confirmação. Serve para as duas impressoras:

| Impressora | O que o sistema envia |
|---|---|
| Zebra ZD220 (MiniDanfe) | ZPL |
| Bematech MP-4200 HS (etiqueta de coleta) | ESC/POS (texto, código de barras e corte) |

Cada trabalho vai para o spooler do Windows como dado cru (RAW), então a impressora corta ao fim de cada comprovante.

## Instalar em um computador

1. Copie a pasta `publish` para o computador, por exemplo `C:\LivrePrintAgent` (precisa do `LivrePrintAgent.exe` e do `agent.config.json`).
2. O endereço de produção (`https://livrecore.livreeleve.net`) **já vem liberado dentro do programa**: não precisa editar nada para ele. Se for usar o sistema por outro endereço, adicione-o em **allowedOrigins** no `agent.config.json`, exatamente como aparece no navegador (a barra no fim é ignorada). Vários endereços são permitidos.
3. Execute `LivrePrintAgent.exe`. Aparece o ícone da impressora na bandeja do Windows (o mesmo ícone do arquivo). Na primeira execução ele já configura para **iniciar com o Windows** (dá para desativar no menu do ícone).
4. No sistema: Faturamentos > menu de ações (...) > **Configurar impressoras**. O quadro de conexão deve mostrar o agente conectado; escolha a impressora da coleta e a da MiniDanfe.

Se o Windows SmartScreen avisar na primeira execução (o programa não é assinado), clique em "Mais informações" > "Executar assim mesmo".

## Menu do ícone (clique com o botão direito)

- **Iniciar com o Windows:** liga ou desliga a abertura automática.
- **Abrir pasta de logs** e **Abrir configuração (agent.config.json)**.
- **Reiniciar o agente:** fecha e abre de novo, relendo o `agent.config.json`. Use depois de editar a configuração ou se o agente travar.
- **Sair:** encerra o programa por completo, inclusive no Gerenciador de Tarefas.

## Como funciona

- Escuta **somente em `127.0.0.1`** (porta 9777 por padrão): outros computadores da rede não falam com o agente.
- Só atende pedidos vindos dos endereços de **allowedOrigins**. Qualquer outro site é recusado antes de imprimir. Pedidos com `Host` diferente de `127.0.0.1`/`localhost` também (proteção contra DNS rebinding).
- API:
  - `GET /status`: nome, versão e nome do computador.
  - `GET /printers`: impressoras instaladas e a padrão.
  - `POST /print/raw` com JSON `{ "printer": "nome", "text": "..." }` ou `{ "printer": "nome", "dataBase64": "..." }`.

## Configuração (`agent.config.json`)

```json
{
  "port": 9777,
  "allowedOrigins": ["https://livrecore.livreeleve.net"]
}
```

Depois de alterar, use **Reiniciar o agente** no menu do ícone. Se mudar a **porta**, o sistema precisa usar a mesma (hoje o front usa 9777).

## Logs

`%LOCALAPPDATA%\LivrePrintAgent\agent.log` (menu do ícone > Abrir pasta de logs).

## Compilar (opcional)

Já existe um executável pronto em `publish`. Para recompilar, no Windows ou no Linux, com o SDK do .NET 8:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

## Observações

- O ícone está em `app.ico` (e `app.png`); para trocá-lo, substitua o `app.ico` e recompile.
- A pasta `livre-print-agent` é temporária: pode ser movida para fora do projeto depois de instalar.
- O executável não é assinado digitalmente, daí o aviso do SmartScreen. Para evitar o aviso, é preciso um certificado de assinatura de código.
# livre-print-app
# livre-print-app
# livre-print-app
