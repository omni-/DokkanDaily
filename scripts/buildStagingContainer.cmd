cd ..
docker stop dokkandaily || cd .
docker rm dokkandaily || cd .
docker image rm -f dokkandaily.azurecr.io/dokkandaily:staging || cd .
docker build . -t dokkandaily.azurecr.io/dokkandaily:staging ^
--build-arg AzureAccountName="%DOTNET_DokkanDailySettings__AzureAccountName%" ^
--build-arg AzureConnectionString="%DOTNET_DokkanDailySettings__AzureBlobConnectionString%" ^
--build-arg BlobContainerName="stg-daily-clears" ^
--build-arg BlobKey="%DOTNET_DokkanDailySettings__AzureBlobKey%" ^
--build-arg OAuth2ClientSecret="%DOTNET_DokkanDailySettings__OAuth2ClientSecret%" ^
--build-arg OAuth2ClientId="%DOTNET_DokkanDailySettings__OAuth2ClientId%" ^
--build-arg SqlServerConnectionString="%DOTNET_DokkanDailySettings__SqlServerConnectionString%" ^
--build-arg WebhookUrl="%DOTNET_DokkanDailySettings__WebhookUrl%" ^
--build-arg EnableJpParsing="true" ^
--build-arg StageRepeatLimit="30" ^
--build-arg EventRepeatLimit="7"
docker run --name dokkandaily -p 8080:8080 -d dokkandaily.azurecr.io/dokkandaily:staging