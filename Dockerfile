FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src

COPY src/Wellcome.Dds/Wellcome.Dds.Server/Wellcome.Dds.Server.csproj Wellcome.Dds.Server/
COPY src/Wellcome.Dds/Wellcome.Dds.Common/Wellcome.Dds.Common.csproj Wellcome.Dds.Common/
COPY src/Wellcome.Dds/Utils/Utils.csproj Utils/
RUN dotnet restore "Wellcome.Dds.Server/Wellcome.Dds.Server.csproj"

# Copy everything else
COPY src/Wellcome.Dds/ ./
WORKDIR "/src/Wellcome.Dds.Server"
RUN dotnet publish "Wellcome.Dds.Server.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim
WORKDIR /app

EXPOSE 80
COPY --from=build /app/publish .
ENTRYPOINT [ "dotnet", "Wellcome.Dds.Server.dll" ]