# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .

# 👉 Bổ sung để Render biết mở đúng cổng
ENV ASPNETCORE_URLS=http://+:$PORT

# 👉 Render mở port 10000–65535, nên cần expose cổng này
EXPOSE 10000

ENTRYPOINT ["dotnet", "QuanLyCotWeb.dll"]
