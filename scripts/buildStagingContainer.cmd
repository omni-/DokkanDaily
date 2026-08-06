cd ..
docker stop dokkandaily || cd .
docker rm dokkandaily || cd .
docker image rm -f dokkandaily.azurecr.io/dokkandaily:staging || cd .
docker build . -t dokkandaily.azurecr.io/dokkandaily:staging
docker run --name dokkandaily -p 127.0.0.1:8080:8080 -d ^
--env DOTNET_DokkanDailySettings__AzureBlobConnectionString ^
--env DOTNET_DokkanDailySettings__AzureBlobContainerName ^
--env DOTNET_DokkanDailySettings__OAuth2ClientSecret ^
--env DOTNET_DokkanDailySettings__OAuth2ClientId ^
--env DOTNET_DokkanDailySettings__SqlServerConnectionString ^
--env DOTNET_DokkanDailySettings__WebhookUrl ^
--env DOTNET_DokkanDailySettings__StageRepeatLimitDays ^
--env DOTNET_DokkanDailySettings__EventRepeatLimitDays ^
--env DOTNET_DokkanDailySettings__FeatureFlags__EnableJapaneseParsing ^
--env DOTNET_DokkanDailySettings__FeatureFlags__EnablePruneJob ^
dokkandaily.azurecr.io/dokkandaily:staging
