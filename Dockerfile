FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# Prevent 'Warning: apt-key output should not be parsed (stdout is not a terminal)'
ENV APT_KEY_DONT_WARN_ON_DANGEROUS_USAGE=1

# install NodeJS 13.x
# see https://github.com/nodesource/distributions/blob/master/README.md#deb
RUN apt-get update -yq
RUN apt-get install curl gnupg -yq
RUN curl -sL https://deb.nodesource.com/setup_14.x | bash -
RUN apt-get install -y nodejs

COPY *.sln .
COPY SegmentChallengeWeb/*.csproj ./SegmentChallengeWeb/
RUN dotnet restore

COPY SegmentChallengeWeb/. ./SegmentChallengeWeb/
WORKDIR /app/SegmentChallengeWeb/ClientApp
# Make sure node-sass is built for the docker environment
RUN npm install node-sass && npm rebuild node-sass
WORKDIR /app/SegmentChallengeWeb
ENTRYPOINT ["bash"]
RUN dotnet publish -c Release -o published


FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
WORKDIR /app
COPY --from=build /app/SegmentChallengeWeb/published ./
EXPOSE 80/tcp
ENTRYPOINT ["dotnet", "SegmentChallengeWeb.dll"]
