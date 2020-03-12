FROM eiriktsarpalis/dotnet-sdk-mono:3.1.101-buster

WORKDIR /app
COPY . .

ENV GIT_ASKPASS=~/.git-askpass
RUN echo 'echo $GITHUB_TOKEN' > ~/.git-askpass

CMD ./build.sh
