# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

WORKDIR /app

COPY . .

RUN dotnet publish -c release -o ./publish

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:5.0

WORKDIR /app
ENV PROD=true
COPY --from=build /app/publish .

#No Expose Because Heroku
#EXPOSE 8080

ENTRYPOINT ["dotnet", "SpaceGame.Web.dll"]