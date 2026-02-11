# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Optional: install .NET diagnostics tools into the final image (for local profiling only).
ARG INSTALL_DIAGNOSTICS_TOOLS=false
RUN mkdir -p /tools \
    && if [ "$INSTALL_DIAGNOSTICS_TOOLS" = "true" ]; then \
         dotnet tool install dotnet-trace --tool-path /tools; \
       fi

# Copy solution and project files
COPY ["src/Web.Api/Web.Api.csproj", "Web.Api/"]
COPY ["src/Application/Application.csproj", "Application/"]
COPY ["src/Domain/Domain.csproj", "Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["src/SharedKernel/SharedKernel.csproj", "SharedKernel/"]

# Restore dependencies
RUN dotnet restore "Web.Api/Web.Api.csproj"

# Copy all source code
COPY ["src/", "./"]

# Build the application
WORKDIR "/src/Web.Api"
RUN dotnet build "Web.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Web.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy optional diagnostics tools (when enabled at build time)
COPY --from=build /tools /tools
ENV PATH="/tools:${PATH}"

# Copy published files
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Web.Api.dll"]
