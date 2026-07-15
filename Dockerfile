FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SW.Scheduler.Web/SW.Scheduler.Web.csproj", "SW.Scheduler.Web/"]
COPY ["SW.Scheduler.Sdk/SW.Scheduler.Sdk.csproj", "SW.Scheduler.Sdk/"]

RUN dotnet restore "SW.Scheduler.Web/SW.Scheduler.Web.csproj"
COPY . .
WORKDIR "/src/SW.Scheduler.Web"
RUN dotnet build "SW.Scheduler.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SW.Scheduler.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .


ENTRYPOINT ["dotnet", "SW.Scheduler.Web.dll"]
 
