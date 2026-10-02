# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props .editorconfig ./
COPY src/ScamDetector.Core/ScamDetector.Core.csproj src/ScamDetector.Core/
COPY src/ScamDetector.Infrastructure/ScamDetector.Infrastructure.csproj src/ScamDetector.Infrastructure/
COPY src/ScamDetector.Api/ScamDetector.Api.csproj src/ScamDetector.Api/
RUN dotnet restore src/ScamDetector.Api/ScamDetector.Api.csproj

COPY src/ src/
RUN dotnet publish src/ScamDetector.Api/ScamDetector.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    OnnxModel__ModelPath=/models/scam-detector.onnx \
    OnnxModel__VocabularyPath=/models/vocab.txt \
    OnnxModel__BpeCodesPath=/models/bpe.codes

COPY --from=build /app ./
VOLUME ["/models"]
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "ScamDetector.Api.dll"]
