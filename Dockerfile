FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
RUN apt-get update
RUN apt-get -y install gss-ntlmssp

WORKDIR /home/app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

ARG GITHUB_NUGET_API_KEY

COPY . /
#RUN ls -la /
RUN dotnet nuget update source 'MCCITGIT' -u 'MCCITADMIN' --store-password-in-clear-text -p $GITHUB_NUGET_API_KEY

RUN dotnet restore "/src/NSOU.ms.Utility.Exam/NSOU.ms.Utility.Exam.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "/src/NSOU.ms.Utility.Exam/NSOU.ms.Utility.Exam.csproj" -c Release -o /home/app/build

FROM build AS publish
RUN dotnet publish "/src/NSOU.ms.Utility.Exam/NSOU.ms.Utility.Exam.csproj" -c Release -o /home/app/publish

FROM base AS final
WORKDIR /home/app
COPY --from=publish /home/app/publish .

# ENV variables
ARG DEPLOY_ENVIRONMENT
ARG DOCKER_IMAGE_TAG
ARG MANDRILL_API_KEY
ARG AZ_KEYVAULT_CLIENT_ID
ARG AZ_KEYVAULT_CLIENT_SECRET
ENV ASPNETCORE_ENVIRONMENT=$DEPLOY_ENVIRONMENT
ENV NSOU_KEYVAULT_CLIENT_ID=$AZ_KEYVAULT_CLIENT_ID
ENV NSOU_KEYVAULT_CLIENT_SECRET=$AZ_KEYVAULT_CLIENT_SECRET
ENV ENV_IMAGE_VERSION=$DOCKER_IMAGE_TAG

ENV ASPNETCORE_URLS=http://*:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "NSOU.ms.Utility.Exam.dll"]
