FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

# Configuration is supplied at runtime through App Service application settings or `docker run -e`.
# The application explicitly loads the existing DOTNET_ prefix after its JSON configuration, so
# these values override appsettings.json without being recoverable from image history.
#
#   DOTNET_DokkanDailySettings__AzureBlobConnectionString
#   DOTNET_DokkanDailySettings__AzureBlobContainerName
#   DOTNET_DokkanDailySettings__SqlServerConnectionString
#   DOTNET_DokkanDailySettings__OAuth2ClientSecret
#   DOTNET_DokkanDailySettings__OAuth2ClientId
#   DOTNET_DokkanDailySettings__WebhookUrl
#   DOTNET_DokkanDailySettings__StageRepeatLimitDays
#   DOTNET_DokkanDailySettings__EventRepeatLimitDays
#   DOTNET_DokkanDailySettings__FeatureFlags__EnableJapaneseParsing
#   DOTNET_DokkanDailySettings__FeatureFlags__EnablePruneJob

ENV LD_LIBRARY_PATH="/lib:/usr/lib:/usr/local/lib"

RUN apt-get update \
    && apt-get install -y --allow-unauthenticated \
        libleptonica-dev \
        libtesseract-dev

RUN apt-get clean && rm -rf /var/lib/apt/lists/*

RUN ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so
RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /proj
COPY . .
RUN dotnet build "src/DokkanDaily/DokkanDaily.csproj" -c Release

FROM build AS publish
RUN dotnet publish "src/DokkanDaily/DokkanDaily.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
COPY --from=ghcr.io/shimat/opencvsharp/ubuntu24-dotnet10-opencv4.13.0:20260214 /usr/lib/libOpenCvSharpExtern.so /app/runtimes/linux-x64/native/libOpenCvSharpExtern.so
COPY --from=ghcr.io/shimat/opencvsharp/ubuntu24-dotnet10-opencv4.13.0:20260214 /lib/x86_64-linux-gnu/ /lib/x86_64-linux-gnu/
ENTRYPOINT ["dotnet", "DokkanDaily.dll"]
