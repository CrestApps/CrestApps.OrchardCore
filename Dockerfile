FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
LABEL stage=build-env
WORKDIR /app

# Copy and build
COPY ./src /app
COPY NuGet.Config /app/
RUN dotnet publish /app/CrestApps.OrchardCore.Web -c Release -o ./build/release --framework net8.0

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0

RUN apt update \
      && apt install -y --no-install-recommends openssh-server \
      && mkdir -p /run/sshd \
      && echo "root:Docker!" | chpasswd

COPY sshd_config /etc/ssh/sshd_config

EXPOSE 80 2222
ENV ASPNETCORE_URLS http://+:80
WORKDIR /app
COPY --from=build-env /app/build/release .
ENTRYPOINT ["/bin/bash", "-c", "/usr/sbin/sshd && dotnet CrestApps.OrchardCore.Web.dll"]
