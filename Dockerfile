FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet build TestJob/TestJob.csproj -c Release && exec dotnet run --project TestJob/TestJob.csproj -c Release --no-build --no-launch-profile --urls http://0.0.0.0:8080"]