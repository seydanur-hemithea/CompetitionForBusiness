FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyasını kopyala ve restore et
COPY ["CompetitionForBusiness/CompetitionForBusiness.csproj", "CompetitionForBusiness/"]
RUN dotnet restore "CompetitionForBusiness/CompetitionForBusiness.csproj"

# Tüm kodları kopyala ve derle
COPY . .
WORKDIR "/src/CompetitionForBusiness"
RUN dotnet build "CompetitionForBusiness.csproj" -c Release -o /app/build
RUN dotnet publish "CompetitionForBusiness.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Canlı ortam imajı
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CompetitionForBusiness.dll"]
