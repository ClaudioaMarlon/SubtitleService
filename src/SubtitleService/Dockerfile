# Usa a imagem oficial do SDK do .NET 8 para compilar a aplicação
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SubtitleService.csproj", "./"]
RUN dotnet restore "./SubtitleService.csproj"
COPY . .
RUN dotnet publish "SubtitleService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Usa a imagem de Runtime do ASP.NET para rodar a aplicação
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Instala as dependências de sistema (MKVToolNix, Tesseract OCR e utilitários)
RUN apt-get update && apt-get install -y \
    mkvtoolnix \
    tesseract-ocr \
    tesseract-ocr-eng \
    wget \
    unzip \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Baixa e configura o Subtitle Edit Command Line para Linux
RUN mkdir -p /opt/subtitleedit 
RUN wget -q "https://github.com/SubtitleEdit/subtitleedit/releases/download/4.0.15/SE4015.zip" -O se.zip
RUN unzip se.zip -d /opt/subtitleedit
RUN rm se.zip
RUN chmod +x /opt/subtitleedit

# Copia a API compilada
COPY --from=build /app/publish .

# Expõe a porta do serviço
EXPOSE 8080

# Inicia o serviço
ENTRYPOINT ["dotnet", "SubtitleService.dll"]