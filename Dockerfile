# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["ClassroomBookingSystem.Api/ClassroomBookingSystem.Api.csproj", "ClassroomBookingSystem.Api/"]
COPY ["ClassroomBookingSystem.Core/ClassroomBookingSystem.Core.csproj", "ClassroomBookingSystem.Core/"]
COPY ["ClassroomBookingSystem.Infrastructure/ClassroomBookingSystem.Infrastructure.csproj", "ClassroomBookingSystem.Infrastructure/"]

RUN dotnet restore "ClassroomBookingSystem.Api/ClassroomBookingSystem.Api.csproj"

# Copy remaining source files and build
COPY ClassroomBookingSystem.Api/ ClassroomBookingSystem.Api/
COPY ClassroomBookingSystem.Core/ ClassroomBookingSystem.Core/
COPY ClassroomBookingSystem.Infrastructure/ ClassroomBookingSystem.Infrastructure/

WORKDIR /src/ClassroomBookingSystem.Api
RUN dotnet build "ClassroomBookingSystem.Api.csproj" -c Release -o /app/build

# Publish the application
RUN dotnet publish "ClassroomBookingSystem.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port and run the application
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClassroomBookingSystem.Api.dll"]