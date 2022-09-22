FROM ubuntu:20.04
RUN apt-get update -y
RUN apt-get install wget -y
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get update -y
RUN apt-get install -y apt-transport-https
RUN apt-get update -y
RUN apt-get install -y aspnetcore-runtime-3.1
COPY ./bin/DiscordBot/netcoreapp3.1 ./app

CMD ["/bin/bash"]