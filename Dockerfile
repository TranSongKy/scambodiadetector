# syntax=docker/dockerfile:1

ARG PROJECT=ScamDetector.Api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /source

COPY Directory.Build.props .editorconfig ./
COPY src/ScamDetector.Core/ScamDetector.Core.csproj src/ScamDetector.Core/
COPY src/ScamDetector.Infrastructure/ScamDetector.Infrastructure.csproj src/ScamDetector.Infrastructure/
COPY src/ScamDetector.Api/ScamDetector.Api.csproj src/ScamDetector.Api/
COPY src/ScamDetector.Bot/ScamDetector.Bot.csproj src/ScamDetector.Bot/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj

COPY src/ src/
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG PROJECT
WORKDIR /app

ENV APP_DLL=${PROJECT}.dll \
    ASPNETCORE_URLS=http://+:8080 \
    OnnxModel__ModelPath=/models/scam-detector.onnx \
    OnnxModel__VocabularyPath=/models/vocab.txt \
    OnnxModel__BpeCodesPath=/models/bpe.codes \
    ThreatIntel__DataDirectory=/threat-intel

COPY --from=build /app ./
COPY data/threat-intel/blocked_domains.csv data/threat-intel/scam_templates.csv /threat-intel/
VOLUME ["/models"]
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
