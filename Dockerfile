# syntax=docker/dockerfile:1
#
# Reproduces the build and the test run inside a Windows container.
#
#   docker build -t codescope .
#   docker run --rm codescope
#
# The container builds the frozen engine, builds the application and runs the
# whole test suite. It does not run the interface: a graphical application
# needs a Windows desktop session, which a container does not have.
#
# The engine is built from the vendored snapshot, so the build needs no
# package feed at all — only the Go toolchain, which is fetched here.

# ---------------------------------------------------------------------------
# Stage 1 — the two bundled executables, built from the frozen snapshot
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore AS engine

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# The Go version the snapshot asks for. It is read from go.mod in the real
# repository; the argument keeps the image readable.
ARG GO_VERSION=1.26.4

RUN Invoke-WebRequest -Uri "https://go.dev/dl/go${GO_VERSION}.windows-amd64.zip" -OutFile go.zip; \
    Expand-Archive -Path go.zip -DestinationPath C:\; \
    Remove-Item go.zip

ENV PATH="C:\go\bin;${PATH}" \
    GOROOT="C:\go" \
    GOFLAGS="-mod=vendor" \
    GOPROXY="off" \
    CGO_ENABLED="0" \
    GOTOOLCHAIN="local"

WORKDIR C:\src

# The snapshot carries its own vendor tree, so the build needs no network.
COPY vendor/scc-source/ C:\src\

RUN New-Item -ItemType Directory -Force -Path C:\out | Out-Null; \
    go build -trimpath -ldflags "-s -w" -o C:\out\scc.exe .; \
    go build -trimpath -ldflags "-s -w" -o C:\out\codescope-export.exe ./tools/codescope-export

# ---------------------------------------------------------------------------
# Stage 2 — build the application and run the test suite
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore AS app

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR C:\src

COPY --from=engine C:\out\ C:\src\backend\
COPY . C:\src\

RUN dotnet restore CodeScope.sln --locked-mode; \
    dotnet build CodeScope.sln -c Release --no-restore; \
    dotnet run --project tests\CodeScope.Tests -c Release --no-build; \
    powershell -ExecutionPolicy Bypass -File .\build\publish.ps1

# The published folder is what a release ships. It is framework-dependent:
# the target machine needs the .NET 8 desktop runtime.
CMD ["powershell", "-Command", "Get-ChildItem -Recurse C:\\src\\publish\\CodeScope | Format-Table Length, Name -AutoSize"]
