# ==========================================
# Stage 1: Base Runtime Image
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# ==========================================
# Stage 2: SDK Image for Building & Restoring
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files for layer caching optimization
COPY ["Services/Booking/EventTix.Booking.Api/EventTix.Booking.Api.csproj", "Services/Booking/EventTix.Booking.Api/"]
COPY ["Services/Booking/EventTix.Booking.Application/EventTix.Booking.Application.csproj", "Services/Booking/EventTix.Booking.Application/"]
COPY ["Services/Booking/EventTix.Booking.Infrastructure/EventTix.Booking.Infrastructure.csproj", "Services/Booking/EventTix.Booking.Infrastructure/"]
COPY ["Services/Booking/EventTix.Booking.Domain/EventTix.Booking.Domain.csproj", "Services/Booking/EventTix.Booking.Domain/"]

# Restore NuGet dependencies
RUN dotnet restore "Services/Booking/EventTix.Booking.Api/EventTix.Booking.Api.csproj"

# Copy remaining source code and build
COPY . .
WORKDIR "/src/Services/Booking/EventTix.Booking.Api"
RUN dotnet build "EventTix.Booking.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build


# ==========================================
# Stage 3: Publish Binaries
# ==========================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EventTix.Booking.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false


# ==========================================
# Stage 4: Final Self-Contained Runtime
# ==========================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EventTix.Booking.Api.dll"]