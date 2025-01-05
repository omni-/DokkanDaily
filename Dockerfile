FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

ARG AzureAccountName
ARG AzureConnectionString
ARG BlobContainerName
ARG BlobKey
ARG SqlServerConnectionString
ARG OAuth2ClientSecret
ARG OAuth2ClientId
ARG WebhookUrl
ARG EnableJpParsing

ENV DOTNET_DokkanDailySettings__AzureAccountName=$AzureAccountName
ENV DOTNET_DokkanDailySettings__AzureBlobConnectionString=$AzureConnectionString
ENV DOTNET_DokkanDailySettings__AzureBlobContainerName=$BlobContainerName
ENV DOTNET_DokkanDailySettings__AzureBlobKey=$BlobKey
ENV DOTNET_DokkanDailySettings__SqlServerConnectionString=$SqlServerConnectionString
ENV DOTNET_DokkanDailySettings__OAuth2ClientSecret=$OAuth2ClientSecret
ENV DOTNET_DokkanDailySettings__OAuth2ClientId=$OAuth2ClientId
ENV DOTNET_DokkanDailySettings__WebhookUrl=$WebhookUrl
ENV DOTNET_DokkanDailySettings__FeatureFlags__EnableJapaneseParsing=${EnableJpParsing:-True}

RUN apt-get update \
    && apt-get install -y --allow-unauthenticated \
        libleptonica-dev \
        libtesseract-dev \
    && rm -rf /var/lib/apt/lists/*
RUN ln -s /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so
RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /proj
COPY . .
RUN dotnet build "src/DokkanDaily/DokkanDaily.csproj" -c Release

FROM build AS publish
RUN dotnet publish "src/DokkanDaily/DokkanDaily.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "DokkanDaily.dll"]