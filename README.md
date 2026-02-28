Aqui está um modelo completo e profissional de README.md para o seu projeto. Ele cobre desde a explicação do serviço até as instruções de build, deploy com Docker e uso da API.

Markdown
# 🎬 Subtitle Extractor API

Um microsserviço automatizado desenvolvido em **C# (.NET 8)** para varredura, extração, conversão e limpeza de legendas em arquivos de vídeo `.mkv`. Projetado para rodar em containers Docker Linux, ele se integra perfeitamente a servidores de mídia automatizados (como Sonarr/Radarr) via Webhooks.

## ✨ Funcionalidades

* **Extração Inteligente:** Analisa metadados nativos do MKV (via `mkvmerge`) e extrai a melhor legenda em inglês disponível (ignorando faixas de "Signs/Songs").
* **Ordem de Prioridade:** Prefere legendas de texto (`.srt`) nativas. Na ausência destas, busca `.sub`, `.ass` ou `.sup`.
* **Conversão e OCR:** Integração nativa com o *Subtitle Edit Command Line*. Converte legendas baseadas em imagem (`.sup`, Blu-ray) ou texto estilizado (`.ass`, Anime) para `.srt` puro.
* **Limpeza de Tags:** Remove automaticamente tags de formatação HTML/XML (como `<i>`, `<b>`, `<font>`) de arquivos `.srt`, garantindo compatibilidade universal com players.
* **Notificações Webhook:** Dispara uma requisição POST (com autenticação `X-Api-Key`) contendo o caminho da nova legenda sempre que um processamento é concluído com sucesso.
* **Dashboard em Tempo Real:** Interface web *Dark Mode* acessível no navegador para acompanhar o progresso das extrações, conversões e OCR em tempo real.
* **Swagger API:** Interface interativa para testar e descobrir os endpoints da API.

---

## 🚀 Como Instalar e Executar (Docker)

A maneira recomendada de rodar este serviço é utilizando o **Docker** e o **Docker Compose**, pois o container já cuida de instalar as dependências pesadas do Linux (MKVToolNix, Tesseract OCR, etc.).

### 1. Preparação dos Arquivos
Certifique-se de que os seguintes arquivos estejam na mesma pasta no seu servidor:
* `Program.cs` (Código fonte da API)
* `SubtitleService.csproj` (Arquivo de projeto do .NET)
* `Dockerfile` (Instruções de build da imagem)
* `docker-compose.yml` (Orquestração do container)

### 2. Configuração do Diretório de Vídeos
Abra o arquivo `docker-compose.yml` e edite a seção de `volumes` para mapear a pasta de vídeos da sua máquina real para dentro do container:


volumes:
  # LADO ESQUERDO (Seu Servidor) : LADO DIREITO (Container)
  - /caminho/real/no/seu/servidor:/videos
Atenção: A API enxergará os arquivos a partir do diretório /videos.

3. Build e Execução
Abra o terminal na pasta onde estão os arquivos e execute:

Bash
`docker-compose up -d --build`

O Docker irá baixar o SDK do .NET, compilar a API, instalar as ferramentas de conversão e iniciar o serviço na porta 8080.

📡 Endpoints da API
Acesse http://SEU_IP:8080/swagger no navegador para testar os endpoints interativamente.

POST /api/process
Inicia a varredura e processamento em segundo plano.
Corpo da Requisição (JSON):

JSON
{
  "directoryPath": "/videos/Series/MinhaSerie"
}
Nota: Utilize o caminho interno mapeado no container (ex: /videos/...), e não o caminho físico do host.

GET /api/status
Retorna o status do processamento atual em tempo real.
Resposta de Exemplo:

JSON
{
  "executando": true,
  "diretorioAlvo": "/videos/Series/MinhaSerie",
  "progressoGeral": "2 de 10 arquivos",
  "porcentagemGeral": 20.0,
  "arquivoAtual": "Episodio_03.mkv",
  "acaoAtual": "Convertendo e limpando ASS...",
  "progressoArquivoAtual": 100,
  "progressoIndeterminado": true,
  "errosEncontrados": 0,
  "detalhesErros": []
}
🖥️ Painel Web (Dashboard)
O serviço possui um painel de monitoramento integrado. Basta acessar o IP e porta do serviço no seu navegador:

Plaintext
http://SEU_IP:8080/
Neste painel, você acompanha o progresso de leitura das faixas, status do OCR e eventuais erros sem precisar olhar os logs do console.

⚙️ Detalhes Técnicos e Dependências
A imagem Docker gerada (Dockerfile) inclui automaticamente:

ASP.NET Core 8.0 Runtime

MKVToolNix (mkvmerge e mkvextract)

Subtitle Edit Command Line (Portable Linux x64)

Tesseract OCR (Engine v4/v5 com dicionário eng nativo)

Bibliotecas do sistema (libgdiplus, libc6-dev) necessárias para o processamento de imagens (.SUP/.SUB).

🛠️ Manutenção
Para ver os logs internos do container em tempo real:

Bash
docker logs -f subtitle-extractor-api
Para parar o serviço:

Bash
docker-compose down

