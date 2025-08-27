# syntax=docker/dockerfile:1
############################################
# Build stage
############################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy only the app project first to leverage layer caching
COPY CsharpBlockchainNode.csproj .
RUN dotnet restore CsharpBlockchainNode.csproj

# now copy the remainder of the source
COPY . .
RUN dotnet publish CsharpBlockchainNode.csproj -c Release -o /out /p:UseAppHost=false

############################################
# Runtime stage
############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# ASP.NET Core will bind to port 5000 inside the container
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

# simple healthcheck that hits the /healthz endpoint
HEALTHCHECK --interval=20s --timeout=3s --retries=3 \
  CMD wget -qO- http://localhost:5000/healthz || exit 1

ENTRYPOINT ["dotnet", "CsharpBlockchainNode.dll"]
