# ── STAGE 1: Base ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ── STAGE 2: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG PROJECT_NAME
WORKDIR /src

# 1. Copy project files first (using lowercase shared)
COPY ["src/ConnectSphere.Auth.API/ConnectSphere.Auth.API.csproj", "src/ConnectSphere.Auth.API/"]
COPY ["src/ConnectSphere.Post.API/ConnectSphere.Post.API.csproj", "src/ConnectSphere.Post.API/"]
COPY ["src/ConnectSphere.Like.API/ConnectSphere.Like.API.csproj", "src/ConnectSphere.Like.API/"]
COPY ["src/ConnectSphere.Comment.API/ConnectSphere.Comment.API.csproj", "src/ConnectSphere.Comment.API/"]
COPY ["src/ConnectSphere.Follow.API/ConnectSphere.Follow.API.csproj", "src/ConnectSphere.Follow.API/"]
COPY ["src/ConnectSphere.Notif.API/ConnectSphere.Notif.API.csproj", "src/ConnectSphere.Notif.API/"]
COPY ["src/ConnectSphere.Feed.API/ConnectSphere.Feed.API.csproj", "src/ConnectSphere.Feed.API/"]
COPY ["src/ConnectSphere.Gateway/ConnectSphere.Gateway.csproj", "src/ConnectSphere.Gateway/"]
COPY ["shared/ConnectSphere.Shared/ConnectSphere.Shared.csproj", "shared/ConnectSphere.Shared/"]

# 2. CREATE A SYMBOLIC LINK TO HANDLE BOTH 'shared' AND 'Shared'
RUN ln -s shared Shared

# 3. Restore dependencies
RUN dotnet restore "src/${PROJECT_NAME}/${PROJECT_NAME}.csproj"

# 4. Copy the rest of the source code
COPY . .

# 5. Ensure the symbolic link is still there after bulk copy
RUN if [ ! -L "Shared" ]; then ln -sf shared Shared; fi

WORKDIR "/src/src/${PROJECT_NAME}"
RUN dotnet build "${PROJECT_NAME}.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ── STAGE 3: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG PROJECT_NAME
RUN dotnet publish "${PROJECT_NAME}.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ── STAGE 4: Final ────────────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ARG PROJECT_NAME
ENV DLL_NAME=${PROJECT_NAME}.dll
ENTRYPOINT ["sh", "-c", "dotnet $DLL_NAME"]
