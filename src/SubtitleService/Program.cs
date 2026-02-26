using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// ================= SERVIÇOS E SWAGGER =================
builder.Services.AddSingleton<JobStatus>();
builder.Services.AddSingleton<SubtitleExtractorService>();

// Configurações do Swagger simplificadas
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Extrator de Legendas v1");
    options.RoutePrefix = "swagger"; 
});

// ================= ENDPOINTS DA API =================

app.MapPost("/api/process", (ProcessRequest request, SubtitleExtractorService extractor, JobStatus status, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.DirectoryPath))
        return Results.BadRequest(new { erro = "O caminho do diretório é obrigatório." });

    if (!Directory.Exists(request.DirectoryPath))
        return Results.NotFound(new { erro = $"O diretório {request.DirectoryPath} não foi encontrado no container." });

    if (status.IsRunning)
        return Results.Conflict(new { erro = "Já existe um processamento em andamento.", statusAtual = status });

    logger.LogInformation("Iniciando processamento no diretório: {Path}", request.DirectoryPath);

    _ = Task.Run(() => extractor.ProcessDirectoryAsync(request.DirectoryPath));

    return Results.Accepted(value: new { message = "Processamento iniciado.", path = request.DirectoryPath });
});

app.MapGet("/api/status", (JobStatus status) =>
{
    double percentualGeral = status.TotalFiles == 0 ? 0 : Math.Round((double)status.ProcessedFiles / status.TotalFiles * 100, 2);

    return Results.Ok(new
    {
        executando = status.IsRunning,
        diretorioAlvo = status.CurrentDirectory,
        progressoGeral = $"{status.ProcessedFiles} de {status.TotalFiles} arquivos",
        porcentagemGeral = percentualGeral,
        arquivoAtual = status.CurrentFile,
        acaoAtual = status.CurrentAction,
        progressoArquivoAtual = status.CurrentFileProgress,
        progressoIndeterminado = status.IsIndeterminate,
        errosEncontrados = status.Errors.Count,
        detalhesErros = status.Errors
    });
});

// ================= ENDPOINT DA INTERFACE WEB (UI) =================

app.MapGet("/", () => Results.Content(ObterHtmlDashboard(), "text/html", Encoding.UTF8))
.ExcludeFromDescription(); 

app.Run("http://0.0.0.0:8080");

// ================= HTML E JAVASCRIPT =================
static string ObterHtmlDashboard() => """
<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Extrator de Legendas - Painel</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #121212; color: #e0e0e0; padding: 20px; margin: 0; }
        .container { max-width: 800px; margin: 0 auto; background-color: #1e1e1e; padding: 25px; border-radius: 10px; box-shadow: 0 4px 6px rgba(0,0,0,0.5); }
        h1 { margin-top: 0; color: #4caf50; font-size: 24px; border-bottom: 1px solid #333; padding-bottom: 10px; display: flex; justify-content: space-between; align-items: center; }
        .btn-swagger { background-color: #89bf04; color: #fff; text-decoration: none; padding: 6px 12px; font-size: 14px; border-radius: 4px; font-weight: bold; }
        .card { background-color: #2c2c2c; padding: 15px; border-radius: 8px; margin-bottom: 20px; }
        .card-title { font-size: 14px; color: #aaa; text-transform: uppercase; margin-bottom: 5px; letter-spacing: 1px; }
        .card-value { font-size: 18px; font-weight: bold; word-break: break-all; }
        
        .progress-container { width: 100%; background-color: #444; border-radius: 5px; height: 25px; overflow: hidden; margin-top: 10px; position: relative; }
        .progress-bar { height: 100%; background-color: #4caf50; width: 0%; transition: width 0.4s ease; }
        .progress-text { position: absolute; top: 0; left: 0; width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; font-size: 12px; font-weight: bold; text-shadow: 1px 1px 2px #000; }
        
        .indeterminate { background: linear-gradient(90deg, #2c2c2c 25%, #4caf50 50%, #2c2c2c 75%); background-size: 200% 100%; animation: loading 1.5s infinite; width: 100% !important; }
        @keyframes loading { 0% { background-position: 100% 0; } 100% { background-position: -100% 0; } }
        
        .status-badge { display: inline-block; padding: 5px 10px; border-radius: 4px; font-size: 12px; font-weight: bold; margin-bottom: 15px; }
        .status-running { background-color: #0277bd; color: white; }
        .status-idle { background-color: #555; color: white; }
        
        ul.errors { color: #ff5252; padding-left: 20px; font-size: 14px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>
            <span>Painel do Extrator de Legendas</span>
            <a href="/swagger" class="btn-swagger" target="_blank">Abrir Swagger API</a>
        </h1>
        <div id="badge-status" class="status-badge status-idle">Aguardando...</div>

        <div class="card">
            <div class="card-title">Diretório Alvo / Progresso Geral</div>
            <div class="card-value" id="dir-value">Nenhum diretório em processamento</div>
            <div class="progress-container">
                <div id="geral-bar" class="progress-bar" style="width: 0%;"></div>
                <div id="geral-text" class="progress-text">0% (0 de 0)</div>
            </div>
        </div>

        <div class="card">
            <div class="card-title">Processamento Atual</div>
            <div class="card-value" id="file-value" style="color: #64b5f6;">Aguardando início...</div>
            <div class="card-title" style="margin-top: 10px; font-size: 12px;" id="action-value">Nenhuma ação</div>
            <div class="progress-container">
                <div id="file-bar" class="progress-bar" style="width: 0%;"></div>
                <div id="file-text" class="progress-text">0%</div>
            </div>
        </div>

        <div class="card" id="error-card" style="display: none; border-left: 4px solid #ff5252;">
            <div class="card-title" style="color: #ff5252;">Erros Encontrados</div>
            <ul class="errors" id="error-list"></ul>
        </div>
    </div>

    <script>
        async function fetchStatus() {
            try {
                const res = await fetch('/api/status');
                const data = await res.json();

                const badge = document.getElementById('badge-status');
                if (data.executando) {
                    badge.className = 'status-badge status-running';
                    badge.innerText = 'PROCESSAMENTO EM ANDAMENTO';
                } else {
                    badge.className = 'status-badge status-idle';
                    badge.innerText = 'OCIOSO';
                }

                document.getElementById('dir-value').innerText = data.diretorioAlvo || '---';
                document.getElementById('geral-bar').style.width = data.porcentagemGeral + '%';
                document.getElementById('geral-text').innerText = data.porcentagemGeral + '% (' + data.progressoGeral + ')';

                document.getElementById('file-value').innerText = data.arquivoAtual || '---';
                document.getElementById('action-value').innerText = data.acaoAtual || '---';

                const fileBar = document.getElementById('file-bar');
                const fileText = document.getElementById('file-text');
                
                if (data.progressoIndeterminado) {
                    fileBar.className = 'progress-bar indeterminate';
                    fileText.innerText = 'Processando...';
                } else {
                    fileBar.className = 'progress-bar';
                    fileBar.style.width = data.progressoArquivoAtual + '%';
                    fileText.innerText = data.progressoArquivoAtual + '%';
                }

                const errorCard = document.getElementById('error-card');
                const errorList = document.getElementById('error-list');
                if (data.errosEncontrados > 0) {
                    errorCard.style.display = 'block';
                    errorList.innerHTML = data.detalhesErros.map(e => `<li>${e}</li>`).join('');
                } else {
                    errorCard.style.display = 'none';
                    errorList.innerHTML = '';
                }

            } catch (err) {
                console.error("Erro ao buscar status:", err);
            }
        }
        setInterval(fetchStatus, 1000);
        fetchStatus();
    </script>
</body>
</html>
""";

// ================= MODELOS E ESTADOS =================

public record ProcessRequest(string DirectoryPath);

public class JobStatus
{
    public bool IsRunning { get; set; } = false;
    public string CurrentDirectory { get; set; } = string.Empty;
    public int TotalFiles { get; set; } = 0;
    public int ProcessedFiles { get; set; } = 0;
    public string CurrentFile { get; set; } = string.Empty;
    public string CurrentAction { get; set; } = "Aguardando...";
    public int CurrentFileProgress { get; set; } = 0;
    public bool IsIndeterminate { get; set; } = false;
    public ConcurrentBag<string> Errors { get; set; } = new();

    public void Reset(string directory)
    {
        IsRunning = true;
        CurrentDirectory = directory;
        TotalFiles = ProcessedFiles = CurrentFileProgress = 0;
        CurrentFile = string.Empty;
        CurrentAction = "Iniciando...";
        IsIndeterminate = false;
        Errors.Clear();
    }
}

public class MkvData { public List<Track>? tracks { get; set; } }
public class Track { public long id { get; set; } public string? type { get; set; } public TrackProperties? properties { get; set; } }
public class TrackProperties { public string? language { get; set; } public string? language_ietf { get; set; } public string? track_name { get; set; } public string? codec_id { get; set; } }

// ================= SERVIÇO PRINCIPAL =================

public class SubtitleExtractorService
{
    private readonly ILogger<SubtitleExtractorService> _logger;
    private readonly JobStatus _status;
    private static readonly HttpClient _client = new();

    // Detecção automática de OS para funcionar tanto nos testes (Windows) quanto no Docker (Linux)
    private readonly string _subtitleEditPath = OperatingSystem.IsWindows() 
        ? @"C:\VideoTools\SE4015FI\SubtitleEdit.exe" 
        : "/opt/subtitleedit/SubtitleEdit";
        
    private readonly string _mkvMergeExe = "mkvmerge";
    private readonly string _mkvExtractExe = "mkvextract";
    private readonly string _webhookUrl = "http://192.168.15.5:9876/api/webhook/sonarr";
    private readonly string _webhookApiKey = "fkmiOAFaNGWYbiWfGwNinkfzgcvBvUG2g7Iy_lVkrzE";

    public SubtitleExtractorService(ILogger<SubtitleExtractorService> logger, JobStatus status)
    {
        _logger = logger;
        _status = status;
    }

    public async Task ProcessDirectoryAsync(string directoryPath)
    {
        _status.Reset(directoryPath);

        try
        {
            var arquivosMkv = Directory.GetFiles(directoryPath, "*.mkv", SearchOption.AllDirectories);
            _status.TotalFiles = arquivosMkv.Length;

            if (_status.TotalFiles == 0)
            {
                _status.CurrentAction = "Nenhum arquivo MKV encontrado.";
                return;
            }

            foreach (var arquivoPath in arquivosMkv)
            {
                _status.CurrentFile = Path.GetFileName(arquivoPath);
                _status.CurrentFileProgress = 0;
                _status.IsIndeterminate = false;

                await ProcessarArquivoAsync(arquivoPath);
                _status.ProcessedFiles++;
            }

            _status.CurrentAction = "Processamento concluído.";
            _status.CurrentFile = "---";
            _status.CurrentFileProgress = 0;
        }
        catch (Exception ex)
        {
            _status.Errors.Add($"Erro crítico no diretório: {ex.Message}");
            _status.CurrentAction = "Interrompido por erro crítico.";
        }
        finally
        {
            _status.IsRunning = false;
        }
    }

private async Task ProcessarArquivoAsync(string arquivoPath)
    {
        string nomeArquivo = Path.GetFileName(arquivoPath);
        string diretorioArquivo = Path.GetDirectoryName(arquivoPath) ?? "";
        string nomeBaseSemExt = Path.GetFileNameWithoutExtension(arquivoPath);
        string nomeBaseLegenda = Path.Combine(diretorioArquivo, nomeBaseSemExt + ".en");
        string caminhoFinalSrt = nomeBaseLegenda + ".srt";

        _status.CurrentAction = "Verificando legendas existentes...";

        // 1. Se o arquivo final (.srt) já existe, assumimos que já está pronto e pulamos tudo.
        if (File.Exists(caminhoFinalSrt))
        {
            _status.CurrentFileProgress = 100;
            return;
        }

        try
        {
            string caminhoExtraido = "";
            string ext = "";
            bool precisaExtrair = true;

            // 2. Verifica se há arquivos brutos (.ass, .sup, .sub) precisando de conversão/limpeza
            var extensoesParaConverter = new[] { ".ass", ".ssa", ".sup", ".sub" };
            foreach (var extCheck in extensoesParaConverter)
            {
                if (File.Exists(nomeBaseLegenda + extCheck))
                {
                    caminhoExtraido = nomeBaseLegenda + extCheck;
                    ext = extCheck;
                    precisaExtrair = false; // Como o arquivo já existe, não precisamos extrair do MKV novamente
                    break;
                }
            }

            // 3. Só extrai do MKV se não existir nenhum arquivo anterior
            if (precisaExtrair)
            {
                _status.CurrentAction = "Lendo metadados do arquivo...";
                string? jsonOutput = RunProcessForOutput(_mkvMergeExe, $"-J \"{arquivoPath}\"");
                if (string.IsNullOrEmpty(jsonOutput)) return;

                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var mkvInfo = JsonSerializer.Deserialize<MkvData>(jsonOutput, jsonOptions);

                var legendasValidas = mkvInfo?.tracks?.Where(t => 
                    t.type == "subtitles" &&
                    (t.properties?.language == "eng" || (t.properties?.language_ietf != null && t.properties.language_ietf.Contains("en"))) &&
                    !Regex.IsMatch(t.properties?.track_name ?? "", "Signs|Songs", RegexOptions.IgnoreCase)
                ).ToList();

                if (legendasValidas == null || legendasValidas.Count == 0) return;

                var legendaAlvo = legendasValidas.OrderBy(t => 
                {
                    if (t.properties?.codec_id == "S_TEXT/UTF8") return 1;
                    if (t.properties?.codec_id == "S_VOBSUB")    return 2;
                    return 3;
                }).First();

                long id = legendaAlvo.id;
                string codec = legendaAlvo.properties?.codec_id ?? "";
                ext = GetExtensionFromCodec(codec);
                caminhoExtraido = nomeBaseLegenda + ext;

                _status.CurrentAction = $"Extraindo legenda em formato {ext}...";
                RunProcessWithProgress(_mkvExtractExe, $"tracks \"{arquivoPath}\" {id}:\"{caminhoExtraido}\"");
            }

            string arquivoParaWebhook = caminhoExtraido;

            // ================= CONVERSÃO E LIMPEZA DE TAGS =================
            if (ext == ".sup" || ext == ".ass" || ext == ".sub" || ext == ".ssa")
            {
                _status.CurrentAction = (ext == ".sup" || ext == ".sub") ? $"Realizando OCR do {ext} (Isso pode demorar)..." : $"Convertendo e limpando {ext}...";
                _status.IsIndeterminate = true; 
                
                if (File.Exists(_subtitleEditPath))
                {
                    string argsConvert = $"/convert \"{caminhoExtraido}\" srt /removeformatting";// /suppressmsgboxes
                    var result = RunProcessWaitCapture(_subtitleEditPath, argsConvert);

                    if (result.ExitCode == 0 && File.Exists(caminhoFinalSrt))
                    {
                        // Exclui o arquivo original (recém extraído ou o que já estava lá)
                        try { File.Delete(caminhoExtraido); } catch { }
                        if (ext == ".sub") { try { File.Delete(nomeBaseLegenda + ".idx"); } catch { } }
                        
                        arquivoParaWebhook = caminhoFinalSrt;
                    }
                    else
                    {
                        string erroMsg = string.IsNullOrWhiteSpace(result.ErrorOutput) ? "Verifique os logs." : result.ErrorOutput;
                        _status.Errors.Add($"Falha na Conversão/OCR de {nomeArquivo}. Erro: {erroMsg}");
                    }
                }
                else
                {
                    _status.Errors.Add($"Subtitle Edit não encontrado em: {_subtitleEditPath}");
                }
            }
            else if (ext == ".srt")
            {
                // Se a legenda extraída foi um SRT nativo, aplicamos apenas a limpeza de tags
                if (File.Exists(_subtitleEditPath))
                {
                    _status.CurrentAction = "Limpando tags de formatação do SRT...";
                    _status.IsIndeterminate = true;
                    
                    string argsClean = $"/convert \"{caminhoExtraido}\" srt /removeformatting /overwrite"; // /suppressmsgboxes
                    var result = RunProcessWaitCapture(_subtitleEditPath, argsClean);

                    if (result.ExitCode != 0)
                    {
                         _status.Errors.Add($"Falha ao limpar SRT de {nomeArquivo}. Erro: {result.ErrorOutput}");
                    }
                }
            }

            _status.IsIndeterminate = false;
            _status.CurrentFileProgress = 100;
            _status.CurrentAction = "Enviando Webhook...";
            
            await EnviarWebhook(arquivoPath, arquivoParaWebhook, nomeArquivo);
        }
        catch (Exception ex)
        {
            _status.Errors.Add($"Erro no arquivo {nomeArquivo}: {ex.Message}");
        }
    }

    private string GetExtensionFromCodec(string codec) => codec switch
    {
        "S_TEXT/UTF8" => ".srt",
        "S_TEXT/ASS" => ".ass",
        "S_TEXT/SSA" => ".ssa",
        "S_HDMV/PGS" => ".sup",
        "S_VOBSUB" => ".sub",
        _ => ".srt"
    };

    private string? RunProcessForOutput(string filename, string arguments)
    {
        try 
        {
            using Process p = new();
            p.StartInfo.FileName = filename;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            p.Start();
            return p.StandardOutput.ReadToEnd();
        }
        catch { return null; }
    }

    // Método aprimorado para capturar o Erro (StandardError) do processo
    private (int ExitCode, string ErrorOutput) RunProcessWaitCapture(string filename, string arguments)
    {
        try
        {
            using Process p = new();
            p.StartInfo.FileName = filename;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true; // Captura falhas silenciosas
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, error.Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private int RunProcessWithProgress(string filename, string arguments)
    {
        try
        {
            using Process p = new();
            p.StartInfo.FileName = filename;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            p.Start();

            Task readTask = Task.Run(() =>
            {
                try
                {
                    char[] buffer = new char[512];
                    int charsRead;
                    StringBuilder currentLine = new();
                    
                    while ((charsRead = p.StandardOutput.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < charsRead; i++)
                        {
                            char c = buffer[i];
                            if (c == '\r' || c == '\n')
                            {
                                if (currentLine.Length > 0)
                                {
                                    AtualizarPorcentagemDaString(currentLine.ToString());
                                    currentLine.Clear();
                                }
                            }
                            else
                            {
                                currentLine.Append(c);
                            }
                        }
                    }
                    if (currentLine.Length > 0) AtualizarPorcentagemDaString(currentLine.ToString());
                }
                catch { }
            });

            p.WaitForExit();
            readTask.Wait();
            return p.ExitCode;
        }
        catch { return -1; }
    }

    private void AtualizarPorcentagemDaString(string line)
    {
        var match = Regex.Match(line, @"(?:Progress|Progresso).*?(\d+)%", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int pct))
        {
            _status.CurrentFileProgress = Math.Clamp(pct, 0, 100);
        }
    }

    private async Task EnviarWebhook(string videoPath, string subPath, string fileName)
    {
        var payloadObj = new { eventType = "SubtitleExtracted", videoPath, subtitlePath = subPath, fileName, timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
        var content = new StringContent(JsonSerializer.Serialize(payloadObj), Encoding.UTF8, "application/json");

        if (_client.DefaultRequestHeaders.Contains("X-Api-Key")) _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Add("X-Api-Key", _webhookApiKey);

        try { await _client.PostAsync(_webhookUrl, content); } catch { }
    }
}